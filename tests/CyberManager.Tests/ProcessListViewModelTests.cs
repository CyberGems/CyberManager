using System.ComponentModel;
using CyberManager.Common.I18n;
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

    [Fact]
    public void Grouping_AggregatesWebView2ProcessesUnderHostApplication()
    {
        var processes = new[]
        {
            new ProcessInfo
            {
                Pid = 15948,
                ParentPid = 1000,
                Name = "CyberClock.exe",
                FriendlyName = "CyberClock",
                MainWindowTitle = "CyberClock",
                CpuPercent = 0.2,
                WorkingSetBytes = 80 * 1024L * 1024L
            },
            new ProcessInfo
            {
                Pid = 16664,
                ParentPid = 15948,
                Name = "msedgewebview2.exe",
                FriendlyName = "Microsoft Edge WebView2",
                CpuPercent = 0.1,
                WorkingSetBytes = 50 * 1024L * 1024L
            },
            new ProcessInfo
            {
                Pid = 18312,
                ParentPid = 16664,
                Name = "msedgewebview2.exe",
                FriendlyName = "Microsoft Edge WebView2",
                CpuPercent = 12.8,
                WorkingSetBytes = 90 * 1024L * 1024L
            }
        };

        var query = new ProcessListQuery(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: true,
            DimSuspended: true,
            SortColumn: "CpuPercent",
            SortDirection: ListSortDirection.Descending,
            ExpandedGroups: new HashSet<string>(["CyberClock.exe"], StringComparer.OrdinalIgnoreCase));

        var result = ProcessListViewModel.Build(processes, query);

        Assert.Equal(4, result.Count); // Parent + 3 children
        var parent = result[0];
        Assert.True(parent.IsGroupParent);
        Assert.Equal("CyberClock.exe", parent.Name);
        Assert.Equal("CyberClock", parent.FriendlyName);
        Assert.Equal(3, parent.InstanceCount);
        Assert.Equal(13.1, Math.Round(parent.CpuPercent, 1));
        Assert.Equal(220 * 1024L * 1024L, parent.WorkingSetBytes);
        Assert.True(result[1].IsGroupChild);
        Assert.True(result[2].IsGroupChild);
        Assert.True(result[3].IsGroupChild);
    }

    [Fact]
    public void Grouping_DoesNotAdoptUnrelatedAppsFromLaunchers()
    {
        var processes = new[]
        {
            new ProcessInfo
            {
                Pid = 100,
                ParentPid = 4,
                Name = "explorer.exe",
                FriendlyName = "Windows Explorer",
                MainWindowTitle = ""
            },
            new ProcessInfo
            {
                Pid = 200,
                ParentPid = 100,
                Name = "notepad.exe",
                FriendlyName = "Notepad",
                MainWindowTitle = "Untitled - Notepad"
            }
        };

        var query = new ProcessListQuery(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: true,
            DimSuspended: true,
            SortColumn: "Name",
            SortDirection: ListSortDirection.Ascending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var result = ProcessListViewModel.Build(processes, query);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.IsGroupParent);
    }

    [Fact]
    public void Grouping_SearchByParentName_IncludesChildHelpers()
    {
        var processes = new[]
        {
            new ProcessInfo
            {
                Pid = 100,
                ParentPid = 10,
                Name = "CyberClock.exe",
                FriendlyName = "CyberClock",
                MainWindowTitle = "CyberClock",
                CpuPercent = 0.5
            },
            new ProcessInfo
            {
                Pid = 101,
                ParentPid = 100,
                Name = "msedgewebview2.exe",
                FriendlyName = "Microsoft Edge WebView2",
                CpuPercent = 10.0
            },
            new ProcessInfo
            {
                Pid = 200,
                ParentPid = 10,
                Name = "unrelated.exe",
                FriendlyName = "Other App",
                CpuPercent = 5.0
            }
        };

        var query = new ProcessListQuery(
            Search: "CyberClock",
            ShowIdleProcess: true,
            GroupProcesses: true,
            DimSuspended: true,
            SortColumn: "CpuPercent",
            SortDirection: ListSortDirection.Descending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var result = ProcessListViewModel.Build(processes, query);

        Assert.Single(result);
        Assert.Equal("CyberClock.exe", result[0].Name);
        Assert.Equal(2, result[0].InstanceCount);
        Assert.Equal(10.5, result[0].CpuPercent);
    }

    [Fact]
    public void Grouping_PropagatesIsNewlyCreatedToParent()
    {
        var processes = new[]
        {
            new ProcessInfo
            {
                Pid = 100,
                Name = "app.exe",
                IsNewlyCreated = false
            },
            new ProcessInfo
            {
                Pid = 101,
                Name = "app.exe",
                IsNewlyCreated = true
            }
        };

        var query = new ProcessListQuery(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: true,
            DimSuspended: true,
            SortColumn: "Name",
            SortDirection: ListSortDirection.Ascending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var result = ProcessListViewModel.Build(processes, query);

        Assert.Single(result);
        Assert.True(result[0].IsGroupParent);
        Assert.True(result[0].IsNewlyCreated);
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

    [Fact]
    public void StatusFormatted_LocalizesCorrectly_InSpanishAndEnglish()
    {
        var originalLang = Strings.Current;
        try
        {
            var proc = new ProcessInfo { Status = "Exited" };

            Strings.Current = Lang.Es;
            Assert.Equal("Finalizado", proc.StatusFormatted);

            Strings.Current = Lang.En;
            Assert.Equal("Exited", proc.StatusFormatted);

            proc.Status = "Running";
            Strings.Current = Lang.Es;
            Assert.Equal("En ejecución", proc.StatusFormatted);
            Strings.Current = Lang.En;
            Assert.Equal("Running", proc.StatusFormatted);

            proc.Status = "Suspended";
            Strings.Current = Lang.Es;
            Assert.Equal("Suspendido", proc.StatusFormatted);
            Strings.Current = Lang.En;
            Assert.Equal("Suspended", proc.StatusFormatted);
        }
        finally
        {
            Strings.Current = originalLang;
        }
    }

    [Fact]
    public void SortByStatus_AscendingAndDescending_OrdersCorrectly()
    {
        var originalLang = Strings.Current;
        try
        {
            Strings.Current = Lang.En;
            var processes = new[]
            {
                new ProcessInfo { Pid = 10, Name = "AppA.exe", Status = "Running" },
                new ProcessInfo { Pid = 20, Name = "AppB.exe", Status = "Exited" },
                new ProcessInfo { Pid = 30, Name = "AppC.exe", Status = "Suspended" }
            };

            var queryAsc = new ProcessListQuery(
                Search: "",
                ShowIdleProcess: true,
                GroupProcesses: false,
                DimSuspended: false,
                SortColumn: "Status",
                SortDirection: ListSortDirection.Ascending,
                ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var resultAsc = ProcessListViewModel.Build(processes, queryAsc);
            Assert.Equal("Exited", resultAsc[0].Status);
            Assert.Equal("Running", resultAsc[1].Status);
            Assert.Equal("Suspended", resultAsc[2].Status);

            var queryDesc = new ProcessListQuery(
                Search: "",
                ShowIdleProcess: true,
                GroupProcesses: false,
                DimSuspended: false,
                SortColumn: "Status",
                SortDirection: ListSortDirection.Descending,
                ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var resultDesc = ProcessListViewModel.Build(processes, queryDesc);
            Assert.Equal("Suspended", resultDesc[0].Status);
            Assert.Equal("Running", resultDesc[1].Status);
            Assert.Equal("Exited", resultDesc[2].Status);
        }
        finally
        {
            Strings.Current = originalLang;
        }
    }

    [Fact]
    public void GroupedProcesses_AggregatesExitingStatus_WhenAllChildrenExit()
    {
        var processes = new[]
        {
            new ProcessInfo { Pid = 100, Name = "MyApp.exe", FriendlyName = "MyApp", Status = "Exited", IsTerminating = true },
            new ProcessInfo { Pid = 101, Name = "MyApp.exe", FriendlyName = "MyApp", Status = "Exited", IsTerminating = true }
        };

        var query = new ProcessListQuery(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: true,
            DimSuspended: false,
            SortColumn: "Name",
            SortDirection: ListSortDirection.Ascending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var result = ProcessListViewModel.Build(processes, query);
        Assert.Single(result);
        Assert.Equal("Exited", result[0].Status);
        Assert.True(result[0].IsTerminating);
    }

    [Fact]
    public void GroupedProcesses_RemainsRunning_WhenOnlySomeChildrenExit()
    {
        var processes = new[]
        {
            new ProcessInfo { Pid = 100, Name = "MyApp.exe", FriendlyName = "MyApp", Status = "Running", IsTerminating = false },
            new ProcessInfo { Pid = 101, Name = "MyApp.exe", FriendlyName = "MyApp", Status = "Exited", IsTerminating = true }
        };

        var query = new ProcessListQuery(
            Search: "",
            ShowIdleProcess: true,
            GroupProcesses: true,
            DimSuspended: false,
            SortColumn: "Name",
            SortDirection: ListSortDirection.Ascending,
            ExpandedGroups: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var result = ProcessListViewModel.Build(processes, query);
        Assert.Single(result);
        Assert.Equal("Running", result[0].Status);
        Assert.False(result[0].IsTerminating);
    }

    private static ProcessInfo CreateProcess(string name, double cpu, int memoryMb) =>
        new()
        {
            Name = name,
            CpuPercent = cpu,
            WorkingSetBytes = memoryMb * 1024L * 1024L
        };
}
