using CyberManager.Common.Models;
using CyberManager.Core.Engine;
using Xunit;

namespace CyberManager.Tests;

public class ProcessCollectorTests
{
    [Fact]
    public void Collect_ReturnsProcesses()
    {
        var collector = new ProcessCollector();
        var result = collector.Collect();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Collect_ContainsCurrentProcess()
    {
        var collector = new ProcessCollector();
        var result = collector.Collect();
        var currentPid = Environment.ProcessId;
        Assert.Contains(result, p => p.Pid == currentPid);
    }

    [Fact]
    public void Collect_CpuPercent_IsWithinValidRange()
    {
        var collector = new ProcessCollector();
        var first = collector.Collect();
        Thread.Sleep(100);
        var second = collector.Collect();
        foreach (var p in second)
        {
            Assert.InRange(p.CpuPercent, 0, 100);
        }
    }

    [Fact]
    public void Collect_CpuPercent_NoInflation()
    {
        var collector = new ProcessCollector();
        var first = collector.Collect();
        Thread.Sleep(200);
        var second = collector.Collect();
        var totalCpu = second.Sum(p => p.CpuPercent);
        Assert.InRange(totalCpu, 0, 100.01);
    }

    [Fact]
    public void Collect_Pid_IsNonNegative()
    {
        var collector = new ProcessCollector();
        var result = collector.Collect();
        foreach (var p in result)
        {
            Assert.True(p.Pid >= 0, $"PID should be non-negative, got {p.Pid}");
        }
    }

    [Fact]
    public void Collect_Name_IsNotEmpty()
    {
        var collector = new ProcessCollector();
        var result = collector.Collect();
        foreach (var p in result)
        {
            Assert.False(string.IsNullOrEmpty(p.Name), "Process name should not be empty");
        }
    }

    [Fact]
    public async Task CollectAsync_ReturnsProcesses()
    {
        var collector = new ProcessCollector();
        var asyncResult = await collector.CollectAsync();
        Assert.NotNull(asyncResult);
        Assert.NotEmpty(asyncResult);
        var currentPid = Environment.ProcessId;
        Assert.Contains(asyncResult, p => p.Pid == currentPid);
    }
}
