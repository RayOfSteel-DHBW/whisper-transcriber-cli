using WhisperTranscriberCLI.Core.Events;

namespace WhisperTranscriberCLI.Core.Interfaces;

public interface ITranscriptionService
{
    event EventHandler<TranscriptionProgressEventArgs>? ProgressChanged;
    
    Task<string> TranscribeAsync(string mediaPath, CancellationToken cancellationToken);
    bool IsAvailable { get; }
    string UnavailabilityReason { get; }
}