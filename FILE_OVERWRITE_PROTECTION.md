# File Overwrite Protection Feature

## Overview
The Whisper Transcription Queue now includes comprehensive file overwrite protection to prevent accidental loss of existing transcription files.

## How It Works

### When Adding Files to Queue
- The application checks if a corresponding `.srt` file already exists for each audio/video file
- If an output file exists, a dialog prompts the user with three options:
  - **Overwrite**: Replace the existing file with new transcription
  - **Skip**: Don't add this file to the queue (keeps existing transcription)
  - **Cancel**: Cancel the entire file addition operation

### During Queue Processing
- Before starting transcription of any task, the system double-checks for existing output files
- If a file was created after the task was added to the queue, the user is prompted again
- Tasks that are skipped are marked as "completed" without processing

### User Experience

#### File Addition Dialog
```
File Already Exists
===================
The output file already exists:

C:\audio\podcast.srt

Do you want to overwrite it?

[Overwrite]  [Skip]  [Cancel]
```

#### Status Updates
- **Adding files**: "Files processed: 5 added, 2 skipped, 0 failed"
- **During processing**: "File exists: podcast.srt - awaiting user decision..."
- **After decision**: "User chose to skip existing file: podcast.srt"

## Implementation Details

### Check Points
1. **Pre-queue check**: When adding files via UI (Add Files/Add Folder/Drag & Drop)
2. **Pre-processing check**: Before starting transcription of each task

### File Path Logic
- Output file path: `Path.ChangeExtension(inputFile, ".srt")`
- Example: `podcast.mp3` ? `podcast.srt`

### Task Status Handling
- **Skipped files**: Marked as `Done` with existing output path
- **Overwritten files**: Processed normally
- **Cancelled operations**: No tasks added to queue

## Benefits

### Data Protection
- Prevents accidental overwriting of existing transcription files
- Gives users full control over file replacement decisions
- Preserves manually edited or corrected transcriptions

### User Control
- Clear dialog options with sensible defaults
- Individual file-by-file decisions
- Batch operation awareness (shows counts of added/skipped/failed)

### Workflow Integration
- Seamless integration with existing queue management
- Proper status reporting and logging
- No disruption to normal transcription workflow

## Example Scenarios

### Scenario 1: Re-transcribing with Better Model
1. User has old transcriptions made with `tiny` model
2. Wants to re-transcribe with `large-v3` model for better accuracy
3. Adds files to queue ? prompted for each existing file
4. Chooses "Overwrite" for files to improve
5. Chooses "Skip" for files that are already accurate

### Scenario 2: Adding Mixed Content
1. User adds folder with 10 audio files
2. 3 files already have transcriptions, 7 are new
3. Status shows: "Files processed: 7 added, 3 skipped"
4. Only new files are processed, existing transcriptions preserved

### Scenario 3: Batch Processing Safety
1. User accidentally adds entire music library
2. Some files have existing transcriptions
3. Can selectively skip files with existing work
4. Prevents hours of unnecessary re-processing

## Technical Implementation

### UI Layer (`MainPage.xaml.cs`)
```csharp
public async Task<bool> ShowFileOverwriteDialogAsync(string outputPath)
{
    var confirmDialog = new ContentDialog
    {
        Title = "File Already Exists",
        Content = $"The output file already exists:\n\n{outputPath}\n\nDo you want to overwrite it?",
        PrimaryButtonText = "Overwrite",
        SecondaryButtonText = "Skip", 
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Secondary // Default to Skip for safety
    };
    
    var result = await confirmDialog.ShowAsync();
    return result == ContentDialogResult.Primary;
}
```

### Processing Layer (`QueueManager.cs`)
```csharp
// Check for existing file before processing
var expectedOutputPath = Path.ChangeExtension(task.FilePath, ".srt");
if (File.Exists(expectedOutputPath))
{
    // Fire event to request user decision
    FileOverwriteRequested?.Invoke(this, overwriteArgs);
    
    // Wait for user response with timeout
    // Handle user decision (overwrite/skip)
}
```

### Event Handling
- Async event handling for UI responsiveness
- Timeout protection (30 seconds default)
- Proper error handling and fallback behavior

## Testing

Use the included test script to verify the feature:
```bash
test-file-overwrite-protection.bat
```

This creates test files with existing `.srt` outputs to verify dialog behavior.