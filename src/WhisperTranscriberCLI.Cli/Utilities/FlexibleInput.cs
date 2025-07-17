using System;
using System.IO;
using System.Linq;

namespace WhisperTranscriberCLI.Utilities
{
    public static class FlexibleInput
    {
        public static string[] GetFlexibleFileInput(string inputPath, string[] supportedExtensions, string pickerTitle = "Select File", string? pickerFilter = null, bool allowMultiple = false, bool recursive = false)
        {
            // Case 1: No input - prompt user for input
            if (string.IsNullOrEmpty(inputPath))
            {
                Console.WriteLine($"{pickerTitle}:");
                var readableExts = supportedExtensions.Select(ext => ext.ToUpper().TrimStart('.')).ToArray();
                var readableString = string.Join(", ", readableExts);
                Console.WriteLine($"Supported file types: {readableString}");
                Console.Write("Enter file or directory path: ");
                
                inputPath = Console.ReadLine() ?? string.Empty;
                if (string.IsNullOrEmpty(inputPath))
                {
                    throw new InvalidOperationException("No file path provided - operation cancelled by user");
                }
            }

            // Case 2: Input provided - check if it's file or directory
            if (!Directory.Exists(inputPath) && !File.Exists(inputPath))
            {
                throw new FileNotFoundException($"Path not found: {inputPath}");
            }

            if (Directory.Exists(inputPath))
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(inputPath, "*.*", searchOption)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                    .ToArray();

                if (files.Length == 0)
                {
                    var extString = string.Join(", ", supportedExtensions);
                    throw new InvalidOperationException($"No supported files found in directory. Looking for: {extString}");
                }

                return files;
            }
            else
            {
                // Single file mode
                if (supportedExtensions.Contains(Path.GetExtension(inputPath).ToLower()))
                {
                    return new[] { inputPath };
                }
                else
                {
                    var extString = string.Join(", ", supportedExtensions);
                    throw new InvalidOperationException($"Unsupported file type: {Path.GetExtension(inputPath)}. Supported: {extString}");
                }
            }
        }
    }
}