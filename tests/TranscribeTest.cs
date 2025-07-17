using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using WhisperTranscriberCLI.Services;

namespace whisper_transcriber_cli.Tests;

public class TranscriptionIntegrationTests : IDisposable
{
    private readonly string _testOutputDirectory;
    private readonly WhisperNetTranscriptionService _transcriptionService;
    private readonly FfmpegMediaConverter _mediaConverter;

    public TranscriptionIntegrationTests()
    {
        // Setup test environment
        _testOutputDirectory = Path.Combine(Path.GetTempPath(), $"TranscriptionTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testOutputDirectory);

        // Initialize services
        _mediaConverter = new FfmpegMediaConverter();
        _transcriptionService = new WhisperNetTranscriptionService(_mediaConverter);
    }

    [Fact]
    public async Task TranscribeAsync_WithWavFile_ShouldCreateSrtFile()
    {
        // Arrange
        var testWavFile = GetTestFilePath("TranscriptionTest.wav");
        Assert.True(File.Exists(testWavFile), $"Test WAV file not found: {testWavFile}");
        
        // Copy test file to a temporary location to avoid modifying the original
        var tempWavFile = Path.Combine(_testOutputDirectory, "test_input.wav");
        File.Copy(testWavFile, tempWavFile);
        
        // Act
        var result = await _transcriptionService.TranscribeAsync(tempWavFile, CancellationToken.None);
        
        // Assert
        if (string.IsNullOrEmpty(result))
        {
            // Skip test if Whisper model is not available
            Assert.Fail("Whisper model not available. Download a model (e.g., ggml-base.bin) to run transcription tests.");
            return;
        }
        
        Assert.True(File.Exists(result), $"Output SRT file was not created: {result}");
        Assert.Equal(".srt", Path.GetExtension(result));
            
        // Check that the SRT file has content
        var srtContent = await File.ReadAllTextAsync(result);
        Assert.NotEmpty(srtContent);
        
        // Specific SRT format and content validation
        Assert.Contains("1", srtContent); // Should contain segment number 1
        Assert.Contains("00:00:00,000 --> 00:00:03,000", srtContent); // Should contain the expected timestamp
        Assert.Contains("Test 1, 3.", srtContent); // Should contain the expected transcribed text
        
        // Validate SRT structure
        var lines = srtContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3, "SRT should have at least 3 lines (number, timestamp, text)");
        Assert.Equal("1", lines[0].Trim()); // First line should be segment number
        Assert.Equal("00:00:00,000 --> 00:00:03,000", lines[1].Trim()); // Second line should be timestamp
        Assert.Equal("Test 1, 3.", lines[2].Trim()); // Third line should be the transcribed text
    }

    [Fact]
    public async Task TranscribeAsync_WithMp3File_ShouldConvertAndCreateSrtFile()
    {
        // Arrange
        var testMp3File = GetTestFilePath("TranscriptionTest.mp3");
        Assert.True(File.Exists(testMp3File), $"Test MP3 file not found: {testMp3File}");
        
        // Copy test file to a temporary location
        var tempMp3File = Path.Combine(_testOutputDirectory, "test_input.mp3");
        File.Copy(testMp3File, tempMp3File);
        
        // Act
        var result = await _transcriptionService.TranscribeAsync(tempMp3File, CancellationToken.None);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        if (!string.IsNullOrEmpty(result))
        {
            Assert.True(File.Exists(result), $"Output SRT file was not created: {result}");
            Assert.Equal(".srt", Path.GetExtension(result));
            
            // Check that the SRT file has content and matches expected format
            var srtContent = await File.ReadAllTextAsync(result);
            Assert.NotEmpty(srtContent);
            
            // Specific SRT format and content validation (should match expected output)
            Assert.Contains("1", srtContent); // Should contain segment number 1
            Assert.Contains("00:00:00,000 --> 00:00:03,000", srtContent); // Should contain the expected timestamp
            Assert.Contains("Test 1, 3.", srtContent); // Should contain the expected transcribed text
            
            // Validate SRT structure
            var lines = srtContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(lines.Length >= 3, "SRT should have at least 3 lines (number, timestamp, text)");
            Assert.Equal("1", lines[0].Trim()); // First line should be segment number
            Assert.Equal("00:00:00,000 --> 00:00:03,000", lines[1].Trim()); // Second line should be timestamp
            Assert.Equal("Test 1, 3.", lines[2].Trim()); // Third line should be the transcribed text
            
            // Note: WAV file should be cleaned up automatically now (temp files)
        }
    }

    [Fact]
    public async Task TranscribeAsync_WithNonExistentFile_ShouldHandleGracefully()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testOutputDirectory, "nonexistent.wav");
        
        // Act & Assert
        // This should either throw an exception or return empty string
        // depending on how the service handles missing files
        try
        {
            var result = await _transcriptionService.TranscribeAsync(nonExistentFile, CancellationToken.None);
            // If no exception is thrown, result should be empty or indicate failure
            if (result != null)
            {
                Assert.True(string.IsNullOrEmpty(result) || !File.Exists(result));
            }
        }
        catch (Exception ex)
        {
            // Expected behavior - should throw an exception for missing files
            Assert.True(ex is FileNotFoundException || ex is ArgumentException || ex is InvalidOperationException);
        }
    }

    [Fact]
    public async Task FfmpegMediaConverter_ConvertWavToWav_ShouldCreateTempFile()
    {
        // Arrange
        var testWavFile = GetTestFilePath("TranscriptionTest.wav");
        Assert.True(File.Exists(testWavFile), $"Test WAV file not found: {testWavFile}");
        
        // Act
        var result = await _mediaConverter.ConvertToWavAsync(testWavFile, CancellationToken.None);
        
        // Assert
        Assert.NotEqual(testWavFile, result); // Should return a temp file path, not the original
        Assert.True(File.Exists(result), "Temp WAV file should exist");
        Assert.Contains("temp", result.ToLower()); // Should be in temp directory
        
        // Cleanup
        if (File.Exists(result))
        {
            File.Delete(result);
        }
    }

    [Fact]
    public async Task FfmpegMediaConverter_ConvertMp3ToWav_ShouldCreateWavFile()
    {
        // Arrange
        var testMp3File = GetTestFilePath("TranscriptionTest.mp3");
        Assert.True(File.Exists(testMp3File), $"Test MP3 file not found: {testMp3File}");
        
        // Copy to temp location
        var tempMp3File = Path.Combine(_testOutputDirectory, "test_convert.mp3");
        File.Copy(testMp3File, tempMp3File);
        
        // Act
        var result = await _mediaConverter.ConvertToWavAsync(tempMp3File, CancellationToken.None);
        
        // Assert
        Assert.NotEqual(tempMp3File, result); // Should be different from input
        Assert.Equal(".wav", Path.GetExtension(result));
        Assert.True(File.Exists(result), $"Converted WAV file should exist: {result}");
        
        // Check file size > 0
        var fileInfo = new FileInfo(result);
        Assert.True(fileInfo.Length > 0, "Converted WAV file should not be empty");
    }

    private string GetTestFilePath(string fileName)
    {
        // Try different possible locations for the test file
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "samples", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "samples", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "tests", "samples", fileName),
            Path.Combine(Path.GetDirectoryName(typeof(TranscriptionIntegrationTests).Assembly.Location) ?? "", "samples", fileName)
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException($"Test file '{fileName}' not found. Searched in: {string.Join(", ", possiblePaths)}");
    }

    public void Dispose()
    {
        // Clean up test files
        try
        {
            if (Directory.Exists(_testOutputDirectory))
            {
                Directory.Delete(_testOutputDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
