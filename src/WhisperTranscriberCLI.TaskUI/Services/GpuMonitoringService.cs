using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WhisperTranscriberCLI.TaskUI.Services;

public class GpuMonitoringService : IDisposable
{
    private readonly ILogger<GpuMonitoringService>? _logger;
    private Timer? _monitoringTimer;
    private bool _isMonitoring = false;
    private bool _disposed = false;

    public event EventHandler<GpuStatusEventArgs>? GpuStatusChanged;

    public GpuMonitoringService(ILogger<GpuMonitoringService>? logger = null)
    {
        _logger = logger;
    }

    public bool IsGpuAvailable { get; private set; }
    public GpuStatus? CurrentStatus { get; private set; }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _logger?.LogDebug("Checking GPU availability");
            IsGpuAvailable = await CheckGpuAvailabilityAsync();
            _logger?.LogInformation("GPU monitoring initialized. GPU available: {Available}", IsGpuAvailable);
            return IsGpuAvailable;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize GPU monitoring");
            IsGpuAvailable = false;
            return false;
        }
    }

    public void StartMonitoring(int intervalSeconds = 5)
    {
        if (_disposed || _isMonitoring || !IsGpuAvailable)
            return;

        _logger?.LogInformation("Starting GPU monitoring with {Interval}s interval", intervalSeconds);
        _isMonitoring = true;
        
        _monitoringTimer = new Timer(MonitorGpuCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring)
            return;

        _logger?.LogInformation("Stopping GPU monitoring");
        _isMonitoring = false;
        
        _monitoringTimer?.Dispose();
        _monitoringTimer = null;
    }

    private async void MonitorGpuCallback(object? state)
    {
        if (_disposed || !_isMonitoring)
            return;

        try
        {
            var status = await GetGpuStatusAsync();
            if (status != null)
            {
                CurrentStatus = status;
                GpuStatusChanged?.Invoke(this, new GpuStatusEventArgs(status));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get GPU status");
        }
    }

    private async Task<bool> CheckGpuAvailabilityAsync()
    {
        try
        {
            // Check for NVIDIA GPU
            var nvidiaResult = await RunNvidiaSmiCommandAsync("--query-gpu=name --format=csv,noheader,nounits");
            if (!string.IsNullOrEmpty(nvidiaResult))
            {
                _logger?.LogDebug("NVIDIA GPU detected: {GpuName}", nvidiaResult.Trim());
                return true;
            }

            // Could add AMD GPU detection here if needed
            // For now, we only support NVIDIA/CUDA monitoring

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<GpuStatus?> GetGpuStatusAsync()
    {
        try
        {
            var result = await RunNvidiaSmiCommandAsync(
                "--query-gpu=name,memory.used,memory.total,utilization.gpu,utilization.memory,temperature.gpu,power.draw,clocks.gr,clocks.mem,clocks_throttle_reasons.active --format=csv,noheader,nounits");

            if (string.IsNullOrEmpty(result))
                return null;

            var parts = result.Split(',');
            if (parts.Length < 10)
                return null;

            var throttleReasons = parts[9].Trim();
            _logger?.LogDebug("Raw throttle reasons from nvidia-smi: '{ThrottleReasons}'", throttleReasons);

            var status = new GpuStatus
            {
                Name = parts[0].Trim(),
                MemoryUsedMB = ParseInt(parts[1]),
                MemoryTotalMB = ParseInt(parts[2]),
                GpuUtilization = ParseInt(parts[3]),
                MemoryUtilization = ParseInt(parts[4]),
                Temperature = ParseInt(parts[5]),
                PowerDraw = ParseInt(parts[6]),
                GraphicsClock = ParseInt(parts[7]),
                MemoryClock = ParseInt(parts[8]),
                ThrottleReasons = throttleReasons,
                IsThrottling = IsActuallyThrottling(throttleReasons)
            };

            // Get system RAM status
            await AddSystemMemoryInfo(status);

            return status;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to parse GPU status");
            return null;
        }
    }

    private bool IsActuallyThrottling(string throttleReasons)
    {
        // According to NVIDIA documentation, these are the possible throttle reasons:
        // - "GpuIdleThrottling" = Normal power saving (NOT actual throttling)
        // - "ApplicationClocksSetting" = Controlled by application (NOT performance throttling)
        // - "SwPowerCap" = Software power cap
        // - "HwSlowdown" = Hardware thermal or power protection (ACTUAL throttling)
        // - "HwThermalSlowdown" = Hardware thermal protection (ACTUAL throttling) 
        // - "HwPowerBrakeSlowdown" = Hardware power brake (ACTUAL throttling)
        // - "SyncBoost" = Sync boost is active
        // - "SwThermalSlowdown" = Software thermal slowdown (ACTUAL throttling)
        // - "DisplayClocksSetting" = Display clocks setting

        if (string.IsNullOrEmpty(throttleReasons) || 
            throttleReasons.Equals("Not Active", StringComparison.OrdinalIgnoreCase) ||
            throttleReasons.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // These are the ACTUAL performance-limiting throttle reasons we should warn about
        var actualThrottleReasons = new[]
        {
            "HwSlowdown",
            "HwThermalSlowdown", 
            "HwPowerBrakeSlowdown",
            "SwThermalSlowdown",
            "SwPowerCap"
        };

        foreach (var reason in actualThrottleReasons)
        {
            if (throttleReasons.Contains(reason, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("GPU is actually throttling due to: {ThrottleReason}", reason);
                return true;
            }
        }

        // Log non-throttling reasons for debugging
        _logger?.LogDebug("GPU throttle reasons detected but not performance-limiting: {ThrottleReasons}", throttleReasons);
        return false;
    }

    private async Task AddSystemMemoryInfo(GpuStatus status)
    {
        try
        {
            // Get system memory info using WMI
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "OS get TotalVisibleMemorySize,FreePhysicalMemory /value",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var lines = output.Split('\n');
                long totalMemoryKB = 0;
                long freeMemoryKB = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("TotalVisibleMemorySize="))
                    {
                        long.TryParse(line.Substring("TotalVisibleMemorySize=".Length).Trim(), out totalMemoryKB);
                    }
                    else if (line.StartsWith("FreePhysicalMemory="))
                    {
                        long.TryParse(line.Substring("FreePhysicalMemory=".Length).Trim(), out freeMemoryKB);
                    }
                }

                if (totalMemoryKB > 0)
                {
                    status.SystemMemoryTotalMB = (int)(totalMemoryKB / 1024);
                    status.SystemMemoryUsedMB = (int)((totalMemoryKB - freeMemoryKB) / 1024);
                }
            }

            // Get CPU utilization
            await AddCpuUtilizationInfo(status);
        }
        catch
        {
            // Fallback method using PerformanceCounter if WMI fails
            try
            {
                var totalMemory = GC.GetTotalMemory(false);
                status.SystemMemoryUsedMB = (int)(totalMemory / (1024 * 1024));
                // Can't get total system memory easily this way, so leave it at 0
            }
            catch
            {
                // If all methods fail, leave memory info at 0
            }
        }
    }

    private async Task AddCpuUtilizationInfo(GpuStatus status)
    {
        try
        {
            // Get CPU utilization using wmic
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "cpu get loadpercentage /value",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("LoadPercentage="))
                    {
                        if (int.TryParse(line.Substring("LoadPercentage=".Length).Trim(), out var cpuLoad))
                        {
                            status.CpuUtilization = cpuLoad;
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // If CPU monitoring fails, leave at 0
        }
    }

    private async Task<string> RunNvidiaSmiCommandAsync(string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value?.Trim(), out var result) ? result : 0;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopMonitoring();
        _disposed = true;
    }
}

public class GpuStatus
{
    public string Name { get; set; } = string.Empty;
    public int MemoryUsedMB { get; set; }
    public int MemoryTotalMB { get; set; }
    public int GpuUtilization { get; set; }
    public int MemoryUtilization { get; set; }
    public int Temperature { get; set; }
    public int PowerDraw { get; set; }
    public int GraphicsClock { get; set; }
    public int MemoryClock { get; set; }
    public bool IsThrottling { get; set; }
    public string ThrottleReasons { get; set; } = string.Empty;
    
    // System RAM and CPU monitoring
    public int SystemMemoryUsedMB { get; set; }
    public int SystemMemoryTotalMB { get; set; }
    public int CpuUtilization { get; set; }

    public double MemoryUsagePercent => MemoryTotalMB > 0 ? (double)MemoryUsedMB / MemoryTotalMB * 100 : 0;
    public double SystemMemoryUsagePercent => SystemMemoryTotalMB > 0 ? (double)SystemMemoryUsedMB / SystemMemoryTotalMB * 100 : 0;

    public string GetMemoryString() => $"{MemoryUsedMB}MB/{MemoryTotalMB}MB ({MemoryUsagePercent:F0}%)";
    public string GetSystemMemoryString() => SystemMemoryTotalMB > 0 ? $"{SystemMemoryUsedMB}MB/{SystemMemoryTotalMB}MB ({SystemMemoryUsagePercent:F0}%)" : "N/A";

    public string GetCompactStatus()
    {
        // GPU Mode with inline indicator spaces: GPU | Load: 35% ___ | Temp: 43°C ___ | VRAM 4.7 / 8 GB ___
        var gpuMemUsedGB = Math.Round(MemoryUsedMB / 1024.0, 1);
        var gpuMemTotalGB = Math.Round(MemoryTotalMB / 1024.0, 1);
        
        return $"GPU | Load: {GpuUtilization}%   | Temp: {Temperature}°C   | VRAM {gpuMemUsedGB} / {gpuMemTotalGB} GB  ";
    }

    public string GetCompactStatusForCpuMode()
    {
        // CPU Mode with inline indicator spaces: CPU | Load: 47% ___ | Temp: 49°C ___ | RAM 6.7 / 16 GB ___
        if (SystemMemoryTotalMB > 0)
        {
            var systemMemUsedGB = Math.Round(SystemMemoryUsedMB / 1024.0, 1);
            var systemMemTotalGB = Math.Round(SystemMemoryTotalMB / 1024.0, 1);
            
            return $"CPU | Load: {CpuUtilization}%   | Temp: {Temperature}°C   | RAM {systemMemUsedGB} / {systemMemTotalGB} GB  ";
        }
        else
        {
            // Fallback if no RAM info available
            return $"CPU | Load: {CpuUtilization}%   | Temp: {Temperature}°C   | RAM N/A  ";
        }
    }

    // New method to get status with inline indicators
    public string GetCompactStatusWithIndicators(bool useSystemMemory = false)
    {
        var loadIndicator = ""; // CPU/GPU load is not currently monitored for warnings
        var tempIndicator = GetTemperatureIndicator();
        var memoryIndicator = GetMemoryIndicator(useSystemMemory);
        
        if (useSystemMemory)
        {
            // CPU Mode: CPU | Load: 47%   | Temp: 49°C ! | RAM 6.7 / 16 GB !   
            if (SystemMemoryTotalMB > 0)
            {
                var systemMemUsedGB = Math.Round(SystemMemoryUsedMB / 1024.0, 1);
                var systemMemTotalGB = Math.Round(SystemMemoryTotalMB / 1024.0, 1);
                
                return $"CPU | Load: {CpuUtilization}%{(string.IsNullOrEmpty(loadIndicator) ? "  " : " " + loadIndicator)} | Temp: {Temperature}°C{(string.IsNullOrEmpty(tempIndicator) ? "  " : " " + tempIndicator)} | RAM {systemMemUsedGB} / {systemMemTotalGB} GB{(string.IsNullOrEmpty(memoryIndicator) ? "   " : " " + memoryIndicator + " ")}";
            }
            else
            {
                return $"CPU | Load: {CpuUtilization}%{(string.IsNullOrEmpty(loadIndicator) ? "  " : " " + loadIndicator)} | Temp: {Temperature}°C{(string.IsNullOrEmpty(tempIndicator) ? "  " : " " + tempIndicator)} | RAM N/A{(string.IsNullOrEmpty(memoryIndicator) ? "   " : " " + memoryIndicator + " ")}";
            }
        }
        else
        {
            // GPU Mode: GPU | Load: 35%   | Temp: 43°C ! | VRAM 4.7 / 8 GB !   
            var gpuMemUsedGB = Math.Round(MemoryUsedMB / 1024.0, 1);
            var gpuMemTotalGB = Math.Round(MemoryTotalMB / 1024.0, 1);
            
            return $"GPU | Load: {GpuUtilization}%{(string.IsNullOrEmpty(loadIndicator) ? "  " : " " + loadIndicator)} | Temp: {Temperature}°C{(string.IsNullOrEmpty(tempIndicator) ? "  " : " " + tempIndicator)} | VRAM {gpuMemUsedGB} / {gpuMemTotalGB} GB{(string.IsNullOrEmpty(memoryIndicator) ? "   " : " " + memoryIndicator + " ")}";
        }
    }

    // Check if GPU has sufficient VRAM for model
    public bool HasSufficientVramForModel(string modelName)
    {
        if (MemoryTotalMB <= 0) return false; // No real GPU VRAM detected
        
        var requiredVramMB = GetModelVramRequirement(modelName);
        var safetyFactor = 1.2; // Require 20% more VRAM than model size for safety
        
        return MemoryTotalMB >= (requiredVramMB * safetyFactor);
    }
    
    // Check if this is a real GPU with dedicated VRAM
    public bool IsRealGpu()
    {
        // Real GPUs typically have at least 1GB of VRAM
        // Integrated/fake GPUs usually show 0 or very small amounts
        return MemoryTotalMB >= 1024; // At least 1GB VRAM
    }
    
    private int GetModelVramRequirement(string modelName)
    {
        // Approximate VRAM requirements for different Whisper models (in MB)
        return modelName.ToLower() switch
        {
            var name when name.Contains("large-v3") => 3072,    // ~3GB
            var name when name.Contains("large-v2") => 3072,    // ~3GB
            var name when name.Contains("large") => 3072,       // ~3GB
            var name when name.Contains("medium") => 1536,      // ~1.5GB
            var name when name.Contains("small") => 768,        // ~768MB
            var name when name.Contains("base") => 512,         // ~512MB
            var name when name.Contains("tiny") => 256,         // ~256MB
            _ => 1024 // Default to 1GB for unknown models
        };
    }

    public string GetDetailedStatus()
    {
        // Non-compact mode shows both GPU and system info
        var gpuInfo = $"GPU: {GetMemoryString()} | {GpuUtilization}% load | {Temperature}°C";
        var systemInfo = SystemMemoryTotalMB > 0 ? $" | System RAM: {GetSystemMemoryString()}" : "";
        var cpuInfo = $" | CPU: {CpuUtilization}% load";
        var throttleText = IsThrottling ? GetThrottleDescription() : "";
        
        return gpuInfo + systemInfo + cpuInfo + throttleText;
    }

    public bool HasPerformanceIssues()
    {
        // Check for potential issues (not necessarily active problems)
        return Temperature > 75 || MemoryUsagePercent > 80 || SystemMemoryUsagePercent > 85;
    }

    public bool HasActivePerformanceIssues()
    {
        // Only show warnings for serious active issues
        return IsThrottling || Temperature > 85 || MemoryUsagePercent > 95 || SystemMemoryUsagePercent > 95;
    }

    public bool IsGpuAccelerationRecommended()
    {
        // GPU acceleration is recommended if no serious issues
        return Temperature < 80 && MemoryUsagePercent < 85 && SystemMemoryUsagePercent < 90 && !IsThrottling;
    }

    public string GetRecommendedModel()
    {
        // Model recommendations based on available memory (prioritize system RAM since it's shared)
        var availableGpuMemoryMB = MemoryTotalMB - MemoryUsedMB;
        var availableSystemMemoryMB = SystemMemoryTotalMB > 0 ? SystemMemoryTotalMB - SystemMemoryUsedMB : int.MaxValue;
        
        // Use the more limiting factor
        var effectiveAvailableMemory = Math.Min(availableGpuMemoryMB, availableSystemMemoryMB / 2); // System RAM is shared
        
        if (HasActivePerformanceIssues())
        {
            return "ggml-tiny.bin"; // Safest option when having active issues
        }
        
        if (effectiveAvailableMemory >= 6000) // 6GB+ available
        {
            return "ggml-large-v3.bin"; // Best quality
        }
        else if (effectiveAvailableMemory >= 4000) // 4GB+ available
        {
            return "ggml-medium.bin"; // Good balance
        }
        else if (effectiveAvailableMemory >= 2000) // 2GB+ available
        {
            return "ggml-base.bin"; // Balanced option
        }
        else if (effectiveAvailableMemory >= 1000) // 1GB+ available
        {
            return "ggml-small.bin"; // Smaller but functional
        }
        else
        {
            return "ggml-tiny.bin"; // Minimal memory usage
        }
    }

    // Status indicators with exclamation marks instead of emojis
    public string GetTemperatureIndicator()
    {
        if (Temperature >= 85) return "!"; // Red exclamation - Critical
        if (Temperature >= 75) return "!"; // Yellow exclamation - Warning  
        return ""; // No indicator - Good
    }

    public string GetMemoryIndicator(bool useSystemMemory = false)
    {
        var memoryPercent = useSystemMemory ? SystemMemoryUsagePercent : MemoryUsagePercent;
        
        if (memoryPercent >= 95) return "!"; // Red exclamation - Critical
        if (memoryPercent >= 85) return "!"; // Yellow exclamation - Warning
        return ""; // No indicator - Good
    }

    public string GetThrottlingIndicator()
    {
        if (IsThrottling) return "!"; // Red exclamation - Throttling active
        return ""; // No indicator - No throttling
    }

    public PerformanceLevel GetTemperatureLevel()
    {
        if (Temperature >= 85) return PerformanceLevel.Critical;
        if (Temperature >= 75) return PerformanceLevel.Warning;
        return PerformanceLevel.Good;
    }

    public PerformanceLevel GetMemoryLevel(bool useSystemMemory = false)
    {
        var memoryPercent = useSystemMemory ? SystemMemoryUsagePercent : MemoryUsagePercent;
        
        if (memoryPercent >= 95) return PerformanceLevel.Critical;
        if (memoryPercent >= 85) return PerformanceLevel.Warning;
        return PerformanceLevel.Good;
    }

    public PerformanceLevel GetThrottlingLevel()
    {
        if (IsThrottling) return PerformanceLevel.Critical;
        return PerformanceLevel.Good;
    }

    public bool ShouldShowCriticalWarning()
    {
        // Only show advice/warnings for critical issues
        return IsThrottling || Temperature >= 85 || MemoryUsagePercent >= 95 || SystemMemoryUsagePercent >= 95;
    }

    public string GetCriticalAdvice()
    {
        // Only show advice for critical issues
        if (IsThrottling)
        {
            return GetThrottleAdvice();
        }
        
        if (Temperature >= 85)
        {
            return "GPU is running very hot - improve ventilation immediately";
        }
        
        if (MemoryUsagePercent >= 95)
        {
            return "GPU memory is nearly full - use a smaller model";
        }
        
        if (SystemMemoryUsagePercent >= 95)
        {
            return "System memory is nearly full - close other applications";
        }
        
        return "";
    }

    private string GetThrottleDescription()
    {
        if (ThrottleReasons.Contains("HwThermalSlowdown", StringComparison.OrdinalIgnoreCase) ||
            ThrottleReasons.Contains("SwThermalSlowdown", StringComparison.OrdinalIgnoreCase))
        {
            return " (Performance limited due to high temperature)";
        }
        
        if (ThrottleReasons.Contains("HwPowerBrakeSlowdown", StringComparison.OrdinalIgnoreCase) ||
            ThrottleReasons.Contains("SwPowerCap", StringComparison.OrdinalIgnoreCase))
        {
            return " (Performance limited due to power constraints)";
        }
        
        if (ThrottleReasons.Contains("HwSlowdown", StringComparison.OrdinalIgnoreCase))
        {
            return " (Performance limited by hardware protection)";
        }
        
        return " (Performance is being limited)";
    }

    private string GetThrottleAdvice()
    {
        if (ThrottleReasons.Contains("HwThermalSlowdown", StringComparison.OrdinalIgnoreCase) ||
            ThrottleReasons.Contains("SwThermalSlowdown", StringComparison.OrdinalIgnoreCase))
        {
            return "Performance is limited due to high temperature - improve cooling immediately";
        }
        
        if (ThrottleReasons.Contains("HwPowerBrakeSlowdown", StringComparison.OrdinalIgnoreCase) ||
            ThrottleReasons.Contains("SwPowerCap", StringComparison.OrdinalIgnoreCase))
        {
            return "Performance is limited due to power constraints - check power supply";
        }
        
        if (ThrottleReasons.Contains("HwSlowdown", StringComparison.OrdinalIgnoreCase))
        {
            return "Performance is limited by hardware protection - check GPU health";
        }
        
        return "Performance is being limited - check GPU status";
    }
}

public class GpuStatusEventArgs : EventArgs
{
    public GpuStatus Status { get; }

    public GpuStatusEventArgs(GpuStatus status)
    {
        Status = status;
    }
}

public enum PerformanceLevel
{
    Good,
    Warning,
    Critical
}