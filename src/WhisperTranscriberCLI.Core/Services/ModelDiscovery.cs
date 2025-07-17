namespace WhisperTranscriberCLI.Core.Services;

public class ModelDiscovery
{
    private readonly string _modelDirectory;

    public ModelDiscovery(string? modelDirectory = null)
    {
        _modelDirectory = modelDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
    }

    public List<ModelInfo> GetAvailableModels()
    {
        var models = new List<ModelInfo>();

        if (!Directory.Exists(_modelDirectory))
        {
            return models;
        }

        var modelFiles = Directory.GetFiles(_modelDirectory, "*.bin");

        foreach (var modelFile in modelFiles)
        {
            var fileInfo = new FileInfo(modelFile);
            var modelName = Path.GetFileName(modelFile);
            
            models.Add(new ModelInfo
            {
                Name = modelName,
                Path = modelFile,
                Size = fileInfo.Length,
                FormattedSize = FormatFileSize(fileInfo.Length),
                LastModified = fileInfo.LastWriteTime
            });
        }

        return models.OrderBy(m => GetModelPriority(m.Name)).ToList();
    }

    public string? FindBestAvailableModel()
    {
        var models = GetAvailableModels();
        return models.FirstOrDefault()?.Path;
    }

    public string? FindModel(string modelName)
    {
        var modelPath = Path.Combine(_modelDirectory, modelName);
        return File.Exists(modelPath) ? modelPath : null;
    }

    private static int GetModelPriority(string modelName)
    {
        string[] preferredModels = {
            "ggml-small.bin",
            "ggml-base.bin",
            "ggml-medium.bin",
            "ggml-medium-q8_0.bin",
            "ggml-tiny.bin",
            "ggml-large-v3-turbo.bin",
            "ggml-large-v3.bin",
            "ggml-large-v2.bin",
            "ggml-large.bin"
        };

        for (int i = 0; i < preferredModels.Length; i++)
        {
            if (modelName.Equals(preferredModels[i], StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}

public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}