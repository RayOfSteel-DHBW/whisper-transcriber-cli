# Test the enhanced error logging in WhisperTranscriberCLI.TaskUI
Write-Host "?? Testing Enhanced Transcription Error Logging" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host

# Build first
Write-Host "?? Building application..." -ForegroundColor Yellow
try {
    dotnet build --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "? Build successful!" -ForegroundColor Green
} catch {
    Write-Host "? Build error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host
Write-Host "?? Starting TaskUI application..." -ForegroundColor Yellow
Write-Host "   - Enhanced logging is now enabled" -ForegroundColor Gray
Write-Host "   - Logs will be written to: %LOCALAPPDATA%\WhisperTranscriberCLI\Logs\" -ForegroundColor Gray
Write-Host "   - Try transcribing a file to generate detailed logs" -ForegroundColor Gray
Write-Host

# Start the TaskUI app
try {
    Start-Process -FilePath ".\src\WhisperTranscriberCLI.TaskUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\WhisperTranscriberCLI.TaskUI.exe" -WorkingDirectory $PWD
    Write-Host "? Application started!" -ForegroundColor Green
    Write-Host
    Write-Host "?? What to do next:" -ForegroundColor Cyan
    Write-Host "   1. Add a test audio file to the queue" -ForegroundColor Gray
    Write-Host "   2. Start transcription" -ForegroundColor Gray
    Write-Host "   3. Check the detailed logs if it fails" -ForegroundColor Gray
    Write-Host
    Write-Host "?? Run this to check logs after testing:" -ForegroundColor Yellow
    Write-Host "   powershell -ExecutionPolicy Bypass -File .\diagnose-transcription-issues.ps1" -ForegroundColor White
} catch {
    Write-Host "? Failed to start application: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Try running: .\launch-debug-taskui.bat" -ForegroundColor Yellow
}