using System.ComponentModel;
using CyberManager.Common.Models;
using CyberManager.UI.ViewModels;
using Xunit;

namespace CyberManager.Tests;

public class ProcessListViewModelTests
{
    [Fact]
    public void HeavyFilter_RanksCpuAndMemoryTogether()
    {
        var processes = new[]
        {
            CreateProcess("CpuHeavy", cpu: 80, memoryMb: 10),
            CreateProcess("MemoryHeavy", cpu: 10, memoryMb: 90),
            CreateProcess("BalancedLow", cpu: 20, memoryMb: 20)
        };
        var query = CreateHeavyQuery(groupProcesses: false, limit: 2);

        var result = ProcessListViewModel.Build(processes, query);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, process => process.Name == "MemoryHeavy");
        Assert.Contains(result, process => process.Name == "CpuHeavy");
        Assert.DoesNotContain(result, process => process.Name == "BalancedLow");
    }

    [Fact]
    public void HeavyFilter_RanksAggregatedGroups()
    {
        var processes = new[]
        {
            CreateProcess("AppA", cpu: 30, memoryMb: 100),
            CreateProcess("AppA", cpu: 30, memoryMb: 100),
            CreateProcess("AppB", cpu: 90, memoryMb: 20),
            CreateProcess("AppC", cpu: 10, memoryMb: 300)
        };
        var query = CreateHeavyQuery(groupProcesses: true, limit: 2);

        var result = ProcessListViewModel.Build(processes, query);

        Assert.Collection(
            result,
            process => Assert.Equal("AppA", process.Name),
            process => Assert.Equal("AppC", process.Name));
        Assert.True(result[0].IsGroupParent);
        Assert.Equal(60, result[0].CpuPercent);
        Assert.Equal(200 * 1024L * 1024L, result[0].WorkingSetBytes);
    }

    private static ProcessListQuery CreateHeavyQuery(bool groupProcesses, int limit) =>
        new(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: groupProcesses,
            DimSuspended: true,
            SortColumn: "CpuPercent",
            SortDirection: ListSortDirection.Descending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            HeavyProcessesOnly: true,
            HeavyProcessLimit: limit);

    private static ProcessInfo CreateProcess(string name, double cpu, int memoryMb) =>
        new()
        {
            Name = name,
            CpuPercent = cpu,
            WorkingSetBytes = memoryMb * 1024L * 1024L
        };
}
