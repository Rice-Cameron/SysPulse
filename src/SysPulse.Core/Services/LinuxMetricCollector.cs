using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SysPulse.Core.Models;

namespace SysPulse.Core.Services;

public class LinuxMetricCollector : ILinuxMetricCollector {
    private long _previousCpuTotalLong;
    private long _previousCpuIdleLong;
    private readonly Dictionary<string, (long Total, long Idle)> _coreStatsDictionary = new Dictionary<string, (long Total, long Idle)>();
    private readonly Dictionary<string, (long Rx, long Tx, DateTime Time)> _networkStatsDictionary = new Dictionary<string, (long Rx, long Tx, DateTime Time)>();
    private string? _cpuModelString;
    private int _coreCountInt;

    public LinuxMetricCollector() {
        InitializeCpuInfo();
    }

    private void InitializeCpuInfo() {
        _coreCountInt = Environment.ProcessorCount;
        try {
            // File.Exists and File.ReadAllLines can throw IOException, SecurityException, or UnauthorizedAccessException
            if (File.Exists("/proc/cpuinfo")) {
                string[] lines = File.ReadAllLines("/proc/cpuinfo");
                for (int i = 0; i < lines.Length; i++) {
                    string line = lines[i];
                    if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase)) {
                        string[] parts = line.Split(':', 2);
                        if (parts.Length == 2) {
                            _cpuModelString = parts[1].Trim();
                            break;
                        }
                    }
                }
            }
        } catch {
            _cpuModelString = "Generic Linux CPU";
        }
        _cpuModelString ??= "Linux Processor";
    }

    public async Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken ct = default) {
        if (!File.Exists("/proc/stat")) {
            return new CpuMetrics(0, _coreCountInt, [], _cpuModelString ?? "CPU", 0);
        }

        // File.ReadAllLinesAsync may throw IOException or OperationCanceledException if reading /proc/stat fails
        string[] lines = await File.ReadAllLinesAsync("/proc/stat", ct);
        double overallUsage = 0;
        List<double> perCoreUsage = new List<double>();
        double currentClockGhz = 0;

        for (int i = 0; i < lines.Length; i++) {
            string line = lines[i];
            if (line.StartsWith("cpu ")) {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5) {
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

                    long deltaTotal = totalTime - _previousCpuTotalLong;
                    long deltaIdle = totalIdle - _previousCpuIdleLong;

                    _previousCpuTotalLong = totalTime;
                    _previousCpuIdleLong = totalIdle;

                    if (deltaTotal > 0) {
                        overallUsage = Math.Clamp(100.0 * (1.0 - ((double)deltaIdle / deltaTotal)), 0.0, 100.0);
                    }
                }
            } else if (line.StartsWith("cpu") && char.IsDigit(line[3])) {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string coreName = parts[0];
                if (parts.Length >= 5) {
                    long user = long.Parse(parts[1]);
                    long nice = long.Parse(parts[2]);
                    long system = long.Parse(parts[3]);
                    long idle = long.Parse(parts[4]);
                    long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;

                    long totalIdle = idle + iowait;
                    long totalTime = user + nice + system + idle + iowait;

                    double coreUsage = 0;
                    if (_coreStatsDictionary.TryGetValue(coreName, out (long Total, long Idle) prev)) {
                        long deltaTotal = totalTime - prev.Total;
                        long deltaIdle = totalIdle - prev.Idle;
                        if (deltaTotal > 0) {
                            coreUsage = Math.Clamp(100.0 * (1.0 - ((double)deltaIdle / deltaTotal)), 0.0, 100.0);
                        }
                    }

                    _coreStatsDictionary[coreName] = (totalTime, totalIdle);
                    perCoreUsage.Add(Math.Round(coreUsage, 1));
                }
            }
        }

        try {
            // File.ReadAllTextAsync can throw FileNotFoundException, IOException, or UnauthorizedAccessException when reading cpufreq sysfs nodes
            if (File.Exists("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq")) {
                string khz = await File.ReadAllTextAsync("/sys/devices/system/cpu/cpu0/cpufreq/scaling_cur_freq", ct);
                if (double.TryParse(khz.Trim(), out double freqKhz)) {
                    currentClockGhz = Math.Round(freqKhz / 1_000_000.0, 2);
                }
            }
        } catch {
            // Fallback when cpufreq scaling sysfs driver is unavailable in this kernel
        }

        return new CpuMetrics(
            Math.Round(overallUsage, 1),
            _coreCountInt,
            perCoreUsage,
            _cpuModelString ?? "CPU",
            currentClockGhz
        );
    }

    public async Task<MemoryMetrics> GetMemoryMetricsAsync(CancellationToken ct = default) {
        if (!File.Exists("/proc/meminfo")) {
            return new MemoryMetrics(0, 0, 0, 0, 0, 0, 0);
        }

        // File.ReadAllLinesAsync may throw IOException or OperationCanceledException if reading /proc/meminfo fails
        string[] lines = await File.ReadAllLinesAsync("/proc/meminfo", ct);
        long memTotalKb = 0;
        long memAvailableKb = 0;
        long swapTotalKb = 0;
        long swapFreeKb = 0;

        for (int i = 0; i < lines.Length; i++) {
            string line = lines[i];
            if (line.StartsWith("MemTotal:")) {
                memTotalKb = ParseKb(line);
            } else if (line.StartsWith("MemAvailable:")) {
                memAvailableKb = ParseKb(line);
            } else if (line.StartsWith("SwapTotal:")) {
                swapTotalKb = ParseKb(line);
            } else if (line.StartsWith("SwapFree:")) {
                swapFreeKb = ParseKb(line);
            }
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

    private static long ParseKb(string line) {
        string[] parts = line.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2) {
            string numPart = parts[1].Replace("kB", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (long.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out long val)) {
                return val;
            }
        }
        return 0;
    }

    public Task<IReadOnlyList<DriveMetrics>> GetDriveMetricsAsync(CancellationToken ct = default) {
        List<DriveMetrics> result = new List<DriveMetrics>();
        try {
            // DriveInfo.GetDrives and accessing DriveInfo properties can throw UnauthorizedAccessException or IOException on disconnected mounts
            DriveInfo[] drives = DriveInfo.GetDrives();
            HashSet<string> seenMounts = new HashSet<string>();

            for (int i = 0; i < drives.Length; i++) {
                DriveInfo drive = drives[i];
                if (!drive.IsReady || drive.TotalSize <= 0) {
                    continue;
                }

                string root = drive.RootDirectory.FullName;
                if (!seenMounts.Add(root)) {
                    continue;
                }

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
        } catch {
            // Fallback when storage subsystem is inaccessible
        }

        return Task.FromResult<IReadOnlyList<DriveMetrics>>(result);
    }

    public async Task<IReadOnlyList<NetworkMetrics>> GetNetworkMetricsAsync(CancellationToken ct = default) {
        List<NetworkMetrics> result = new List<NetworkMetrics>();
        if (!File.Exists("/proc/net/dev")) {
            return result;
        }

        // File.ReadAllLinesAsync may throw IOException or OperationCanceledException
        string[] lines = await File.ReadAllLinesAsync("/proc/net/dev", ct);
        DateTime now = DateTime.UtcNow;

        for (int i = 2; i < lines.Length; i++) {
            string line = lines[i];
            int colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) {
                continue;
            }

            string iface = line[..colonIdx].Trim();
            if (iface == "lo") {
                continue;
            }

            string[] tokens = line[(colonIdx + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 9) {
                continue;
            }

            if (long.TryParse(tokens[0], out long rxBytes) && long.TryParse(tokens[8], out long txBytes)) {
                double rxSpeed = 0;
                double txSpeed = 0;

                if (_networkStatsDictionary.TryGetValue(iface, out (long Rx, long Tx, DateTime Time) prev)) {
                    double deltaSec = (now - prev.Time).TotalSeconds;
                    if (deltaSec > 0.1) {
                        rxSpeed = Math.Max(0, (rxBytes - prev.Rx) / deltaSec / 1024.0);
                        txSpeed = Math.Max(0, (txBytes - prev.Tx) / deltaSec / 1024.0);
                    }
                }

                _networkStatsDictionary[iface] = (rxBytes, txBytes, now);

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

    public Task<IReadOnlyList<ProcessMetric>> GetTopProcessesAsync(int count = 10, CancellationToken ct = default) {
        List<ProcessMetric> list = new List<ProcessMetric>();
        try {
            // Process.GetProcesses() enumerates system processes; accessing Id or WorkingSet64 may throw InvalidOperationException if the process has already terminated
            Process[] processes = Process.GetProcesses();
            for (int i = 0; i < processes.Length; i++) {
                Process p = processes[i];
                try {
                    // p.WorkingSet64 or p.ProcessName may throw InvalidOperationException if the process exits mid-inspection
                    list.Add(new ProcessMetric(
                        p.Id,
                        p.ProcessName,
                        0,
                        p.WorkingSet64,
                        null
                    ));
                } catch {
                    // Process exited during query
                } finally {
                    p.Dispose();
                }
            }
        } catch {
            // Process enumeration security or permission exceptions
        }

        List<ProcessMetric> top = list.OrderByDescending(x => x.WorkingSetBytes)
                                      .Take(count)
                                      .ToList();

        return Task.FromResult<IReadOnlyList<ProcessMetric>>(top);
    }

    public SystemOverview GetSystemOverview() {
        string hostname = Environment.MachineName;
        string os = RuntimeInformation.OSDescription;
        string arch = RuntimeInformation.OSArchitecture.ToString();
        TimeSpan uptime = TimeSpan.Zero;

        try {
            // File.ReadAllText can throw FileNotFoundException or IOException if /proc/uptime is unavailable
            if (File.Exists("/proc/uptime")) {
                string text = File.ReadAllText("/proc/uptime").Split(' ')[0];
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double secs)) {
                    uptime = TimeSpan.FromSeconds(secs);
                }
            }
        } catch {
            // Fallback when /proc/uptime is unreadable
        }

        int totalProcesses = 0;
        try {
            // Process.GetProcesses() may throw PlatformNotSupportedException or SecurityException
            totalProcesses = Process.GetProcesses().Length;
        } catch {
            // Fallback
        }

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
