using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace SysPulse.Desktop.Services;

public class ThemeService : IDisposable{
    private static ThemeService? _instance;
    public static ThemeService Instance{
        get{
            if(_instance == null){
                _instance=new ThemeService();
            }
            return _instance;
        }
    }

    private FileSystemWatcher? _fileWatcher;
    private Timer? _debounceTimer;
    private string _currentThemeMode="Dark";
    public string CurrentThemeMode{
        get{
            return _currentThemeMode;
        }
    }

    public event Action<string>? ThemeChanged;

    public bool IsOmarchyAvailable{
        get{
            string colorsPath=GetOmarchyColorsPath();
            return File.Exists(colorsPath);
        }
    }

    public string OmarchyThemeDisplayName{
        get{
            string namePath=GetOmarchyThemeNamePath();
            if(File.Exists(namePath)){
                try{
                    string raw=File.ReadAllText(namePath).Trim();
                    return FormatThemeSlug(raw);
                }
                catch(Exception){
                    return "Omarchy";
                }
            }
            return "Omarchy";
        }
    }

    public void ApplyTheme(string themeMode){
        _currentThemeMode=themeMode;
        StopWatcher();
        if(themeMode == "Light"){
            ApplyLightPalette();
        }else if(themeMode == "Omarchy"){
            if(IsOmarchyAvailable){
                ApplyOmarchyPalette();
                StartWatcher();
            }else{
                ApplyDarkPalette();
            }
        }else{
            ApplyDarkPalette();
        }
        ThemeChanged?.Invoke(_currentThemeMode);
    }

    private void ApplyDarkPalette(){
        if(Application.Current == null){
            return;
        }
        Application.Current.RequestedThemeVariant=ThemeVariant.Dark;
        SetBrush("SysPulseWindowBackgroundBrush", "#13141f");
        SetBrush("SysPulseCardBackgroundBrush", "#1c1d2d");
        SetBrush("SysPulseCardSecondaryBackgroundBrush", "#24263a");
        SetBrush("SysPulseInputBackgroundBrush", "#2a2c42");
        SetBrush("SysPulseAccentBrush", "#3b82f6");
        SetBrush("SysPulseTextPrimaryBrush", "#f8fafc");
        SetBrush("SysPulseTextSecondaryBrush", "#cbd5e1");
        SetBrush("SysPulseTextMutedBrush", "#94a3b8");
        SetBrush("SysPulseTextSubduedBrush", "#64748b");
        SetBrush("SysPulseSuccessBrush", "#10b981");
        SetBrush("SysPulseCpuBrush", "#38bdf8");
        SetBrush("SysPulseMemoryBrush", "#34d399");
        SetBrush("SysPulseSwapBrush", "#fb923c");
        SetBrush("SysPulseNetworkBrush", "#c084fc");
        SetBrush("SysPulseDangerBrush", "#ef4444");
        SetBrush("ComboBoxBackground", "#2a2c42");
        SetBrush("ComboBoxBackgroundPointerOver", "#2a2c42");
        SetBrush("ComboBoxBackgroundPressed", "#2a2c42");
        SetBrush("ComboBoxBackgroundUnfocused", "#2a2c42");
        SetBrush("ComboBoxDropDownBackground", "#1c1d2d");
        SetBrush("TextControlBackground", "#2a2c42");
        SetBrush("TextControlBackgroundPointerOver", "#2a2c42");
        SetBrush("TextControlBackgroundFocused", "#2a2c42");
    }

    private void ApplyLightPalette(){
        if(Application.Current == null){
            return;
        }
        Application.Current.RequestedThemeVariant=ThemeVariant.Light;
        SetBrush("SysPulseWindowBackgroundBrush", "#f1f5f9");
        SetBrush("SysPulseCardBackgroundBrush", "#ffffff");
        SetBrush("SysPulseCardSecondaryBackgroundBrush", "#f8fafc");
        SetBrush("SysPulseInputBackgroundBrush", "#dee3eb");
        SetBrush("SysPulseAccentBrush", "#2563eb");
        SetBrush("SysPulseTextPrimaryBrush", "#0f172a");
        SetBrush("SysPulseTextSecondaryBrush", "#334155");
        SetBrush("SysPulseTextMutedBrush", "#64748b");
        SetBrush("SysPulseTextSubduedBrush", "#94a3b8");
        SetBrush("SysPulseSuccessBrush", "#059669");
        SetBrush("SysPulseCpuBrush", "#0284c7");
        SetBrush("SysPulseMemoryBrush", "#10b981");
        SetBrush("SysPulseSwapBrush", "#ea580c");
        SetBrush("SysPulseNetworkBrush", "#9333ea");
        SetBrush("SysPulseDangerBrush", "#dc2626");
        SetBrush("ComboBoxBackground", "#dee3eb");
        SetBrush("ComboBoxBackgroundPointerOver", "#dee3eb");
        SetBrush("ComboBoxBackgroundPressed", "#dee3eb");
        SetBrush("ComboBoxBackgroundUnfocused", "#dee3eb");
        SetBrush("ComboBoxDropDownBackground", "#ffffff");
        SetBrush("TextControlBackground", "#dee3eb");
        SetBrush("TextControlBackgroundPointerOver", "#dee3eb");
        SetBrush("TextControlBackgroundFocused", "#dee3eb");
    }

    private void ApplyOmarchyPalette(){
        if(Application.Current == null){
            return;
        }
        string colorsPath=GetOmarchyColorsPath();
        if(!File.Exists(colorsPath)){
            ApplyDarkPalette();
            return;
        }
        try{
            string content=File.ReadAllText(colorsPath);
            Dictionary<string, string> toml=ParseToml(content);
            string mode=toml.GetValueOrDefault("mode", "dark");
            if(mode.Equals("light", StringComparison.OrdinalIgnoreCase)){
                Application.Current.RequestedThemeVariant=ThemeVariant.Light;
            }else{
                Application.Current.RequestedThemeVariant=ThemeVariant.Dark;
            }
            string darkerBg=toml.GetValueOrDefault("darker_background", toml.GetValueOrDefault("background", "#111c18"));
            string bg=toml.GetValueOrDefault("background", toml.GetValueOrDefault("dark_background", "#1c1d2d"));
            string selection=toml.GetValueOrDefault("selection", toml.GetValueOrDefault("lighter_background", "#23372B"));
            string lighterBg=toml.GetValueOrDefault("lighter_background", toml.GetValueOrDefault("selection", "#2a2c42"));
            string accent=toml.GetValueOrDefault("accent", "#509475");
            string fg=toml.GetValueOrDefault("foreground", toml.GetValueOrDefault("bright_foreground", "#f8fafc"));
            string lightFg=toml.GetValueOrDefault("light_foreground", toml.GetValueOrDefault("foreground", "#cbd5e1"));
            string darkFg=toml.GetValueOrDefault("dark_foreground", toml.GetValueOrDefault("muted", "#94a3b8"));
            string muted=toml.GetValueOrDefault("muted", "#64748b");
            string green=toml.GetValueOrDefault("green", "#549e6a");
            string cyan=toml.GetValueOrDefault("cyan", toml.GetValueOrDefault("blue", "#2DD5B7"));
            string brightGreen=toml.GetValueOrDefault("bright_green", green);
            string orange=toml.GetValueOrDefault("orange", "#a2734b");
            string magenta=toml.GetValueOrDefault("magenta", "#D2689C");
            string red=toml.GetValueOrDefault("red", "#FF5345");
            SetBrush("SysPulseWindowBackgroundBrush", darkerBg);
            SetBrush("SysPulseCardBackgroundBrush", bg);
            SetBrush("SysPulseCardSecondaryBackgroundBrush", selection);
            SetBrush("SysPulseInputBackgroundBrush", lighterBg);
            SetBrush("SysPulseAccentBrush", accent);
            SetBrush("SysPulseTextPrimaryBrush", fg);
            SetBrush("SysPulseTextSecondaryBrush", lightFg);
            SetBrush("SysPulseTextMutedBrush", darkFg);
            SetBrush("SysPulseTextSubduedBrush", muted);
            SetBrush("SysPulseSuccessBrush", green);
            SetBrush("SysPulseCpuBrush", cyan);
            SetBrush("SysPulseMemoryBrush", brightGreen);
            SetBrush("SysPulseSwapBrush", orange);
            SetBrush("SysPulseNetworkBrush", magenta);
            SetBrush("SysPulseDangerBrush", red);
            SetBrush("ComboBoxBackground", lighterBg);
            SetBrush("ComboBoxBackgroundPointerOver", lighterBg);
            SetBrush("ComboBoxBackgroundPressed", lighterBg);
            SetBrush("ComboBoxBackgroundUnfocused", lighterBg);
            SetBrush("ComboBoxDropDownBackground", bg);
            SetBrush("TextControlBackground", lighterBg);
            SetBrush("TextControlBackgroundPointerOver", lighterBg);
            SetBrush("TextControlBackgroundFocused", lighterBg);
        }
        catch(Exception){
            ApplyDarkPalette();
        }
    }

    private static void SetBrush(string key, string hexColor){
        if(Application.Current == null){
            return;
        }
        if(Color.TryParse(hexColor, out Color col)){
            Application.Current.Resources[key]=new SolidColorBrush(col);
        }
    }

    private static Dictionary<string, string> ParseToml(string content){
        Dictionary<string, string> dict=new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines=content.Split('\n');
        for(int i=0;i<lines.Length;i++){
            string line=lines[i].Trim();
            if(string.IsNullOrEmpty(line) || line.StartsWith('#')){
                continue;
            }
            int eqIndex=line.IndexOf('=');
            if(eqIndex > 0){
                string k=line.Substring(0, eqIndex).Trim();
                string v=line.Substring(eqIndex + 1).Trim().Trim('"');
                dict[k]=v;
            }
        }
        return dict;
    }

    private static string GetOmarchyColorsPath(){
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "state", "omarchy", "current", "theme", "colors.toml");
    }

    private static string GetOmarchyThemeNamePath(){
        string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "state", "omarchy", "current", "theme.name");
    }

    private static string FormatThemeSlug(string slug){
        if(string.IsNullOrWhiteSpace(slug)){
            return "Omarchy";
        }
        string[] parts=slug.Split('-');
        for(int i=0;i<parts.Length;i++){
            if(parts[i].Length > 0){
                parts[i]=char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
        }
        return string.Join(" ", parts);
    }

    private void StartWatcher(){
        try{
            string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string watchDir=Path.Combine(home, ".local", "state", "omarchy", "current");
            if(!Directory.Exists(watchDir)){
                return;
            }
            _fileWatcher=new FileSystemWatcher(watchDir);
            _fileWatcher.NotifyFilter=NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _fileWatcher.Changed+=OnWatcherEvent;
            _fileWatcher.Created+=OnWatcherEvent;
            _fileWatcher.Renamed+=OnWatcherRenamed;
            _fileWatcher.EnableRaisingEvents=true;
        }
        catch(Exception){
            // Watcher failure is non-fatal
        }
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e){
        ScheduleReload();
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e){
        ScheduleReload();
    }

    private void ScheduleReload(){
        _debounceTimer?.Dispose();
        _debounceTimer=new Timer(_=>{
            Dispatcher.UIThread.Post(()=>{
                if(_currentThemeMode == "Omarchy"){
                    ApplyOmarchyPalette();
                    ThemeChanged?.Invoke(_currentThemeMode);
                }
            });
        }, null, 150, Timeout.Infinite);
    }

    private void StopWatcher(){
        if(_fileWatcher != null){
            _fileWatcher.EnableRaisingEvents=false;
            _fileWatcher.Dispose();
            _fileWatcher=null;
        }
        _debounceTimer?.Dispose();
        _debounceTimer=null;
    }

    public void Dispose(){
        StopWatcher();
    }
}
