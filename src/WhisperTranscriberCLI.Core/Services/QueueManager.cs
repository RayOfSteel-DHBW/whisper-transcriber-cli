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
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly SemaphoreSlim _queueSemaphore;
    private TranscriptionQueue _queue;
    private bool _isProcessing;
    private bool _isPaused;
    private bool _useGpu;

    public event EventHandler<TranscriptionProgressEventArgs>? ProgressChanged;
    public event EventHandler<TranscriptionStatusEventArgs>? StatusChanged;
    public event EventHandler<TranscriptionTask>? TaskCompleted;
    public event EventHandler<TranscriptionTask>? TaskFailed;

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
    }

    public TranscriptionQueue Queue => _queue;
    public bool IsProcessing => _isProcessing;
    public bool IsPaused => _isPaused;

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

    public async Task StartProcessingAsync()
    {
        if (_isProcessing)
            return;

        _isProcessing = true;
        _isPaused = false;

        await Task.Run(async () =>
        {
            while (_isProcessing && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    continue;
                }

                var nextTask = await GetNextPendingTaskAsync();
                if (nextTask == null)
                {
                    _isProcessing = false;
                    break;
                }

                await ProcessTaskAsync(nextTask);
            }
        });
    }

    public void PauseProcessing()
    {
        _isPaused = true;
    }

    public void ResumeProcessing()
    {
        _isPaused = false;
    }

    public void StopProcessing()
    {
        _isProcessing = false;
        _isPaused = false;
        _cancellationTokenSource.Cancel();
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

            // Create transcription service with proper logger type
            var transcriptionLogger = _logger as ILogger<WhisperNetTranscriptionService> ?? 
                                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<WhisperNetTranscriptionService>();
            
            var transcriptionService = new WhisperNetTranscriptionService(
                _mediaConverter, 
                _useGpu, 
                task.ModelName,
                transcriptionLogger
            );

            // Subscribe to progress events to forward to UI
            transcriptionService.ProgressChanged += (sender, progressArgs) =>
            {
                // Update task progress
                task.Progress = progressArgs.Progress * 100; // Convert to percentage
                
                // Forward progress event to UI
                ProgressChanged?.Invoke(this, progressArgs);
                
                _logger?.LogTrace("Progress update for task {TaskId}: {Progress:F1}%", task.Id, task.Progress);
            };

            // Check if service is available before attempting transcription
            if (!transcriptionService.IsAvailable)
            {
                var error = $"Transcription service not available: {transcriptionService.UnavailabilityReason}";
                _logger?.LogError("Task {TaskId} failed - {Error}", task.Id, error);
                throw new InvalidOperationException(error);
            }

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
            task.ErrorMessage = "Transcription was cancelled";
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Error);
            TaskFailed?.Invoke(this, task);
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
}