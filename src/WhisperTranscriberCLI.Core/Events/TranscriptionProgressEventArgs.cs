namespace WhisperTranscriberCLI.Core.Events;

public class TranscriptionProgressEventArgs : EventArgs
{
    public string TaskId { get; set; } = string.Empty;
    public double Progress { get; set; }
    public TimeSpan ProcessedTime { get; set; }
    public TimeSpan TotalTime { get; set; }
    public double ProcessingSpeed { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
}

public class TranscriptionStatusEventArgs : EventArgs
{
    public string TaskId { get; set; } = string.Empty;
    public Models.TaskStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputPath { get; set; }
}