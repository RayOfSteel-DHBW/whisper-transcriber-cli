using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WhisperTranscriberCLI.Core.Services;

[JsonSerializable(typeof(UserSettings))]
internal partial class UserSettingsJsonContext : JsonSerializerContext
{
}

public class UserSettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<UserSettingsService>? _logger;
    private UserSettings _settings;

    public UserSettingsService(ILogger<UserSettingsService>? logger = null)
    {
        _logger = logger;
        // Store settings in user's app data directory
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "WhisperTranscriberCLI"
        );
        Directory.CreateDirectory(appDataDir);
        _settingsFilePath = Path.Combine(appDataDir, "settings.json");
        
        _settings = LoadSettings();
    }

    public UserSettings Settings => _settings;

    public void SetModelsPath(string modelsPath)
    {
        _settings.ModelsPath = modelsPath;
        SaveSettings();
    }

    public void SetLastUsedModel(string modelName)
    {
        _settings.LastUsedModel = modelName;
        SaveSettings();
    }

    private UserSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize(json, UserSettingsJsonContext.Default.UserSettings);
                return settings ?? new UserSettings();
            }
        }
        catch (Exception ex)
        {
            // Log the error but continue with defaults
            _logger?.LogWarning(ex, "Failed to load user settings, using defaults");
        }

        return new UserSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, UserSettingsJsonContext.Default.UserSettings);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the app
            _logger?.LogError(ex, "Failed to save user settings");
        }
    }
}

public class UserSettings
{
    public string ModelsPath { get; set; } = string.Empty;
    public string LastUsedModel { get; set; } = string.Empty;
    public bool FirstRun { get; set; } = true;
}