using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysPulse.Core.Models;
using SysPulse.Core.Services;
using SysPulse.Data.Entities;
using SysPulse.Data.Repositories;

namespace SysPulse.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ILinuxMetricCollector _collector;
    private readonly ISnapshotRepository _snapshotRepo;
    private readonly PeriodicTimer _timer;
    private CancellationTokenSource? _cts;

    // System Overview
    [ObservableProperty]
    private string _hostname = "localhost";

    [ObservableProperty]
    private string _osDescription = "Linux";

    [ObservableProperty]
    private string _architecture = "x64";

    [ObservableProperty]
    private string _uptime = "0h 0m";

    // CPU Metrics
    [ObservableProperty]
    private double _cpuUsagePercent;

    [ObservableProperty]
    private string _cpuModel = "Loading CPU...";

    [ObservableProperty]
    private int _coreCount;

    [ObservableProperty]
    private double _cpuClockGhz;

    // Memory Metrics
    [ObservableProperty]
    private double _memoryUsagePercent;

    [ObservableProperty]
    private double _memoryUsedGb;

    [ObservableProperty]
    private double _memoryTotalGb;

    [ObservableProperty]
    private double _memoryAvailableGb;

    [ObservableProperty]
    private double _swapUsagePercent;

    [ObservableProperty]
    private double _swapUsedGb;

    [ObservableProperty]
    private double _swapTotalGb;

    // Collections
    public ObservableCollection<double> PerCoreUsage { get; } = [];
    public ObservableCollection<DriveItemViewModel> Drives { get; } = [];
    public ObservableCollection<NetworkItemViewModel> NetworkInterfaces { get; } = [];
    public ObservableCollection<ProcessItemViewModel> TopProcesses { get; } = [];

    // Persistence & Status
    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _totalSnapshotsRecorded;

    [ObservableProperty]
    private bool _isAutoLogging = true;

    private int _tickCount = 0;

    public MainViewModel(ILinuxMetricCollector collector, ISnapshotRepository snapshotRepo)
    {
        _collector = collector;
        _snapshotRepo = snapshotRepo;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        InitializeSystemInfo();
        StartMonitoringLoop();
    }

    // Default constructor for designer / fallback
    public MainViewModel() : this(new LinuxMetricCollector(), new SnapshotRepository(new Data.Context.SysPulseDbContext()))
    {
    }

    private void InitializeSystemInfo()
    {
        try
        {
            var overview = _collector.GetSystemOverview();
            Hostname = overview.Hostname;
            OsDescription = overview.OsDescription;
            Architecture = overview.Architecture;
            Uptime = FormatUptime(overview.Uptime);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Overview error: {ex.Message}";
        }
    }

    private void StartMonitoringLoop()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            try
            {
                // Ensure database tables exist
                await _snapshotRepo.EnsureDatabaseCreatedAsync(_cts.Token);
                var recent = await _snapshotRepo.GetRecentSnapshotsAsync(1, _cts.Token);
                Dispatcher.UIThread.Post(() => TotalSnapshotsRecorded = recent.Count);
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => StatusMessage = $"DB Init: {ex.Message}");
            }

            // Initial poll
            await PollMetricsAsync(_cts.Token);

            while (!_cts.Token.IsCancellationRequested && await _timer.WaitForNextTickAsync(_cts.Token))
            {
                await PollMetricsAsync(_cts.Token);
            }
        });
    }

    private async Task PollMetricsAsync(CancellationToken ct)
    {
        try
        {
            var cpuTask = _collector.GetCpuMetricsAsync(ct);
            var memTask = _collector.GetMemoryMetricsAsync(ct);
            var driveTask = _collector.GetDriveMetricsAsync(ct);
            var netTask = _collector.GetNetworkMetricsAsync(ct);
            var procTask = _collector.GetTopProcessesAsync(8, ct);

            await Task.WhenAll(cpuTask, memTask, driveTask, netTask, procTask);

            var cpu = await cpuTask;
            var mem = await memTask;
            var drives = await driveTask;
            var nets = await netTask;
            var procs = await procTask;
            var overview = _collector.GetSystemOverview();

            Dispatcher.UIThread.Post(() =>
            {
                // Update Overview
                Uptime = FormatUptime(overview.Uptime);

                // Update CPU
                CpuUsagePercent = cpu.UsagePercent;
                CpuModel = cpu.ModelName;
                CoreCount = cpu.CoreCount;
                CpuClockGhz = cpu.CurrentClockSpeedGHz;

                PerCoreUsage.Clear();
                foreach (var core in cpu.PerCoreUsage)
                {
                    PerCoreUsage.Add(core);
                }

                // Update Memory
                MemoryUsagePercent = mem.UsagePercent;
                MemoryUsedGb = mem.UsedGB;
                MemoryTotalGb = mem.TotalGB;
                MemoryAvailableGb = mem.AvailableGB;
                SwapUsagePercent = mem.SwapPercent;
                SwapUsedGb = mem.SwapUsedGB;
                SwapTotalGb = mem.SwapTotalGB;

                // Update Drives
                Drives.Clear();
                foreach (var d in drives)
                {
                    Drives.Add(new DriveItemViewModel
                    {
                        Name = d.Name,
                        MountPoint = d.MountPoint,
                        TotalGB = d.TotalGB,
                        UsedGB = d.UsedGB,
                        FreeGB = d.FreeGB,
                        UsagePercent = d.UsagePercent,
                        Format = d.DriveFormat
                    });
                }

                // Update Network
                NetworkInterfaces.Clear();
                foreach (var n in nets)
                {
                    NetworkInterfaces.Add(new NetworkItemViewModel
                    {
                        InterfaceName = n.InterfaceName,
                        DownloadSpeedKBps = n.DownloadSpeedKBps,
                        UploadSpeedKBps = n.UploadSpeedKBps,
                        TotalReceivedMB = n.TotalBytesReceived / (1024.0 * 1024.0),
                        TotalSentMB = n.TotalBytesSent / (1024.0 * 1024.0)
                    });
                }

                // Update Top Processes
                TopProcesses.Clear();
                foreach (var p in procs)
                {
                    TopProcesses.Add(new ProcessItemViewModel
                    {
                        Id = p.Id,
                        Name = p.Name,
                        MemoryMB = p.MemoryMB
                    });
                }

                StatusMessage = $"Updated at {DateTime.Now:T}";
            });

            // Periodic auto-snapshot (every 60 ticks = 1 minute)
            _tickCount++;
            if (IsAutoLogging && _tickCount % 60 == 0)
            {
                await SaveSnapshotAsync(cpu, mem, drives, nets, "Auto Snapshot", ct);
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => StatusMessage = $"Polling warning: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TakeSnapshotAsync()
    {
        try
        {
            var cpu = await _collector.GetCpuMetricsAsync();
            var mem = await _collector.GetMemoryMetricsAsync();
            var drives = await _collector.GetDriveMetricsAsync();
            var nets = await _collector.GetNetworkMetricsAsync();

            await SaveSnapshotAsync(cpu, mem, drives, nets, "Manual Snapshot");
            StatusMessage = $"Saved snapshot #{TotalSnapshotsRecorded} to DB at {DateTime.Now:T}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Snapshot error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshNowAsync()
    {
        await PollMetricsAsync(CancellationToken.None);
    }

    private async Task SaveSnapshotAsync(
        CpuMetrics cpu,
        MemoryMetrics mem,
        IReadOnlyList<DriveMetrics> drives,
        IReadOnlyList<NetworkMetrics> nets,
        string note,
        CancellationToken ct = default)
    {
        var primaryDrive = drives.FirstOrDefault(d => d.MountPoint == "/") ?? drives.FirstOrDefault();
        var primaryNet = nets.FirstOrDefault();

        var snapshot = new SystemSnapshot
        {
            TimestampUtc = DateTime.UtcNow,
            CpuUsagePercent = cpu.UsagePercent,
            MemoryUsagePercent = mem.UsagePercent,
            MemoryUsedGb = mem.UsedGB,
            MemoryTotalGb = mem.TotalGB,
            SwapUsagePercent = mem.SwapPercent,
            DiskUsagePercent = primaryDrive?.UsagePercent ?? 0,
            NetworkDownloadKbps = primaryNet?.DownloadSpeedKBps ?? 0,
            NetworkUploadKbps = primaryNet?.UploadSpeedKBps ?? 0,
            Note = note
        };

        await _snapshotRepo.SaveSnapshotAsync(snapshot, ct);
        Dispatcher.UIThread.Post(() => TotalSnapshotsRecorded++);
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }
}
