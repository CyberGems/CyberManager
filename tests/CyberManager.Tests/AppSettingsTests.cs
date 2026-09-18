using CyberManager.Common.Settings;
using Xunit;

namespace CyberManager.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultShowIdleProcess_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ShowIdleProcess);
    }

    [Fact]
    public void AppSettings_DefaultGroupProcesses_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.GroupProcesses);
    }

    [Fact]
    public void AppSettings_DefaultAutoCheckForUpdates_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.AutoCheckForUpdates);
    }

    [Fact]
    public void AppSettings_DefaultStartWithWindows_IsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void AppSettings_DefaultTrayBehaviors_AreExplicit()
    {
        var settings = new AppSettings();
        Assert.False(settings.MinimizeToTrayOnMinimize);
        Assert.True(settings.MinimizeToTrayOnClose);
    }

    [Fact]
    public void AppSettings_DefaultCompactMode_IsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.CompactMode);
        Assert.Equal(560, settings.CompactWindowWidth);
        Assert.Equal(360, settings.CompactWindowHeight);
    }

    [Fact]
    public void AppSettings_ConfirmationSuppression_IsScopedPerAction()
    {
        var settings = new AppSettings();

        Assert.False(settings.IsConfirmationSuppressed("process.kill"));

        settings.SetConfirmationSuppressed("process.kill", suppressed: true);

        Assert.True(settings.IsConfirmationSuppressed("process.kill"));
        Assert.False(settings.IsConfirmationSuppressed("process.suspend"));

        settings.SetConfirmationSuppressed("process.kill", suppressed: false);

        Assert.False(settings.IsConfirmationSuppressed("process.kill"));
    }

    [Fact]
    public void AppSettings_SearchHistory_DeduplicatesAndKeepsTenMostRecent()
    {
        var settings = new AppSettings();

        for (var index = 0; index < 12; index++)
        {
            settings.RecordSearch($" process-{index} ");
        }
        settings.RecordSearch("PROCESS-5");

        Assert.Equal(10, settings.RecentSearches.Count);
        Assert.Equal("PROCESS-5", settings.RecentSearches[0]);
        Assert.DoesNotContain("process-0", settings.RecentSearches);
        Assert.Equal(10, settings.RecentSearches.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AppSettings_SearchHistory_IgnoresBlankQueries()
    {
        var settings = new AppSettings();

        settings.RecordSearch("  ");

        Assert.Empty(settings.RecentSearches);
    }

    [Fact]
    public void AppSettings_ResetToDefaults_RestoresAllUserPreferences()
    {
        var settings = new AppSettings
        {
            Language = CyberManager.Common.I18n.Lang.En,
            Theme = AppTheme.Light,
            AlwaysOnTop = true,
            GroupProcesses = false,
            HeavyProcessesOnly = true,
            SearchText = "chrome",
            MinimizeToTrayOnMinimize = true,
            MinimizeToTrayOnClose = false,
            StartWithWindows = false,
            StartMinimized = true,
            AutoCheckForUpdates = false,
            GlobalHotkey = "Ctrl+Shift+P",
            CompactMode = true,
            MainWindowBoundsSaved = true,
            CompactWindowBoundsSaved = true
        };
        settings.RecordSearch("chrome");
        settings.SetConfirmationSuppressed("process.kill", suppressed: true);

        settings.ResetToDefaults();

        Assert.Equal(CyberManager.Common.I18n.Lang.Es, settings.Language);
        Assert.Equal(AppTheme.CyberManager, settings.Theme);
        Assert.False(settings.AlwaysOnTop);
        Assert.True(settings.GroupProcesses);
        Assert.False(settings.HeavyProcessesOnly);
        Assert.Empty(settings.SearchText);
        Assert.Empty(settings.RecentSearches);
        Assert.Empty(settings.SuppressedConfirmations);
        Assert.False(settings.MinimizeToTrayOnMinimize);
        Assert.True(settings.MinimizeToTrayOnClose);
        Assert.True(settings.StartWithWindows);
        Assert.False(settings.StartMinimized);
        Assert.True(settings.AutoCheckForUpdates);
        Assert.Equal("Ctrl+Alt+M", settings.GlobalHotkey);
        Assert.False(settings.CompactMode);
        Assert.False(settings.MainWindowBoundsSaved);
        Assert.False(settings.CompactWindowBoundsSaved);
    }
}
