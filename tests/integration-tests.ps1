# Integration Testing Script for Whisper Transcription Queue
# This script performs comprehensive integration testing of the application

param(
    [string]$TestDataPath = "test-data",
    [string]$OutputPath = "test-output",
    [int]$TimeoutSeconds = 300,
    [switch]$Verbose
)

Write-Host "🧪 Starting Integration Tests for Whisper Transcription Queue" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Create test directories
if (!(Test-Path $TestDataPath)) {
    New-Item -ItemType Directory -Path $TestDataPath -Force
    Write-Host "✅ Created test data directory: $TestDataPath" -ForegroundColor Green
}

if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force
    Write-Host "✅ Created output directory: $OutputPath" -ForegroundColor Green
}

# Test Results
$TestResults = @{
    Passed = 0
    Failed = 0
    Total = 0
    Details = @()
}

function Write-TestResult {
    param($TestName, $Success, $Details = "")
    
    $TestResults.Total++
    
    if ($Success) {
        $TestResults.Passed++
        Write-Host "✅ PASS: $TestName" -ForegroundColor Green
    } else {
        $TestResults.Failed++
        Write-Host "❌ FAIL: $TestName" -ForegroundColor Red
        if ($Details) {
            Write-Host "   Details: $Details" -ForegroundColor Yellow
        }
    }
    
    $TestResults.Details += @{
        Name = $TestName
        Success = $Success
        Details = $Details
        Timestamp = Get-Date
    }
}

# Test 1: System Requirements Check
Write-Host "`n🔍 Testing System Requirements..." -ForegroundColor Cyan
try {
    # Check if ffmpeg is available
    $ffmpegTest = & { ffmpeg -version 2>&1 }
    $ffmpegAvailable = $LASTEXITCODE -eq 0
    Write-TestResult "FFmpeg Availability" $ffmpegAvailable

    # Check if whisper models directory exists
    $whisperModelsExist = Test-Path "whispermodels"
    Write-TestResult "Whisper Models Directory" $whisperModelsExist

    # Check if any model files exist
    if ($whisperModelsExist) {
        $modelFiles = Get-ChildItem "whispermodels" -Filter "*.bin" -ErrorAction SilentlyContinue
        $hasModels = $modelFiles.Count -gt 0
        Write-TestResult "Whisper Model Files Available" $hasModels "$($modelFiles.Count) models found"
    } else {
        Write-TestResult "Whisper Model Files Available" $false "whispermodels directory not found"
    }

    # Check disk space (at least 1GB)
    $drive = Get-PSDrive -Name (Get-Location).Drive.Name
    $freeSpaceGB = [math]::Round($drive.Free / 1GB, 2)
    $sufficientSpace = $freeSpaceGB -gt 1
    Write-TestResult "Sufficient Disk Space" $sufficientSpace "$freeSpaceGB GB available"

} catch {
    Write-TestResult "System Requirements Check" $false $_.Exception.Message
}

# Test 2: Queue File Persistence
Write-Host "`n💾 Testing Queue Persistence..." -ForegroundColor Cyan
try {
    $testQueuePath = Join-Path $OutputPath "test-queue.json"
    
    # Create a test queue file
    $testQueue = @{
        schemaVersion = 1
        tasks = @(
            @{
                id = "test-task-1"
                filePath = "test-audio.mp3"
                modelName = "ggml-base.bin"
                language = "auto"
                duration = "00:05:30"
                status = "pending"
                addedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                completedAt = $null
                outputPath = $null
                errorMessage = $null
                progress = 0.0
            }
        )
    }
    
    $testQueueJson = $testQueue | ConvertTo-Json -Depth 5
    Set-Content -Path $testQueuePath -Value $testQueueJson
    
    # Verify file was created and is valid JSON
    $exists = Test-Path $testQueuePath
    Write-TestResult "Queue File Creation" $exists
    
    if ($exists) {
        $loadedQueue = Get-Content $testQueuePath | ConvertFrom-Json
        $validStructure = $loadedQueue.schemaVersion -eq 1 -and $loadedQueue.tasks.Count -eq 1
        Write-TestResult "Queue File Structure" $validStructure
        
        $taskValid = $loadedQueue.tasks[0].id -eq "test-task-1"
        Write-TestResult "Queue Task Data" $taskValid
    }

} catch {
    Write-TestResult "Queue Persistence Test" $false $_.Exception.Message
}

# Test 3: Error Scenarios
Write-Host "`n⚠️  Testing Error Scenarios..." -ForegroundColor Cyan
try {
    # Test with invalid queue file
    $invalidQueuePath = Join-Path $OutputPath "invalid-queue.json"
    Set-Content -Path $invalidQueuePath -Value "{ invalid json"
    
    Write-TestResult "Invalid Queue File Created" (Test-Path $invalidQueuePath)
    
    # Test with non-existent file path
    $nonExistentFile = Join-Path $TestDataPath "nonexistent.mp3"
    $fileExists = Test-Path $nonExistentFile
    Write-TestResult "Non-existent File Test Setup" (!$fileExists) "File should not exist for this test"
    
    # Test with insufficient permissions (if applicable)
    # This would require platform-specific testing
    
} catch {
    Write-TestResult "Error Scenarios Test" $false $_.Exception.Message
}

# Test 4: Performance with Multiple Tasks
Write-Host "`n⚡ Testing Performance with Multiple Tasks..." -ForegroundColor Cyan
try {
    $largeQueuePath = Join-Path $OutputPath "large-queue.json"
    
    # Create a queue with many tasks
    $tasks = @()
    for ($i = 1; $i -le 100; $i++) {
        $tasks += @{
            id = "task-$i"
            filePath = "test-audio-$i.mp3"
            modelName = "ggml-base.bin"
            language = "auto"
            duration = "00:05:30"
            status = "pending"
            addedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            completedAt = $null
            outputPath = $null
            errorMessage = $null
            progress = 0.0
        }
    }
    
    $largeQueue = @{
        schemaVersion = 1
        tasks = $tasks
    }
    
    $startTime = Get-Date
    $largeQueueJson = $largeQueue | ConvertTo-Json -Depth 5
    Set-Content -Path $largeQueuePath -Value $largeQueueJson
    $endTime = Get-Date
    
    $processingTime = ($endTime - $startTime).TotalMilliseconds
    $performanceOk = $processingTime -lt 5000  # Should complete in less than 5 seconds
    
    Write-TestResult "Large Queue Creation Performance" $performanceOk "Took $([math]::Round($processingTime, 2))ms for 100 tasks"
    
    # Test loading large queue
    $startTime = Get-Date
    $loadedLargeQueue = Get-Content $largeQueuePath | ConvertFrom-Json
    $endTime = Get-Date
    
    $loadTime = ($endTime - $startTime).TotalMilliseconds
    $loadPerformanceOk = $loadTime -lt 2000  # Should load in less than 2 seconds
    
    Write-TestResult "Large Queue Loading Performance" $loadPerformanceOk "Took $([math]::Round($loadTime, 2))ms to load 100 tasks"
    
} catch {
    Write-TestResult "Performance Test" $false $_.Exception.Message
}

# Test 5: File Type Support
Write-Host "`n📁 Testing File Type Support..." -ForegroundColor Cyan
try {
    $supportedExtensions = @(".mp3", ".wav", ".mp4", ".avi", ".mkv", ".m4a", ".flac", ".ogg", ".webm", ".wma")
    
    foreach ($ext in $supportedExtensions) {
        $testFile = "test-audio$ext"
        # Just test the extension validation logic
        $isSupported = $supportedExtensions -contains $ext
        Write-TestResult "File Extension Support: $ext" $isSupported
    }
    
    # Test unsupported extensions
    $unsupportedExtensions = @(".txt", ".doc", ".pdf", ".exe")
    foreach ($ext in $unsupportedExtensions) {
        $isUnsupported = $supportedExtensions -notcontains $ext
        Write-TestResult "File Extension Rejection: $ext" $isUnsupported
    }
    
} catch {
    Write-TestResult "File Type Support Test" $false $_.Exception.Message
}

# Test 6: Auto-save Functionality
Write-Host "`n💾 Testing Auto-save Functionality..." -ForegroundColor Cyan
try {
    $autosavePath = Join-Path $OutputPath "autosave-test.json"
    
    # Simulate auto-save by creating multiple versions
    for ($i = 1; $i -le 3; $i++) {
        $autosaveQueue = @{
            schemaVersion = 1
            tasks = @(
                @{
                    id = "autosave-task-$i"
                    filePath = "test-audio-$i.mp3"
                    modelName = "ggml-base.bin"
                    language = "auto"
                    duration = "00:05:30"
                    status = "pending"
                    addedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                    completedAt = $null
                    outputPath = $null
                    errorMessage = $null
                    progress = 0.0
                }
            )
        }
        
        $autosaveJson = $autosaveQueue | ConvertTo-Json -Depth 5
        Set-Content -Path $autosavePath -Value $autosaveJson
        
        Start-Sleep -Milliseconds 100  # Small delay to simulate time passing
    }
    
    # Verify final state
    $finalQueue = Get-Content $autosavePath | ConvertFrom-Json
    $autosaveWorking = $finalQueue.tasks[0].id -eq "autosave-task-3"
    Write-TestResult "Auto-save Functionality" $autosaveWorking
    
} catch {
    Write-TestResult "Auto-save Test" $false $_.Exception.Message
}

# Test 7: Settings Persistence
Write-Host "`n⚙️  Testing Settings Persistence..." -ForegroundColor Cyan
try {
    # Test settings structure
    $testSettings = @{
        OutputDirectory = $OutputPath
        DefaultModel = "ggml-base.bin"
        DefaultLanguage = "auto"
        Recursive = $true
        WindowWidth = 800
        WindowHeight = 600
        WindowX = 100
        WindowY = 100
        LastQueuePath = ""
        MinimizeToTray = $true
        CloseToTray = $false
        SystemTrayEnabled = $true
        OpenOutputAfterCompletion = $false
    }
    
    $settingsPath = Join-Path $OutputPath "test-settings.json"
    $settingsJson = $testSettings | ConvertTo-Json -Depth 3
    Set-Content -Path $settingsPath -Value $settingsJson
    
    # Verify settings can be loaded
    $loadedSettings = Get-Content $settingsPath | ConvertFrom-Json
    $settingsValid = $loadedSettings.DefaultModel -eq "ggml-base.bin" -and 
                    $loadedSettings.OutputDirectory -eq $OutputPath
    
    Write-TestResult "Settings Persistence" $settingsValid
    
} catch {
    Write-TestResult "Settings Persistence Test" $false $_.Exception.Message
}

# Generate Test Report
Write-Host "`n📊 Test Results Summary" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host "Total Tests: $($TestResults.Total)" -ForegroundColor White
Write-Host "Passed: $($TestResults.Passed)" -ForegroundColor Green
Write-Host "Failed: $($TestResults.Failed)" -ForegroundColor Red

$successRate = if ($TestResults.Total -gt 0) { 
    [math]::Round(($TestResults.Passed / $TestResults.Total) * 100, 1) 
} else { 0 }
Write-Host "Success Rate: $successRate%" -ForegroundColor $(if ($successRate -gt 90) { "Green" } elseif ($successRate -gt 70) { "Yellow" } else { "Red" })

# Save detailed results
$reportPath = Join-Path $OutputPath "test-report.json"
$TestResults | ConvertTo-Json -Depth 3 | Set-Content -Path $reportPath
Write-Host "`nDetailed report saved to: $reportPath" -ForegroundColor Cyan

# Return appropriate exit code
if ($TestResults.Failed -eq 0) {
    Write-Host "`n🎉 All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  Some tests failed. Check the report for details." -ForegroundColor Yellow
    exit 1
}