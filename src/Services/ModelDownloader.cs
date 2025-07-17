using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace WhisperTranscriberCLI.Services
{
    public class ModelDownloader
    {
        private readonly string _modelDirectory;
        private readonly HttpClient _httpClient;

        public ModelDownloader(string modelDirectory)
        {
            _modelDirectory = modelDirectory;
            _httpClient = new HttpClient();
        }

        public async Task DownloadModelAsync(string modelName, string modelUrl)
        {
            string modelPath = Path.Combine(_modelDirectory, modelName);

            if (File.Exists(modelPath))
            {
                Console.WriteLine($"Model '{modelName}' already exists. Skipping download.");
                return;
            }

            Console.WriteLine($"Downloading model '{modelName}' from '{modelUrl}'...");

            try
            {
                byte[] modelData = await _httpClient.GetByteArrayAsync(modelUrl);
                await File.WriteAllBytesAsync(modelPath, modelData);
                Console.WriteLine($"Model '{modelName}' downloaded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download model '{modelName}': {ex.Message}");
            }
        }
    }
}