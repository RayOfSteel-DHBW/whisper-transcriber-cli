@echo off
echo ======================================
echo Whisper Transcription Diagnostic Tool
echo ======================================
echo.

set "LOG_DIR=%LOCALAPPDATA%\WhisperTranscriberCLI\Logs"
set "TODAY=%date:~-4,4%%date:~-10,2%%date:~-7,2%"
set "LOG_FILE=%LOG_DIR%\WhisperTranscriberUI_%TODAY%.log"

echo 1. Checking log directory...
if not exist "%LOG_DIR%" (
    echo    Log directory does not exist: %LOG_DIR%
    echo    This is normal if the app hasn't been run yet.
) else (
    echo    Log directory exists: %LOG_DIR%
    echo    Files in log directory:
    dir "%LOG_DIR%" /b
)

echo.
echo 2. Checking for today's log file...
if not exist "%LOG_FILE%" (
    echo    Today's log file does not exist: %LOG_FILE%
    echo    This is normal if the app hasn't been run today.
) else (
    echo    Today's log file exists: %LOG_FILE%
    echo    File size: 
    for %%F in ("%LOG_FILE%") do echo    %%~zF bytes
    echo.
    echo    Last 50 lines of log:
    echo    ----------------------------------------
    powershell "Get-Content '%LOG_FILE%' | Select-Object -Last 50"
    echo    ----------------------------------------
)

echo.
echo 3. Checking model directory...
set "MODEL_DIR=%CD%\whispermodels"
if not exist "%MODEL_DIR%" (
    echo    Model directory does not exist: %MODEL_DIR%
    echo    ERROR: This could be why transcription is failing!
) else (
    echo    Model directory exists: %MODEL_DIR%
    echo    Model files found:
    dir "%MODEL_DIR%\*.bin" /b 2>nul
    if errorlevel 1 (
        echo    ERROR: No .bin model files found!
        echo    This is likely why transcription is failing.
    )
)

echo.
echo 4. Checking FFmpeg availability...
ffmpeg -version >nul 2>&1
if errorlevel 1 (
    echo    ERROR: FFmpeg not found in PATH!
    echo    This could cause media conversion failures.
) else (
    echo    FFmpeg is available
)

echo.
echo 5. System information...
echo    Current directory: %CD%
echo    Date/Time: %date% %time%
echo    User: %USERNAME%

echo.
echo ======================================
echo Diagnostic complete!
echo.
echo If you're experiencing transcription errors:
echo 1. Check that model files (.bin) exist in: %MODEL_DIR%
echo 2. Check the log file for detailed error messages: %LOG_FILE%
echo 3. Ensure FFmpeg is installed and in your PATH
echo.
pause