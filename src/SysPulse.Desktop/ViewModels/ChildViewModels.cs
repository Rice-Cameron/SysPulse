using CommunityToolkit.Mvvm.ComponentModel;

namespace SysPulse.Desktop.ViewModels;

public partial class DriveItemViewModel : ViewModelBase{
    [ObservableProperty]
    private string _name=string.Empty;
    [ObservableProperty]
    private string _mountPoint=string.Empty;
    [ObservableProperty]
    private double _totalGB;
    [ObservableProperty]
    private double _usedGB;
    [ObservableProperty]
    private double _freeGB;
    [ObservableProperty]
    private double _usagePercent;
    [ObservableProperty]
    private string _format=string.Empty;

    public string SummaryText => $"{UsedGB:F1} GB / {TotalGB:F1} GB ({UsagePercent:F1}%)";
}

public partial class NetworkItemViewModel : ViewModelBase{
    [ObservableProperty]
    private string _interfaceName=string.Empty;
    [ObservableProperty]
    private double _downloadSpeedKBps;
    [ObservableProperty]
    private double _uploadSpeedKBps;
    [ObservableProperty]
    private double _totalReceivedMB;
    [ObservableProperty]
    private double _totalSentMB;

    public string DownloadText => DownloadSpeedKBps > 1024 
        ? $"{DownloadSpeedKBps / 1024.0:F2} MB/s" 
        : $"{DownloadSpeedKBps:F1} KB/s";

    public string UploadText => UploadSpeedKBps > 1024 
        ? $"{UploadSpeedKBps / 1024.0:F2} MB/s" 
        : $"{UploadSpeedKBps:F1} KB/s";
}

public partial class ProcessItemViewModel : ViewModelBase{
    [ObservableProperty]
    private int _id;
    [ObservableProperty]
    private string _name=string.Empty;
    [ObservableProperty]
    private double _memoryMB;
}

public partial class SnapshotItemViewModel : ViewModelBase{
    [ObservableProperty]
    private int _id;
    [ObservableProperty]
    private string _hostname=string.Empty;
    [ObservableProperty]
    private DateTime _timestampUtc;
    [ObservableProperty]
    private double _cpuUsagePercent;
    [ObservableProperty]
    private double _memoryUsagePercent;
    [ObservableProperty]
    private double _memoryUsedGb;
    [ObservableProperty]
    private double _memoryTotalGb;
    [ObservableProperty]
    private double _swapUsagePercent;
    [ObservableProperty]
    private double _diskUsagePercent;
    [ObservableProperty]
    private double _networkDownloadKbps;
    [ObservableProperty]
    private double _networkUploadKbps;
    [ObservableProperty]
    private string _note=string.Empty;

    public string TimestampLocalText => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string SummaryText => $"CPU: {CpuUsagePercent:F1}% | RAM: {MemoryUsagePercent:F1}% | Disk: {DiskUsagePercent:F1}%";
    public string NetworkSpeedText => $"Down: {NetworkDownloadKbps:F1} KB/s  Up: {NetworkUploadKbps:F1} KB/s";
}
