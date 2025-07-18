using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using WhisperTranscriberCLI.Core.Models;

namespace WhisperTranscriberCLI.TaskUI.Services;

[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

public class SettingsService
{
    private const string SettingsFileName = "appsettings.json";
    private ApplicationDataContainer? _localSettings;
    private AppSettings _settings;
    private bool _useFileStorage = false;

    public SettingsService()
    {
        _settings = new AppSettings();
        
        // For unpackaged WinUI 3 apps, ApplicationData.Current is not available
        // Force file storage for better reliability
        _useFileStorage = IsUnpackagedApp();
        
        LoadSettings();
    }

    public AppSettings Settings => _settings;

    private static bool IsUnpackagedApp()
    {
        // Check if we're running as an unpackaged app
        try
        {
            var package = Windows.ApplicationModel.Package.Current;
            return false; // We have a package
        }
        catch
        {
            return true; // No package = unpackaged app
        }
    }

    private static void LogError(Exception ex, string message = "")
    {
        if (!string.IsNullOrEmpty(message))
        {
            LogError(message);
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Exception Type: {ex.GetType().Name}");
        builder.AppendLine($"Message: {ex.Message}");
        builder.AppendLine($"StackTrace:\n{ex.StackTrace}");
        LogError(builder.ToString());
        
        if (ex.InnerException != null)
        {
            LogError("--- Inner Exception ---");
            LogError(ex.InnerException);
        }
    }

    private static void LogError(string message)
    {
        // Use both Debug.WriteLine (for Visual Studio) and Console.WriteLine (for standalone)
        Debug.WriteLine(message);
        try 
        { 
            Console.WriteLine(message);
        } 
        catch 
        { 
            // Ignore console errors in WinUI apps
        }
    }

    public void LoadSettings()
    {
        if (_useFileStorage)
        {
            LogError("Using file storage for settings (unpackaged app or ApplicationData unavailable)");
            LoadSettingsFromFile();
            return;
        }

        try
        {
            // Initialize ApplicationData.Current safely
            if (_localSettings == null)
            {
                try
                {
                    _localSettings = ApplicationData.Current.LocalSettings;
                    LogError("Successfully initialized ApplicationData.Current");
                }
                catch (InvalidOperationException ex)
                {
                    // ApplicationData.Current is not available, fallback to JSON file
                    _useFileStorage = true;
                    LogError(ex, "ApplicationData.Current not available, falling back to file storage");
                    LoadSettingsFromFile();
                    return;
                }
                catch (Exception ex)
                {
                    // Other exceptions (like "parameter is incorrect"), also fallback to file
                    _useFileStorage = true;
                    LogError(ex, "ApplicationData.Current initialization failed, falling back to file storage");
                    LoadSettingsFromFile();
                    return;
                }
            }

            // Try to load from ApplicationData first
            if (_localSettings?.Values.ContainsKey("OutputDirectory") == true)
            {
                _settings.OutputDirectory = _localSettings.Values["OutputDirectory"] as string ?? "";
            }
            if (_localSettings?.Values.ContainsKey("DefaultModel") == true)
            {
                _settings.DefaultModel = _localSettings.Values["DefaultModel"] as string ?? "ggml-base.bin";
            }
            if (_localSettings?.Values.ContainsKey("DefaultLanguage") == true)
            {
                _settings.DefaultLanguage = _localSettings.Values["DefaultLanguage"] as string ?? "auto";
            }
            if (_localSettings?.Values.ContainsKey("Recursive") == true)
            {
                _settings.Recursive = (bool)(_localSettings.Values["Recursive"] ?? true);
            }
            if (_localSettings?.Values.ContainsKey("WindowWidth") == true)
            {
                _settings.WindowWidth = (int)(_localSettings.Values["WindowWidth"] ?? 800);
            }
            if (_localSettings?.Values.ContainsKey("WindowHeight") == true)
            {
                _settings.WindowHeight = (int)(_localSettings.Values["WindowHeight"] ?? 600);
            }
            if (_localSettings?.Values.ContainsKey("WindowX") == true)
            {
                _settings.WindowX = (int)(_localSettings.Values["WindowX"] ?? 100);
            }
            if (_localSettings?.Values.ContainsKey("WindowY") == true)
            {
                _settings.WindowY = (int)(_localSettings.Values["WindowY"] ?? 100);
            }
            if (_localSettings?.Values.ContainsKey("LastQueuePath") == true)
            {
                _settings.LastQueuePath = _localSettings.Values["LastQueuePath"] as string ?? "";
            }
            if (_localSettings?.Values.ContainsKey("MinimizeToTray") == true)
            {
                _settings.MinimizeToTray = (bool)(_localSettings.Values["MinimizeToTray"] ?? true);
            }
            if (_localSettings?.Values.ContainsKey("CloseToTray") == true)
            {
                _settings.CloseToTray = (bool)(_localSettings.Values["CloseToTray"] ?? false);
            }
            if (_localSettings?.Values.ContainsKey("SystemTrayEnabled") == true)
            {
                _settings.SystemTrayEnabled = (bool)(_localSettings.Values["SystemTrayEnabled"] ?? true);
            }
            if (_localSettings?.Values.ContainsKey("OpenOutputAfterCompletion") == true)
            {
                _settings.OpenOutputAfterCompletion = (bool)(_localSettings.Values["OpenOutputAfterCompletion"] ?? false);
            }
            if (_localSettings?.Values.ContainsKey("UseGpu") == true)
            {
                _settings.UseGpu = (bool)(_localSettings.Values["UseGpu"] ?? false);
            }
            
            LogError("Successfully loaded settings from ApplicationData");
        }
        catch (Exception ex)
        {
            // If loading fails, fallback to file storage
            LogError(ex, "Failed to load settings from ApplicationData");
            _useFileStorage = true;
            LoadSettingsFromFile();
        }
    }

    private void LoadSettingsFromFile()
    {
        try
        {
            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                          "WhisperTranscriberCLI", SettingsFileName);
            
            LogError($"Loading settings from file: {settingsPath}");
            
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var loadedSettings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                if (loadedSettings != null)
                {
                    _settings = loadedSettings;
                    LogError($"Successfully loaded settings from file");
                }
            }
            else
            {
                LogError($"Settings file does not exist, using defaults");
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to load settings from file");
        }
    }

    public void SaveSettings()
    {
        if (_useFileStorage)
        {
            SaveSettingsToFile();
            return;
        }

        try
        {
            // Try to save to ApplicationData first
            if (_localSettings != null)
            {
                _localSettings.Values["OutputDirectory"] = _settings.OutputDirectory;
                _localSettings.Values["DefaultModel"] = _settings.DefaultModel;
                _localSettings.Values["DefaultLanguage"] = _settings.DefaultLanguage;
                _localSettings.Values["Recursive"] = _settings.Recursive;
                _localSettings.Values["WindowWidth"] = _settings.WindowWidth;
                _localSettings.Values["WindowHeight"] = _settings.WindowHeight;
                _localSettings.Values["WindowX"] = _settings.WindowX;
                _localSettings.Values["WindowY"] = _settings.WindowY;
                _localSettings.Values["LastQueuePath"] = _settings.LastQueuePath;
                _localSettings.Values["MinimizeToTray"] = _settings.MinimizeToTray;
                _localSettings.Values["CloseToTray"] = _settings.CloseToTray;
                _localSettings.Values["SystemTrayEnabled"] = _settings.SystemTrayEnabled;
                _localSettings.Values["OpenOutputAfterCompletion"] = _settings.OpenOutputAfterCompletion;
                _localSettings.Values["UseGpu"] = _settings.UseGpu;
                
                LogError("Successfully saved settings to ApplicationData");
            }
            else
            {
                // Fallback to JSON file
                SaveSettingsToFile();
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to save settings to ApplicationData");
            // Try fallback to file
            SaveSettingsToFile();
        }
    }

    private void SaveSettingsToFile()
    {
        try
        {
            var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                         "WhisperTranscriberCLI");
            Directory.CreateDirectory(settingsDir);
            
            var settingsPath = Path.Combine(settingsDir, SettingsFileName);
            var json = JsonSerializer.Serialize(_settings, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(settingsPath, json);
            
            LogError($"Successfully saved settings to file: {settingsPath}");
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to save settings to file");
        }
    }

    public void UpdateWindowPosition(int x, int y, int width, int height)
    {
        _settings.WindowX = x;
        _settings.WindowY = y;
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        SaveSettings();
    }

    public void UpdateOutputDirectory(string path)
    {
        _settings.OutputDirectory = path;
        SaveSettings();
    }

    public void UpdateDefaultModel(string model)
    {
        _settings.DefaultModel = model;
        SaveSettings();
    }

    public void UpdateDefaultLanguage(string language)
    {
        _settings.DefaultLanguage = language;
        SaveSettings();
    }

    public void UpdateRecursive(bool recursive)
    {
        _settings.Recursive = recursive;
        SaveSettings();
    }

    public void UpdateLastQueuePath(string path)
    {
        _settings.LastQueuePath = path;
        SaveSettings();
    }

    public void UpdateUseGpu(bool useGpu)
    {
        _settings.UseGpu = useGpu;
        SaveSettings();
    }
}

public class AppSettings
{
    public string OutputDirectory { get; set; } = "";
    public string DefaultModel { get; set; } = "ggml-base.bin";
    public string DefaultLanguage { get; set; } = "auto";
    public bool Recursive { get; set; } = true;
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
    public int WindowX { get; set; } = 100;
    public int WindowY { get; set; } = 100;
    public string LastQueuePath { get; set; } = "";
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = false;
    public bool SystemTrayEnabled { get; set; } = true;
    public bool OpenOutputAfterCompletion { get; set; } = false;
    public bool UseGpu { get; set; } = false;
}