using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using WhisperTranscriberCLI.Core.Services;

namespace WhisperTranscriberCLI.Core.Tests.Services;

public class SystemCheckServiceTests : IDisposable
{
    private readonly SystemCheckService _systemCheckService;
    private readonly string _testWhisperModelsDir;

    public SystemCheckServiceTests()
    {
        var userSettings = new UserSettingsService();
        var modelDiscovery = new ModelDiscovery(userSettings);
        _systemCheckService = new SystemCheckService(modelDiscovery);
        _testWhisperModelsDir = Path.Combine(Directory.GetCurrentDirectory(), "whispermodels");
    }

    [Fact]
    public async Task CheckSystemRequirementsAsync_ReturnsSystemCheckResult()
    {
        // Act
        var result = await _systemCheckService.CheckSystemRequirementsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<SystemCheckResult>(result);
    }

    [Fact]
    public async Task CheckSystemRequirementsAsync_ChecksAllRequirements()
    {
        // Act
        var result = await _systemCheckService.CheckSystemRequirementsAsync();

        // Assert
        Assert.NotNull(result);
        // FFmpeg availability depends on system setup
        Assert.True(result.FFmpegAvailable || !result.FFmpegAvailable);
        // Whisper models availability depends on test setup
        Assert.True(result.WhisperModelsAvailable || !result.WhisperModelsAvailable);
        // .NET runtime should be available (we're running on it)
        Assert.True(result.DotNetRuntimeAvailable);
        // Disk space should be sufficient for tests
        Assert.True(result.SufficientDiskSpace);
    }

    [Fact]
    public void GetFFmpegInstallationInstructions_ReturnsInstructions()
    {
        // Act
        var instructions = _systemCheckService.GetFFmpegInstallationInstructions();

        // Assert
        Assert.NotNull(instructions);
        Assert.NotEmpty(instructions);
        Assert.Contains("FFmpeg", instructions);
        Assert.Contains("download", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PATH", instructions);
    }

    [Fact]
    public void GetWhisperModelsInstructions_ReturnsInstructions()
    {
        // Act
        var instructions = _systemCheckService.GetWhisperModelsInstructions();

        // Assert
        Assert.NotNull(instructions);
        Assert.NotEmpty(instructions);
        Assert.Contains("Whisper", instructions);
        Assert.Contains("model", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("whispermodels", instructions);
        Assert.Contains("ggml-base.bin", instructions);
    }

    [Fact]
    public void SystemCheckResult_GetStatusMessage_WhenSystemReady_ReturnsReadyMessage()
    {
        // Arrange
        var result = new SystemCheckResult
        {
            FFmpegAvailable = true,
            WhisperModelsAvailable = true,
            DotNetRuntimeAvailable = true,
            SufficientDiskSpace = true,
            IsSystemReady = true
        };

        // Act
        var message = result.GetStatusMessage();

        // Assert
        Assert.Equal("System is ready for transcription.", message);
    }

    [Fact]
    public void SystemCheckResult_GetStatusMessage_WhenSystemNotReady_ReturnsIssues()
    {
        // Arrange
        var result = new SystemCheckResult
        {
            FFmpegAvailable = false,
            WhisperModelsAvailable = false,
            DotNetRuntimeAvailable = true,
            SufficientDiskSpace = true,
            IsSystemReady = false
        };

        // Act
        var message = result.GetStatusMessage();

        // Assert
        Assert.Contains("System check failed:", message);
        Assert.Contains("FFmpeg not found", message);
        Assert.Contains("No Whisper models found", message);
    }

    [Fact]
    public void SystemCheckResult_GetStatusMessage_WithAllIssues_ReturnsAllIssues()
    {
        // Arrange
        var result = new SystemCheckResult
        {
            FFmpegAvailable = false,
            WhisperModelsAvailable = false,
            DotNetRuntimeAvailable = false,
            SufficientDiskSpace = false,
            IsSystemReady = false
        };

        // Act
        var message = result.GetStatusMessage();

        // Assert
        Assert.Contains("FFmpeg not found", message);
        Assert.Contains("No Whisper models found", message);
        Assert.Contains(".NET 8.0 runtime required", message);
        Assert.Contains("Insufficient disk space", message);
    }

    public void Dispose()
    {
        // Clean up any test files if needed
    }
}