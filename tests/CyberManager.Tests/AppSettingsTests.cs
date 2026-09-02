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
}
