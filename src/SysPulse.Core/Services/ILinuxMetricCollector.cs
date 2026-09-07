using SysPulse.Core.Models;

namespace SysPulse.Core.Services;

public interface ILinuxMetricCollector{
    Task<CpuMetrics> GetCpuMetricsAsync(CancellationToken ct=default);
    Task<MemoryMetrics> GetMemoryMetricsAsync(CancellationToken ct=default);
    Task<IReadOnlyList<DriveMetrics>> GetDriveMetricsAsync(CancellationToken ct=default);
    Task<IReadOnlyList<NetworkMetrics>> GetNetworkMetricsAsync(CancellationToken ct=default);
    Task<IReadOnlyList<ProcessMetric>> GetTopProcessesAsync(int count=10, CancellationToken ct=default);
    SystemOverview GetSystemOverview();
}
