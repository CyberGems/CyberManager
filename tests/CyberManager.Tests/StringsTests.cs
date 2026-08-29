using CyberManager.Common.I18n;
using Xunit;

namespace CyberManager.Tests;

public class StringsTests
{
    [Fact]
    public void T_ExistingKeySpanish_ReturnsSpanish()
    {
        Strings.Current = Lang.Es;
        var result = Strings.T("Kill");
        Assert.Equal("Finalizar tarea", result);
    }

    [Fact]
    public void T_ExistingKeyEnglish_ReturnsEnglish()
    {
        Strings.Current = Lang.En;
        var result = Strings.T("Kill");
        Assert.Equal("End Task", result);
    }

    [Fact]
    public void T_MissingKey_ReturnsKey()
    {
        var result = Strings.T("NonExistentKey");
        Assert.Equal("NonExistentKey", result);
    }

    [Fact]
    public void T_WithArgs_FormatsCorrectly()
    {
        Strings.Current = Lang.En;
        var result = Strings.T("ProcessesCount", 42);
        Assert.Equal("42 processes", result);
    }

    [Fact]
    public void T_WithArgs_Spanish_FormatsCorrectly()
    {
        Strings.Current = Lang.Es;
        var result = Strings.T("ProcessesCount", 42);
        Assert.Equal("42 procesos", result);
    }

    [Fact]
    public void T_SearchPlaceholder_English()
    {
        Strings.Current = Lang.En;
        Assert.Equal("Search process, PID or path...", Strings.T("SearchPlaceholder"));
    }

    [Fact]
    public void T_SearchPlaceholder_Spanish()
    {
        Strings.Current = Lang.Es;
        Assert.Equal("Buscar proceso, PID o ruta...", Strings.T("SearchPlaceholder"));
    }

    [Fact]
    public void T_AllKeys_HaveBothLanguages()
    {
        var keys = new[]
        {
            "AppTitle", "AppSubtitle", "SearchPlaceholder", "Process", "Pid", "Cpu", "Memory",
            "Threads", "Path", "Status", "Running", "Suspended", "Kill", "KillTree",
            "Suspend", "Resume", "CopyPath", "OpenFolder", "SearchOnline", "Priority",
            "NoProcesses", "Refresh", "AlwaysOnTop", "KillConfirm", "KillTreeConfirm",
            "Settings", "About", "ThemeCyberManager", "ThemeDark", "ThemeLight",
            "Ok", "Cancel", "Close", "Updated", "Ready", "CheckUpdatesAction", "CheckUpdates",
            "UpdatesAndMaintenance", "ElevationRequired", "ConfirmAction",
            "GroupByApp", "Ungroup", "KillGroupConfirm", "SuspendGroupConfirm", "ResumeGroupConfirm",
            "MainProcess", "WorkerProcess", "PriorityIdle", "PriorityBelowNormal", "TextSize"
        };
        foreach (var key in keys)
        {
            Strings.Current = Lang.En;
            var en = Strings.T(key);
            Strings.Current = Lang.Es;
            var es = Strings.T(key);
            Assert.False(string.IsNullOrEmpty(en), $"EN value for {key} is empty");
            Assert.False(string.IsNullOrEmpty(es), $"ES value for {key} is empty");
        }
    }
}
