using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysPulse.Desktop.Services;

namespace SysPulse.Desktop.ViewModels;

public partial class MainViewModel{
    // Settings Applied State
    [ObservableProperty]
    private bool _isAutoSnapshotEnabled=true;
    [ObservableProperty]
    private int _autoSnapshotIntervalMinutes=5;
    [ObservableProperty]
    private int _metricsUpdateIntervalSeconds=1;
    [ObservableProperty]
    private string _themeMode="Dark";

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
    private string _pendingThemeMode="Dark";
    [ObservableProperty]
    private bool _isOmarchyAvailable=false;
    [ObservableProperty]
    private string _omarchyThemeDisplayName="Omarchy";
    [ObservableProperty]
    private bool _hasUnsavedSettingsChanges=false;
    [ObservableProperty]
    private string _settingsStatusMessage=string.Empty;

    partial void OnPendingIsAutoSnapshotEnabledChanged(bool value){
        UpdateHasUnsavedSettingsChanges();
    }

    partial void OnPendingAutoSnapshotIntervalMinutesChanged(int value){
        UpdateHasUnsavedSettingsChanges();
    }

    partial void OnPendingThemeModeChanged(string value){
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
        HasUnsavedSettingsChanges=PendingIsAutoSnapshotEnabled != IsAutoSnapshotEnabled || PendingAutoSnapshotIntervalMinutes != AutoSnapshotIntervalMinutes || PendingMetricsUpdateIntervalSeconds != MetricsUpdateIntervalSeconds || PendingThemeMode != ThemeMode;
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
        PendingThemeMode=ThemeMode;
        HasUnsavedSettingsChanges=false;
        SettingsStatusMessage=string.Empty;
    }

    private void InitializeSettings(){
        IsOmarchyAvailable=ThemeService.Instance.IsOmarchyAvailable;
        OmarchyThemeDisplayName=ThemeService.Instance.OmarchyThemeDisplayName;
        ThemeService.Instance.ThemeChanged+=mode=>{
            Dispatcher.UIThread.Post(()=>{
                OmarchyThemeDisplayName=ThemeService.Instance.OmarchyThemeDisplayName;
            });
        };
        try{
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
                if(root.TryGetProperty("ThemeMode", out JsonElement themeProp)){
                    string? savedMode=themeProp.GetString();
                    if(!string.IsNullOrEmpty(savedMode) && (savedMode == "Dark" || savedMode == "Light" || savedMode == "Omarchy")){
                        ThemeMode=savedMode;
                    }
                }
            }
        }
        catch(Exception){
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
        PendingThemeMode=ThemeMode;
        ThemeService.Instance.ApplyTheme(ThemeMode);
        HasUnsavedSettingsChanges=false;
    }

    [RelayCommand]
    private void SelectThemeMode(string mode){
        if(mode == "Dark" || mode == "Light" || mode == "Omarchy"){
            PendingThemeMode=mode;
            UpdateHasUnsavedSettingsChanges();
            SettingsStatusMessage=$"Theme selected: {PendingThemeMode}. Click Save Settings to apply.";
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
        PendingThemeMode="Dark";
        UpdateHasUnsavedSettingsChanges();
        SettingsStatusMessage="Settings set to defaults (Dark theme, 1s refresh, 5m snapshots, enabled). Click Save Settings to persist.";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync(){
        try{
            IsAutoSnapshotEnabled=PendingIsAutoSnapshotEnabled;
            AutoSnapshotIntervalMinutes=PendingAutoSnapshotIntervalMinutes;
            MetricsUpdateIntervalSeconds=PendingMetricsUpdateIntervalSeconds;
            ThemeMode=PendingThemeMode;
            ThemeService.Instance.ApplyTheme(ThemeMode);
            _periodicTimer.Period=TimeSpan.FromSeconds(MetricsUpdateIntervalSeconds);
            HasUnsavedSettingsChanges=false;
            string localDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
            Directory.CreateDirectory(localDir);
            string settingsPath=Path.Combine(localDir, "settings.json");
            string jsonContent=$"{{\n  \"EnableAutoSnapshot\": {(IsAutoSnapshotEnabled ? "true" : "false")},\n  \"AutoSnapshotIntervalMinutes\": {AutoSnapshotIntervalMinutes},\n  \"MetricsUpdateIntervalSeconds\": {MetricsUpdateIntervalSeconds},\n  \"ThemeMode\": \"{ThemeMode}\"\n}}";
            await File.WriteAllTextAsync(settingsPath, jsonContent);
            SettingsStatusMessage="Settings saved successfully.";
            StatusMessage=$"Settings saved at {DateTime.Now:T} (refresh: {FormatUpdateInterval(MetricsUpdateIntervalSeconds)}, theme: {ThemeMode})";
            await PollMetricsAsync(CancellationToken.None);
        }
        catch(Exception ex){
            SettingsStatusMessage=$"Error saving settings: {ex.Message}";
            StatusMessage=$"Settings error: {ex.Message}";
        }
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
        if(seconds < 60){
            return $"{seconds} seconds";
        }
        return $"{seconds / 60} minutes";
    }

    private static string FormatUpdateInterval(int seconds){
        if(seconds == 1){
            return "1 second";
        }
        if(seconds < 60){
            return $"{seconds} seconds";
        }
        if(seconds == 60){
            return "1 minute";
        }
        if(seconds == 3600){
            return "1 hour";
        }
        int min=seconds / 60;
        int rem=seconds % 60;
        if(rem == 0){
            return $"{min} minutes";
        }
        return $"{min}m {rem}s";
    }
}
