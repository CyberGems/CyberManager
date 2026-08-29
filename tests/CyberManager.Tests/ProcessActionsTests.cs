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
}
