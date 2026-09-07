using SysPulse.Core.Models;
using SysPulse.Core.Services;
using Xunit;

namespace SysPulse.Tests;

public class MetricCollectorTests{
    private readonly LinuxMetricCollector _linuxMetricCollector=new LinuxMetricCollector();

    [Fact]
    public async Task GetCpuMetricsAsync_ReturnsValidMetrics(){
        CpuMetrics metrics=await _linuxMetricCollector.GetCpuMetricsAsync();

        Assert.NotNull(metrics);
        Assert.True(metrics.CoreCount > 0, "Core count should be greater than 0");
        Assert.InRange(metrics.UsagePercent, 0.0, 100.0);
        Assert.False(string.IsNullOrWhiteSpace(metrics.ModelName), "CPU model should not be empty");
    }

    [Fact]
    public async Task GetMemoryMetricsAsync_ReturnsValidMemory(){
        MemoryMetrics mem=await _linuxMetricCollector.GetMemoryMetricsAsync();

        Assert.NotNull(mem);
        Assert.True(mem.TotalBytes > 0, "Total RAM should be > 0");
        Assert.True(mem.AvailableBytes > 0, "Available RAM should be > 0");
        Assert.InRange(mem.UsagePercent, 0.0, 100.0);
        Assert.True(mem.TotalGB > 0, "Total GB should be > 0");
    }

    [Fact]
    public async Task GetDriveMetricsAsync_ReturnsMountedDrives(){
        IReadOnlyList<DriveMetrics> drives=await _linuxMetricCollector.GetDriveMetricsAsync();

        Assert.NotNull(drives);
        Assert.NotEmpty(drives);

        DriveMetrics? rootDrive=drives.FirstOrDefault(d=>d.MountPoint == "/");
        Assert.NotNull(rootDrive);
        Assert.True(rootDrive.TotalBytes > 0, "Root drive total size should be > 0");
        Assert.InRange(rootDrive.UsagePercent, 0.0, 100.0);
    }

    [Fact]
    public void GetSystemOverview_ReturnsHostAndUptime(){
        SystemOverview overview=_linuxMetricCollector.GetSystemOverview();

        Assert.NotNull(overview);
        Assert.False(string.IsNullOrWhiteSpace(overview.Hostname), "Hostname should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(overview.OsDescription), "OS should not be empty");
        Assert.True(overview.Uptime > TimeSpan.Zero, "Uptime should be positive");
    }

    [Fact]
    public async Task GetTopProcessesAsync_ReturnsProcesses(){
        IReadOnlyList<ProcessMetric> procs=await _linuxMetricCollector.GetTopProcessesAsync(5);

        Assert.NotNull(procs);
        Assert.NotEmpty(procs);
        Assert.True(procs.Count <= 5);
        Assert.All(procs, p=>Assert.True(p.Id > 0));
    }
}
