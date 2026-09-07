using CommunityToolkit.Mvvm.ComponentModel;

namespace SysPulse.Desktop.ViewModels;

public partial class DriveItemViewModel : ViewModelBase {
    [ObservableProperty]
    private string _name = string.Empty;
    [ObservableProperty]
    private string _mountPoint = string.Empty;
    [ObservableProperty]
    private double _totalGB;
    [ObservableProperty]
    private double _usedGB;
    [ObservableProperty]
    private double _freeGB;
    [ObservableProperty]
    private double _usagePercent;
    [ObservableProperty]
    private string _format = string.Empty;

    public string SummaryText => $"{UsedGB:F1} GB / {TotalGB:F1} GB ({UsagePercent:F1}%)";
}

public partial class NetworkItemViewModel : ViewModelBase {
    [ObservableProperty]
    private string _interfaceName = string.Empty;
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

public partial class ProcessItemViewModel : ViewModelBase {
    [ObservableProperty]
    private int _id;
    [ObservableProperty]
    private string _name = string.Empty;
    [ObservableProperty]
    private double _memoryMB;
}
