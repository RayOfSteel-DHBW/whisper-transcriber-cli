using FFMpegCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WhisperTranscriberCLI.Core.Services;

public class AudioDurationService
{
    private readonly ILogger<AudioDurationService>? _logger;
    
    public AudioDurationService(ILogger<AudioDurationService>? logger = null)
    {
        _logger = logger;
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
            _logger?.LogError(ex, "Failed to analyze audio duration for {FilePath}", filePath);
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