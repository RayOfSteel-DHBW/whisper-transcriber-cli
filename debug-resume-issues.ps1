Write-Host "Whisper Transcription Queue - Debug Resume Issue" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "This script will help diagnose queue resume issues." -ForegroundColor Yellow

Write-Host ""
Write-Host "1. Creating test queue with tasks..." -ForegroundColor Green

# Navigate to the script directory
Set-Location -Path $PSScriptRoot

# Check if the shared directory exists
if (-not (Test-Path "shared")) {
    New-Item -ItemType Directory -Path "shared" | Out-Null
}

# Create a test queue file with tasks in different states
Write-Host "Creating test queue file..." -ForegroundColor Green

$testQueue = @{
    schemaVersion = 1
    tasks = @(
        @{
            id = "test-pending-1"
            filePath = "C:\test\audio1.mp3"
            modelName = "ggml-base.bin"
            language = "auto"
            duration = "00:03:30"
            status = 0
            addedAt = "2025-01-17T10:00:00Z"
            completedAt = $null
            outputPath = $null
            errorMessage = $null
            progress = 0.0
        },
        @{
            id = "test-processing-reset"
            filePath = "C:\test\audio2.mp3"
            modelName = "ggml-base.bin"
            language = "auto"
            duration = "00:05:00"
            status = 1  # Processing status
            addedAt = "2025-01-17T10:00:00Z"
            completedAt = $null
            outputPath = $null
            errorMessage = $null
            progress = 45.67  # This should be reset to 0
        },
        @{
            id = "test-pending-2"
            filePath = "C:\test\audio3.mp3"
            modelName = "ggml-base.bin"
            language = "auto"
            duration = "00:02:15"
            status = 0
            addedAt = "2025-01-17T10:00:00Z"
            completedAt = $null
            outputPath = $null
            errorMessage = $null
            progress = 0.0
        }
    )
    settings = @{
        autoSaveInterval = 300
        defaultOutputDirectory = "C:\audio\transcripts"
    }
}

# Convert to JSON and save
$testQueue | ConvertTo-Json -Depth 10 | Out-File -FilePath "shared\TranscriptionQueue.json" -Encoding UTF8

Write-Host "Test queue created with:" -ForegroundColor Green
Write-Host "- 2 Pending tasks (should be processable)" -ForegroundColor White
Write-Host "- 1 Processing task with 45.67% progress (should be reset to Pending with 0% progress)" -ForegroundColor White

Write-Host ""
Write-Host "2. Launching TaskUI for debugging..." -ForegroundColor Green
Start-Process -FilePath "src\WhisperTranscriberCLI.TaskUI\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\WhisperTranscriberCLI.TaskUI.exe"

Write-Host ""
Write-Host "Debug Instructions:" -ForegroundColor Cyan
Write-Host "==================" -ForegroundColor Cyan
Write-Host "1. Check if the 'test-processing-reset' task shows 0% progress (not 45.67%)" -ForegroundColor Yellow
Write-Host "2. Verify you have 3 total tasks: all should show 'Pending' status" -ForegroundColor Yellow
Write-Host "3. Try clicking 'Start Queue' - it should work if there are pending tasks" -ForegroundColor Yellow
Write-Host "4. Check the logs for any error messages" -ForegroundColor Yellow

Write-Host ""
Write-Host "Expected Behavior:" -ForegroundColor Cyan
Write-Host "- All tasks should show 0% progress on app startup" -ForegroundColor White
Write-Host "- 'Start Queue' button should work if pending tasks exist" -ForegroundColor White
Write-Host "- Queue state logging should show the actual task counts" -ForegroundColor White

Write-Host ""
Write-Host "Debugging Tips:" -ForegroundColor Cyan
Write-Host "- Look for log messages containing 'Queue state:' in the console" -ForegroundColor White
Write-Host "- Check if CanStartProcessing() returns true" -ForegroundColor White
Write-Host "- Verify the cancellation token is properly recreated" -ForegroundColor White

Write-Host ""
Write-Host "Press any key to view the test queue file..." -ForegroundColor Green
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

Write-Host ""
Write-Host "Test Queue File Contents:" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Get-Content "shared\TranscriptionQueue.json" | Write-Host

Write-Host ""
Write-Host "Press any key to continue..." -ForegroundColor Green
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")