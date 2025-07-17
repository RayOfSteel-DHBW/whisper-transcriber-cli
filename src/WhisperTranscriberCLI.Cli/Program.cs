using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using WhisperTranscriberCLI.Utilities;
using WhisperTranscriberCLI.Core.Services;

namespace WhisperTranscriberCLI
{
    public class Program
    {
        private static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".mp4", ".avi", ".mkv", ".m4a", ".flac", ".ogg", ".webm", ".wma" };
        
        public static async Task Main(string[] args)
        {
            var options = ParseCommandLineArgs(args);
            
            if (options.ShowHelp || options.ShowVersion)
            {
                if (options.ShowVersion)
                    ShowVersion();
                else
                    ShowHelp();
                return;
            }

            if (options.ListModels)
            {
                ListAvailableModels();
                return;
            }

            // Validate input
            if (string.IsNullOrEmpty(options.InputPath))
            {
                Console.Error.WriteLine("Error: No input file or directory specified.");
                Console.Error.WriteLine("Use --help for usage information.");
                Environment.Exit(1);
            }

            // Interactive model selection if no model specified
            if (options.Model == "")
            {
                string selectedModel = ShowModelPicker();
                if (string.IsNullOrEmpty(selectedModel))
                {
                    Console.Error.WriteLine("No model selected. Exiting.");
                    Environment.Exit(1);
                }
                options.Model = selectedModel;
            }

            try
            {
                var mediaConverter = new FfmpegMediaConverter();
                var transcriptionService = new WhisperNetTranscriptionService(mediaConverter, options.UseGpu, options.Model);

                // Get files to process
                var filesToProcess = GetFilesToProcess(options.InputPath, options.Recursive);
                
                if (filesToProcess.Count == 0)
                {
                    Console.WriteLine($"No supported audio/video files found in: {options.InputPath}");
                    Console.WriteLine($"Supported formats: {string.Join(", ", SupportedExtensions)}");
                    return;
                }

                Console.WriteLine($"Found {filesToProcess.Count} file(s) to process");
                Console.WriteLine($"Using model: {options.Model}");
                Console.WriteLine($"Acceleration: {(options.UseGpu ? "GPU (CUDA/Vulkan)" : "CPU")}");
                
                if (options.Language != "auto")
                    Console.WriteLine($"Language: {options.Language}");
                
                Console.WriteLine();

                int processed = 0;
                int failed = 0;

                foreach (string filePath in filesToProcess)
                {
                    try
                    {
                        Console.Write($"Processing: {Path.GetFileName(filePath)} ... ");
                        
                        string outputPath = await transcriptionService.TranscribeAsync(filePath, CancellationToken.None);
                        
                        if (string.IsNullOrEmpty(outputPath))
                        {
                            Console.WriteLine($"✗ Failed: Transcription service not available");
                            failed++;
                        }
                        else
                        {
                            Console.WriteLine($"✓ → {Path.GetFileName(outputPath)}");
                            processed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Failed: {ex.Message}");
                        failed++;
                        
                        if (options.Verbose)
                        {
                            Console.WriteLine($"   Details: {ex}");
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"Completed: {processed} successful, {failed} failed");
                
                if (failed > 0)
                {
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                if (options.Verbose)
                {
                    Console.Error.WriteLine($"Details: {ex}");
                }
                Environment.Exit(1);
            }
        }

        private static List<string> GetFilesToProcess(string inputPath, bool recursive)
        {
            var files = new List<string>();

            if (File.Exists(inputPath))
            {
                // Single file
                if (IsSupportedFile(inputPath))
                {
                    files.Add(inputPath);
                }
                else
                {
                    Console.Error.WriteLine($"Unsupported file format: {Path.GetExtension(inputPath)}");
                }
            }
            else if (Directory.Exists(inputPath))
            {
                // Directory
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                foreach (string ext in SupportedExtensions)
                {
                    string pattern = "*" + ext;
                    files.AddRange(Directory.GetFiles(inputPath, pattern, searchOption));
                }
                
                files.Sort(); // Process files in alphabetical order
            }
            else
            {
                Console.Error.WriteLine($"Input path not found: {inputPath}");
            }

            return files;
        }

        private static bool IsSupportedFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return SupportedExtensions.Contains(extension);
        }

        private static void ListAvailableModels()
        {
            Console.WriteLine("Available Whisper models:");
            
            string modelDir = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
            
            if (!Directory.Exists(modelDir))
            {
                Console.WriteLine("No models directory found. Run with a valid model to download default models.");
                return;
            }

            var modelFiles = Directory.GetFiles(modelDir, "*.bin");
            
            if (modelFiles.Length == 0)
            {
                Console.WriteLine("No model files found in whispermodels directory.");
                return;
            }

            foreach (string modelFile in modelFiles)
            {
                string modelName = Path.GetFileName(modelFile);
                long sizeBytes = new FileInfo(modelFile).Length;
                string size = FormatFileSize(sizeBytes);
                
                Console.WriteLine($"  {modelName} ({size})");
            }
        }

        private static string ShowModelPicker()
        {
            string modelDir = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
            
            if (!Directory.Exists(modelDir))
            {
                Console.WriteLine("No models directory found. Please place model files (.bin) in the whispermodels directory.");
                return string.Empty;
            }

            var modelFiles = Directory.GetFiles(modelDir, "*.bin");
            
            if (modelFiles.Length == 0)
            {
                Console.WriteLine("No model files found in whispermodels directory.");
                return string.Empty;
            }

            Console.WriteLine();
            Console.WriteLine("Multiple models available. Please select one:");
            Console.WriteLine();

            for (int i = 0; i < modelFiles.Length; i++)
            {
                string modelName = Path.GetFileName(modelFiles[i]);
                long sizeBytes = new FileInfo(modelFiles[i]).Length;
                string size = FormatFileSize(sizeBytes);
                
                Console.WriteLine($"  {i + 1}. {modelName} ({size})");
            }

            Console.WriteLine();
            Console.Write($"Enter your choice (1-{modelFiles.Length}): ");

            while (true)
            {
                string input = Console.ReadLine()?.Trim() ?? "";
                
                if (int.TryParse(input, out int choice) && choice >= 1 && choice <= modelFiles.Length)
                {
                    string selectedModel = Path.GetFileName(modelFiles[choice - 1]);
                    Console.WriteLine($"Selected: {selectedModel}");
                    Console.WriteLine();
                    return selectedModel;
                }
                
                Console.Write($"Invalid choice. Please enter a number between 1 and {modelFiles.Length}: ");
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }

        private static void ShowVersion()
        {
            Console.WriteLine("Whisper Transcriber CLI v1.0.0");
            Console.WriteLine("A command-line tool for audio/video transcription using OpenAI Whisper");
        }

        private static CommandLineOptions ParseCommandLineArgs(string[] args)
        {
            var options = new CommandLineOptions();
            
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-r":
                    case "--recursive":
                        options.Recursive = true;
                        break;
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;
                    case "-v":
                    case "--verbose":
                        options.Verbose = true;
                        break;
                    case "--version":
                        options.ShowVersion = true;
                        break;
                    case "--list-models":
                        options.ListModels = true;
                        break;
                    case "--gpu":
                        options.UseGpu = true;
                        break;
                    case "-m":
                    case "--model":
                        if (i + 1 < args.Length)
                        {
                            options.Model = args[++i];
                        }
                        break;
                    case "-l":
                    case "--language":
                        if (i + 1 < args.Length)
                        {
                            options.Language = args[++i];
                        }
                        break;
                    case "-o":
                    case "--output":
                        if (i + 1 < args.Length)
                        {
                            options.OutputFormat = args[++i];
                        }
                        break;
                    case "-i":
                    case "--input":
                        if (i + 1 < args.Length)
                        {
                            options.InputPath = args[++i];
                        }
                        break;
                    default:
                        // If it's not a flag and no input path is set, treat it as input path
                        if (!args[i].StartsWith("-") && string.IsNullOrEmpty(options.InputPath))
                        {
                            options.InputPath = args[i];
                        }
                        break;
                }
            }
            
            return options;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Whisper Transcriber CLI v1.0.0");
            Console.WriteLine("A command-line tool for audio/video transcription using OpenAI Whisper");
            Console.WriteLine();
            Console.WriteLine("Usage: whisper-transcriber-cli [OPTIONS] INPUT");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  INPUT                    Path to audio/video file or directory");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -m, --model MODEL        Whisper model to use (interactive picker if not specified)");
            Console.WriteLine("  -l, --language LANG      Language of the audio (default: auto-detect)");
            Console.WriteLine("  -o, --output FORMAT      Output format: srt, txt, vtt (default: srt)");
            Console.WriteLine("  -r, --recursive          Process directories recursively");
            Console.WriteLine("      --gpu                Enable GPU acceleration (CUDA/Vulkan)");
            Console.WriteLine("  -v, --verbose            Enable verbose output");
            Console.WriteLine("  -h, --help               Show this help message");
            Console.WriteLine("      --version            Show version information");
            Console.WriteLine("      --list-models        List available models");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  whisper-transcriber-cli audio.mp3");
            Console.WriteLine("  whisper-transcriber-cli -m ggml-large-v3.bin video.mp4");
            Console.WriteLine("  whisper-transcriber-cli --gpu audio.wav");
            Console.WriteLine("  whisper-transcriber-cli -r /path/to/audio/files");
            Console.WriteLine("  whisper-transcriber-cli --language en audio.wav");
            Console.WriteLine("  whisper-transcriber-cli --output txt audio.mp3");
            Console.WriteLine();
            Console.WriteLine("Supported formats: mp3, wav, mp4, avi, mkv, m4a, flac, ogg, webm, wma");
        }

        private class CommandLineOptions
        {
            public string InputPath { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty; // No default model
            public string Language { get; set; } = "auto";
            public string OutputFormat { get; set; } = "srt";
            public bool Recursive { get; set; } = false;
            public bool Verbose { get; set; } = false;
            public bool ShowHelp { get; set; } = false;
            public bool ShowVersion { get; set; } = false;
            public bool ListModels { get; set; } = false;
            public bool UseGpu { get; set; } = false;
        }
    }
}