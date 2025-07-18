# PowerShell Transcription Diagnostic Tool
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Whisper Transcription Diagnostic Tool" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host

$LogDir = "$env:LOCALAPPDATA\WhisperTranscriberCLI\Logs"
$Today = Get-Date -Format "yyyyMMdd"
$LogFile = "$LogDir\WhisperTranscriberUI_$Today.log"

Write-Host "1. Checking log directory..." -ForegroundColor Yellow
if (-not (Test-Path $LogDir)) {
    Write-Host "   Log directory does not exist: $LogDir" -ForegroundColor Red
    Write-Host "   This is normal if the app hasn't been run yet." -ForegroundColor Gray
} else {
    Write-Host "   Log directory exists: $LogDir" -ForegroundColor Green
    Write-Host "   Files in log directory:" -ForegroundColor Gray
    Get-ChildItem $LogDir | ForEach-Object { Write-Host "   - $($_.Name)" -ForegroundColor Gray }
}

Write-Host
Write-Host "2. Checking for today's log file..." -ForegroundColor Yellow
if (-not (Test-Path $LogFile)) {
    Write-Host "   Today's log file does not exist: $LogFile" -ForegroundColor Red
    Write-Host "   This is normal if the app hasn't been run today." -ForegroundColor Gray
} else {
    Write-Host "   Today's log file exists: $LogFile" -ForegroundColor Green
    $FileSize = (Get-Item $LogFile).Length
    Write-Host "   File size: $FileSize bytes" -ForegroundColor Gray
    Write-Host
    Write-Host "   Last 50 lines of log:" -ForegroundColor Gray
    Write-Host "   ----------------------------------------" -ForegroundColor DarkGray
    Get-Content $LogFile | Select-Object -Last 50 | ForEach-Object { 
        if ($_ -match "\[Error\]|\[Critical\]") {
            Write-Host $_ -ForegroundColor Red
        } elseif ($_ -match "\[Warning\]") {
            Write-Host $_ -ForegroundColor Yellow
        } elseif ($_ -match "\[Information\]") {
            Write-Host $_ -ForegroundColor White
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }
    Write-Host "   ----------------------------------------" -ForegroundColor DarkGray
}

Write-Host
Write-Host "3. Checking model directory..." -ForegroundColor Yellow
$ModelDir = "$PWD\whispermodels"
if (-not (Test-Path $ModelDir)) {
    Write-Host "   ERROR: Model directory does not exist: $ModelDir" -ForegroundColor Red
    Write-Host "   This is likely why transcription is failing!" -ForegroundColor Red
} else {
    Write-Host "   Model directory exists: $ModelDir" -ForegroundColor Green
    $ModelFiles = Get-ChildItem "$ModelDir\*.bin" -ErrorAction SilentlyContinue
    if ($ModelFiles) {
        Write-Host "   Model files found:" -ForegroundColor Green
        $ModelFiles | ForEach-Object { 
            $Size = [math]::Round($_.Length / 1MB, 1)
            Write-Host "   - $($_.Name) ($Size MB)" -ForegroundColor Green
        }
    } else {
        Write-Host "   ERROR: No .bin model files found!" -ForegroundColor Red
        Write-Host "   This is likely why transcription is failing." -ForegroundColor Red
    }
}

Write-Host
Write-Host "4. Checking FFmpeg availability..." -ForegroundColor Yellow
try {
    $FFmpegVersion = & ffmpeg -version 2>$null | Select-Object -First 1
    if ($FFmpegVersion) {
        Write-Host "   FFmpeg is available: $($FFmpegVersion.Split(' ')[2])" -ForegroundColor Green
    } else {
        Write-Host "   ERROR: FFmpeg not found in PATH!" -ForegroundColor Red
        Write-Host "   This could cause media conversion failures." -ForegroundColor Red
    }
} catch {
    Write-Host "   ERROR: FFmpeg not found in PATH!" -ForegroundColor Red
    Write-Host "   This could cause media conversion failures." -ForegroundColor Red
}

Write-Host
Write-Host "5. System information..." -ForegroundColor Yellow
Write-Host "   Current directory: $PWD" -ForegroundColor Gray
Write-Host "   Date/Time: $(Get-Date)" -ForegroundColor Gray
Write-Host "   User: $env:USERNAME" -ForegroundColor Gray
Write-Host "   PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Gray

Write-Host
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Diagnostic complete!" -ForegroundColor Cyan

Write-Host
if (-not (Test-Path "$ModelDir\*.bin")) {
    Write-Host "?? LIKELY ISSUE: No model files found!" -ForegroundColor Red
    Write-Host "   1. Download Whisper models (.bin files)" -ForegroundColor Yellow
    Write-Host "   2. Place them in: $ModelDir" -ForegroundColor Yellow
    Write-Host "   3. Try transcription again" -ForegroundColor Yellow
} elseif (-not (Test-Path $LogFile)) {
    Write-Host "?? INFO: No logs yet - try running a transcription first" -ForegroundColor Cyan
} else {
    Write-Host "?? Check the log above for detailed error messages" -ForegroundColor Cyan
    Write-Host "?? Full log file: $LogFile" -ForegroundColor Cyan
}