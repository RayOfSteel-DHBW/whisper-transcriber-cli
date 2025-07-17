using System;
using Xunit;
using WhisperTranscriberCLI.Core.Models;
using TaskStatus = WhisperTranscriberCLI.Core.Models.TaskStatus;

namespace WhisperTranscriberCLI.Core.Tests.Models;

public class TranscriptionTaskTests
{
    [Fact]
    public void TranscriptionTask_DefaultConstructor_SetsDefaults()
    {
        // Arrange & Act
        var task = new TranscriptionTask();

        // Assert
        Assert.NotNull(task.Id);
        Assert.NotEmpty(task.Id);
        Assert.Equal(string.Empty, task.FilePath);
        Assert.Equal(string.Empty, task.ModelName);
        Assert.Equal("auto", task.Language);
        Assert.Equal(string.Empty, task.Duration);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.True(task.AddedAt <= DateTime.UtcNow);
        Assert.True(task.AddedAt >= DateTime.UtcNow.AddMinutes(-1));
        Assert.Null(task.CompletedAt);
        Assert.Null(task.OutputPath);
        Assert.Null(task.ErrorMessage);
        Assert.Equal(0.0, task.Progress);
    }

    [Fact]
    public void TranscriptionTask_SetProperties_UpdatesCorrectly()
    {
        // Arrange
        var task = new TranscriptionTask();
        var testPath = "C:\\test\\audio.mp3";
        var testModel = "ggml-base.bin";
        var testLanguage = "en";
        var testDuration = "00:05:30";
        var testOutput = "C:\\test\\audio.txt";
        var testError = "Test error";
        var testProgress = 50.5;
        var completedTime = DateTime.UtcNow;

        // Act
        task.FilePath = testPath;
        task.ModelName = testModel;
        task.Language = testLanguage;
        task.Duration = testDuration;
        task.Status = TaskStatus.Processing;
        task.CompletedAt = completedTime;
        task.OutputPath = testOutput;
        task.ErrorMessage = testError;
        task.Progress = testProgress;

        // Assert
        Assert.Equal(testPath, task.FilePath);
        Assert.Equal(testModel, task.ModelName);
        Assert.Equal(testLanguage, task.Language);
        Assert.Equal(testDuration, task.Duration);
        Assert.Equal(TaskStatus.Processing, task.Status);
        Assert.Equal(completedTime, task.CompletedAt);
        Assert.Equal(testOutput, task.OutputPath);
        Assert.Equal(testError, task.ErrorMessage);
        Assert.Equal(testProgress, task.Progress);
    }

    [Theory]
    [InlineData(TaskStatus.Pending)]
    [InlineData(TaskStatus.Processing)]
    [InlineData(TaskStatus.Done)]
    [InlineData(TaskStatus.Error)]
    public void TranscriptionTask_StatusEnum_AllValuesValid(TaskStatus status)
    {
        // Arrange & Act
        var task = new TranscriptionTask { Status = status };

        // Assert
        Assert.Equal(status, task.Status);
    }

    [Fact]
    public void TranscriptionTask_UniqueIds_GeneratedForDifferentInstances()
    {
        // Arrange & Act
        var task1 = new TranscriptionTask();
        var task2 = new TranscriptionTask();

        // Assert
        Assert.NotEqual(task1.Id, task2.Id);
    }
}