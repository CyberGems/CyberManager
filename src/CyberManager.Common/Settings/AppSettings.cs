using System.Diagnostics;
using System.Text;
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
    public bool AlwaysOnTop { get; set; }
    public bool GroupProcesses { get; set; } = true;
    public double RowFontSize { get; set; } = 13.0;
    public bool ShowSuspended { get; set; } = true;
    public string SearchText { get; set; } = "";
    public HashSet<string> SuppressedConfirmations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool AutoCheckForUpdates { get; set; } = true;
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+M";
    public bool ShowIdleProcess { get; set; }
    public bool CompactMode { get; set; }
    public bool MainWindowBoundsSaved { get; set; }
    public string MainWindowMonitor { get; set; } = "";
    public double MainWindowLeft { get; set; }
    public double MainWindowTop { get; set; }
    public double MainWindowWidth { get; set; } = 1100;
    public double MainWindowHeight { get; set; } = 700;
    public bool MainWindowMaximized { get; set; }
    public bool CompactWindowBoundsSaved { get; set; }
    public string CompactWindowMonitor { get; set; } = "";
    public double CompactWindowLeft { get; set; }
    public double CompactWindowTop { get; set; }
    public double CompactWindowWidth { get; set; } = 560;
    public double CompactWindowHeight { get; set; } = 360;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CyberManager",
        "settings.json");
    private static string LegacySettingsPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "CyberManager",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var sourcePath = File.Exists(SettingsPath)
                ? SettingsPath
                : LegacySettingsPath;
            if (File.Exists(sourcePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(sourcePath),
                    JsonOptions) ?? new();
                settings.Normalize();
                return settings;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to load settings: {ex}");
        }

        var s = new AppSettings();
        var sys = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Lang.Es : Lang.En;
        s.Language = sys;
        return s;
    }

    public void Save()
    {
        TrySave();
    }

    public bool TrySave()
    {
        try
        {
            Normalize();
            var directory = System.IO.Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = CreateTemporaryPath(directory);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, JsonOptions), Encoding.UTF8);
            ReplaceAtomically(temporaryPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to save settings: {ex}");
            return false;
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        var temporaryPath = "";
        try
        {
            Normalize();
            var directory = System.IO.Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = CreateTemporaryPath(directory);
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(this, JsonOptions),
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
            ReplaceAtomically(temporaryPath);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to save settings asynchronously: {ex}");
            return false;
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch (IOException) { }
            }
        }
    }

    private void Normalize()
    {
        RefreshIntervalMs = Math.Clamp(RefreshIntervalMs, 500, 2000);
        RowFontSize = Math.Clamp(RowFontSize, 11, 17);
        MainWindowWidth = NormalizeDimension(MainWindowWidth, 900, 1100);
        MainWindowHeight = NormalizeDimension(MainWindowHeight, 520, 700);
        CompactWindowWidth = NormalizeDimension(CompactWindowWidth, 500, 560);
        CompactWindowHeight = NormalizeDimension(CompactWindowHeight, 220, 360);
        MainWindowLeft = NormalizeCoordinate(MainWindowLeft);
        MainWindowTop = NormalizeCoordinate(MainWindowTop);
        CompactWindowLeft = NormalizeCoordinate(CompactWindowLeft);
        CompactWindowTop = NormalizeCoordinate(CompactWindowTop);
        SearchText ??= "";
        SuppressedConfirmations ??= new(StringComparer.OrdinalIgnoreCase);
        GlobalHotkey = string.IsNullOrWhiteSpace(GlobalHotkey) ? "Ctrl+Alt+M" : GlobalHotkey.Trim();
    }

    public bool IsConfirmationSuppressed(string key) =>
        !string.IsNullOrWhiteSpace(key) && SuppressedConfirmations.Contains(key);

    public void SetConfirmationSuppressed(string key, bool suppressed)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (suppressed)
        {
            SuppressedConfirmations.Add(key);
        }
        else
        {
            SuppressedConfirmations.Remove(key);
        }
    }

    private static double NormalizeDimension(double value, double minimum, double fallback) =>
        double.IsFinite(value) ? Math.Max(value, minimum) : fallback;

    private static double NormalizeCoordinate(double value) =>
        double.IsFinite(value) ? value : 0;

    private static string CreateTemporaryPath(string directory) =>
        System.IO.Path.Combine(directory, $"settings.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

    private static void ReplaceAtomically(string temporaryPath)
    {
        if (File.Exists(SettingsPath))
        {
            File.Replace(temporaryPath, SettingsPath, null);
        }
        else
        {
            File.Move(temporaryPath, SettingsPath);
        }
    }
}
