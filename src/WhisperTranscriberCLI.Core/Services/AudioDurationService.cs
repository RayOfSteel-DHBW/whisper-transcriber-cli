using FFMpegCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WhisperTranscriberCLI.Core.Services;

public class AudioDurationService
{
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
        catch (Exception)
        {
            // If FFmpeg analysis fails, return zero duration
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