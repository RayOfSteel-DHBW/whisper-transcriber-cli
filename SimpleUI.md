# Simple Task Queue UI Implementation Plan

## Overview
Create a modern WinUI 3 application that allows users to build a transcription task queue using file dialogs, with persistence to handle long-running batch jobs without worrying about system restarts.

## Goal
A "plan and forget" workflow - add files/folders through dialogs, configure models, start the queue, and leave it running overnight.

## Architecture

### Project Structure
```
whisper-transcriber-cli
├── src/
│   ├── WhisperTranscriberCLI.Cli/     # Current CLI (renamed)
│   ├── WhisperTranscriberCLI.Core/    # Shared transcription logic
│   └── WhisperTranscriberCLI.TaskUI/  # New simple UI
├── shared/
│   └── TranscriptionQueue.json        # Persisted task list
├── whispermodels/                     # Model files
├── tests/                             # Unit tests
└── TranscriberCLI.sln                 # Solution file
```

### UI Layout
```
┌─ Whisper Transcription Queue ────────────────────────────────────┐
│ [Add Files...] [Add Folder...] ☑Recursive Model:[base▼] Lang:[auto▼]│
│ Output Dir: [C:\audio\transcripts...] [Browse...]                │
│ ┌───────────────────────────────────────────────────────────────┐│
│ │File              │Duration│Model│Lang│Status   │Progress      ││
│ │podcast.mp3       │45:30   │base │en  │Done     │████████████ ││  
│ │interview/part1.wav│12:15   │base │auto│Processing│████████░░░░ ││
│ │interview/part2.wav│08:42   │base │auto│Pending  │             ││
│ └───────────────────────────────────────────────────────────────┘│
│ [▶ Start Queue] [⏸ Pause] [⏹ Cancel Current] [🗑 Clear Done]    │
│ [💾 Save Queue] [📁 Load Queue] [🔧 Settings] [? About]          │
│ Status: Processing interview/part1.wav (2/3) • ETA: 8min        │
└───────────────────────────────────────────────────────────────────┘
``` 
### Task Persistence Format
```json
{  
  "schemaVersion": 1,
  "tasks": [
    {
      "id": "guid",
      "filePath": "C:/audio/podcast.mp3",
      "modelName": "ggml-base.bin", 
      "language": "auto",
      "duration": "00:45:30",
      "status": "pending",
      "addedAt": "2025-01-17T10:30:00Z",
      "completedAt": null,
      "outputPath": null,
      "errorMessage": null
    }
  ],
  "settings": {
    "defaultModel": "ggml-base.bin",
    "defaultLanguage": "auto",
    "recursive": true,
    "outputDirectory": "C:/audio/transcripts",
    "lastQueuePath": "C:/shared/TranscriptionQueue.json",
    "windowBounds": { "x": 100, "y": 100, "width": 800, "height": 600 },
    "columnWidths": { "file": 200, "duration": 80, "model": 100, "lang": 60, "status": 100, "progress": 120 },
    "autoSaveInterval": 300,
    "systemTrayEnabled": true,
    "minimizeToTray": true,
    "closeToTray": false,
    "openOutputAfterCompletion": false
  }
}
```

### UI Features
- **File Management**: Modern file picker for individual files, folder picker for directories with recursive toggle
- **Task List**: ListView/DataGrid showing file path, duration, model, language, status, and progress
- **Model Selection**: ComboBox with modern styling, populated by scanning `whispermodels/` directory
- **Language Selection**: ComboBox with auto-detect and manual language override options
- **Output Management**: Modern folder picker for output directory
- **Queue Controls**: Modern button styles with icons (Start/Pause/Cancel Current/Clear Done)
- **Queue Persistence**: Save/Load queue files with modern file dialogs, auto-save every 5 minutes
- **System Integration**: System tray with Windows 11 notifications and progress, minimize to tray functionality
- **Auto-persistence**: Save queue state and UI settings using WinUI 3 settings storage
- **Background Processing**: Queue runs in background thread without freezing UI
- **Status Updates**: Real-time progress with modern progress rings and ETA display
- **Error Handling**: Modern info bars for error messages and retry capabilities
- **Drag & Drop**: Native drag-and-drop support for files/folders onto the task list

### Task Status States
- **Pending**: Queued but not started
- **Processing**: Currently being transcribed
- **Done**: Successfully completed
- **Error**: Failed with error message
- **Paused**: Queue stopped by user

## Implementation Checklist

### Phase 1: Project Setup
- [x] Conduct spike to identify hidden CLI dependencies
- [x] Create `WhisperTranscriberCLI.Core` class library project
- [x] Extract transcription logic from CLI to Core project  
- [x] Create `WhisperTranscriberCLI.TaskUI` WinUI 3 project
- [x] Rename current `src/` to `WhisperTranscriberCLI.Cli`
- [x] Update solution file with new project structure
- [x] Update CLI project to reference Core library

### Phase 2: Core Shared Logic
- [x] Add JSON schema version field to persistence format
- [x] Create `TranscriptionTask` data model
- [x] Create `TranscriptionQueue` class with JSON persistence
- [x] Create `ModelDiscovery` class to scan whispermodels directory
- [x] Extract `ITranscriptionService` interface from existing service
- [x] Create `TranscriptionProgress` event/callback system
- [x] Add error handling and logging to core services

### Phase 3: Basic UI Implementation  
- [x] Create MainWindow with modern WinUI 3 layout and XAML
- [x] Add modern file picker for "Add Files..." button
- [x] Add folder picker for "Add Folder..." with recursive toggle switch
- [x] Create ListView/DataGrid for task list with modern styling
- [x] Add ComboBox for model selection with modern design
- [x] Add ComboBox for language selection (auto-detect default)
- [x] Add folder picker for output directory
- [x] Implement task list data binding with ObservableCollection
- [x] Add modern button controls with Fluent icons (Start/Pause/Cancel/Clear)
- [x] Add Save/Load Queue buttons with modern file dialogs

### Phase 4: Queue Management
- [x] Implement JSON persistence for task queue
- [x] Add task status tracking and updates
- [x] Create background worker for queue processing
- [x] Implement progress reporting per task with ETA calculation
- [x] Add error handling and retry logic
- [x] Implement queue pause/resume functionality
- [ ] Add audio duration detection using FFmpeg
- [x] Implement auto-save every 5 minutes
- [x] Add cancellation support for current task

### Phase 5: UI Polish
- [x] Add modern progress rings and bars for individual tasks
- [x] Implement status bar with modern progress indicators and ETA
- [ ] Add context menu for task list using MenuFlyout (remove, retry, open output, etc.)
- [ ] Add application settings persistence using WinUI 3 ApplicationData
- [ ] Implement modern drag-and-drop with visual feedback
- [ ] Add keyboard shortcuts and accelerator keys
- [ ] Create system tray integration with Windows 11 notifications
- [ ] Implement minimize to tray functionality (minimize button and window state handling)
- [ ] Add system tray context menu (Show/Hide, Start/Pause Queue, Exit)
- [ ] Add option for close button to minimize to tray instead of exit
- [ ] Add Settings page using modern WinUI 3 controls
- [ ] Add About dialog with modern styling and version info
- [ ] Implement light/dark theme support with system theme detection
- [ ] Add post-processing options with toggle switches

### Phase 6: Integration & Testing
- [ ] Test queue persistence across application restarts
- [ ] Test error scenarios and recovery
- [ ] Add unit tests for core logic
- [ ] Test with various file types and models
- [ ] Performance testing with large queues
- [ ] User acceptance testing
- [ ] Test FFmpeg availability check at startup
- [ ] Test system tray functionality and notifications
- [ ] Test minimize to tray behavior and restore functionality
- [ ] Validate tray icon context menu and double-click behavior
- [ ] Validate auto-save and crash recovery

### Phase 7: Documentation & Distribution
- [ ] Update README.md with UI documentation
- [ ] Create user guide with screenshots
- [ ] Add build instructions for UI project
- [ ] Create release packaging scripts
- [ ] Document deployment requirements

## Technical Considerations

### Dependencies
- **Core**: System.Text.Json for persistence, existing Whisper.net packages, FFmpeg for duration detection
- **UI**: WinUI 3 (.NET 8), Windows App SDK, CommunityToolkit.WinUI for additional controls
- **CLI**: Minimal changes, just reference Core library
- **System**: FFmpeg binaries (bundled or system PATH), Windows notification APIs, MSIX packaging

### Threading
- UI thread handles all UI updates
- Background thread processes transcription queue
- Use `Invoke()` for cross-thread UI updates
- Implement cancellation tokens for graceful shutdown

### Error Handling
- Persist failed tasks with error messages
- Allow retry of failed tasks
- Graceful handling of missing models or files
- User-friendly error notifications

### Performance
- Lazy loading of large file lists
- Chunked processing to prevent UI freezing
- Memory management for large audio files
- Configurable concurrent task limits
- Efficient audio duration caching
- Background thread for file scanning and duration detection

## Future Enhancements
- [ ] Multiple output formats (VTT, JSON, plain text)
- [ ] Batch model assignment and language detection
- [ ] Queue templates and presets
- [ ] Network file support (UNC paths, cloud storage)
- [ ] Integration with cloud storage providers
- [ ] Scheduled queue execution with Windows Task Scheduler
- [ ] Email notifications on completion
- [ ] Batch rename/organize output files
- [ ] Integration with video editing software
- [ ] API endpoint for remote queue management
- [ ] Plugin system for custom post-processing
- [ ] Multi-language UI support
