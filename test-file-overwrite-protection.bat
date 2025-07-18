@echo off
echo Testing File Overwrite Protection Feature
echo =======================================

echo.
echo Creating test files...

:: Create test directory
if not exist "test-overwrite" mkdir test-overwrite
cd test-overwrite

:: Create a dummy audio file for testing
echo This is a test audio file > test-audio.mp3

:: Create an existing SRT file to test overwrite protection
echo 1 > test-audio.srt
echo 00:00:00,000 --^> 00:00:05,000 >> test-audio.srt
echo This is existing transcription content. >> test-audio.srt

echo.
echo Test files created:
echo - test-audio.mp3 (dummy audio file)
echo - test-audio.srt (existing transcription file)

echo.
echo Instructions:
echo 1. Launch the TaskUI application
echo 2. Add the test-audio.mp3 file to the queue
echo 3. You should see a dialog asking whether to overwrite the existing .srt file
echo 4. Test both "Overwrite" and "Skip" options

echo.
echo Expected behavior:
echo - If you choose "Overwrite": The transcription should proceed normally
echo - If you choose "Skip": The task should be marked as completed without processing
echo - Status should show "Files processed: X added, Y skipped" when adding files

echo.
echo Press any key to launch the TaskUI application...
pause >nul

cd ..
start "" "src\WhisperTranscriberCLI.TaskUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\WhisperTranscriberCLI.TaskUI.exe"

echo.
echo TaskUI launched. Navigate to the test-overwrite folder to test file overwrite protection.
echo.
pause