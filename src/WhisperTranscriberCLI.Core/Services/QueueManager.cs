using System.Text.Json;
using WhisperTranscriberCLI.Core.Models;
using WhisperTranscriberCLI.Core.Events;
using WhisperTranscriberCLI.Core.Interfaces;

namespace WhisperTranscriberCLI.Core.Services;

public class QueueManager : IDisposable
{
    private readonly string _queueFilePath;
    private readonly IMediaConverter _mediaConverter;
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

    public QueueManager(string queueFilePath, IMediaConverter mediaConverter, bool useGpu = false)
    {
        _queueFilePath = queueFilePath;
        _mediaConverter = mediaConverter;
        _useGpu = useGpu;
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
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Processing);

            var transcriptionService = new WhisperNetTranscriptionService(
                _mediaConverter, 
                _useGpu, 
                task.ModelName
            );

            var outputPath = await transcriptionService.TranscribeAsync(
                task.FilePath, 
                _cancellationTokenSource.Token
            );

            task.OutputPath = outputPath;
            task.CompletedAt = DateTime.UtcNow;
            await UpdateTaskStatusAsync(task, Models.TaskStatus.Done);

            TaskCompleted?.Invoke(this, task);
        }
        catch (Exception ex)
        {
            task.ErrorMessage = ex.Message;
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
            System.Diagnostics.Debug.WriteLine($"Failed to save queue: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to load queue: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _cancellationTokenSource?.Dispose();
        _queueSemaphore?.Dispose();
    }
}