# Whisper Transcriber CLI

## Overview
The Whisper Transcriber CLI is a command-line application designed to facilitate audio transcription using the Whisper model. It provides a flexible input handling mechanism, allowing users to specify audio files or directories for transcription. The application also supports downloading necessary Whisper models from specified URLs.

## Features
- **Flexible Input Handling**: Users can select files through a dialog or specify paths directly.
- **Audio Conversion**: Converts various media formats to WAV format for transcription.
- **Model Support**: Supports various Whisper model sizes for optimal performance vs. accuracy trade-offs.

## Project Structure
```
whisper-transcriber-cli
├── src
│   ├── Program.cs                # Entry point of the application
│   ├── Services
│   │   ├── WhisperNetTranscriptionService.cs  # Transcription logic
│   │   └── FfmpegMediaConverter.cs           # Media conversion logic
│   └── Utilities
│       └── FlexibleInput.cs      # Flexible input handling
├── whispermodels                  # Directory for Whisper models (*.bin files)
│   └── .gitkeep
├── tests                          # Unit tests
├── TranscriberCLI.sln             # Solution file
└── README.md                      # Project documentation
```

## Installation
1. Clone the repository:
   ```
   git clone <repository-url>
   cd whisper-transcriber-cli
   ```

2. Restore dependencies:
   ```
   dotnet restore
   ```

3. Build the project:
   ```
   dotnet build
   ```

4. Download Whisper models:
   - Visit [Hugging Face Whisper.cpp models](https://huggingface.co/ggerganov/whisper.cpp/tree/main)
   - Download one or more `.bin` model files (e.g., `ggml-base.bin`, `ggml-small.bin`)
   - Place them in the `whispermodels/` directory
   - Recommended models:
     - `ggml-base.bin` - Good balance of speed and accuracy
     - `ggml-small.bin` - Faster, slightly less accurate
     - `ggml-medium.bin` - More accurate, slower

## Usage

### Command Line Options
```bash
# Basic usage - transcribe a single file
./whisper-transcriber-cli audio-file.mp3

# Specify a model
./whisper-transcriber-cli -m ggml-base.bin audio-file.wav

# Transcribe all audio files in a directory recursively
./whisper-transcriber-cli -r /path/to/audio/directory

# Show available models
./whisper-transcriber-cli --list-models

# Show help
./whisper-transcriber-cli --help
```

### Running with dotnet
```bash
# Run directly
dotnet run --project src -- audio-file.mp3

# With model selection
dotnet run --project src -- -m ggml-base.bin audio-file.wav
```

### Output
The application creates `.srt` subtitle files in the same directory as the input audio files. These files contain timestamped transcriptions that can be used with video players or subtitle editors.

### Supported Audio Formats
- WAV, MP3, MP4, M4A, FLAC, OGG
- Video files with audio tracks (MP4, AVI, MOV, etc.)

## Model Information
Models are automatically selected based on availability. If no model is specified with `-m`, the application will:
1. Show an interactive picker if multiple models are available
2. Use the best available model automatically

Larger models provide better accuracy but require more processing time and memory.

## Contributing
Contributions are welcome! Please submit a pull request or open an issue for any enhancements or bug fixes.

## License
This project is licensed under the MIT License. See the LICENSE file for details.