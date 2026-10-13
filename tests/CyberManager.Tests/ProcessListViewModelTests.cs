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
    public void HeavyFilter_IncludesEveryProcessWithAHighlightedCpuOrMemoryValue()
    {
        var processes = Enumerable.Range(0, 12)
            .Select(index => CreateProcess($"MemoryHeavy{index}", cpu: 1, memoryMb: 300 + index))
            .ToList();
        processes.Add(CreateProcess("CpuColored", cpu: 1.01, memoryMb: 299));
        processes.Add(CreateProcess("BothColored", cpu: 5.01, memoryMb: 299));
        processes.Add(CreateProcess("Uncolored", cpu: 1, memoryMb: 299));

        var result = ProcessListViewModel.Build(processes, CreateHeavyQuery(groupProcesses: false));

        Assert.Equal(14, result.Count);
        Assert.Contains(result, process => process.Name == "CpuColored");
        Assert.Contains(result, process => process.Name == "BothColored");
        Assert.DoesNotContain(result, process => process.Name == "Uncolored");
        Assert.Equal(12, result.Count(process => process.Name.StartsWith("MemoryHeavy", StringComparison.Ordinal)));
    }

    [Fact]
    public void HeavyFilter_UsesAggregatedValuesForGroupedProcesses()
    {
        var processes = new[]
        {
            CreateProcess("AppA", cpu: 0.2, memoryMb: 160),
            CreateProcess("AppA", cpu: 0.2, memoryMb: 160),
            CreateProcess("AppB", cpu: 1, memoryMb: 299),
            CreateProcess("AppC", cpu: 1.01, memoryMb: 10)
        };

        var result = ProcessListViewModel.Build(processes, CreateHeavyQuery(groupProcesses: true));

        Assert.Collection(
            result,
            process => Assert.Equal("AppC", process.Name),
            process => Assert.Equal("AppA", process.Name));
        Assert.True(result[1].IsGroupParent);
        Assert.Equal(0.4, result[1].CpuPercent);
        Assert.Equal(320 * 1024L * 1024L, result[1].WorkingSetBytes);
    }

    [Fact]
    public void HeavyFilter_ExpandedGroupsOnlyShowChildrenWithHighlightedValues()
    {
        var processes = new[]
        {
            CreateProcess("AppA", cpu: 0.2, memoryMb: 310),
            CreateProcess("AppA", cpu: 0.2, memoryMb: 250),
            CreateProcess("AppA", cpu: 0.2, memoryMb: 40),
            CreateProcess("AppB", cpu: 1, memoryMb: 100)
        };

        var result = ProcessListViewModel.Build(
            processes,
            CreateHeavyQuery(groupProcesses: true, expandedGroups: ["AppA"]));

        Assert.Equal(2, result.Count);
        Assert.Equal("AppA", result[0].Name);
        Assert.True(result[0].IsGroupParent);
        Assert.True(result[0].IsExpanded);
        Assert.Equal(600 * 1024L * 1024L, result[0].WorkingSetBytes);
        Assert.Equal(310 * 1024L * 1024L, result[1].WorkingSetBytes);
        Assert.True(result[1].IsGroupChild);
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

    private static ProcessListQuery CreateHeavyQuery(bool groupProcesses, string[]? expandedGroups = null) =>
        new(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: groupProcesses,
            DimSuspended: true,
            SortColumn: "CpuPercent",
            SortDirection: ListSortDirection.Descending,
            ExpandedGroups: new HashSet<string>(expandedGroups ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            HeavyProcessesOnly: true);

    private static ProcessInfo CreateProcess(string name, double cpu, int memoryMb) =>
        new()
        {
            Name = name,
            CpuPercent = cpu,
            WorkingSetBytes = memoryMb * 1024L * 1024L
        };
}
