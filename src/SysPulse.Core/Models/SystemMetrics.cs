namespace SysPulse.Core.Models;

public record CpuMetrics(
    double UsagePercent,
    int CoreCount,
    IReadOnlyList<double> PerCoreUsage,
    string ModelName,
    double CurrentClockSpeedGHz
);

public record MemoryMetrics(
    long TotalBytes,
    long AvailableBytes,
    long UsedBytes,
    double UsagePercent,
    long SwapTotalBytes,
    long SwapUsedBytes,
    double SwapPercent
) {
    public double TotalGB => TotalBytes / (1024.0 * 1024 * 1024);
    public double UsedGB => UsedBytes / (1024.0 * 1024 * 1024);
    public double AvailableGB => AvailableBytes / (1024.0 * 1024 * 1024);
    public double SwapTotalGB => SwapTotalBytes / (1024.0 * 1024 * 1024);
    public double SwapUsedGB => SwapUsedBytes / (1024.0 * 1024 * 1024);
}

public record DriveMetrics(
    string Name,
    string MountPoint,
    long TotalBytes,
    long AvailableFreeBytes,
    long UsedBytes,
    double UsagePercent,
    string DriveFormat
) {
    public double TotalGB => TotalBytes / (1024.0 * 1024 * 1024);
    public double UsedGB => UsedBytes / (1024.0 * 1024 * 1024);
    public double FreeGB => AvailableFreeBytes / (1024.0 * 1024 * 1024);
}

public record NetworkMetrics(
    string InterfaceName,
    double DownloadSpeedKBps,
    double UploadSpeedKBps,
    long TotalBytesReceived,
    long TotalBytesSent
);

public record ProcessMetric(
    int Id,
    string Name,
    double CpuPercent,
    long WorkingSetBytes,
    string? User
) {
    public double MemoryMB => WorkingSetBytes / (1024.0 * 1024);
}

public record SystemOverview(
    string Hostname,
    string OsDescription,
    string Architecture,
    TimeSpan Uptime,
    int TotalProcesses,
    DateTime Timestamp
);
