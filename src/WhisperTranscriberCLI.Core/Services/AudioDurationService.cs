using FFMpegCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WhisperTranscriberCLI.Core.Services;

public class AudioDurationService
{
    private static void LogError(string message)
    {
        // Use both Debug.WriteLine (for Visual Studio) and Console.WriteLine (for standalone)
        System.Diagnostics.Debug.WriteLine(message);
        try 
        { 
            Console.WriteLine(message); 
        } 
        catch 
        { 
            // Ignore console errors in WinUI apps
        }
    }

    public async Task<TimeSpan> GetDurationAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return TimeSpan.Zero;
            }

            var mediaInfo = await FFProbe.AnalyseAsync(filePath);
            return mediaInfo.Duration;
        }
        catch (Exception ex)
        {
            // If FFmpeg analysis fails, return zero duration
            LogError($"Failed to analyze audio duration for {filePath}: {ex.Message}");
            return TimeSpan.Zero;
        }
    }

    public string FormatDuration(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
            return "00:00:00";

        return duration.ToString(@"hh\:mm\:ss");
    }
}