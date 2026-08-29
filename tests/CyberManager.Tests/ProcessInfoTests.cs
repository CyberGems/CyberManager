using CyberManager.Common.Models;
using Xunit;

namespace CyberManager.Tests;

public class ProcessInfoTests
{
    [Fact]
    public void MemoryFormatted_Bytes_ReturnsMB()
    {
        var info = new ProcessInfo { WorkingSetBytes = 50 * 1024 * 1024 };
        Assert.Equal("50 MB", info.MemoryFormatted);
    }

    [Fact]
    public void MemoryFormatted_LargeBytes_ReturnsGB()
    {
        var info = new ProcessInfo { WorkingSetBytes = 2L * 1024 * 1024 * 1024 };
        Assert.Contains("2", info.MemoryFormatted);
        Assert.Contains("GB", info.MemoryFormatted);
    }

    [Fact]
    public void MemoryFormatted_ZeroBytes_ReturnsZeroMB()
    {
        var info = new ProcessInfo { WorkingSetBytes = 0 };
        Assert.Equal("0 MB", info.MemoryFormatted);
    }

    [Fact]
    public void CpuFormatted_ShowsOneDecimal()
    {
        var info = new ProcessInfo { CpuPercent = 12.3456 };
        Assert.Contains("12", info.CpuFormatted);
        Assert.Contains("%", info.CpuFormatted);
    }

    [Fact]
    public void CpuFormatted_Zero_ShowsZero()
    {
        var info = new ProcessInfo { CpuPercent = 0 };
        Assert.Contains("0", info.CpuFormatted);
        Assert.Contains("%", info.CpuFormatted);
    }

    [Fact]
    public void Status_DefaultIsRunning()
    {
        var info = new ProcessInfo();
        Assert.Equal("Running", info.Status);
    }

    [Fact]
    public void Pid_DefaultIsZero()
    {
        var info = new ProcessInfo();
        Assert.Equal(0, info.Pid);
    }

    [Fact]
    public void Name_DefaultIsEmpty()
    {
        var info = new ProcessInfo();
        Assert.Equal("", info.Name);
    }
}
