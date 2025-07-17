using System.Text.Json.Serialization;

namespace WhisperTranscriberCLI.Core.Models;

public class TranscriptionQueue
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("tasks")]
    public List<TranscriptionTask> Tasks { get; set; } = new();

    [JsonPropertyName("settings")]
    public QueueSettings Settings { get; set; } = new();
}

public class QueueSettings
{
    [JsonPropertyName("defaultModel")]
    public string DefaultModel { get; set; } = "ggml-base.bin";

    [JsonPropertyName("defaultLanguage")]
    public string DefaultLanguage { get; set; } = "auto";

    [JsonPropertyName("recursive")]
    public bool Recursive { get; set; } = true;

    [JsonPropertyName("outputDirectory")]
    public string OutputDirectory { get; set; } = string.Empty;

    [JsonPropertyName("lastQueuePath")]
    public string LastQueuePath { get; set; } = string.Empty;

    [JsonPropertyName("windowBounds")]
    public WindowBounds WindowBounds { get; set; } = new();

    [JsonPropertyName("columnWidths")]
    public Dictionary<string, int> ColumnWidths { get; set; } = new()
    {
        { "file", 200 },
        { "duration", 80 },
        { "model", 100 },
        { "lang", 60 },
        { "status", 100 },
        { "progress", 120 }
    };

    [JsonPropertyName("autoSaveInterval")]
    public int AutoSaveInterval { get; set; } = 300;

    [JsonPropertyName("systemTrayEnabled")]
    public bool SystemTrayEnabled { get; set; } = true;

    [JsonPropertyName("minimizeToTray")]
    public bool MinimizeToTray { get; set; } = true;

    [JsonPropertyName("closeToTray")]
    public bool CloseToTray { get; set; } = false;

    [JsonPropertyName("openOutputAfterCompletion")]
    public bool OpenOutputAfterCompletion { get; set; } = false;
}

public class WindowBounds
{
    [JsonPropertyName("x")]
    public int X { get; set; } = 100;

    [JsonPropertyName("y")]
    public int Y { get; set; } = 100;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 800;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 600;
}