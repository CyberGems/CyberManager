using System.ComponentModel;
using CyberManager.Common.Models;
using CyberManager.UI.ViewModels;
using Xunit;

namespace CyberManager.Tests;

public class ProcessListViewModelTests
{
    private static readonly string[] ExpectedNamesAscending =
    {
        "Acrobat.exe", "chrome.exe", "explorer.exe", "winword.exe", "Zoom.exe"
    };

    private static readonly string[] ExpectedNamesDescending =
    {
        "Zoom.exe", "winword.exe", "explorer.exe", "chrome.exe", "Acrobat.exe"
    };

    private static readonly string[] ExpectedFriendlyNamesAscending =
    {
        "Adobe Acrobat", "Google Chrome", "Microsoft Edge"
    };

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

    [Fact]
    public void SortByName_Ascending_IsCaseInsensitiveAndAlphabetical()
    {
        var processes = new[]
        {
            CreateProcess("chrome.exe"),
            CreateProcess("Acrobat.exe"),
            CreateProcess("winword.exe"),
            CreateProcess("Zoom.exe"),
            CreateProcess("explorer.exe")
        };

        var result = ProcessListViewModel.Build(processes, CreateNameQuery(ListSortDirection.Ascending));

        Assert.Equal(
            ExpectedNamesAscending,
            result.Select(process => process.Name));
    }

    [Fact]
    public void SortByName_Descending_IsExactReverse()
    {
        var processes = new[]
        {
            CreateProcess("chrome.exe"),
            CreateProcess("Acrobat.exe"),
            CreateProcess("winword.exe"),
            CreateProcess("Zoom.exe"),
            CreateProcess("explorer.exe")
        };

        var result = ProcessListViewModel.Build(processes, CreateNameQuery(ListSortDirection.Descending));

        Assert.Equal(
            ExpectedNamesDescending,
            result.Select(process => process.Name));
    }

    [Fact]
    public void SortByName_UsesFriendlyNameLikeTheVisibleColumn()
    {
        var processes = new[]
        {
            CreateProcess("chrome.exe", friendlyName: "Google Chrome"),
            CreateProcess("acrobat.exe", friendlyName: "Adobe Acrobat"),
            CreateProcess("msedge.exe", friendlyName: "Microsoft Edge")
        };

        var result = ProcessListViewModel.Build(processes, CreateNameQuery(ListSortDirection.Ascending));

        Assert.Equal(
            ExpectedFriendlyNamesAscending,
            result.Select(process => process.FriendlyName));
    }

    [Fact]
    public void HideSystemProcesses_RemovesSystemCategory()
    {
        var processes = new[]
        {
            new ProcessInfo { Pid = 4, Name = "System" },
            new ProcessInfo
            {
                Pid = 100,
                Name = "svchost.exe",
                ExePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32",
                    "svchost.exe")
            },
            new ProcessInfo
            {
                Pid = 200,
                Name = "app.exe",
                ExePath = @"C:\Program Files\App\app.exe"
            }
        };

        var result = ProcessListViewModel.Build(processes, CreateSystemFilterQuery(hideSystemProcesses: true));

        Assert.Single(result);
        Assert.Equal("app.exe", result[0].Name);
    }

    [Fact]
    public void HideSystemProcesses_Disabled_KeepsSystemCategory()
    {
        var processes = new[]
        {
            new ProcessInfo { Pid = 4, Name = "System" },
            new ProcessInfo
            {
                Pid = 200,
                Name = "app.exe",
                ExePath = @"C:\Program Files\App\app.exe"
            }
        };

        var result = ProcessListViewModel.Build(processes, CreateSystemFilterQuery(hideSystemProcesses: false));

        Assert.Equal(2, result.Count);
    }

    private static ProcessListQuery CreateSystemFilterQuery(bool hideSystemProcesses) =>
        new(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: false,
            DimSuspended: true,
            SortColumn: "Name",
            SortDirection: ListSortDirection.Ascending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            HideSystemProcesses: hideSystemProcesses);

    private static ProcessListQuery CreateNameQuery(ListSortDirection direction) =>
        new(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: false,
            DimSuspended: true,
            SortColumn: "Name",
            SortDirection: direction,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static ProcessInfo CreateProcess(string name, string? friendlyName = null) =>
        new()
        {
            Name = name,
            FriendlyName = friendlyName ?? name
        };

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
