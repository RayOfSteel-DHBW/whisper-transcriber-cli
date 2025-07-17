# Manual Testing Checklist for Whisper Transcription Queue

## 🚀 Application Startup Tests

### System Requirements Check
- [ ] **FFmpeg Detection**: Launch app, verify FFmpeg availability is checked
- [ ] **Whisper Models**: Verify whisper models directory is checked
- [ ] **System Requirements Dialog**: If missing components, verify dialog appears with installation instructions
- [ ] **Disk Space**: Verify sufficient disk space is checked
- [ ] **Status Bar**: Verify startup status messages appear

### Initial UI State  
- [ ] **Window Layout**: Verify all UI elements are properly positioned
- [ ] **Default Settings**: Verify default values are loaded (model, language, output directory)
- [ ] **Theme**: Verify app respects system light/dark theme
- [ ] **Status Bar**: Shows "Ready" or system check status

## 📁 File Management Tests

### Adding Files
- [ ] **Add Files Button**: Click "Add Files..." and select multiple audio/video files
- [ ] **File Types**: Test supported formats (.mp3, .wav, .mp4, .avi, .mkv, .m4a, .flac, .ogg, .webm, .wma)
- [ ] **File Rejection**: Try unsupported formats, verify they're filtered out
- [ ] **Duration Detection**: Verify file duration is detected and displayed
- [ ] **File Path Display**: Verify file names/paths are displayed correctly

### Adding Folders
- [ ] **Add Folder Button**: Click "Add Folder..." and select directory
- [ ] **Recursive Option**: Test with recursive checkbox checked and unchecked
- [ ] **Mixed File Types**: Test folder with mixed supported/unsupported files
- [ ] **Empty Folder**: Test with empty folder
- [ ] **Nested Folders**: Test with deeply nested folder structure

### Drag and Drop
- [ ] **File Drag and Drop**: Drag files from file explorer onto task list
- [ ] **Folder Drag and Drop**: Drag folders onto task list
- [ ] **Multiple Items**: Drag multiple files and folders simultaneously
- [ ] **Visual Feedback**: Verify drag over feedback appears
- [ ] **Unsupported Files**: Drag unsupported files, verify they're filtered

## ⚙️ Settings Tests

### General Settings
- [ ] **Output Directory**: Change output directory, verify it persists
- [ ] **Default Model**: Change default model, verify new tasks use it
- [ ] **Default Language**: Change default language, verify new tasks use it
- [ ] **Recursive Toggle**: Toggle recursive option, verify it affects folder addition

### System Tray Settings
- [ ] **Enable System Tray**: Toggle system tray on/off
- [ ] **Minimize to Tray**: Toggle minimize to tray behavior
- [ ] **Close to Tray**: Toggle close to tray behavior
- [ ] **Tray Settings Dependencies**: Verify tray options are disabled when system tray is off

### Post-Processing Settings
- [ ] **Open Output After Completion**: Toggle auto-open output files
- [ ] **Auto-save Queue**: Verify auto-save is enabled (currently fixed)

### Settings Persistence
- [ ] **Save Settings**: Make changes, click Save, verify settings are applied
- [ ] **Cancel Settings**: Make changes, click Cancel, verify changes are reverted
- [ ] **Settings on Restart**: Restart app, verify settings are restored
- [ ] **Reset Settings**: Click Reset, verify all settings return to defaults

## 🔄 Queue Management Tests

### Task Operations
- [ ] **Task Display**: Verify tasks show file, duration, model, language, status, progress
- [ ] **Task Selection**: Click on tasks to select them
- [ ] **Task Sorting**: Verify tasks maintain order (newest first/last)

### Context Menu
- [ ] **Right-click Menu**: Right-click on task to show context menu
- [ ] **Remove Task**: Remove task from queue
- [ ] **Retry Task**: Retry failed task (change status back to pending)
- [ ] **Open Output**: Open completed task output file
- [ ] **Open Folder**: Open folder containing output file
- [ ] **Copy Path**: Copy file path to clipboard
- [ ] **Show Properties**: Show task properties dialog
- [ ] **Menu Item States**: Verify menu items are enabled/disabled appropriately

### Queue Controls
- [ ] **Start Queue**: Click "Start Queue" button
- [ ] **Pause Queue**: Click "Pause" button during processing
- [ ] **Cancel Current**: Click "Cancel Current" button during processing
- [ ] **Clear Done**: Click "Clear Done" to remove completed tasks
- [ ] **Button States**: Verify buttons are enabled/disabled appropriately

### Queue Persistence
- [ ] **Save Queue**: Click "Save Queue" button, verify file is saved
- [ ] **Load Queue**: Click "Load Queue" button, verify queue is loaded
- [ ] **Auto-save**: Verify queue auto-saves periodically
- [ ] **Crash Recovery**: Force quit app, restart, verify queue is restored

## 🎯 Processing Tests

### Single Task Processing
- [ ] **Status Updates**: Verify task status changes (Pending → Processing → Done)
- [ ] **Progress Updates**: Verify progress bar updates during processing
- [ ] **Error Handling**: Test with invalid file, verify error status and message
- [ ] **Output Generation**: Verify output file is created in correct location
- [ ] **Completion Notification**: Verify completion notification appears

### Multiple Task Processing
- [ ] **Queue Order**: Verify tasks are processed in correct order
- [ ] **Batch Processing**: Add multiple tasks, start queue, verify all are processed
- [ ] **Mixed Results**: Test with mix of valid/invalid files
- [ ] **Queue Progress**: Verify overall queue progress in status bar

### Error Scenarios
- [ ] **Missing File**: Test with file that gets deleted after adding to queue
- [ ] **Invalid Model**: Test with invalid model name
- [ ] **Insufficient Permissions**: Test with write-protected output directory
- [ ] **Disk Space**: Test with insufficient disk space (if possible)
- [ ] **Network Interruption**: Test with network files (if applicable)

## 🔔 System Tray Tests

### Tray Icon
- [ ] **Tray Icon Appearance**: Verify tray icon appears when enabled
- [ ] **Tray Icon Tooltip**: Hover over tray icon, verify tooltip
- [ ] **Tray Icon Removal**: Disable system tray, verify icon is removed

### Tray Notifications
- [ ] **Startup Notification**: Verify notification appears on app startup
- [ ] **Task Completion**: Verify notification appears when task completes
- [ ] **Task Failure**: Verify notification appears when task fails
- [ ] **Queue Completion**: Verify notification appears when entire queue completes
- [ ] **Notification Content**: Verify notification titles and messages are appropriate

### Tray Interaction
- [ ] **Double-click Tray**: Double-click tray icon to show/hide window
- [ ] **Tray Context Menu**: Right-click tray icon to show context menu
- [ ] **Show/Hide Window**: Use tray menu to show/hide window
- [ ] **Start/Pause Queue**: Use tray menu to control queue
- [ ] **Exit from Tray**: Use tray menu to exit application

### Window State Management
- [ ] **Minimize to Tray**: Minimize window, verify it goes to tray (if enabled)
- [ ] **Close to Tray**: Close window, verify it goes to tray (if enabled)
- [ ] **Restore from Tray**: Restore window from tray, verify it appears correctly
- [ ] **Window Position**: Verify window position is remembered after restore

## ⌨️ Keyboard Shortcuts Tests

### File Operations
- [ ] **Ctrl+O**: Open files dialog
- [ ] **Ctrl+Shift+O**: Open folder dialog
- [ ] **Ctrl+S**: Save queue
- [ ] **Ctrl+L**: Load queue

### Queue Operations
- [ ] **F5**: Start queue
- [ ] **Pause**: Pause queue
- [ ] **Escape**: Cancel current task
- [ ] **Delete**: Remove selected task

### General
- [ ] **F1**: Show About dialog
- [ ] **Tab Navigation**: Navigate through UI elements with Tab
- [ ] **Enter/Space**: Activate buttons with keyboard

## 📊 Performance Tests

### Large Queue Handling
- [ ] **100+ Tasks**: Add 100+ tasks to queue, verify UI remains responsive
- [ ] **Memory Usage**: Monitor memory usage with large queue
- [ ] **Startup Time**: Measure startup time with large saved queue
- [ ] **UI Responsiveness**: Verify UI remains responsive during processing

### File Size Handling
- [ ] **Large Files**: Test with very large audio/video files (1GB+)
- [ ] **Many Small Files**: Test with many small files
- [ ] **Mixed File Sizes**: Test with mix of small and large files

### Long-running Operations
- [ ] **Overnight Processing**: Start large queue, leave overnight, verify completion
- [ ] **System Sleep/Wake**: Test queue processing through system sleep/wake
- [ ] **Memory Leaks**: Run extended processing, monitor for memory leaks

## 🔍 Edge Cases and Error Handling

### File System Edge Cases
- [ ] **Read-only Files**: Test with read-only audio files
- [ ] **Network Files**: Test with files on network shares
- [ ] **Special Characters**: Test with files containing special characters
- [ ] **Long Paths**: Test with very long file paths
- [ ] **Unicode Names**: Test with files containing Unicode characters

### Application State Edge Cases
- [ ] **Rapid Clicking**: Rapidly click buttons, verify no crashes
- [ ] **Simultaneous Operations**: Try multiple operations simultaneously
- [ ] **Window Resizing**: Resize window during processing
- [ ] **Theme Changes**: Change system theme during app operation

### Recovery Scenarios
- [ ] **Corrupted Queue File**: Test with corrupted queue file
- [ ] **Missing Output Directory**: Test with deleted output directory
- [ ] **Changed File Locations**: Test with moved/renamed files in queue
- [ ] **Settings Corruption**: Test with corrupted settings file

## 📱 User Experience Tests

### First-time User Experience
- [ ] **Initial Setup**: Fresh install, verify first-time setup experience
- [ ] **Default Settings**: Verify sensible defaults are set
- [ ] **Helpful Error Messages**: Verify error messages are user-friendly
- [ ] **Progress Feedback**: Verify adequate progress feedback throughout

### Accessibility
- [ ] **High Contrast**: Test with high contrast themes
- [ ] **Screen Reader**: Test with screen reader (if available)
- [ ] **Keyboard Navigation**: Verify full keyboard navigation support
- [ ] **Font Scaling**: Test with different system font sizes

### Internationalization
- [ ] **Different Languages**: Test with different system languages
- [ ] **RTL Languages**: Test with right-to-left languages (if applicable)
- [ ] **Date/Time Formats**: Test with different date/time formats

## ✅ Test Completion Checklist

- [ ] All startup tests passed
- [ ] All file management tests passed  
- [ ] All settings tests passed
- [ ] All queue management tests passed
- [ ] All processing tests passed
- [ ] All system tray tests passed
- [ ] All keyboard shortcut tests passed
- [ ] All performance tests passed
- [ ] All edge case tests passed
- [ ] All user experience tests passed

## 📋 Test Results Summary

**Test Environment:**
- OS: Windows ___________
- .NET Version: ___________
- FFmpeg Version: ___________
- Whisper Models: ___________
- Test Date: ___________
- Tester: ___________

**Overall Results:**
- Total Tests: ___ / ___
- Passed: ___ 
- Failed: ___
- Success Rate: ___%

**Critical Issues Found:**
- [ ] None
- [ ] Minor issues (list below)
- [ ] Major issues (list below)

**Issues Found:**
1. ________________________________
2. ________________________________
3. ________________________________

**Recommendations:**
1. ________________________________
2. ________________________________
3. ________________________________