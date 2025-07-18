@echo off
echo Whisper Transcription Queue - Debug Resume Issue
echo ===============================================

echo.
echo This script will help diagnose queue resume issues.

echo.
echo 1. Creating test queue with tasks...

:: Navigate to the app directory
cd /d "%~dp0"

:: Check if the shared directory and queue file exist
if not exist "shared" mkdir shared

:: Create a test queue file with tasks in different states
echo Creating test queue file...
> shared\TranscriptionQueue.json (
echo {
echo   "schemaVersion": 1,
echo   "tasks": [
echo     {
echo       "id": "test-pending-1",
echo       "filePath": "C:\\test\\audio1.mp3",
echo       "modelName": "ggml-base.bin",
echo       "language": "auto",
echo       "duration": "00:03:30",
echo       "status": 0,
echo       "addedAt": "2025-01-17T10:00:00Z",
echo       "completedAt": null,
echo       "outputPath": null,
echo       "errorMessage": null,
echo       "progress": 0.0
echo     },
echo     {
echo       "id": "test-processing-reset",
echo       "filePath": "C:\\test\\audio2.mp3",
echo       "modelName": "ggml-base.bin",
echo       "language": "auto",
echo       "duration": "00:05:00",
echo       "status": 1,
echo       "addedAt": "2025-01-17T10:00:00Z",
echo       "completedAt": null,
echo       "outputPath": null,
echo       "errorMessage": null,
echo       "progress": 45.67
echo     },
echo     {
echo       "id": "test-pending-2",
echo       "filePath": "C:\\test\\audio3.mp3",
echo       "modelName": "ggml-base.bin",
echo       "language": "auto",
echo       "duration": "00:02:15",
echo       "status": 0,
echo       "addedAt": "2025-01-17T10:00:00Z",
echo       "completedAt": null,
echo       "outputPath": null,
echo       "errorMessage": null,
echo       "progress": 0.0
echo     }
echo   ],
echo   "settings": {
echo     "autoSaveInterval": 300,
echo     "defaultOutputDirectory": "C:\\audio\\transcripts"
echo   }
echo }
)

echo Test queue created with:
echo - 2 Pending tasks (should be processable)
echo - 1 Processing task with 45.67%% progress (should be reset to Pending with 0%% progress)

echo.
echo 2. Launching TaskUI for debugging...
start "" "src\WhisperTranscriberCLI.TaskUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\WhisperTranscriberCLI.TaskUI.exe"

echo.
echo Debug Instructions:
echo ==================
echo 1. Check if the "test-processing-reset" task shows 0%% progress (not 45.67%%)
echo 2. Verify you have 3 total tasks: all should show "Pending" status
echo 3. Try clicking "Start Queue" - it should work if there are pending tasks
echo 4. Check the logs for any error messages

echo.
echo Expected Behavior:
echo - All tasks should show 0%% progress on app startup
echo - "Start Queue" button should work if pending tasks exist
echo - Queue state logging should show the actual task counts

echo.
echo Debugging Tips:
echo - Look for log messages containing "Queue state:" in the console
echo - Check if CanStartProcessing() returns true
echo - Verify the cancellation token is properly recreated

echo.
echo Press any key to view the test queue file...
pause >nul
type shared\TranscriptionQueue.json

echo.
echo Press any key to continue...
pause >nul