using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Pipes;
using WhisperTranscriberCLI.Core.Interfaces;

namespace WhisperTranscriberCLI.Core.Services
{
    public sealed class FfmpegMediaConverter : IMediaConverter
    {
        public async Task<string> ConvertToWavAsync(string inputPath, CancellationToken ct)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Input file not found: {inputPath}");

            // Use temporary file in system temp directory instead of target directory
            string tempDir = Path.GetTempPath();
            string tempFileName = $"whisper_temp_{Guid.NewGuid():N}.wav";
            string outputPath = Path.Combine(tempDir, tempFileName);

            try
            {
                // Always convert to ensure 16KHz sample rate and correct format for Whisper
                await FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, true, options => options
                        .WithAudioCodec("pcm_s16le")
                        .WithAudioSamplingRate(16000)  // Force 16KHz sample rate for Whisper
                        .WithCustomArgument("-ac 1")   // Mono audio
                        .ForceFormat("wav"))
                    .CancellableThrough(ct)
                    .ProcessAsynchronously(false);

                return outputPath;
            }
            catch
            {
                // Clean up temp file if conversion failed
                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }
                throw;
            }
        }

        public async Task<Stream> ConvertToWavStreamAsync(string inputPath, CancellationToken ct)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Input file not found: {inputPath}");

            var memoryStream = new MemoryStream();

            try
            {
                // Convert directly to memory stream
                await FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToPipe(new StreamPipeSink(memoryStream), options => options
                        .WithAudioCodec("pcm_s16le")
                        .WithAudioSamplingRate(16000)  // Force 16KHz sample rate for Whisper
                        .WithCustomArgument("-ac 1")   // Mono audio
                        .ForceFormat("wav"))
                    .CancellableThrough(ct)
                    .ProcessAsynchronously(false);

                memoryStream.Position = 0;
                return memoryStream;
            }
            catch
            {
                memoryStream?.Dispose();
                throw;
            }
        }
    }
}