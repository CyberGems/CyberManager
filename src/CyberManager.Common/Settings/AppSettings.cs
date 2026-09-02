using System.IO;
using System.Text.Json;
using CyberManager.Common.I18n;

namespace CyberManager.Common.Settings;

public enum AppTheme { CyberManager, Dark, Light }

public sealed class AppSettings
{
    public Lang Language { get; set; } = Lang.Es;
    public AppTheme Theme { get; set; } = AppTheme.CyberManager;
    public double RefreshIntervalMs { get; set; } = 800;
    public bool AlwaysOnTop { get; set; } = false;
    public bool GroupProcesses { get; set; } = true;
    public double RowFontSize { get; set; } = 13.0;
    public bool ShowSuspended { get; set; } = true;
    public string SearchText { get; set; } = "";
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool AutoCheckForUpdates { get; set; } = true;
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+M";
    public bool ShowIdleProcess { get; set; } = false;
    public bool MainWindowBoundsSaved { get; set; } = false;
    public string MainWindowMonitor { get; set; } = "";
    public double MainWindowLeft { get; set; }
    public double MainWindowTop { get; set; }
    public double MainWindowWidth { get; set; } = 1100;
    public double MainWindowHeight { get; set; } = 700;
    public bool MainWindowMaximized { get; set; }

    private static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberManager", "settings.json");

    public static AppSettings Load()
    {
        try { if (File.Exists(Path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new(); } catch { }
        var s = new AppSettings();
        var sys = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Lang.Es : Lang.En;
        s.Language = sys;
        return s;
    }

    public void Save()
    {
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!); File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }
}
