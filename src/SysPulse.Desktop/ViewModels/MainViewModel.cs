using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
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

    // Snapshot Archive Menu Collections & State
    public ObservableCollection<SnapshotItemViewModel> SnapshotsList{get;}=new ObservableCollection<SnapshotItemViewModel>();
    [ObservableProperty]
    private SnapshotItemViewModel? _selectedSnapshot;
    [ObservableProperty]
    private string _hostnameFilter=string.Empty;
    [ObservableProperty]
    private bool _isLoadingSnapshots;
    [ObservableProperty]
    private bool _isDeleteAllModalOpen=false;
    [ObservableProperty]
    private string _databaseProviderName="SQLite";
    [ObservableProperty]
    private int _selectedTabIndex=0;
    [ObservableProperty]
    private string _customSnapshotNote=string.Empty;

    // Settings Applied State
    [ObservableProperty]
    private bool _isAutoSnapshotEnabled=true;
    [ObservableProperty]
    private int _autoSnapshotIntervalMinutes=5;
    [ObservableProperty]
    private int _metricsUpdateIntervalSeconds=1;

    // Interval Presets Collections
    public ObservableCollection<string> IntervalPresetNames{get;}=new ObservableCollection<string>{
        "1 second",
        "2 seconds",
        "3 seconds",
        "5 seconds",
        "10 seconds",
        "30 seconds",
        "1 minute",
        "2 minutes",
        "5 minutes",
        "10 minutes",
        "15 minutes",
        "30 minutes",
        "1 hour"
    };
    public ObservableCollection<string> CustomUnitOptions{get;}=new ObservableCollection<string>{
        "Seconds",
        "Minutes"
    };

    // Settings Draft / Pending State (Only applied on Save)
    [ObservableProperty]
    private bool _pendingIsAutoSnapshotEnabled=true;
    [ObservableProperty]
    private int _pendingAutoSnapshotIntervalMinutes=5;
    [ObservableProperty]
    private int _pendingMetricsUpdateIntervalSeconds=1;
    [ObservableProperty]
    private string _pendingMetricsUpdateIntervalDisplay="1 second";
    [ObservableProperty]
    private string _selectedIntervalPresetName="1 second";
    [ObservableProperty]
    private string _customIntervalValueText="1";
    [ObservableProperty]
    private string _selectedCustomUnit="Seconds";
    [ObservableProperty]
    private bool _hasUnsavedSettingsChanges=false;
    [ObservableProperty]
    private string _settingsStatusMessage=string.Empty;

    // Persistence & Status
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

    partial void OnPendingIsAutoSnapshotEnabledChanged(bool value){
        UpdateHasUnsavedSettingsChanges();
    }

    partial void OnPendingAutoSnapshotIntervalMinutesChanged(int value){
        UpdateHasUnsavedSettingsChanges();
    }

    partial void OnSelectedIntervalPresetNameChanged(string value){
        if(string.IsNullOrEmpty(value)){
            return;
        }
        int sec=ConvertPresetNameToSeconds(value);
        if(sec != PendingMetricsUpdateIntervalSeconds){
            PendingMetricsUpdateIntervalSeconds=sec;
        }
    }

    partial void OnPendingMetricsUpdateIntervalSecondsChanged(int value){
        PendingMetricsUpdateIntervalDisplay=FormatUpdateInterval(value);
        string presetName=ConvertSecondsToPresetName(value);
        if(SelectedIntervalPresetName != presetName){
            SelectedIntervalPresetName=presetName;
        }
        if(value < 60){
            CustomIntervalValueText=value.ToString();
            SelectedCustomUnit="Seconds";
        }else{
            CustomIntervalValueText=(value / 60).ToString();
            SelectedCustomUnit="Minutes";
        }
        UpdateHasUnsavedSettingsChanges();
    }

    private void UpdateHasUnsavedSettingsChanges(){
        HasUnsavedSettingsChanges=PendingIsAutoSnapshotEnabled != IsAutoSnapshotEnabled || PendingAutoSnapshotIntervalMinutes != AutoSnapshotIntervalMinutes || PendingMetricsUpdateIntervalSeconds != MetricsUpdateIntervalSeconds;
    }

    private void DiscardUnsavedSettings(){
        PendingIsAutoSnapshotEnabled=IsAutoSnapshotEnabled;
        PendingAutoSnapshotIntervalMinutes=AutoSnapshotIntervalMinutes;
        PendingMetricsUpdateIntervalSeconds=MetricsUpdateIntervalSeconds;
        PendingMetricsUpdateIntervalDisplay=FormatUpdateInterval(MetricsUpdateIntervalSeconds);
        SelectedIntervalPresetName=ConvertSecondsToPresetName(MetricsUpdateIntervalSeconds);
        if(MetricsUpdateIntervalSeconds < 60){
            CustomIntervalValueText=MetricsUpdateIntervalSeconds.ToString();
            SelectedCustomUnit="Seconds";
        }else{
            CustomIntervalValueText=(MetricsUpdateIntervalSeconds / 60).ToString();
            SelectedCustomUnit="Minutes";
        }
        HasUnsavedSettingsChanges=false;
        SettingsStatusMessage=string.Empty;
    }

    private void InitializeSettings(){
        try{
            // Read local settings file if present; File.ReadAllText and JsonDocument.Parse can throw IOException or JsonException
            string localDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
            string settingsPath=Path.Combine(localDir, "settings.json");
            if(File.Exists(settingsPath)){
                string text=File.ReadAllText(settingsPath);
                using JsonDocument jsonDoc=JsonDocument.Parse(text);
                JsonElement root=jsonDoc.RootElement;
                if(root.TryGetProperty("EnableAutoSnapshot", out JsonElement autoProp)){
                    IsAutoSnapshotEnabled=autoProp.GetBoolean();
                }
                if(root.TryGetProperty("AutoSnapshotIntervalMinutes", out JsonElement intervalProp)){
                    AutoSnapshotIntervalMinutes=Math.Clamp(intervalProp.GetInt32(), 1, 120);
                }
                if(root.TryGetProperty("MetricsUpdateIntervalSeconds", out JsonElement updateProp)){
                    MetricsUpdateIntervalSeconds=Math.Clamp(updateProp.GetInt32(), 1, 3600);
                }
            }
        }
        catch{
            // Fallback to defaults if settings read or JSON parsing fails
        }
        PendingIsAutoSnapshotEnabled=IsAutoSnapshotEnabled;
        PendingAutoSnapshotIntervalMinutes=AutoSnapshotIntervalMinutes;
        PendingMetricsUpdateIntervalSeconds=MetricsUpdateIntervalSeconds;
        PendingMetricsUpdateIntervalDisplay=FormatUpdateInterval(MetricsUpdateIntervalSeconds);
        SelectedIntervalPresetName=ConvertSecondsToPresetName(MetricsUpdateIntervalSeconds);
        if(MetricsUpdateIntervalSeconds < 60){
            CustomIntervalValueText=MetricsUpdateIntervalSeconds.ToString();
            SelectedCustomUnit="Seconds";
        }else{
            CustomIntervalValueText=(MetricsUpdateIntervalSeconds / 60).ToString();
            SelectedCustomUnit="Minutes";
        }
        HasUnsavedSettingsChanges=false;
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
    public async Task LoadSnapshotsAsync(){
        IsLoadingSnapshots=true;
        try{
            // GetSnapshotsAsync executes query on configured database (SQLite or MySQL) and can throw DbException
            string? host=string.IsNullOrWhiteSpace(HostnameFilter) ? null : HostnameFilter.Trim();
            IReadOnlyList<SystemSnapshot> list=await _snapshotRepository.GetSnapshotsAsync(host, 100);
            SnapshotsList.Clear();
            for(int i=0;i<list.Count;i++){
                SystemSnapshot s=list[i];
                SnapshotItemViewModel item=new SnapshotItemViewModel();
                item.Id=s.Id;
                item.Hostname=s.Hostname;
                item.TimestampUtc=s.TimestampUtc;
                item.CpuUsagePercent=s.CpuUsagePercent;
                item.MemoryUsagePercent=s.MemoryUsagePercent;
                item.MemoryUsedGb=s.MemoryUsedGb;
                item.MemoryTotalGb=s.MemoryTotalGb;
                item.SwapUsagePercent=s.SwapUsagePercent;
                item.DiskUsagePercent=s.DiskUsagePercent;
                item.NetworkDownloadKbps=s.NetworkDownloadKbps;
                item.NetworkUploadKbps=s.NetworkUploadKbps;
                item.Note=s.Note ?? "Snapshot";
                SnapshotsList.Add(item);
            }
            if(SelectedSnapshot == null && SnapshotsList.Count > 0){
                SelectedSnapshot=SnapshotsList[0];
            }
            TotalSnapshotsRecorded=SnapshotsList.Count;
            StatusMessage=$"Loaded {SnapshotsList.Count} snapshots from {DatabaseProviderName} at {DateTime.Now:T}";
        }
        catch(Exception ex){
            StatusMessage=$"Load snapshots error: {ex.Message}";
        }
        finally{
            IsLoadingSnapshots=false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedSnapshotAsync(){
        if(SelectedSnapshot is null){
            return;
        }
        int targetId=SelectedSnapshot.Id;
        try{
            // DeleteSnapshotAsync executes DB delete and can throw DbException
            bool success=await _snapshotRepository.DeleteSnapshotAsync(targetId);
            if(success){
                SnapshotsList.Remove(SelectedSnapshot);
                SelectedSnapshot=SnapshotsList.FirstOrDefault();
                TotalSnapshotsRecorded=SnapshotsList.Count;
                StatusMessage=$"Deleted snapshot #{targetId} from {DatabaseProviderName}";
            }
        }
        catch(Exception ex){
            StatusMessage=$"Delete snapshot error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RequestDeleteAllSnapshots(){
        IsDeleteAllModalOpen=true;
    }

    [RelayCommand]
    private void CancelDeleteAllSnapshots(){
        IsDeleteAllModalOpen=false;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAllSnapshotsAsync(){
        try{
            // DeleteAllSnapshotsAsync executes DB delete and can throw DbException
            int deletedCount=await _snapshotRepository.DeleteAllSnapshotsAsync();
            SnapshotsList.Clear();
            SelectedSnapshot=null;
            TotalSnapshotsRecorded=0;
            IsDeleteAllModalOpen=false;
            StatusMessage=$"Deleted all {deletedCount} snapshots from {DatabaseProviderName}";
        }
        catch(Exception ex){
            IsDeleteAllModalOpen=false;
            StatusMessage=$"Delete all snapshots error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearFilterAsync(){
        HostnameFilter=string.Empty;
        await LoadSnapshotsAsync();
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

    [RelayCommand]
    private void ApplyCustomInterval(){
        if(int.TryParse(CustomIntervalValueText, out int val)){
            if(SelectedCustomUnit == "Minutes"){
                int clampedMinutes=Math.Clamp(val, 1, 60);
                PendingMetricsUpdateIntervalSeconds=clampedMinutes * 60;
                CustomIntervalValueText=clampedMinutes.ToString();
            }else{
                int clampedSeconds=Math.Clamp(val, 1, 59);
                PendingMetricsUpdateIntervalSeconds=clampedSeconds;
                CustomIntervalValueText=clampedSeconds.ToString();
            }
            UpdateHasUnsavedSettingsChanges();
            SettingsStatusMessage=$"Update frequency set to {PendingMetricsUpdateIntervalDisplay}. Click Save Settings to persist.";
        }
    }

    [RelayCommand]
    private void SetIntervalPreset(string minutesString){
        if(int.TryParse(minutesString, out int min)){
            PendingAutoSnapshotIntervalMinutes=Math.Clamp(min, 1, 120);
            UpdateHasUnsavedSettingsChanges();
            SettingsStatusMessage=$"Interval set to {PendingAutoSnapshotIntervalMinutes} minute(s). Click Save Settings to persist.";
        }
    }

    [RelayCommand]
    private void DiscardSettings(){
        DiscardUnsavedSettings();
        SettingsStatusMessage="Unsaved changes discarded.";
    }

    [RelayCommand]
    private void ResetSettings(){
        PendingIsAutoSnapshotEnabled=true;
        PendingAutoSnapshotIntervalMinutes=5;
        PendingMetricsUpdateIntervalSeconds=1;
        PendingMetricsUpdateIntervalDisplay=FormatUpdateInterval(1);
        SelectedIntervalPresetName=ConvertSecondsToPresetName(1);
        CustomIntervalValueText="1";
        SelectedCustomUnit="Seconds";
        UpdateHasUnsavedSettingsChanges();
        SettingsStatusMessage="Settings set to defaults (1s refresh, 5m snapshots, enabled). Click Save Settings to persist.";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync(){
        try{
            // File.WriteAllTextAsync writes configuration JSON to application data folder
            IsAutoSnapshotEnabled=PendingIsAutoSnapshotEnabled;
            AutoSnapshotIntervalMinutes=PendingAutoSnapshotIntervalMinutes;
            MetricsUpdateIntervalSeconds=PendingMetricsUpdateIntervalSeconds;
            _periodicTimer.Period=TimeSpan.FromSeconds(MetricsUpdateIntervalSeconds);
            HasUnsavedSettingsChanges=false;
            string localDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
            Directory.CreateDirectory(localDir);
            string settingsPath=Path.Combine(localDir, "settings.json");
            string jsonContent=$"{{\n  \"EnableAutoSnapshot\": {(IsAutoSnapshotEnabled ? "true" : "false")},\n  \"AutoSnapshotIntervalMinutes\": {AutoSnapshotIntervalMinutes},\n  \"MetricsUpdateIntervalSeconds\": {MetricsUpdateIntervalSeconds}\n}}";
            await File.WriteAllTextAsync(settingsPath, jsonContent);
            SettingsStatusMessage="Settings saved successfully.";
            StatusMessage=$"Settings saved at {DateTime.Now:T} (refresh: {FormatUpdateInterval(MetricsUpdateIntervalSeconds)})";
            await PollMetricsAsync(CancellationToken.None);
        }
        catch(Exception ex){
            SettingsStatusMessage=$"Error saving settings: {ex.Message}";
            StatusMessage=$"Settings error: {ex.Message}";
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

    private static int ConvertPresetNameToSeconds(string name){
        if(name == "1 second"){
            return 1;
        }
        if(name == "2 seconds"){
            return 2;
        }
        if(name == "3 seconds"){
            return 3;
        }
        if(name == "5 seconds"){
            return 5;
        }
        if(name == "10 seconds"){
            return 10;
        }
        if(name == "30 seconds"){
            return 30;
        }
        if(name == "1 minute"){
            return 60;
        }
        if(name == "2 minutes"){
            return 120;
        }
        if(name == "5 minutes"){
            return 300;
        }
        if(name == "10 minutes"){
            return 600;
        }
        if(name == "15 minutes"){
            return 900;
        }
        if(name == "30 minutes"){
            return 1800;
        }
        if(name == "1 hour"){
            return 3600;
        }
        return 1;
    }

    private static string ConvertSecondsToPresetName(int seconds){
        if(seconds == 1){
            return "1 second";
        }
        if(seconds == 2){
            return "2 seconds";
        }
        if(seconds == 3){
            return "3 seconds";
        }
        if(seconds == 5){
            return "5 seconds";
        }
        if(seconds == 10){
            return "10 seconds";
        }
        if(seconds == 30){
            return "30 seconds";
        }
        if(seconds == 60){
            return "1 minute";
        }
        if(seconds == 120){
            return "2 minutes";
        }
        if(seconds == 300){
            return "5 minutes";
        }
        if(seconds == 600){
            return "10 minutes";
        }
        if(seconds == 900){
            return "15 minutes";
        }
        if(seconds == 1800){
            return "30 minutes";
        }
        if(seconds == 3600){
            return "1 hour";
        }
        return FormatUpdateInterval(seconds);
    }

    private static string FormatUpdateInterval(int seconds){
        if(seconds <= 1){
            return "1 second";
        }
        if(seconds < 60){
            return $"{seconds} seconds";
        }
        if(seconds < 3600){
            int m=seconds / 60;
            return m == 1 ? "1 minute" : $"{m} minutes";
        }
        int h=seconds / 3600;
        return h == 1 ? "1 hour" : $"{h} hours";
    }
}
