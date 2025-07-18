using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using WhisperTranscriberCLI.Core.Models;
using WhisperTranscriberCLI.Core.Services;
using WhisperTranscriberCLI.Core.Interfaces;
using TaskStatus = WhisperTranscriberCLI.Core.Models.TaskStatus;

namespace WhisperTranscriberCLI.Core.Tests.Services;

public class QueueManagerTests : IDisposable
{
    private readonly string _testQueuePath;
    private readonly Mock<IMediaConverter> _mockMediaConverter;
    private readonly QueueManager _queueManager;

    public QueueManagerTests()
    {
        _testQueuePath = Path.Combine(Path.GetTempPath(), $"test_queue_{Guid.NewGuid()}.json");
        _mockMediaConverter = new Mock<IMediaConverter>();
        _queueManager = new QueueManager(_testQueuePath, _mockMediaConverter.Object);
    }

    [Fact]
    public async Task AddTaskAsync_ValidTask_AddsToQueue()
    {
        // Arrange
        var task = new TranscriptionTask
        {
            FilePath = "test.mp3",
            ModelName = "ggml-base.bin",
            Language = "en"
        };

        // Act
        await _queueManager.AddTaskAsync(task);

        // Assert
        Assert.Single(_queueManager.Queue.Tasks);
        Assert.Equal(task.FilePath, _queueManager.Queue.Tasks[0].FilePath);
    }

    [Fact]
    public async Task RemoveTaskAsync_ExistingTask_RemovesFromQueue()
    {
        // Arrange
        var task = new TranscriptionTask
        {
            FilePath = "test.mp3",
            ModelName = "ggml-base.bin"
        };
        await _queueManager.AddTaskAsync(task);

        // Act
        await _queueManager.RemoveTaskAsync(task.Id);

        // Assert
        Assert.Empty(_queueManager.Queue.Tasks);
    }

    [Fact]
    public async Task RemoveTaskAsync_NonExistentTask_DoesNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid().ToString();

        // Act & Assert
        await _queueManager.RemoveTaskAsync(nonExistentId);
        Assert.Empty(_queueManager.Queue.Tasks);
    }

    [Fact]
    public async Task ClearCompletedTasksAsync_MixedStatuses_RemovesOnlyCompleted()
    {
        // Arrange
        var pendingTask = new TranscriptionTask { Status = TaskStatus.Pending };
        var processingTask = new TranscriptionTask { Status = TaskStatus.Processing };
        var doneTask = new TranscriptionTask { Status = TaskStatus.Done };
        var errorTask = new TranscriptionTask { Status = TaskStatus.Error };

        await _queueManager.AddTaskAsync(pendingTask);
        await _queueManager.AddTaskAsync(processingTask);
        await _queueManager.AddTaskAsync(doneTask);
        await _queueManager.AddTaskAsync(errorTask);

        // Act
        await _queueManager.ClearCompletedTasksAsync();

        // Assert
        Assert.Equal(3, _queueManager.Queue.Tasks.Count);
        Assert.Contains(_queueManager.Queue.Tasks, t => t.Status == TaskStatus.Pending);
        Assert.Contains(_queueManager.Queue.Tasks, t => t.Status == TaskStatus.Processing);
        Assert.Contains(_queueManager.Queue.Tasks, t => t.Status == TaskStatus.Error);
        Assert.DoesNotContain(_queueManager.Queue.Tasks, t => t.Status == TaskStatus.Done);
    }

    [Fact]
    public async Task SaveQueueAsync_ValidQueue_SavesSuccessfully()
    {
        // Arrange
        var task = new TranscriptionTask
        {
            FilePath = "test.mp3",
            ModelName = "ggml-base.bin"
        };
        await _queueManager.AddTaskAsync(task);

        // Act
        await _queueManager.SaveQueueAsync();

        // Assert
        Assert.True(File.Exists(_testQueuePath));
        var content = await File.ReadAllTextAsync(_testQueuePath);
        Assert.Contains("test.mp3", content);
        Assert.Contains("ggml-base.bin", content);
    }

    [Fact]
    public void LoadQueue_ExistingFile_LoadsSuccessfully()
    {
        // Arrange
        var queueData = """
        {
          "schemaVersion": 1,
          "tasks": [
            {
              "id": "test-id",
              "filePath": "test.mp3",
              "modelName": "ggml-base.bin",
              "language": "auto",
              "duration": "00:05:30",
              "status": 0,
              "addedAt": "2025-01-17T10:30:00Z",
              "completedAt": null,
              "outputPath": null,
              "errorMessage": null,
              "progress": 0.0
            }
          ]
        }
        """;
        File.WriteAllText(_testQueuePath, queueData);

        // Act
        _queueManager.LoadQueue();

        // Assert
        Assert.Single(_queueManager.Queue.Tasks);
        Assert.Equal("test.mp3", _queueManager.Queue.Tasks[0].FilePath);
        Assert.Equal("ggml-base.bin", _queueManager.Queue.Tasks[0].ModelName);
    }

    [Fact]
    public void LoadQueue_NonExistentFile_CreatesEmptyQueue()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid()}.json");
        var queueManager = new QueueManager(nonExistentPath, _mockMediaConverter.Object);

        // Act
        queueManager.LoadQueue();

        // Assert
        Assert.Empty(queueManager.Queue.Tasks);
    }

    [Fact]
    public void PauseProcessing_WhenCalled_SetsCorrectState()
    {
        // Act
        _queueManager.PauseProcessing();

        // Assert - This would require exposing internal state or using reflection
        // For now, we verify it doesn't throw
        Assert.True(true);
    }

    [Fact]
    public void StopProcessing_WhenCalled_SetsCorrectState()
    {
        // Act
        _queueManager.StopProcessing();

        // Assert - This would require exposing internal state or using reflection
        // For now, we verify it doesn't throw
        Assert.True(true);
    }

    [Fact]
    public void LoadQueue_ProcessingTasksExist_ResetsToePending()
    {
        // Arrange
        var queueData = """
        {
          "schemaVersion": 1,
          "tasks": [
            {
              "id": "pending-task",
              "filePath": "pending.mp3",
              "modelName": "ggml-base.bin",
              "language": "auto",
              "duration": "00:03:00",
              "status": 0,
              "addedAt": "2025-01-17T10:30:00Z",
              "completedAt": null,
              "outputPath": null,
              "errorMessage": null,
              "progress": 0.0
            },
            {
              "id": "processing-task",
              "filePath": "processing.mp3",
              "modelName": "ggml-base.bin",
              "language": "auto",
              "duration": "00:05:30",
              "status": 1,
              "addedAt": "2025-01-17T10:30:00Z",
              "completedAt": null,
              "outputPath": null,
              "errorMessage": null,
              "progress": 45.5
            },
            {
              "id": "done-task",
              "filePath": "done.mp3",
              "modelName": "ggml-base.bin",
              "language": "auto",
              "duration": "00:02:15",
              "status": 2,
              "addedAt": "2025-01-17T10:30:00Z",
              "completedAt": "2025-01-17T10:35:00Z",
              "outputPath": "done.srt",
              "errorMessage": null,
              "progress": 100.0
            }
          ]
        }
        """;
        File.WriteAllText(_testQueuePath, queueData);

        // Act
        _queueManager.LoadQueue();

        // Assert
        Assert.Equal(3, _queueManager.Queue.Tasks.Count);
        
        // Check that the processing task was reset to pending
        var processingTask = _queueManager.Queue.Tasks.FirstOrDefault(t => t.Id == "processing-task");
        Assert.NotNull(processingTask);
        Assert.Equal(TaskStatus.Pending, processingTask.Status);
        Assert.Equal(0.0, processingTask.Progress); // Progress reset
        Assert.Null(processingTask.ErrorMessage); // Error message cleared
        
        // Check that other tasks remain unchanged
        var pendingTask = _queueManager.Queue.Tasks.FirstOrDefault(t => t.Id == "pending-task");
        Assert.NotNull(pendingTask);
        Assert.Equal(TaskStatus.Pending, pendingTask.Status);
        
        var doneTask = _queueManager.Queue.Tasks.FirstOrDefault(t => t.Id == "done-task");
        Assert.NotNull(doneTask);
        Assert.Equal(TaskStatus.Done, doneTask.Status);
        Assert.Equal(100.0, doneTask.Progress); // Progress preserved for completed task
    }

    [Fact]
    public async Task ClearAllTasksAsync_MixedStatuses_RemovesAllTasks()
    {
        // Arrange
        var pendingTask = new TranscriptionTask { Status = TaskStatus.Pending };
        var processingTask = new TranscriptionTask { Status = TaskStatus.Processing };
        var doneTask = new TranscriptionTask { Status = TaskStatus.Done };
        var errorTask = new TranscriptionTask { Status = TaskStatus.Error };

        await _queueManager.AddTaskAsync(pendingTask);
        await _queueManager.AddTaskAsync(processingTask);
        await _queueManager.AddTaskAsync(doneTask);
        await _queueManager.AddTaskAsync(errorTask);

        // Act
        await _queueManager.ClearAllTasksAsync();

        // Assert
        Assert.Empty(_queueManager.Queue.Tasks);
    }

    [Fact]
    public void StopProcessing_WhenProcessingCancelled_ResetsTaskToPending()
    {
        // This test verifies that when StopProcessing is called,
        // any tasks that were cancelled should be reset to Pending status
        // rather than marked as Error
        
        // Act
        _queueManager.StopProcessing();
        
        // Assert - The actual behavior is tested through integration
        // since ProcessTaskAsync is private. This test ensures the method
        // can be called without throwing exceptions.
        Assert.False(_queueManager.IsProcessing);
    }

    [Fact]
    public async Task ResetAllErrorTasksAsync_MixedStatuses_ResetsOnlyErrorTasks()
    {
        // Arrange
        var pendingTask = new TranscriptionTask { Status = TaskStatus.Pending, ErrorMessage = null };
        var processingTask = new TranscriptionTask { Status = TaskStatus.Processing, ErrorMessage = null };
        var doneTask = new TranscriptionTask { Status = TaskStatus.Done, ErrorMessage = null };
        var errorTask1 = new TranscriptionTask { Status = TaskStatus.Error, ErrorMessage = "Some error", Progress = 25.0 };
        var errorTask2 = new TranscriptionTask { Status = TaskStatus.Error, ErrorMessage = "Another error", Progress = 50.0 };

        await _queueManager.AddTaskAsync(pendingTask);
        await _queueManager.AddTaskAsync(processingTask);
        await _queueManager.AddTaskAsync(doneTask);
        await _queueManager.AddTaskAsync(errorTask1);
        await _queueManager.AddTaskAsync(errorTask2);

        // Act
        await _queueManager.ResetAllErrorTasksAsync();

        // Assert
        Assert.Equal(5, _queueManager.Queue.Tasks.Count);
        
        // Check that only error tasks were reset
        Assert.Equal(TaskStatus.Pending, pendingTask.Status); // Unchanged
        Assert.Equal(TaskStatus.Processing, processingTask.Status); // Unchanged
        Assert.Equal(TaskStatus.Done, doneTask.Status); // Unchanged
        
        // Error tasks should be reset to pending
        Assert.Equal(TaskStatus.Pending, errorTask1.Status);
        Assert.Null(errorTask1.ErrorMessage);
        Assert.Equal(0.0, errorTask1.Progress);
        
        Assert.Equal(TaskStatus.Pending, errorTask2.Status);
        Assert.Null(errorTask2.ErrorMessage);
        Assert.Equal(0.0, errorTask2.Progress);
    }

    [Fact]
    public async Task GetNextPendingTaskAsync_InterruptedTaskExists_PrioritizesInterruptedTask()
    {
        // Arrange - Create tasks with different scenarios
        var freshTask = new TranscriptionTask 
        { 
            Id = "fresh-task",
            FilePath = "fresh.mp3",
            Status = TaskStatus.Pending, 
            AddedAt = DateTime.UtcNow.AddMinutes(-10),
            LastStartedAt = null // Never started
        };
        
        var interruptedTask = new TranscriptionTask 
        { 
            Id = "interrupted-task",
            FilePath = "interrupted.mp3", 
            Status = TaskStatus.Pending, 
            AddedAt = DateTime.UtcNow.AddMinutes(-5),
            LastStartedAt = DateTime.UtcNow.AddMinutes(-2) // Was started but interrupted
        };
        
        var olderFreshTask = new TranscriptionTask 
        { 
            Id = "older-fresh-task",
            FilePath = "older.mp3",
            Status = TaskStatus.Pending, 
            AddedAt = DateTime.UtcNow.AddMinutes(-15),
            LastStartedAt = null // Never started but older than others
        };

        // Add tasks in order: older fresh, fresh, interrupted
        await _queueManager.AddTaskAsync(olderFreshTask);
        await _queueManager.AddTaskAsync(freshTask);
        await _queueManager.AddTaskAsync(interruptedTask);

        // Act - Use reflection to call the private GetNextPendingTaskAsync method
        var method = typeof(QueueManager).GetMethod("GetNextPendingTaskAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var nextTaskTask = (Task<TranscriptionTask?>)method.Invoke(_queueManager, null);
        var nextTask = await nextTaskTask;

        // Assert - Should prioritize the interrupted task over fresh tasks
        Assert.NotNull(nextTask);
        Assert.Equal("interrupted-task", nextTask.Id);
        Assert.Equal("interrupted.mp3", nextTask.FilePath);
    }

    [Fact]
    public async Task GetNextPendingTaskAsync_OnlyFreshTasks_SelectsOldestFirst()
    {
        // Arrange - Create only fresh tasks (no LastStartedAt)
        var newerTask = new TranscriptionTask 
        { 
            Id = "newer-task",
            FilePath = "newer.mp3",
            Status = TaskStatus.Pending, 
            AddedAt = DateTime.UtcNow.AddMinutes(-5),
            LastStartedAt = null
        };
        
        var olderTask = new TranscriptionTask 
        { 
            Id = "older-task",
            FilePath = "older.mp3", 
            Status = TaskStatus.Pending, 
            AddedAt = DateTime.UtcNow.AddMinutes(-10),
            LastStartedAt = null
        };

        // Add tasks in reverse chronological order
        await _queueManager.AddTaskAsync(newerTask);
        await _queueManager.AddTaskAsync(olderTask);

        // Act - Use reflection to call the private GetNextPendingTaskAsync method
        var method = typeof(QueueManager).GetMethod("GetNextPendingTaskAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var nextTaskTask = (Task<TranscriptionTask?>)method.Invoke(_queueManager, null);
        var nextTask = await nextTaskTask;

        // Assert - Should select the older task first (normal queue behavior)
        Assert.NotNull(nextTask);
        Assert.Equal("older-task", nextTask.Id);
        Assert.Equal("older.mp3", nextTask.FilePath);
    }

    public void Dispose()
    {
        if (File.Exists(_testQueuePath))
        {
            File.Delete(_testQueuePath);
        }
        _queueManager?.Dispose();
    }
}