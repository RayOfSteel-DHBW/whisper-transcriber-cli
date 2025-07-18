using System.Text.Json;
using Microsoft.Extensions.Logging;
using WhisperTranscriberCLI.Core.Models;
using WhisperTranscriberCLI.Core.Events;
using WhisperTranscriberCLI.Core.Interfaces;

namespace WhisperTranscriberCLI.Core.Services;

public class QueueManager : IDisposable
{
    private readonly string _queueFilePath;
    private readonly IMediaConverter _mediaConverter;
    private readonly ILogger<QueueManager>? _logger;
    private readonly Timer _autoSaveTimer;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly SemaphoreSlim _queueSemaphore;
    private TranscriptionQueue _queue;
    private bool _isProcessing;
    private bool _isPaused;
    private bool _useGpu;

    public event EventHandler<TranscriptionProgressEventArgs>? ProgressChanged;
    public event EventHandler<TranscriptionStatusEventArgs>? StatusChanged;
    public event EventHandler<TranscriptionTask>? TaskCompleted;
    public event EventHandler<TranscriptionTask>? TaskFailed;
    public event EventHandler<FileOverwriteEventArgs>? FileOverwriteRequested;

    public QueueManager(string queueFilePath, IMediaConverter mediaConverter, bool useGpu = false, ILogger<QueueManager>? logger = null)
    {
        _queueFilePath = queueFilePath;
        _mediaConverter = mediaConverter;
        _useGpu = useGpu;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
        _queueSemaphore = new SemaphoreSlim(1, 1);
        _queue = new TranscriptionQueue();

        LoadQueue();

        _autoSaveTimer = new Timer(AutoSaveCallback, null, 
            TimeSpan.FromSeconds(_queue.Settings.AutoSaveInterval), 
            TimeSpan.FromSeconds(_queue.Settings.AutoSaveInterval));
        
        _logger?.LogInformation("QueueManager initialized with {TaskCount} tasks, GPU: {UseGpu}", _queue.Tasks.Count, useGpu);
    }

    public TranscriptionQueue Queue => _queue;
    public bool IsProcessing => _isProcessing;
    public bool IsPaused => _isPaused;

    // Method to check if we can start processing
    public bool CanStartProcessing()
    {
        return !_isProcessing && _queue.Tasks.Any(t => t.Status == Models.TaskStatus.Pending);
    }

    // Method to get current queue state for debugging
    public string GetQueueState()
    {
        var pending = _queue.Tasks.Count(t => t.Status == Models.TaskStatus.Pending);
        var processing = _queue.Tasks.Count(t => t.Status == Models.TaskStatus.Processing);
        var done = _queue.Tasks.Count(t => t.Status == Models.TaskStatus.Done);
        var error = _queue.Tasks.Count(t => t.Status == Models.TaskStatus.Error);
        
        return $"Queue State: Pending={pending}, Processing={processing}, Done={done}, Error={error}, IsProcessing={_isProcessing}, IsPaused={_isPaused}";
    }

    public async Task AddTaskAsync(TranscriptionTask task)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            _queue.Tasks.Add(task);
            await SaveQueueAsync();
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task RemoveTaskAsync(string taskId)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            _queue.Tasks.RemoveAll(t => t.Id == taskId);
            await SaveQueueAsync();
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task ClearCompletedTasksAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            _queue.Tasks.RemoveAll(t => t.Status == Models.TaskStatus.Done);
            await SaveQueueAsync();
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task ClearAllTasksAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            _queue.Tasks.Clear();
            await SaveQueueAsync();
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task StartProcessingAsync()
    {
        if (_isProcessing)
        {
            _logger?.LogInformation("StartProcessingAsync called but queue is already processing");
            return;
        }

        // Ensure we have a valid cancellation token
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _logger?.LogInformation("Recreating cancellation token for new processing session");
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        _isProcessing = true;
        _isPaused = false;
        
        _logger?.LogInformation("Starting queue processing with {TaskCount} tasks", _queue.Tasks.Count);
        _logger?.LogDebug("Queue state: {QueueState}", GetQueueState());

        await Task.Run(async () =>
        {
            try
            {
                while (_isProcessing && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (_isPaused)
                    {
                        _logger?.LogDebug("Queue processing is paused, waiting...");
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                        continue;
                    }

                    var nextTask = await GetNextPendingTaskAsync();
                    if (nextTask == null)
                    {
                        _logger?.LogInformation("No more pending tasks found, stopping queue processing");
                        _isProcessing = false;
                        break;
                    }

                    _logger?.LogInformation("Processing next task: {TaskId} - {FilePath}", nextTask.Id, nextTask.FilePath);
                    await ProcessTaskAsync(nextTask);
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Queue processing was cancelled");
                _isProcessing = false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error in queue processing loop");
                _isProcessing = false;
            }
        }, _cancellationTokenSource.Token);
        
        _logger?.LogInformation("Queue processing completed. Final state: {QueueState}", GetQueueState());
    }

    public void PauseProcessing()
    {
        _logger?.LogInformation("Pausing queue processing");
        _isPaused = true;
    }

    public void ResumeProcessing()
    {
        _logger?.LogInformation("Resuming queue processing");
        _isPaused = false;
    }

    public void StopProcessing()
    {
        _logger?.LogInformation("Stopping queue processing");
        _isProcessing = false;
        _isPaused = false;
        
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if already disposed
            _logger?.LogDebug("CancellationTokenSource was already disposed");
        }
    }

    private async Task<TranscriptionTask?> GetNextPendingTaskAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            return _queue.Tasks.FirstOrDefault(t => t.Status == Models.TaskStatus.Pending);
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    private async Task ProcessTaskAsync(TranscriptionTask task)
    {
        try
        {
            _logger?.LogInformation("Starting transcription for task {TaskId}: {FilePath}", task.Id, task.FilePath);
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Processing);

            // Update status to show we're starting
            StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
            {
                TaskId = task.Id,
                Status = Models.TaskStatus.Processing,
                ErrorMessage = "Initializing transcription...",
                OutputPath = null
            });

            // Check if output file already exists during processing
            var expectedOutputPath = Path.ChangeExtension(task.FilePath, ".srt");
            if (File.Exists(expectedOutputPath))
            {
                _logger?.LogInformation("Output file already exists for task {TaskId}: {OutputPath}", task.Id, expectedOutputPath);
                
                // Update status to show we're checking for existing files
                StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
                {
                    TaskId = task.Id,
                    Status = Models.TaskStatus.Processing,
                    ErrorMessage = "Checking existing output file...",
                    OutputPath = null
                });
                
                // Create event args for file overwrite request
                var overwriteArgs = new FileOverwriteEventArgs
                {
                    TaskId = task.Id,
                    FilePath = task.FilePath,
                    OutputPath = expectedOutputPath,
                    ShouldOverwrite = false // Default to not overwrite
                };

                // Fire event to request user decision (UI will handle this)
                FileOverwriteRequested?.Invoke(this, overwriteArgs);

                // Wait for user decision (timeout after 30 seconds)
                var timeout = DateTime.Now.AddSeconds(30);
                while (!overwriteArgs.UserDecisionMade && DateTime.Now < timeout && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(500, _cancellationTokenSource.Token);
                }

                // Check if cancellation was requested during the wait
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _logger?.LogInformation("Queue stop requested during file overwrite dialog for task {TaskId}", task.Id);
                    throw new OperationCanceledException("Queue processing was stopped by user");
                }

                if (!overwriteArgs.UserDecisionMade)
                {
                    // Timeout - default to skip
                    _logger?.LogWarning("File overwrite decision timeout for task {TaskId}, skipping", task.Id);
                    task.ErrorMessage = "File overwrite decision timeout - task skipped";
                    task.OutputPath = expectedOutputPath; // Mark as if completed
                    task.CompletedAt = DateTime.UtcNow;
                    await UpdateTaskStatusAsync(task, Models.TaskStatus.Done);
                    TaskCompleted?.Invoke(this, task);
                    return;
                }

                if (!overwriteArgs.ShouldOverwrite)
                {
                    // User chose not to overwrite - mark as completed without processing
                    _logger?.LogInformation("User chose not to overwrite existing file for task {TaskId}, marking as completed", task.Id);
                    task.OutputPath = expectedOutputPath;
                    task.CompletedAt = DateTime.UtcNow;
                    await UpdateTaskStatusAsync(task, Models.TaskStatus.Done);
                    TaskCompleted?.Invoke(this, task);
                    return;
                }

                // User chose to overwrite - continue with transcription
                _logger?.LogInformation("User chose to overwrite existing file for task {TaskId}, continuing with transcription", task.Id);
            }

            // Update status to show we're loading the model
            StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
            {
                TaskId = task.Id,
                Status = Models.TaskStatus.Processing,
                ErrorMessage = $"Loading Whisper model ({task.ModelName})...",
                OutputPath = null
            });

            _logger?.LogInformation("Loading Whisper model for task {TaskId}: {ModelName}", task.Id, task.ModelName);

            // Create transcription service with proper logger type
            var transcriptionLogger = _logger as ILogger<WhisperNetTranscriptionService> ?? 
                                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<WhisperNetTranscriptionService>();
            
            var transcriptionService = new WhisperNetTranscriptionService(
                _mediaConverter, 
                _useGpu, 
                task.ModelName,
                transcriptionLogger
            );

            // Check if service is available before attempting transcription
            if (!transcriptionService.IsAvailable)
            {
                var error = $"Transcription service not available: {transcriptionService.UnavailabilityReason}";
                _logger?.LogError("Task {TaskId} failed - {Error}", task.Id, error);
                throw new InvalidOperationException(error);
            }

            // Update status to show model is loaded and we're starting transcription
            StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
            {
                TaskId = task.Id,
                Status = Models.TaskStatus.Processing,
                ErrorMessage = "Model loaded, starting transcription...",
                OutputPath = null
            });

            // Subscribe to progress events to forward to UI
            bool firstProgressUpdate = true;
            transcriptionService.ProgressChanged += (sender, progressArgs) =>
            {
                // Update task progress
                task.Progress = progressArgs.Progress * 100; // Convert to percentage
                
                // Clear loading message on first progress update
                if (firstProgressUpdate)
                {
                    firstProgressUpdate = false;
                    task.ErrorMessage = null; // Clear loading message
                    StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
                    {
                        TaskId = task.Id,
                        Status = Models.TaskStatus.Processing,
                        ErrorMessage = null, // Clear loading message
                        OutputPath = null
                    });
                }
                
                // Forward progress event to UI
                ProgressChanged?.Invoke(this, progressArgs);
                
                _logger?.LogTrace("Progress update for task {TaskId}: {Progress:F1}%", task.Id, task.Progress);
            };

            _logger?.LogInformation("Transcription service ready, starting transcription for {FilePath}", task.FilePath);
            var outputPath = await transcriptionService.TranscribeAsync(
                task.FilePath, 
                _cancellationTokenSource.Token
            );

            if (string.IsNullOrEmpty(outputPath))
            {
                var error = "Transcription completed but no output file was created";
                _logger?.LogError("Task {TaskId} failed - {Error}", task.Id, error);
                throw new InvalidOperationException(error);
            }

            _logger?.LogInformation("Transcription completed successfully for task {TaskId}, output: {OutputPath}", task.Id, outputPath);
            task.OutputPath = outputPath;
            task.CompletedAt = DateTime.UtcNow;
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Done);

            TaskCompleted?.Invoke(this, task);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Transcription cancelled for task {TaskId}", task.Id);
            
            // When cancellation occurs due to stopping the queue, reset the task to pending
            // so it can be restarted later, rather than marking it as an error
            task.ErrorMessage = null; // Clear any error message
            task.Progress = 0; // Reset progress
            task.CompletedAt = null; // Clear completion time
            task.OutputPath = null; // Clear output path
            
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Pending);
            
            // Don't invoke TaskFailed since this is a user-initiated stop, not a failure
            _logger?.LogInformation("Task {TaskId} reset to pending due to queue stop", task.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Transcription failed for task {TaskId}: {FilePath}", task.Id, task.FilePath);
            task.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
            {
                task.ErrorMessage += $" -> {ex.InnerException.Message}";
            }
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Error);
            TaskFailed?.Invoke(this, task);
        }
    }

    private async Task UpdateTaskStatusAsync(TranscriptionTask task, Models.TaskStatus status)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            task.Status = status;
            await SaveQueueAsync();
            
            StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
            {
                TaskId = task.Id,
                Status = status,
                ErrorMessage = task.ErrorMessage,
                OutputPath = task.OutputPath
            });
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public async Task SaveQueueAsync()
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_queueFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use source generator for JSON serialization
            var json = JsonSerializer.Serialize(_queue, TranscriptionQueueJsonContext.Default.TranscriptionQueue);
            await File.WriteAllTextAsync(_queueFilePath, json);
        }
        catch (Exception ex)
        {
            // Log to debug output but don't crash the app
            _logger?.LogError(ex, "Failed to save queue");
            // Also try to notify via StatusChanged if possible
            StatusChanged?.Invoke(this, new TranscriptionStatusEventArgs
            {
                TaskId = "system",
                Status = Models.TaskStatus.Error,
                ErrorMessage = $"Failed to save queue: {ex.Message}"
            });
        }
    }

    public void LoadQueue()
    {
        try
        {
            if (File.Exists(_queueFilePath))
            {
                var json = File.ReadAllText(_queueFilePath);
                var loadedQueue = JsonSerializer.Deserialize(json, TranscriptionQueueJsonContext.Default.TranscriptionQueue);
                _queue = loadedQueue ?? new TranscriptionQueue();
                
                // Reset any tasks that were in "Processing" state back to "Pending"
                // since we can't resume transcription from where it left off
                var processingTasks = _queue.Tasks.Where(t => t.Status == Models.TaskStatus.Processing).ToList();
                foreach (var task in processingTasks)
                {
                    _logger?.LogInformation("Resetting task {TaskId} from Processing to Pending (cannot resume): {FilePath}", 
                        task.Id, task.FilePath);
                    task.Status = Models.TaskStatus.Pending;
                    task.Progress = 0; // Reset progress
                    task.ErrorMessage = null; // Clear any error message
                    // Keep CompletedAt and OutputPath as null since it wasn't completed
                }
                
                if (processingTasks.Count > 0)
                {
                    _logger?.LogInformation("Reset {Count} processing tasks to pending on queue load", processingTasks.Count);
                    // Save the queue immediately to persist the status changes
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SaveQueueAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to save queue after resetting processing tasks");
                        }
                    });
                }
            }
            else
            {
                _queue = new TranscriptionQueue();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load queue, using empty queue");
            _queue = new TranscriptionQueue();
        }
    }

    private void AutoSaveCallback(object? state)
    {
        try
        {
            SaveQueueAsync().Wait();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Auto-save failed");
        }
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _cancellationTokenSource?.Dispose();
        _queueSemaphore?.Dispose();
    }

    // New method to reset all error tasks back to pending status
    public async Task ResetAllErrorTasksAsync()
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            var errorTasks = _queue.Tasks.Where(t => t.Status == Models.TaskStatus.Error).ToList();
            foreach (var task in errorTasks)
            {
                _logger?.LogInformation("Resetting error task {TaskId} to pending: {FilePath}", task.Id, task.FilePath);
                task.Status = Models.TaskStatus.Pending;
                task.ErrorMessage = null;
                task.Progress = 0;
                task.CompletedAt = null;
                task.OutputPath = null;
            }
            
            if (errorTasks.Count > 0)
            {
                await SaveQueueAsync();
                _logger?.LogInformation("Reset {Count} error tasks to pending", errorTasks.Count);
            }
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }
}

// New event args class for file overwrite requests
public class FileOverwriteEventArgs : EventArgs
{
    public string TaskId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public bool ShouldOverwrite { get; set; }
    public bool UserDecisionMade { get; set; }
}