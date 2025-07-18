using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using FFMpegCore;
using Whisper.net;
using Whisper.net.Ggml;
using WhisperTranscriberCLI.Core.Interfaces;
using WhisperTranscriberCLI.Core.Events;

namespace WhisperTranscriberCLI.Core.Services
{
    public sealed class WhisperNetTranscriptionService : ITranscriptionService, IDisposable
    {
        private WhisperFactory? _factory;
        private readonly IMediaConverter _converter;
        private readonly bool _isAvailable;
        private readonly string _unavailabilityReason;
        private readonly ILogger<WhisperNetTranscriptionService>? _logger;
        private bool _disposed = false;

        public event EventHandler<TranscriptionProgressEventArgs>? ProgressChanged;
        
        public bool IsAvailable => _isAvailable && !_disposed;
        public string UnavailabilityReason => _disposed ? "Service has been disposed" : _unavailabilityReason;

        public WhisperNetTranscriptionService(IMediaConverter converter, bool useGpu = false, string? modelName = null, ILogger<WhisperNetTranscriptionService>? logger = null)
        {
            _converter = converter;
            _logger = logger;

            try
            {
                _logger?.LogInformation("Initializing WhisperNet transcription service with GPU: {UseGpu}, Model: {ModelName}", useGpu, modelName);
                
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
                    _logger?.LogError("Model initialization failed: {Reason}", _unavailabilityReason);
                    return;
                }

                _logger?.LogInformation("Loading Whisper model from: {ModelPath}", modelPath);
                _factory = WhisperFactory.FromPath(modelPath);
                _isAvailable = true;
                _unavailabilityReason = string.Empty;
                
                string accelerationNote = useGpu 
                    ? "(GPU acceleration enabled - will use CUDA/Vulkan if available)" 
                    : "(CPU-only mode)";
                _logger?.LogInformation("Loaded model: {ModelName} {AccelerationNote}", Path.GetFileName(modelPath), accelerationNote);
                Console.WriteLine($"Loaded model: {Path.GetFileName(modelPath)} {accelerationNote}");
            }
            catch (Exception ex)
            {
                _isAvailable = false;
                _unavailabilityReason = $"Failed to initialize Whisper model: {ex.Message}";
                _logger?.LogError(ex, "Failed to initialize Whisper model");
            }
        }

        private string FindBestAvailableModel()
        {
            _logger?.LogDebug("Searching for best available model");
            
            // Use user settings to get the model directory instead of current directory
            var userSettings = new UserSettingsService();
            var modelDiscovery = new ModelDiscovery(userSettings); // Don't pass logger to avoid type conflicts
            var modelDir = modelDiscovery.ModelDirectory;
            
            _logger?.LogDebug("Model directory: {ModelDir}", modelDir);
            
            if (!Directory.Exists(modelDir))
            {
                _logger?.LogWarning("Model directory does not exist: {ModelDir}", modelDir);
                return string.Empty;
            }

            var modelFiles = Directory.GetFiles(modelDir, "*.bin");
            _logger?.LogInformation("Found {ModelCount} model files in {ModelDir}", modelFiles.Length, modelDir);
            
            if (modelFiles.Length == 0)
            {
                _logger?.LogWarning("No .bin model files found in {ModelDir}", modelDir);
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
                    _logger?.LogInformation("Selected preferred model: {ModelPath}", preferredPath);
                    return preferredPath;
                }
            }

            // If no preferred model found, return the first available .bin file
            var fallbackModel = modelFiles[0];
            _logger?.LogInformation("Using fallback model: {ModelPath}", fallbackModel);
            return fallbackModel;
        }

        private string FindSpecificModel(string modelName)
        {
            _logger?.LogDebug("Searching for specific model: {ModelName}", modelName);
            
            // Use user settings to get the model directory instead of current directory
            var userSettings = new UserSettingsService();
            var modelDiscovery = new ModelDiscovery(userSettings); // Don't pass logger to avoid type conflicts
            var modelDir = modelDiscovery.ModelDirectory;
            
            if (!Directory.Exists(modelDir))
            {
                _logger?.LogWarning("Model directory does not exist: {ModelDir}", modelDir);
                return string.Empty;
            }

            string modelPath = Path.Combine(modelDir, modelName);
            bool exists = File.Exists(modelPath);
            _logger?.LogDebug("Model path: {ModelPath}, Exists: {Exists}", modelPath, exists);
            
            return exists ? modelPath : string.Empty;
        }

        public async Task<string> TranscribeAsync(string mediaPath, CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WhisperNetTranscriptionService));
            }

            if (!_isAvailable || _factory == null)
            {
                var error = $"Whisper transcription service is not available. Reason: {_unavailabilityReason}";
                _logger?.LogError(error);
                throw new InvalidOperationException(error);
            }

            _logger?.LogInformation("Starting transcription of: {MediaPath}", mediaPath);

            string wavPath = string.Empty;
            TimeSpan? totalDuration = null;
            
            try
            {
                // Get the total audio duration first for accurate progress calculation
                try
                {
                    var audioDurationService = new AudioDurationService();
                    totalDuration = await audioDurationService.GetDurationAsync(mediaPath);
                    _logger?.LogDebug("Total audio duration: {Duration}", totalDuration);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to get audio duration for progress calculation");
                }
                
                _logger?.LogDebug("Creating Whisper processor");
                await using WhisperProcessor processor = _factory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();
                
                _logger?.LogDebug("Converting media to WAV format");
                wavPath = await _converter.ConvertToWavAsync(mediaPath, ct);
                _logger?.LogDebug("Converted WAV file: {WavPath}", wavPath);
                
                if (!File.Exists(wavPath))
                {
                    throw new FileNotFoundException($"Converted WAV file not found: {wavPath}");
                }
                
                await using FileStream stream = File.OpenRead(wavPath);
                _logger?.LogDebug("Opening WAV file stream, size: {FileSize} bytes", stream.Length);
                
                List<SegmentData> segments = new List<SegmentData>();
                var startTime = DateTime.Now;
                var lastUpdateTime = DateTime.Now;
                TimeSpan lastProcessedTime = TimeSpan.Zero;
                
                _logger?.LogInformation("Starting Whisper processing");
                await foreach (SegmentData segment in processor.ProcessAsync(stream, ct))
                {
                    segments.Add(segment);
                    _logger?.LogTrace("Processed segment {SegmentCount}: {Start} - {End}: '{Text}'", 
                        segments.Count, segment.Start, segment.End, segment.Text?.Trim());
                    
                    // Update progress every second or so
                    var now = DateTime.Now;
                    if ((now - lastUpdateTime).TotalSeconds >= 1.0)
                    {
                        var elapsedWallTime = now - startTime;
                        var processedAudioTime = segment.End;
                        
                        if (elapsedWallTime.TotalSeconds > 0 && processedAudioTime.TotalSeconds > 0)
                        {
                            var speed = processedAudioTime.TotalSeconds / elapsedWallTime.TotalSeconds;
                            
                            // Calculate progress based on total file duration if available
                            double progress = 0.0;
                            TimeSpan estimatedTimeRemaining = TimeSpan.Zero;
                            
                            if (totalDuration.HasValue && totalDuration.Value.TotalSeconds > 0)
                            {
                                // Use known total duration for accurate progress
                                progress = Math.Min(1.0, processedAudioTime.TotalSeconds / totalDuration.Value.TotalSeconds);
                                
                                if (speed > 0 && progress < 1.0)
                                {
                                    var remainingSeconds = totalDuration.Value.TotalSeconds - processedAudioTime.TotalSeconds;
                                    estimatedTimeRemaining = TimeSpan.FromSeconds(remainingSeconds / speed);
                                }
                            }
                            else
                            {
                                // Fallback: estimate progress based on current processing (less accurate)
                                // This assumes we're making steady progress, but caps at 95% until completion
                                var estimatedTotalDuration = processedAudioTime.TotalSeconds / Math.Max(0.1, speed * elapsedWallTime.TotalSeconds / processedAudioTime.TotalSeconds);
                                progress = Math.Min(0.95, processedAudioTime.TotalSeconds / estimatedTotalDuration);
                            }
                            
                            _logger?.LogDebug("Progress: {Progress:P1}, Speed: {Speed:F2}x, Processed: {ProcessedTime}, Total: {TotalDuration}", 
                                progress, speed, processedAudioTime, totalDuration);
                            
                            ProgressChanged?.Invoke(this, new TranscriptionProgressEventArgs
                            {
                                Progress = progress,
                                ProcessedTime = processedAudioTime,
                                ProcessingSpeed = speed,
                                EstimatedTimeRemaining = estimatedTimeRemaining
                            });
                        }
                        
                        lastUpdateTime = now;
                        lastProcessedTime = processedAudioTime;
                    }
                }
                
                _logger?.LogInformation("Whisper processing completed, {SegmentCount} segments generated", segments.Count);
                
                // Final progress update
                ProgressChanged?.Invoke(this, new TranscriptionProgressEventArgs
                {
                    Progress = 1.0,
                    ProcessedTime = segments.Count > 0 ? segments.Last().End : TimeSpan.Zero,
                    ProcessingSpeed = 1.0,
                    EstimatedTimeRemaining = TimeSpan.Zero
                });
                
                string outPath = Path.ChangeExtension(mediaPath, ".srt");
                _logger?.LogDebug("Writing SRT file to: {OutputPath}", outPath);
                await File.WriteAllTextAsync(outPath, ToSrt(segments), ct);
                
                _logger?.LogInformation("Transcription completed successfully: {OutputPath}", outPath);
                return outPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Transcription failed for {MediaPath}", mediaPath);
                throw;
            }
            finally
            {
                // Clean up temporary WAV file
                if (!string.IsNullOrEmpty(wavPath) && File.Exists(wavPath))
                {
                    try
                    {
                        File.Delete(wavPath);
                        _logger?.LogDebug("Cleaned up temporary WAV file: {WavPath}", wavPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to clean up temporary WAV file: {WavPath}", wavPath);
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _factory?.Dispose();
                    _logger?.LogInformation("WhisperNetTranscriptionService disposed - GPU memory released");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing WhisperFactory");
                }
                finally
                {
                    _factory = null;
                    _disposed = true;
                }
            }
        }

        ~WhisperNetTranscriptionService()
        {
            Dispose(false);
        }
    }
}