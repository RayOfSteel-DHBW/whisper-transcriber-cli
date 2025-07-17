using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;

namespace WhisperTranscriberCLI.Core.Services;

public class SystemCheckService
{
    public async Task<SystemCheckResult> CheckSystemRequirementsAsync()
    {
        var result = new SystemCheckResult();
        
        // Check FFmpeg availability
        result.FFmpegAvailable = await CheckFFmpegAsync();
        
        // Check whisper models
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
        catch (Exception)
        {
            return false;
        }
    }
    
    private bool CheckWhisperModels()
    {
        try
        {
            var modelDir = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
            if (!Directory.Exists(modelDir))
                return false;
                
            var modelFiles = Directory.GetFiles(modelDir, "*.bin");
            return modelFiles.Length > 0;
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
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
        catch (Exception)
        {
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
        return "Whisper model files are required for transcription. Please:\n\n" +
               "1. Download Whisper model files (.bin format) from:\n" +
               "   https://huggingface.co/ggerganov/whisper.cpp/tree/main\n" +
               "2. Place the model files in the 'whispermodels' folder\n" +
               "3. Recommended models:\n" +
               "   • ggml-base.bin (good balance of speed/accuracy)\n" +
               "   • ggml-small.bin (faster)\n" +
               "   • ggml-large-v3.bin (most accurate)\n\n" +
               "The application will create the whispermodels folder if it doesn't exist.";
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