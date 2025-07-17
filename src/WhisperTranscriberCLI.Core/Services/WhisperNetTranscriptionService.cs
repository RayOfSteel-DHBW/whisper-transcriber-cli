using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using FFMpegCore;
using Whisper.net;
using Whisper.net.Ggml;
using WhisperTranscriberCLI.Core.Interfaces;
using WhisperTranscriberCLI.Core.Events;

namespace WhisperTranscriberCLI.Core.Services
{
    public sealed class WhisperNetTranscriptionService : ITranscriptionService
    {
        private readonly WhisperFactory? _factory;
        private readonly IMediaConverter _converter;
        private readonly bool _isAvailable;
        private readonly string _unavailabilityReason;

        public event EventHandler<TranscriptionProgressEventArgs>? ProgressChanged;
        
        public bool IsAvailable => _isAvailable;
        public string UnavailabilityReason => _unavailabilityReason;

        public WhisperNetTranscriptionService(IMediaConverter converter, bool useGpu = false, string? modelName = null)
        {
            _converter = converter;

            try
            {
                // Find the best available model or use specified model
                string modelPath = string.IsNullOrEmpty(modelName) 
                    ? FindBestAvailableModel() 
                    : FindSpecificModel(modelName);

                if (string.IsNullOrEmpty(modelPath))
                {
                    _isAvailable = false;
                    _unavailabilityReason = string.IsNullOrEmpty(modelName)
                        ? "No Whisper models found. Please place model files (.bin) in the whispermodels directory."
                        : $"Specified model '{modelName}' not found in whispermodels directory.";
                    return;
                }

                _factory = WhisperFactory.FromPath(modelPath);
                _isAvailable = true;
                _unavailabilityReason = string.Empty;
                
                string accelerationNote = useGpu 
                    ? "(GPU acceleration enabled - will use CUDA/Vulkan if available)" 
                    : "(CPU-only mode)";
                Console.WriteLine($"Loaded model: {Path.GetFileName(modelPath)} {accelerationNote}");
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _unavailabilityReason = $"Failed to initialize Whisper model: {ex.Message}";
            }
        }

        private static string FindBestAvailableModel()
        {
            var modelDir = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
            
            if (!Directory.Exists(modelDir))
            {
                return string.Empty;
            }

            var modelFiles = Directory.GetFiles(modelDir, "*.bin");
            
            if (modelFiles.Length == 0)
            {
                return string.Empty;
            }

            // Preferred model order (faster/smaller first for better user experience)
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

            // Try to find preferred models first
            foreach (string preferredModel in preferredModels)
            {
                string preferredPath = Path.Combine(modelDir, preferredModel);
                if (File.Exists(preferredPath))
                {
                    return preferredPath;
                }
            }

            // If no preferred model found, return the first available .bin file
            return modelFiles[0];
        }

        private static string FindSpecificModel(string modelName)
        {
            var modelDir = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
            
            if (!Directory.Exists(modelDir))
            {
                return string.Empty;
            }

            string modelPath = Path.Combine(modelDir, modelName);
            return File.Exists(modelPath) ? modelPath : string.Empty;
        }

        public async Task<string> TranscribeAsync(string mediaPath, CancellationToken ct)
        {
            if (!_isAvailable || _factory == null)
            {
                throw new InvalidOperationException($"Whisper transcription service is not available. Reason: {_unavailabilityReason}");
            }

            string wavPath = string.Empty;
            try
            {
                await using WhisperProcessor processor = _factory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();
                
                wavPath = await _converter.ConvertToWavAsync(mediaPath, ct);
                await using FileStream stream = File.OpenRead(wavPath);
                
                List<SegmentData> segments = new List<SegmentData>();
                var startTime = DateTime.Now;
                var lastUpdateTime = DateTime.Now;
                TimeSpan lastProcessedTime = TimeSpan.Zero;
                
                await foreach (SegmentData segment in processor.ProcessAsync(stream, ct))
                {
                    segments.Add(segment);
                    
                    // Update progress every second or so
                    var now = DateTime.Now;
                    if ((now - lastUpdateTime).TotalSeconds >= 1.0)
                    {
                        var elapsedWallTime = now - startTime;
                        var processedAudioTime = segment.End;
                        
                        if (elapsedWallTime.TotalSeconds > 0 && processedAudioTime.TotalSeconds > 0)
                        {
                            var speed = processedAudioTime.TotalSeconds / elapsedWallTime.TotalSeconds;
                            var progress = processedAudioTime.TotalSeconds / (segments.Count > 0 ? segments.Last().End.TotalSeconds : processedAudioTime.TotalSeconds);
                            
                            ProgressChanged?.Invoke(this, new TranscriptionProgressEventArgs
                            {
                                Progress = Math.Min(progress, 1.0),
                                ProcessedTime = processedAudioTime,
                                ProcessingSpeed = speed,
                                EstimatedTimeRemaining = speed > 0 ? TimeSpan.FromSeconds((1.0 - progress) * processedAudioTime.TotalSeconds / speed) : TimeSpan.Zero
                            });
                        }
                        
                        lastUpdateTime = now;
                        lastProcessedTime = processedAudioTime;
                    }
                }
                
                // Final progress update
                ProgressChanged?.Invoke(this, new TranscriptionProgressEventArgs
                {
                    Progress = 1.0,
                    ProcessedTime = segments.Count > 0 ? segments.Last().End : TimeSpan.Zero,
                    ProcessingSpeed = 1.0,
                    EstimatedTimeRemaining = TimeSpan.Zero
                });
                
                string outPath = Path.ChangeExtension(mediaPath, ".srt");
                await File.WriteAllTextAsync(outPath, ToSrt(segments), ct);
                return outPath;
            }
            finally
            {
                // Clean up temporary WAV file
                if (!string.IsNullOrEmpty(wavPath) && File.Exists(wavPath))
                {
                    try
                    {
                        File.Delete(wavPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors - temp files will be cleaned by OS eventually
                    }
                }
            }
        }

        private static string ToSrt(IReadOnlyList<SegmentData> segments)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                SegmentData s = segments[i];
                sb.AppendLine((i + 1).ToString());
                sb.AppendLine($"{FormatTime(s.Start)} --> {FormatTime(s.End)}");
                sb.AppendLine(s.Text.Trim());
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string FormatTime(TimeSpan ts)
        {
            return ts.ToString(@"hh\:mm\:ss\,fff");
        }
    }
}