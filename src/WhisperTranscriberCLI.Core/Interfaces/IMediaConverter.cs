namespace WhisperTranscriberCLI.Core.Interfaces;

public interface IMediaConverter
{
    Task<string> ConvertToWavAsync(string inputPath, CancellationToken cancellationToken);
    Task<Stream> ConvertToWavStreamAsync(string inputPath, CancellationToken cancellationToken);
}