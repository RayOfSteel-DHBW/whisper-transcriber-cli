using System.Text.Json.Serialization;

namespace WhisperTranscriberCLI.Core.Models;

public class TranscriptionTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("modelName")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "auto";

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    [JsonPropertyName("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("lastStartedAt")]
    public DateTime? LastStartedAt { get; set; }

    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("progress")]
    public double Progress { get; set; } = 0.0;
}

public enum TaskStatus
{
    Pending,
    Processing,
    Done,
    Error,
    Paused
}