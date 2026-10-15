using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class SingleInstanceTests
{
    private static string FreshName() => "CyberManager_Tests_" + Guid.NewGuid().ToString("N");

    [Fact]
    public void TryAcquire_Succeeds_WhenMutexIsFree()
    {
        var single = new SingleInstance(FreshName());

        Assert.True(single.TryAcquire());
    }

    [Fact]
    public void TryAcquire_Fails_WhileAnotherInstanceHoldsTheMutex()
    {
        var name = FreshName();
        var first = new SingleInstance(name);
        Assert.True(first.TryAcquire());

        var second = new SingleInstance(name);

        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void TryAcquire_Succeeds_AfterTheHolderReleases()
    {
        var name = FreshName();
        var first = new SingleInstance(name);
        Assert.True(first.TryAcquire());
        Assert.False(new SingleInstance(name).TryAcquire());

        first.Release();

        Assert.True(new SingleInstance(name).TryAcquire());
    }
}
