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

public partial class MainViewModel : ViewModelBase{
    private readonly ILinuxMetricCollector _linuxMetricCollector;
    private readonly ISnapshotRepository _snapshotRepository;
    private PeriodicTimer _periodicTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _lastAutoSnapshotTimeUtc=DateTime.UtcNow;

    // System Overview
    [ObservableProperty]
    private string _hostname="localhost";
    [ObservableProperty]
    private string _osDescription="Linux";
    [ObservableProperty]
    private string _architecture="x64";
    [ObservableProperty]
    private string _uptime="0h 0m";

    // CPU Metrics
    [ObservableProperty]
    private double _cpuUsagePercent;
    [ObservableProperty]
    private string _cpuModel="Loading CPU...";
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

    // Live Collections
    public ObservableCollection<double> PerCoreUsage{get;}=new ObservableCollection<double>();
    public ObservableCollection<DriveItemViewModel> Drives{get;}=new ObservableCollection<DriveItemViewModel>();
    public ObservableCollection<NetworkItemViewModel> NetworkInterfaces{get;}=new ObservableCollection<NetworkItemViewModel>();
    public ObservableCollection<ProcessItemViewModel> TopProcesses{get;}=new ObservableCollection<ProcessItemViewModel>();

    // Navigation & Global Header / Status
    [ObservableProperty]
    private int _selectedTabIndex=0;
    [ObservableProperty]
    private string _customSnapshotNote=string.Empty;
    [ObservableProperty]
    private string _statusMessage="Ready";
    [ObservableProperty]
    private int _totalSnapshotsRecorded;

    public MainViewModel(ILinuxMetricCollector linuxMetricCollector, ISnapshotRepository snapshotRepository){
        _linuxMetricCollector=linuxMetricCollector;
        _snapshotRepository=snapshotRepository;
        InitializeSettings();
        _periodicTimer=new PeriodicTimer(TimeSpan.FromSeconds(MetricsUpdateIntervalSeconds));
        InitializeSystemInfo();
        StartMonitoringLoop();
    }

    // Default constructor for designer / fallback
    public MainViewModel() : this(new LinuxMetricCollector(), new SnapshotRepository(new Data.Context.SysPulseDbContext())){
    }

    partial void OnSelectedTabIndexChanged(int value){
        if(value == 1){
            _=LoadSnapshotsAsync();
        }else if(value == 2){
            DiscardUnsavedSettings();
        }else{
            DiscardUnsavedSettings();
        }
    }

    private void InitializeSystemInfo(){
        try{
            // _linuxMetricCollector.GetSystemOverview() queries /proc/uptime and can throw IOException or PlatformNotSupportedException
            SystemOverview overview=_linuxMetricCollector.GetSystemOverview();
            Hostname=overview.Hostname;
            OsDescription=overview.OsDescription;
            Architecture=overview.Architecture;
            Uptime=FormatUptime(overview.Uptime);
        }
        catch(Exception ex){
            StatusMessage=$"Overview error: {ex.Message}";
        }
    }

    private void StartMonitoringLoop(){
        _cancellationTokenSource=new CancellationTokenSource();
        Task.Run(async () =>{
            try{
                // EnsureDatabaseCreatedAsync and GetRecentSnapshotsAsync execute database calls that can throw DbException or SocketException
                await _snapshotRepository.EnsureDatabaseCreatedAsync(_cancellationTokenSource.Token);
                IReadOnlyList<SystemSnapshot> recentSnapshots=await _snapshotRepository.GetRecentSnapshotsAsync(1, _cancellationTokenSource.Token);
                Dispatcher.UIThread.Post(() => TotalSnapshotsRecorded=recentSnapshots.Count);
            }
            catch(Exception ex){
                Dispatcher.UIThread.Post(() => StatusMessage=$"DB Init: {ex.Message}");
            }
            // Initial poll
            await PollMetricsAsync(_cancellationTokenSource.Token);
            // WaitForNextTickAsync can throw OperationCanceledException when token is cancelled
            while(!_cancellationTokenSource.Token.IsCancellationRequested && await _periodicTimer.WaitForNextTickAsync(_cancellationTokenSource.Token)){
                await PollMetricsAsync(_cancellationTokenSource.Token);
            }
        });
    }

    private async Task PollMetricsAsync(CancellationToken ct){
        try{
            // Metric collector tasks execute async /proc reads and can throw IOException or OperationCanceledException
            Task<CpuMetrics> cpuTask=_linuxMetricCollector.GetCpuMetricsAsync(ct);
            Task<MemoryMetrics> memTask=_linuxMetricCollector.GetMemoryMetricsAsync(ct);
            Task<IReadOnlyList<DriveMetrics>> driveTask=_linuxMetricCollector.GetDriveMetricsAsync(ct);
            Task<IReadOnlyList<NetworkMetrics>> netTask=_linuxMetricCollector.GetNetworkMetricsAsync(ct);
            Task<IReadOnlyList<ProcessMetric>> procTask=_linuxMetricCollector.GetTopProcessesAsync(8, ct);
            await Task.WhenAll(cpuTask, memTask, driveTask, netTask, procTask);
            CpuMetrics cpu=await cpuTask;
            MemoryMetrics mem=await memTask;
            IReadOnlyList<DriveMetrics> drives=await driveTask;
            IReadOnlyList<NetworkMetrics> nets=await netTask;
            IReadOnlyList<ProcessMetric> procs=await procTask;
            SystemOverview overview=_linuxMetricCollector.GetSystemOverview();
            Dispatcher.UIThread.Post(() =>{
                // Update Overview
                Uptime=FormatUptime(overview.Uptime);
                // Update CPU
                CpuUsagePercent=cpu.UsagePercent;
                CpuModel=cpu.ModelName;
                CoreCount=cpu.CoreCount;
                CpuClockGhz=cpu.CurrentClockSpeedGHz;
                PerCoreUsage.Clear();
                for(int i=0;i<cpu.PerCoreUsage.Count;i++){
                    PerCoreUsage.Add(cpu.PerCoreUsage[i]);
                }
                // Update Memory
                MemoryUsagePercent=mem.UsagePercent;
                MemoryUsedGb=mem.UsedGB;
                MemoryTotalGb=mem.TotalGB;
                MemoryAvailableGb=mem.AvailableGB;
                SwapUsagePercent=mem.SwapPercent;
                SwapUsedGb=mem.SwapUsedGB;
                SwapTotalGb=mem.SwapTotalGB;
                // Update Drives
                Drives.Clear();
                for(int i=0;i<drives.Count;i++){
                    DriveMetrics d=drives[i];
                    DriveItemViewModel driveItem=new DriveItemViewModel();
                    driveItem.Name=d.Name;
                    driveItem.MountPoint=d.MountPoint;
                    driveItem.TotalGB=d.TotalGB;
                    driveItem.UsedGB=d.UsedGB;
                    driveItem.FreeGB=d.FreeGB;
                    driveItem.UsagePercent=d.UsagePercent;
                    driveItem.Format=d.DriveFormat;
                    Drives.Add(driveItem);
                }
                // Update Network
                NetworkInterfaces.Clear();
                for(int i=0;i<nets.Count;i++){
                    NetworkMetrics n=nets[i];
                    NetworkItemViewModel netItem=new NetworkItemViewModel();
                    netItem.InterfaceName=n.InterfaceName;
                    netItem.DownloadSpeedKBps=n.DownloadSpeedKBps;
                    netItem.UploadSpeedKBps=n.UploadSpeedKBps;
                    netItem.TotalReceivedMB=n.TotalBytesReceived / (1024.0 * 1024.0);
                    netItem.TotalSentMB=n.TotalBytesSent / (1024.0 * 1024.0);
                    NetworkInterfaces.Add(netItem);
                }
                // Update Top Processes
                TopProcesses.Clear();
                for(int i=0;i<procs.Count;i++){
                    ProcessMetric p=procs[i];
                    ProcessItemViewModel procItem=new ProcessItemViewModel();
                    procItem.Id=p.Id;
                    procItem.Name=p.Name;
                    procItem.MemoryMB=p.MemoryMB;
                    TopProcesses.Add(procItem);
                }
                StatusMessage=$"Updated at {DateTime.Now:T}";
            });
            // Periodic auto-snapshot
            if(IsAutoSnapshotEnabled && (DateTime.UtcNow - _lastAutoSnapshotTimeUtc >= TimeSpan.FromMinutes(AutoSnapshotIntervalMinutes))){
                _lastAutoSnapshotTimeUtc=DateTime.UtcNow;
                // SaveSnapshotAsync executes database inserts and can throw DbUpdateException or DbException
                await SaveSnapshotAsync(cpu, mem, drives, nets, "Auto Snapshot", ct);
            }
        }
        catch(Exception ex){
            Dispatcher.UIThread.Post(() => StatusMessage=$"Polling warning: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TakeSnapshotAsync(){
        try{
            // Collecting metrics can throw IOException; SaveSnapshotAsync can throw DbException
            CpuMetrics cpu=await _linuxMetricCollector.GetCpuMetricsAsync();
            MemoryMetrics mem=await _linuxMetricCollector.GetMemoryMetricsAsync();
            IReadOnlyList<DriveMetrics> drives=await _linuxMetricCollector.GetDriveMetricsAsync();
            IReadOnlyList<NetworkMetrics> nets=await _linuxMetricCollector.GetNetworkMetricsAsync();
            string note=string.IsNullOrWhiteSpace(CustomSnapshotNote) ? "Manual Snapshot" : CustomSnapshotNote.Trim();
            await SaveSnapshotAsync(cpu, mem, drives, nets, note);
            CustomSnapshotNote=string.Empty;
            StatusMessage=$"Saved snapshot to {DatabaseProviderName} at {DateTime.Now:T}";
            if(SelectedTabIndex == 1){
                await LoadSnapshotsAsync();
            }
        }
        catch(Exception ex){
            StatusMessage=$"Snapshot error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshNowAsync(){
        if(SelectedTabIndex == 1){
            await LoadSnapshotsAsync();
        }else{
            await PollMetricsAsync(CancellationToken.None);
        }
    }

    private async Task SaveSnapshotAsync(
        CpuMetrics cpu,
        MemoryMetrics mem,
        IReadOnlyList<DriveMetrics> drives,
        IReadOnlyList<NetworkMetrics> nets,
        string note,
        CancellationToken ct=default){
        DriveMetrics? primaryDrive=drives.FirstOrDefault(d => d.MountPoint == "/") ?? drives.FirstOrDefault();
        NetworkMetrics? primaryNet=nets.FirstOrDefault();
        SystemSnapshot snapshot=new SystemSnapshot();
        snapshot.Hostname=Hostname;
        snapshot.TimestampUtc=DateTime.UtcNow;
        snapshot.CpuUsagePercent=cpu.UsagePercent;
        snapshot.MemoryUsagePercent=mem.UsagePercent;
        snapshot.MemoryUsedGb=mem.UsedGB;
        snapshot.MemoryTotalGb=mem.TotalGB;
        snapshot.SwapUsagePercent=mem.SwapPercent;
        snapshot.DiskUsagePercent=primaryDrive != null ? primaryDrive.UsagePercent : 0;
        snapshot.NetworkDownloadKbps=primaryNet != null ? primaryNet.DownloadSpeedKBps : 0;
        snapshot.NetworkUploadKbps=primaryNet != null ? primaryNet.UploadSpeedKBps : 0;
        snapshot.Note=note;
        // _snapshotRepository.SaveSnapshotAsync can throw DbUpdateException or DbException
        await _snapshotRepository.SaveSnapshotAsync(snapshot, ct);
        Dispatcher.UIThread.Post(() => TotalSnapshotsRecorded++);
    }

    private static string FormatUptime(TimeSpan uptime){
        if(uptime.TotalDays >= 1){
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }
}
