# Whisper Transcriber CLI

## Overview
The Whisper Transcriber CLI is a command-line application designed to facilitate audio transcription using the Whisper model. It provides a flexible input handling mechanism, allowing users to specify audio files or directories for transcription. The application also supports downloading necessary Whisper models from specified URLs.

## Features
- **Flexible Input Handling**: Users can select files through a dialog or specify paths directly.
- **Audio Conversion**: Converts various media formats to WAV format for transcription.
- **Model Management**: Automatically downloads and manages Whisper models required for transcription.

## Project Structure
```
whisper-transcriber-cli
├── src
│   ├── Program.cs                # Entry point of the application
│   ├── appsettings.json          # Configuration settings
│   ├── Services
│   │   ├── WhisperNetTranscriptionService.cs  # Transcription logic
│   │   ├── FfmpegMediaConverter.cs           # Media conversion logic
│   │   └── ModelDownloader.cs                 # Model downloading functionality
│   └── Utilities
│       ├── FlexibleInput.cs      # Flexible input handling
│       ├── AppPaths.cs           # Application path management
│       └── UserSettingsOptions.cs # User configuration settings
├── whispermodels                  # Directory for Whisper models
│   └── .gitkeep
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

## Usage
To run the application, use the following command:
```
dotnet run --project src/Program.cs
```

### Transcription
You can specify the audio file or directory containing audio files for transcription. The application will handle the conversion and transcription process.

### Model Downloading
The application will check for existing Whisper models and download them if they are not present. Ensure you have an internet connection for this feature.

## Configuration
Modify the `appsettings.json` file to set paths for model files and user preferences.

## Contributing
Contributions are welcome! Please submit a pull request or open an issue for any enhancements or bug fixes.

## License
This project is licensed under the MIT License. See the LICENSE file for details.