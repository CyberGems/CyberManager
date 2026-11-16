using CyberManager.Core.Engine;
using Xunit;

namespace CyberManager.Tests;

public class StartupAppsCollectorTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --minimized", "C:\\Program Files\\App\\app.exe")]
    [InlineData("\"C:\\App\\run.exe\"", "C:\\App\\run.exe")]
    [InlineData("C:\\Windows\\notepad.exe", "C:\\Windows\\notepad.exe")]
    [InlineData("C:\\Tools\\agent.exe /background /quiet", "C:\\Tools\\agent.exe")]
    public void ExtractExecutablePath_HandlesQuotedAndBareCommands(string command, string expected)
    {
        Assert.Equal(expected, StartupAppsCollector.ExtractExecutablePath(command));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractExecutablePath_ReturnsNullForEmptyCommands(string? command)
    {
        Assert.Null(StartupAppsCollector.ExtractExecutablePath(command));
    }

    [Fact]
    public void ExtractExecutablePath_ExpandsEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("CYBERMANAGER_STARTUP_TEST", @"C:\Tools");
        try
        {
            Assert.Equal(
                @"C:\Tools\agent.exe",
                StartupAppsCollector.ExtractExecutablePath("\"%CYBERMANAGER_STARTUP_TEST%\\agent.exe\" /background"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CYBERMANAGER_STARTUP_TEST", null);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x02, 0x00 }, true)]
    [InlineData(new byte[] { 0x06, 0x00 }, true)]
    [InlineData(new byte[] { 0x03, 0x00 }, false)]
    [InlineData(new byte[] { 0x07, 0x00 }, false)]
    public void IsEnabledFromApprovedValue_ReadsTheDisabledBit(byte[] value, bool expected)
    {
        Assert.Equal(expected, StartupAppsCollector.IsEnabledFromApprovedValue(value));
    }

    [Fact]
    public void IsEnabledFromApprovedValue_TreatsMissingValueAsEnabled()
    {
        Assert.True(StartupAppsCollector.IsEnabledFromApprovedValue(null));
        Assert.True(StartupAppsCollector.IsEnabledFromApprovedValue(Array.Empty<byte>()));
    }

    [Fact]
    public void Collect_ReturnsOnlyUsableEntries()
    {
        var entries = new StartupAppsCollector().Collect();

        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Command));
        });
    }
}
