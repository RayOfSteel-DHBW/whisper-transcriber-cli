using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FFMpegCore;

namespace WhisperTranscriberCLI.Core.Services;

public class SystemCheckService
{
    private readonly ModelDiscovery _modelDiscovery;
    private readonly ILogger<SystemCheckService>? _logger;

    public SystemCheckService(ModelDiscovery modelDiscovery, ILogger<SystemCheckService>? logger = null)
    {
        _logger = logger;
        _modelDiscovery = modelDiscovery;
    }

    public async Task<SystemCheckResult> CheckSystemRequirementsAsync()
    {
        var result = new SystemCheckResult();
        
        // Check FFmpeg availability
        result.FFmpegAvailable = await CheckFFmpegAsync();
        
        // Check whisper models using ModelDiscovery
        result.WhisperModelsAvailable = CheckWhisperModels();
        
        // Check .NET runtime
        result.DotNetRuntimeAvailable = CheckDotNetRuntime();
        
        // Check disk space (basic check)
        result.SufficientDiskSpace = CheckDiskSpace();
        
        result.IsSystemReady = result.FFmpegAvailable && 
                              result.WhisperModelsAvailable && 
                              result.DotNetRuntimeAvailable && 
                              result.SufficientDiskSpace;
        
        return result;
    }
    
    private async Task<bool> CheckFFmpegAsync()
    {
        try
        {
            // Try to run FFmpeg to check if it's available
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();
            await process.WaitForExitAsync();
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check FFmpeg availability");
            return false;
        }
    }
    
    private bool CheckWhisperModels()
    {
        try
        {
            // Use ModelDiscovery to check for models consistently
            var models = _modelDiscovery.GetAvailableModels();
            var hasModels = models.Count > 0;
            
            _logger?.LogInformation("Found {ModelCount} models in directory: {ModelDirectory}", models.Count, _modelDiscovery.ModelDirectory);
            
            return hasModels;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check Whisper models");
            return false;
        }
    }
    
    private bool CheckDotNetRuntime()
    {
        try
        {
            // Check if we can access the current runtime
            var version = Environment.Version;
            return version.Major >= 8;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check .NET runtime");
            return false;
        }
    }
    
    private bool CheckDiskSpace()
    {
        try
        {
            var currentDir = Directory.GetCurrentDirectory();
            var driveInfo = new DriveInfo(Path.GetPathRoot(currentDir) ?? "C:");
            
            // Check if we have at least 1GB of free space
            return driveInfo.AvailableFreeSpace > 1024 * 1024 * 1024;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check disk space");
            return true; // Assume OK if we can't check
        }
    }
    
    public string GetFFmpegInstallationInstructions()
    {
        return "FFmpeg is required for audio/video processing. Please install FFmpeg:\n\n" +
               "1. Download FFmpeg from https://ffmpeg.org/download.html\n" +
               "2. Extract the files to a folder (e.g., C:\\ffmpeg)\n" +
               "3. Add the bin folder to your system PATH environment variable\n" +
               "4. Restart the application\n\n" +
               "Alternative: Use a package manager like Chocolatey or Scoop:\n" +
               "• choco install ffmpeg\n" +
               "• scoop install ffmpeg";
    }
    
    public string GetWhisperModelsInstructions()
    {
        var modelDir = _modelDiscovery.ModelDirectory;
        var instructions = "Whisper model files are required for transcription. Please:\n\n" +
               "1. Download Whisper model files (.bin format) from:\n" +
               "   https://huggingface.co/ggerganov/whisper.cpp/tree/main\n" +
               "2. Place the model files in the models folder\n" +
               "3. Recommended models:\n" +
               "   • ggml-base.bin (good balance of speed/accuracy)\n" +
               "   • ggml-small.bin (faster)\n" +
               "   • ggml-large-v3.bin (most accurate)\n\n";

        if (!string.IsNullOrEmpty(modelDir))
        {
            instructions += $"Current models directory: {modelDir}";
        }
        else
        {
            instructions += "Use the Settings button to configure the models directory.";
        }

        return instructions;
    }
}

public class SystemCheckResult
{
    public bool FFmpegAvailable { get; set; }
    public bool WhisperModelsAvailable { get; set; }
    public bool DotNetRuntimeAvailable { get; set; }
    public bool SufficientDiskSpace { get; set; }
    public bool IsSystemReady { get; set; }
    
    public string GetStatusMessage()
    {
        if (IsSystemReady)
            return "System is ready for transcription.";
            
        var issues = new List<string>();
        
        if (!FFmpegAvailable)
            issues.Add("FFmpeg not found");
        if (!WhisperModelsAvailable)
            issues.Add("No Whisper models found");
        if (!DotNetRuntimeAvailable)
            issues.Add(".NET 8.0 runtime required");
        if (!SufficientDiskSpace)
            issues.Add("Insufficient disk space");
            
        return $"System check failed: {string.Join(", ", issues)}";
    }
}