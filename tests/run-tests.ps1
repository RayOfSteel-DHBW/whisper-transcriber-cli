# Automated Build and Test Runner for Whisper Transcription Queue
param(
    [switch]$SkipBuild,
    [switch]$SkipUnitTests,
    [switch]$SkipIntegrationTests,
    [switch]$GenerateReport,
    [switch]$Verbose
)

Write-Host "🚀 Automated Testing Pipeline for Whisper Transcription Queue" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

$ErrorActionPreference = "Stop"
$TestResults = @{
    BuildSuccess = $false
    UnitTestsSuccess = $false
    IntegrationTestsSuccess = $false
    TotalTestsRun = 0
    TotalTestsPassed = 0
    TotalTestsFailed = 0
    StartTime = Get-Date
    EndTime = $null
}

# Build the solution
if (-not $SkipBuild) {
    Write-Host "`n🔨 Building Solution..." -ForegroundColor Cyan
    try {
        # Clean previous builds
        dotnet clean TranscriberCLI.sln --configuration Release
        
        # Restore packages
        dotnet restore TranscriberCLI.sln
        
        # Build the solution
        dotnet build TranscriberCLI.sln --configuration Release --no-restore
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Build successful" -ForegroundColor Green
            $TestResults.BuildSuccess = $true
        } else {
            Write-Host "❌ Build failed" -ForegroundColor Red
            exit 1
        }
    } catch {
        Write-Host "❌ Build failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Run unit tests
if (-not $SkipUnitTests) {
    Write-Host "`n🧪 Running Unit Tests..." -ForegroundColor Cyan
    try {
        $unitTestResult = dotnet test tests/WhisperTranscriberCLI.Core.Tests/WhisperTranscriberCLI.Core.Tests.csproj --configuration Release --logger "trx;LogFileName=unit-test-results.trx" --collect:"XPlat Code Coverage"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Unit tests passed" -ForegroundColor Green
            $TestResults.UnitTestsSuccess = $true
            
            # Parse test results
            $trxFile = Get-ChildItem -Path "tests/WhisperTranscriberCLI.Core.Tests/TestResults" -Filter "*.trx" -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($trxFile) {
                [xml]$testResults = Get-Content $trxFile.FullName
                $total = [int]$testResults.TestRun.ResultSummary.Counters.total
                $passed = [int]$testResults.TestRun.ResultSummary.Counters.passed
                $failed = [int]$testResults.TestRun.ResultSummary.Counters.failed
                
                $TestResults.TotalTestsRun += $total
                $TestResults.TotalTestsPassed += $passed
                $TestResults.TotalTestsFailed += $failed
                
                Write-Host "   Total: $total, Passed: $passed, Failed: $failed" -ForegroundColor White
            }
        } else {
            Write-Host "❌ Unit tests failed" -ForegroundColor Red
            $TestResults.UnitTestsSuccess = $false
        }
    } catch {
        Write-Host "❌ Unit tests failed: $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.UnitTestsSuccess = $false
    }
}

# Run integration tests
if (-not $SkipIntegrationTests) {
    Write-Host "`n🔗 Running Integration Tests..." -ForegroundColor Cyan
    try {
        $integrationResult = & "./tests/integration-tests.ps1" -Verbose:$Verbose
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Integration tests passed" -ForegroundColor Green
            $TestResults.IntegrationTestsSuccess = $true
        } else {
            Write-Host "❌ Integration tests failed" -ForegroundColor Red
            $TestResults.IntegrationTestsSuccess = $false
        }
        
        # Parse integration test results
        $integrationReportPath = "test-output/test-report.json"
        if (Test-Path $integrationReportPath) {
            $integrationReport = Get-Content $integrationReportPath | ConvertFrom-Json
            $TestResults.TotalTestsRun += $integrationReport.Total
            $TestResults.TotalTestsPassed += $integrationReport.Passed
            $TestResults.TotalTestsFailed += $integrationReport.Failed
        }
    } catch {
        Write-Host "❌ Integration tests failed: $($_.Exception.Message)" -ForegroundColor Red
        $TestResults.IntegrationTestsSuccess = $false
    }
}

# Generate comprehensive test report
$TestResults.EndTime = Get-Date
$duration = ($TestResults.EndTime - $TestResults.StartTime).TotalMinutes

if ($GenerateReport) {
    Write-Host "`n📊 Generating Test Report..." -ForegroundColor Cyan
    
    $reportContent = @"
# Test Execution Report
Generated: $($TestResults.EndTime.ToString("yyyy-MM-dd HH:mm:ss"))
Duration: $([math]::Round($duration, 2)) minutes

## Summary
- **Build**: $(if ($TestResults.BuildSuccess) { "✅ PASSED" } else { "❌ FAILED" })
- **Unit Tests**: $(if ($TestResults.UnitTestsSuccess) { "✅ PASSED" } else { "❌ FAILED" })
- **Integration Tests**: $(if ($TestResults.IntegrationTestsSuccess) { "✅ PASSED" } else { "❌ FAILED" })

## Test Statistics
- **Total Tests Run**: $($TestResults.TotalTestsRun)
- **Tests Passed**: $($TestResults.TotalTestsPassed)
- **Tests Failed**: $($TestResults.TotalTestsFailed)
- **Success Rate**: $(if ($TestResults.TotalTestsRun -gt 0) { [math]::Round(($TestResults.TotalTestsPassed / $TestResults.TotalTestsRun) * 100, 1) } else { 0 })%

## Environment
- **OS**: $($env:OS)
- **PowerShell Version**: $($PSVersionTable.PSVersion)
- **dotnet Version**: $(dotnet --version)
- **Machine**: $($env:COMPUTERNAME)
- **User**: $($env:USERNAME)

## Next Steps
$(if ($TestResults.BuildSuccess -and $TestResults.UnitTestsSuccess -and $TestResults.IntegrationTestsSuccess) {
    "🎉 All tests passed! Ready for deployment."
} else {
    "⚠️ Some tests failed. Please review the results and fix any issues before deployment."
})

## Manual Testing
After automated tests pass, please run the manual testing checklist:
- Review: tests/manual-testing-checklist.md
- Focus on UI interactions and user experience
- Test system tray functionality
- Verify settings persistence
- Test with real audio files

"@
    
    $reportPath = "test-output/test-execution-report.md"
    if (!(Test-Path "test-output")) {
        New-Item -ItemType Directory -Path "test-output" -Force | Out-Null
    }
    
    Set-Content -Path $reportPath -Value $reportContent
    Write-Host "📋 Test report saved to: $reportPath" -ForegroundColor Cyan
}

# Final summary
Write-Host "`n📊 Test Execution Summary" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Build: $(if ($TestResults.BuildSuccess) { "✅ PASSED" } else { "❌ FAILED" })" -ForegroundColor $(if ($TestResults.BuildSuccess) { "Green" } else { "Red" })
Write-Host "Unit Tests: $(if ($TestResults.UnitTestsSuccess) { "✅ PASSED" } else { "❌ FAILED" })" -ForegroundColor $(if ($TestResults.UnitTestsSuccess) { "Green" } else { "Red" })
Write-Host "Integration Tests: $(if ($TestResults.IntegrationTestsSuccess) { "✅ PASSED" } else { "❌ FAILED" })" -ForegroundColor $(if ($TestResults.IntegrationTestsSuccess) { "Green" } else { "Red" })
Write-Host "Total Tests: $($TestResults.TotalTestsRun)" -ForegroundColor White
Write-Host "Success Rate: $(if ($TestResults.TotalTestsRun -gt 0) { [math]::Round(($TestResults.TotalTestsPassed / $TestResults.TotalTestsRun) * 100, 1) } else { 0 })%" -ForegroundColor White
Write-Host "Duration: $([math]::Round($duration, 2)) minutes" -ForegroundColor White

# Exit with appropriate code
$allTestsPassed = $TestResults.BuildSuccess -and $TestResults.UnitTestsSuccess -and $TestResults.IntegrationTestsSuccess
if ($allTestsPassed) {
    Write-Host "`n🎉 All tests passed! Application is ready for deployment." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️ Some tests failed. Please review the results and fix any issues." -ForegroundColor Yellow
    exit 1
}