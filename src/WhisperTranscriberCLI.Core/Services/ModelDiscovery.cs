using Microsoft.Extensions.Logging;

namespace WhisperTranscriberCLI.Core.Services;

public class ModelDiscovery
{
    private readonly UserSettingsService _settingsService;
    private readonly ILogger<ModelDiscovery>? _logger;
    private string _modelDirectory;

    public ModelDiscovery(ILogger<ModelDiscovery>? logger = null)
    {
        _logger = logger;
        // Pass null to UserSettingsService instead of mismatched logger type
        _settingsService = new UserSettingsService(null);
        _modelDirectory = GetModelDirectory();
    }

    public string ModelDirectory => _modelDirectory;

    public bool NeedsModelPathSetup
    {
        get
        {
            try
            {
                _logger?.LogDebug("Checking if model path setup is needed");
                
                // Check if we have a saved user preference
                var savedPath = _settingsService.Settings.ModelsPath;
                _logger?.LogDebug("User saved path: '{SavedPath}'", savedPath);
                
                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath) && 
                    Directory.GetFiles(savedPath, "*.bin").Length > 0)
                {
                    var modelCount = Directory.GetFiles(savedPath, "*.bin").Length;
                    _logger?.LogDebug("User saved path is valid with {ModelCount} models", modelCount);
                    return false; // User has valid saved preference
                }

                // Check if central repo path exists (for dev environment)
                var repoPath = GetCentralRepoPath();
                _logger?.LogDebug("Central repo path: '{RepoPath}'", repoPath);
                
                if (!string.IsNullOrEmpty(repoPath) && Directory.Exists(repoPath) && 
                    Directory.GetFiles(repoPath, "*.bin").Length > 0)
                {
                    var modelCount = Directory.GetFiles(repoPath, "*.bin").Length;
                    _logger?.LogDebug("Central repo path is valid with {ModelCount} models", modelCount);
                    // Auto-save this path to avoid future prompts in dev environment
                    _settingsService.SetModelsPath(repoPath);
                    _modelDirectory = repoPath;
                    _logger?.LogInformation("Auto-saved central repo path to user settings");
                    return false;
                }

                // Check current directory fallback
                var localPath = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
                _logger?.LogDebug("Local fallback path: '{LocalPath}'", localPath);
                
                if (Directory.Exists(localPath) && Directory.GetFiles(localPath, "*.bin").Length > 0)
                {
                    _logger?.LogDebug("Local fallback path is valid with {ModelCount} models", Directory.GetFiles(localPath, "*.bin").Length);
                    return false;
                }

                // No valid path found - need setup
                _logger?.LogWarning("No valid model paths found - setup needed");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking model path setup requirements");
                return true; // Assume setup needed if error occurs
            }
        }
    }

    public void SetModelDirectory(string modelDirectory)
    {
        try
        {
            if (Directory.Exists(modelDirectory))
            {
                _modelDirectory = modelDirectory;
                _settingsService.SetModelsPath(modelDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set model directory to {ModelDirectory}", modelDirectory);
        }
    }

    public List<ModelInfo> GetAvailableModels()
    {
        var models = new List<ModelInfo>();

        try
        {
            if (!Directory.Exists(_modelDirectory))
            {
                return models;
            }

            var modelFiles = Directory.GetFiles(_modelDirectory, "*.bin");

            foreach (var modelFile in modelFiles)
            {
                try
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to process model file {modelFile}: {ex.Message}");
                    // Continue processing other files
                }
            }

            return models.OrderBy(m => GetModelPriority(m.Name)).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get available models from {_modelDirectory}: {ex.Message}");
            return models;
        }
    }

    public string? FindBestAvailableModel()
    {
        try
        {
            var models = GetAvailableModels();
            return models.FirstOrDefault()?.Path;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find best available model");
            return null;
        }
    }

    public string? FindModel(string modelName)
    {
        try
        {
            if (!Directory.Exists(_modelDirectory))
                return null;

            var modelPath = Path.Combine(_modelDirectory, modelName);
            return File.Exists(modelPath) ? modelPath : null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find model {ModelName}", modelName);
            return null;
        }
    }

    public void ResetModelPath()
    {
        try
        {
            // Clear the saved path to force setup dialog
            _settingsService.SetModelsPath(string.Empty);
            _modelDirectory = string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reset model path");
        }
    }

    private string GetModelDirectory()
    {
        try
        {
            // First, try user's saved preference
            var savedPath = _settingsService.Settings.ModelsPath;
            if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
            {
                // Verify it still has models
                if (Directory.GetFiles(savedPath, "*.bin").Length > 0)
                {
                    return savedPath;
                }
            }

            // Try central repo path (for dev environment)
            var repoPath = GetCentralRepoPath();
            if (!string.IsNullOrEmpty(repoPath) && Directory.Exists(repoPath) && 
                Directory.GetFiles(repoPath, "*.bin").Length > 0)
            {
                return repoPath;
            }

            // Fallback to current directory
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
            if (Directory.Exists(localPath) && Directory.GetFiles(localPath, "*.bin").Length > 0)
            {
                return localPath;
            }

            // If nothing found, return empty (will trigger setup)
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get model directory");
            return string.Empty;
        }
    }

    private string GetCentralRepoPath()
    {
        // Central repo path for dev environment
        return @"C:\Users\Rainer\source\repos\RayOfSteel-DHBW\whisper-transcriber-cli\whispermodels";
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