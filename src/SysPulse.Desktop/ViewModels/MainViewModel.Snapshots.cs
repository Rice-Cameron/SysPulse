using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysPulse.Data.Entities;

namespace SysPulse.Desktop.ViewModels;

public partial class MainViewModel{
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
            await RefreshTotalSnapshotsCountAsync();
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
                await RefreshTotalSnapshotsCountAsync();
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
            await RefreshTotalSnapshotsCountAsync();
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
}
