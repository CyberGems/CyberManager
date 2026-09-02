using CyberManager.Core.Engine;
using Xunit;

namespace CyberManager.Tests;

public class SystemMetricsCollectorTests
{
    [Fact]
    public void Sample_ReturnsValidMetrics()
    {
        var collector = new SystemMetricsCollector();
        var sample1 = collector.Sample(100);
        Assert.NotNull(sample1);
        Assert.True(sample1.LogicalProcessors > 0);
        Assert.True(sample1.PhysicalCores > 0);
        Assert.True(sample1.TotalPhysicalBytes > 0);

        Thread.Sleep(100);
        var sample2 = collector.Sample(100);
        Assert.InRange(sample2.CpuTotalPercent, 0.0, 100.0);
        Assert.InRange(sample2.CpuKernelPercent, 0.0, 100.0);
        Assert.InRange(sample2.CpuUserPercent, 0.0, 100.0);
        Assert.InRange(sample2.MemoryLoadPercent, 0.0, 100.0);
    }

    [Fact]
    public void GetCpuHistory_ReturnsFilledBuffers()
    {
        var collector = new SystemMetricsCollector();
        for (int i = 0; i < 5; i++)
        {
            collector.Sample(100);
            Thread.Sleep(20);
        }

        var (total, kernel) = collector.GetCpuHistory();
        Assert.NotNull(total);
        Assert.NotNull(kernel);
        Assert.Equal(5, total.Length);
        Assert.Equal(5, kernel.Length);
    }

    [Fact]
    public void GetRamHistory_ReturnsValidRamValues()
    {
        var collector = new SystemMetricsCollector();
        collector.Sample(100);

        var (gb, pct) = collector.GetRamHistory();
        Assert.NotNull(gb);
        Assert.NotNull(pct);
        Assert.NotEmpty(gb);
        Assert.NotEmpty(pct);
        Assert.True(gb[0] > 0);
        Assert.InRange(pct[0], 1, 100);
    }
}
