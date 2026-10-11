using CyberManager.Core.Engine;
using Xunit;

namespace CyberManager.Tests;

public class ProcessActionsTests
{
    [Fact]
    public void Kill_InvalidPid_ReturnsFalse()
    {
        var result = ProcessActions.Kill(-1);
        Assert.False(result);
    }

    [Fact]
    public void KillTree_InvalidPid_ReturnsFalse()
    {
        var result = ProcessActions.KillTree(-1);
        Assert.False(result);
    }

    [Fact]
    public void SetPriority_InvalidPid_ReturnsFalse()
    {
        var result = ProcessActions.SetPriority(-1, System.Diagnostics.ProcessPriorityClass.Normal);
        Assert.False(result);
    }

    [Fact]
    public void IsElevated_ReturnsBool()
    {
        var result = ProcessActions.IsElevated;
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void Restart_InvalidPid_ReturnsFalse()
    {
        var result = ProcessActions.TryRestart(-1, @"C:\Windows\System32\notepad.exe");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Restart_MissingExePath_ReturnsFalse()
    {
        var result = ProcessActions.TryRestart(1234, "");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Restart_NonExistentExecutable_ReturnsFalseWithoutKilling()
    {
        var result = ProcessActions.TryRestart(1234, @"C:\does-not-exist\ghost.exe");
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void Restart_PackagedAppPath_ReturnsPackagedMarker()
    {
        var result = ProcessActions.TryRestart(
            1234,
            @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator_11.2607.0.0_x64__8wekyb3d8bbwe\CalculatorApp.exe");

        Assert.False(result.Succeeded);
        Assert.Equal(ProcessActions.PackagedAppRestartMessage, result.ErrorMessage);
    }

    [Fact]
    public void IsPackagedApp_InvalidPid_ReturnsFalse()
    {
        Assert.False(ProcessActions.IsPackagedApp(-1));
    }

    [Fact]
    public void IsPackagedApp_UnpackagedProcess_ReturnsFalse()
    {
        // The test host itself is a plain desktop process, never a packaged (UWP) app.
        Assert.False(ProcessActions.IsPackagedApp(Environment.ProcessId));
    }
}
