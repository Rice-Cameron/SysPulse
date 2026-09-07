using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SysPulse.Core.Models;

namespace SysPulse.Core.Services;

public class LinuxMetricCollector : ILinuxMetricCollector
{
    private long _prevCpuTotal;
    private long _prevCpuIdle;
    private readonly Dictionary<string, (long Total, long Idle)> _prevCoreStats = new();
    private readonly Dictionary<string, (long Rx, long Tx, DateTime Time)> _prevNetStats = new();

    private string? _cpuModelCached;
    private int _coreCountCached;

    public LinuxMetricCollector()
    {
        InitializeCpuInfo();
    }

    private void InitializeCpuInfo()
    {
        _coreCountCached = Environment.ProcessorCount;
        try
        {
            if (File.Exists("/proc/cpuinfo"))
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            _cpuModelCached = parts[1].Trim();
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            _cpuModelCached = "Generic Linux CPU";
        }

        _cpuModelCached ??= "Linux Processor";
    }

    public async Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken ct = default)
    {
        if (!File.Exists("/proc/stat"))
        {
            return new CpuMetrics(0, _coreCountCached, [], _cpuModelCached ?? "CPU", 0);
        }

        var lines = await File.ReadAllLinesAsync("/proc/stat", ct);
        double overallUsage = 0;
        var perCoreUsage = new List<double>();
        double currentClockGhz = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("cpu "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    long user = long.Parse(parts[1]);
                    long nice = long.Parse(parts[2]);
                    long system = long.Parse(parts[3]);
                    long idle = long.Parse(parts[4]);
                    long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
                    long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
                    long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;
                    long steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

                    long totalIdle = idle + iowait;
                    long totalTime = user + nice + system + idle + iowait + irq + softirq + steal;

                    long deltaTotal = totalTime - _prevCpuTotal;
                    long deltaIdle = totalIdle - _prevCpuIdle;

                    _prevCpuTotal = totalTime;
                    _prevCpuIdle = totalIdle;

                    if (deltaTotal > 0)
                    {
                        overallUsage = Math.Clamp(100.0 * (1.0 - ((double)deltaIdle / deltaTotal)), 0.0, 100.0);
                    }
                }
            }
            else if (line.StartsWith("cpu") && char.IsDigit(line[3]))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string coreName = parts[0];
                if (parts.Length >= 5)
                {
                    long user = long.Parse(parts[1]);
                    long nice = long.Parse(parts[2]);
                    long system = long.Parse(parts[3]);
                    long idle = long.Parse(parts[4]);
                    long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;

                    long totalIdle = idle + iowait;
                    long totalTime = user + nice + system + idle + iowait;

                    double coreUsage = 0;
                    if (_prevCoreStats.TryGetValue(coreName, out var prev))
                    {
                        long deltaTotal = totalTime - prev.Total;
                        long deltaIdle = totalIdle - prev.Idle;
                        if (deltaTotal > 0)
                        {
                            coreUsage = Math.Clamp(100.0 * (1.0 - ((double)deltaIdle / deltaTotal)), 0.0, 100.0);
                        }
                    }

                    _prevCoreStats[coreName] = (totalTime, totalIdle);
                    perCoreUsage.Add(Math.Round(coreUsage, 1));
                }
            }
        }

        // Try reading clock frequency from /proc/cpuinfo or sysfs
        try
        {
            if (File.Exists("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq"))
            {
                string khz = await File.ReadAllTextAsync("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq", ct);
                if (double.TryParse(khz.Trim(), out double freqKhz))
                {
                    currentClockGhz = Math.Round(freqKhz / 1_000_000.0, 2);
                }
            }
        }
        catch
        {
            // fallback
        }

        return new CpuMetrics(
            Math.Round(overallUsage, 1),
            _coreCountCached,
            perCoreUsage,
            _cpuModelCached ?? "CPU",
            currentClockGhz
        );
    }

    public async Task<MemoryMetrics> GetMemoryMetricsAsync(CancellationToken ct = default)
    {
        if (!File.Exists("/proc/meminfo"))
        {
            return new MemoryMetrics(0, 0, 0, 0, 0, 0, 0);
        }

        var lines = await File.ReadAllLinesAsync("/proc/meminfo", ct);
        long memTotalKb = 0;
        long memAvailableKb = 0;
        long swapTotalKb = 0;
        long swapFreeKb = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("MemTotal:"))
                memTotalKb = ParseKb(line);
            else if (line.StartsWith("MemAvailable:"))
                memAvailableKb = ParseKb(line);
            else if (line.StartsWith("SwapTotal:"))
                swapTotalKb = ParseKb(line);
            else if (line.StartsWith("SwapFree:"))
                swapFreeKb = ParseKb(line);
        }

        long totalBytes = memTotalKb * 1024;
        long availableBytes = memAvailableKb * 1024;
        long usedBytes = Math.Max(0, totalBytes - availableBytes);
        double usagePercent = totalBytes > 0 ? Math.Round((double)usedBytes / totalBytes * 100.0, 1) : 0;

        long swapTotalBytes = swapTotalKb * 1024;
        long swapUsedBytes = Math.Max(0, (swapTotalKb - swapFreeKb) * 1024);
        double swapPercent = swapTotalBytes > 0 ? Math.Round((double)swapUsedBytes / swapTotalBytes * 100.0, 1) : 0;

        return new MemoryMetrics(
            totalBytes,
            availableBytes,
            usedBytes,
            usagePercent,
            swapTotalBytes,
            swapUsedBytes,
            swapPercent
        );
    }

    private static long ParseKb(string line)
    {
        var parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            var numPart = parts[1].Replace("kB", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (long.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out long val))
            {
                return val;
            }
        }
        return 0;
    }

    public Task<IReadOnlyList<DriveMetrics>> GetDriveMetricsAsync(CancellationToken ct = default)
    {
        var result = new List<DriveMetrics>();
        try
        {
            var drives = DriveInfo.GetDrives();
            var seenMounts = new HashSet<string>();

            foreach (var drive in drives)
            {
                if (!drive.IsReady || drive.TotalSize <= 0)
                    continue;

                // Skip duplicate mounts or virtual pseudo filesystems
                string root = drive.RootDirectory.FullName;
                if (!seenMounts.Add(root))
                    continue;

                // Common useful linux root and home mounts
                long total = drive.TotalSize;
                long free = drive.AvailableFreeSpace;
                long used = Math.Max(0, total - free);
                double percent = total > 0 ? Math.Round((double)used / total * 100.0, 1) : 0;

                result.Add(new DriveMetrics(
                    drive.Name,
                    root,
                    total,
                    free,
                    used,
                    percent,
                    drive.DriveFormat
                ));
            }
        }
        catch
        {
            // Fallback
        }

        return Task.FromResult<IReadOnlyList<DriveMetrics>>(result);
    }

    public async Task<IReadOnlyList<NetworkMetrics>> GetNetworkMetricsAsync(CancellationToken ct = default)
    {
        var result = new List<NetworkMetrics>();
        if (!File.Exists("/proc/net/dev"))
        {
            return result;
        }

        var lines = await File.ReadAllLinesAsync("/proc/net/dev", ct);
        var now = DateTime.UtcNow;

        foreach (var line in lines.Skip(2))
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) continue;

            string iface = line[..colonIdx].Trim();
            if (iface == "lo") continue; // Skip loopback

            var tokens = line[(colonIdx + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 9) continue;

            if (long.TryParse(tokens[0], out long rxBytes) && long.TryParse(tokens[8], out long txBytes))
            {
                double rxSpeed = 0;
                double txSpeed = 0;

                if (_prevNetStats.TryGetValue(iface, out var prev))
                {
                    double deltaSec = (now - prev.Time).TotalSeconds;
                    if (deltaSec > 0.1)
                    {
                        rxSpeed = Math.Max(0, (rxBytes - prev.Rx) / deltaSec / 1024.0);
                        txSpeed = Math.Max(0, (txBytes - prev.Tx) / deltaSec / 1024.0);
                    }
                }

                _prevNetStats[iface] = (rxBytes, txBytes, now);

                result.Add(new NetworkMetrics(
                    iface,
                    Math.Round(rxSpeed, 1),
                    Math.Round(txSpeed, 1),
                    rxBytes,
                    txBytes
                ));
            }
        }

        return result;
    }

    public Task<IReadOnlyList<ProcessMetric>> GetTopProcessesAsync(int count = 10, CancellationToken ct = default)
    {
        var list = new List<ProcessMetric>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    list.Add(new ProcessMetric(
                        p.Id,
                        p.ProcessName,
                        0, // Detailed per-process CPU requires multi-sample tracking
                        p.WorkingSet64,
                        null
                    ));
                }
                catch
                {
                    // Process exited during query
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch
        {
            // Ignore security/access exceptions
        }

        var top = list.OrderByDescending(x => x.WorkingSetBytes)
                      .Take(count)
                      .ToList();

        return Task.FromResult<IReadOnlyList<ProcessMetric>>(top);
    }

    public SystemOverview GetSystemOverview()
    {
        string hostname = Environment.MachineName;
        string os = RuntimeInformation.OSDescription;
        string arch = RuntimeInformation.OSArchitecture.ToString();
        TimeSpan uptime = TimeSpan.Zero;

        try
        {
            if (File.Exists("/proc/uptime"))
            {
                var text = File.ReadAllText("/proc/uptime").Split(' ')[0];
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double secs))
                {
                    uptime = TimeSpan.FromSeconds(secs);
                }
            }
        }
        catch
        {
            // fallback
        }

        int totalProcesses = 0;
        try
        {
            totalProcesses = Process.GetProcesses().Length;
        }
        catch { }

        return new SystemOverview(
            hostname,
            os,
            arch,
            uptime,
            totalProcesses,
            DateTime.UtcNow
        );
    }
}
