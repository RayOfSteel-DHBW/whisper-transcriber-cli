using WhisperTranscriberCLI.Core.Services;

namespace WhisperTranscriberCLI.Utilities;

public static class ModelSetup
{
    public static string? PromptForModelsDirectory()
    {
        Console.WriteLine();
        Console.WriteLine("?? Whisper Models Setup Required");
        Console.WriteLine("=====================================");
        Console.WriteLine();
        Console.WriteLine("No Whisper models found in the default locations.");
        Console.WriteLine("Please specify the directory containing your Whisper model files (.bin).");
        Console.WriteLine();
        Console.WriteLine("Common model locations:");
        Console.WriteLine("  • Download from: https://huggingface.co/ggerganov/whisper.cpp/tree/main");
        Console.WriteLine("  • Recommended models: ggml-small.bin, ggml-base.bin, ggml-medium.bin");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Enter path to your whisper models directory: ");
            string? input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("? No path entered. Please specify a valid directory path.");
                continue;
            }

            // Handle quoted paths
            if (input.StartsWith('"') && input.EndsWith('"'))
            {
                input = input[1..^1];
            }

            if (!Directory.Exists(input))
            {
                Console.WriteLine($"? Directory not found: {input}");
                Console.WriteLine("Please enter a valid directory path.");
                continue;
            }

            var modelFiles = Directory.GetFiles(input, "*.bin");
            if (modelFiles.Length == 0)
            {
                Console.WriteLine($"? No .bin model files found in: {input}");
                Console.WriteLine("Please choose a directory containing Whisper model files.");
                continue;
            }

            Console.WriteLine($"? Found {modelFiles.Length} model(s) in: {input}");
            foreach (var modelFile in modelFiles)
            {
                var fileName = Path.GetFileName(modelFile);
                var size = FormatFileSize(new FileInfo(modelFile).Length);
                Console.WriteLine($"   • {fileName} ({size})");
            }

            Console.WriteLine();
            Console.Write("Use this directory? (y/n): ");
            string? confirm = Console.ReadLine()?.Trim().ToLower();

            if (confirm == "y" || confirm == "yes")
            {
                Console.WriteLine($"? Models directory set to: {input}");
                Console.WriteLine();
                return input;
            }

            Console.WriteLine("Please enter a different path.");
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
}