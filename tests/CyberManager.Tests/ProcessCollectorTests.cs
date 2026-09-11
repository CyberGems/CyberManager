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
        var tracker = new ProcessCpuTracker();
        var processes = new List<ProcessInfo>
        {
            new() { Pid = 10, StartTime = DateTime.FromFileTimeUtc(1_000_000), CpuTimeTicks = 0 },
            new() { Pid = 11, StartTime = DateTime.FromFileTimeUtc(2_000_000), CpuTimeTicks = 0 }
        };

        tracker.Update(processes, timestamp: 1, timestampFrequency: 10, processorCount: 1);
        processes[0].CpuTimeTicks = 6_000_000;
        processes[1].CpuTimeTicks = 4_000_000;
        tracker.Update(processes, timestamp: 11, timestampFrequency: 10, processorCount: 1);

        Assert.Equal(100, processes.Sum(p => p.CpuPercent), precision: 6);
    }

    [Fact]
    public void ProcessCpuTracker_DoesNotReuseCpuDeltaAcrossPidReuse()
    {
        var tracker = new ProcessCpuTracker();
        var first = new List<ProcessInfo>
        {
            new() { Pid = 42, StartTime = DateTime.FromFileTimeUtc(1_000_000), CpuTimeTicks = 10_000_000 }
        };
        tracker.Update(first, timestamp: 1, timestampFrequency: 10, processorCount: 1);

        var replacement = new List<ProcessInfo>
        {
            new() { Pid = 42, StartTime = DateTime.FromFileTimeUtc(2_000_000), CpuTimeTicks = 20_000_000 }
        };
        tracker.Update(replacement, timestamp: 11, timestampFrequency: 10, processorCount: 1);

        Assert.Equal(0, replacement[0].CpuPercent);
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
