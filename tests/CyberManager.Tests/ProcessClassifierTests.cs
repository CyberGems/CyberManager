using CyberManager.Common.Models;
using Xunit;

namespace CyberManager.Tests;

public class ProcessClassifierTests
{
    private static readonly string WindowsDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void IsSystemProcess_KernelTasks_AreSystem(int pid)
    {
        var process = new ProcessInfo { Pid = pid, Name = $"PID_{pid}" };

        Assert.True(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_EmptyExePath_IsSystem()
    {
        var process = new ProcessInfo { Pid = 1234, Name = "Registry", ExePath = "" };

        Assert.True(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_ImageUnderWindowsDirectory_IsSystem()
    {
        var process = new ProcessInfo
        {
            Pid = 900,
            Name = "svchost.exe",
            ExePath = Path.Combine(WindowsDirectory, "System32", "svchost.exe")
        };

        Assert.True(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_DesktopShellUnderWindows_IsNotSystem()
    {
        var process = new ProcessInfo
        {
            Pid = 800,
            Name = "explorer.exe",
            ExePath = Path.Combine(WindowsDirectory, "explorer.exe")
        };

        Assert.False(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_ShellException_IsCaseInsensitive()
    {
        var process = new ProcessInfo
        {
            Pid = 801,
            Name = "Explorer.EXE",
            ExePath = Path.Combine(WindowsDirectory, "Explorer.EXE")
        };

        Assert.False(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_UserApplication_IsNotSystem()
    {
        var process = new ProcessInfo
        {
            Pid = 4242,
            Name = "app.exe",
            ExePath = @"C:\Program Files\SomeApp\app.exe"
        };

        Assert.False(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_SiblingFolderOfWindows_IsNotSystem()
    {
        // A prefix match without a trailing separator would wrongly swallow C:\WindowsApps.
        var sibling = WindowsDirectory.TrimEnd('\\') + "Apps\\tool.exe";
        var process = new ProcessInfo { Pid = 5150, Name = "tool.exe", ExePath = sibling };

        Assert.False(ProcessClassifier.IsSystemProcess(process));
    }

    [Fact]
    public void IsSystemProcess_Null_IsNotSystem()
    {
        Assert.False(ProcessClassifier.IsSystemProcess(null));
    }

    [Fact]
    public void ProcessInfo_IsSystemProcess_MirrorsClassifier()
    {
        var system = new ProcessInfo { Pid = 4, Name = "System" };
        var user = new ProcessInfo { Pid = 2000, Name = "chrome.exe", ExePath = @"C:\Program Files\Google\chrome.exe" };

        Assert.True(system.IsSystemProcess);
        Assert.False(user.IsSystemProcess);
    }
}
