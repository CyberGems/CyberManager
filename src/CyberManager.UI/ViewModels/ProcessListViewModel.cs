using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CyberManager.Common.Models;
using CyberManager.UI.Services;

namespace CyberManager.UI.ViewModels;

public sealed record ProcessListQuery(
    string Search,
    bool ShowIdleProcess,
    bool GroupProcesses,
    bool DimSuspended,
    string SortColumn,
    ListSortDirection SortDirection,
    IReadOnlySet<string> ExpandedGroups,
    bool HeavyProcessesOnly = false,
    bool HideSystemProcesses = false);

public sealed class ProcessListViewModel
{
    private static readonly HashSet<string> SystemLaunchers = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "explorer",
        "services.exe", "services",
        "svchost.exe", "svchost",
        "smss.exe", "smss",
        "csrss.exe", "csrss",
        "wininit.exe", "wininit",
        "winlogon.exe", "winlogon",
        "lsass.exe", "lsass",
        "taskhostw.exe", "taskhostw",
        "runtimebroker.exe", "runtimebroker"
    };

    private static readonly HashSet<string> KnownHelperNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedgewebview2.exe", "msedgewebview2",
        "conhost.exe", "conhost",
        "crashpad_handler.exe", "crashpad_handler",
        "werfault.exe", "werfault"
    };

    public ObservableCollection<ProcessInfo> Items { get; } = new();

    public static List<ProcessInfo> Build(IReadOnlyList<ProcessInfo> all, ProcessListQuery query)
    {
        var byPid = new Dictionary<int, ProcessInfo>(all.Count);
        foreach (var p in all)
        {
            if (p.Pid > 0)
            {
                byPid.TryAdd(p.Pid, p);
            }
        }

        IEnumerable<ProcessInfo> filtered = all;
        if (!query.ShowIdleProcess)
        {
            filtered = filtered.Where(process => process.Pid != 0);
        }

        if (query.HideSystemProcesses)
        {
            filtered = filtered.Where(process => !process.IsSystemProcess);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var isPid = int.TryParse(search, out var pid);
            filtered = filtered.Where(process =>
            {
                if (MatchesSearch(process, search, isPid, pid)) return true;
                if (query.GroupProcesses)
                {
                    var root = FindRootProcess(process, byPid);
                    if (!ReferenceEquals(root, process) && MatchesSearch(root, search, isPid, pid))
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        if (!query.GroupProcesses)
        {
            foreach (var process in filtered)
            {
                process.IsGroupParent = false;
                process.IsGroupChild = false;
                process.InstanceCount = 1;
                process.DimWhenSuspended = query.DimSuspended && process.Status == "Suspended";
            }

            if (query.HeavyProcessesOnly)
            {
                filtered = filtered.Where(HasHighlightedMetric);
            }

            return ApplySorting(filtered, query).ToList();
        }

        var topLevel = new List<ProcessInfo>();
        foreach (var group in filtered.GroupBy(process => FindRootProcess(process, byPid).Name, StringComparer.OrdinalIgnoreCase))
        {
            var processes = group.ToList();
            if (processes.Count == 1)
            {
                var single = processes[0];
                single.IsGroupParent = false;
                single.IsGroupChild = false;
                single.InstanceCount = 1;
                single.DimWhenSuspended = query.DimSuspended && single.Status == "Suspended";
                topLevel.Add(single);
                continue;
            }

            var mainProcess = processes.FirstOrDefault(process => process.HasWindow && process.Name.Equals(group.Key, StringComparison.OrdinalIgnoreCase)) ??
                              processes.FirstOrDefault(process => process.Name.Equals(group.Key, StringComparison.OrdinalIgnoreCase)) ??
                              processes.FirstOrDefault(process => process.HasWindow) ??
                              processes.OrderBy(process => process.Pid).First();
            var parent = new ProcessInfo
            {
                Pid = mainProcess.Pid,
                ParentPid = mainProcess.ParentPid,
                Name = group.Key,
                FriendlyName = mainProcess.FriendlyName,
                ExePath = !string.IsNullOrEmpty(mainProcess.ExePath)
                    ? mainProcess.ExePath
                    : processes.FirstOrDefault(process => !string.IsNullOrEmpty(process.ExePath))?.ExePath ?? "",
                UserName = mainProcess.UserName,
                Status = processes.Any(process => process.Status == "Running") ? "Running" :
                         processes.All(process => process.Status == "Exited") ? "Exited" : "Suspended",
                CpuPercent = processes.Sum(process => process.CpuPercent),
                WorkingSetBytes = processes.Sum(process => process.WorkingSetBytes),
                PrivateBytes = processes.Sum(process => process.PrivateBytes),
                ThreadCount = processes.Sum(process => process.ThreadCount),
                StartTime = mainProcess.StartTime,
                Priority = mainProcess.Priority,
                MainWindowTitle = mainProcess.MainWindowTitle,
                IsGroupParent = true,
                IsGroupChild = false,
                IsExpanded = query.ExpandedGroups.Contains(group.Key),
                InstanceCount = processes.Count,
                DimWhenSuspended = query.DimSuspended && processes.All(process => process.Status == "Suspended"),
                IsNewlyCreated = processes.Any(process => process.IsNewlyCreated),
                IsTerminating = processes.All(process => process.IsTerminating),
                Children = processes.OrderByDescending(process => process.HasWindow)
                    .ThenByDescending(process => process.CpuPercent)
                    .ThenByDescending(process => process.WorkingSetBytes)
                    .ToList()
            };

            foreach (var child in parent.Children)
            {
                child.IsGroupChild = true;
                child.IsGroupParent = false;
                child.DimWhenSuspended = query.DimSuspended && child.Status == "Suspended";
            }

            topLevel.Add(parent);
        }

        var candidates = query.HeavyProcessesOnly
            ? topLevel.Where(HasHighlightedMetric)
            : topLevel;
        var result = new List<ProcessInfo>();
        foreach (var item in ApplySorting(candidates, query))
        {
            result.Add(item);
            if (item.IsGroupParent && item.IsExpanded)
            {
                result.AddRange(query.HeavyProcessesOnly
                    ? item.Children.Where(HasHighlightedMetric)
                    : item.Children);
            }
        }

        return result;
    }

    private static bool HasHighlightedMetric(ProcessInfo process) =>
        HeatmapThresholds.CpuBucket(process.CpuPercent) > 0 ||
        HeatmapThresholds.MemoryBucket(process.WorkingSetBytes) > 0;

    public void Update(IReadOnlyList<ProcessInfo> next)
    {
        Update(next, preserveOrder: false);
    }

    public void UpdatePreservingOrder(IReadOnlyList<ProcessInfo> next)
    {
        Update(next, preserveOrder: true);
    }

    private void Update(IReadOnlyList<ProcessInfo> next, bool preserveOrder)
    {
        if (preserveOrder)
        {
            UpdateWithoutReordering(next);
            return;
        }

        for (var index = 0; index < next.Count; index++)
        {
            if (index < Items.Count && HasSameIdentity(Items[index], next[index]))
            {
                CopyProcessInfo(Items[index], next[index]);
            }
            else if (index < Items.Count)
            {
                Items[index] = next[index];
            }
            else
            {
                Items.Add(next[index]);
            }
        }

        while (Items.Count > next.Count)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    private void UpdateWithoutReordering(IReadOnlyList<ProcessInfo> next)
    {
        var pending = next.ToDictionary(GetIdentity);
        var desired = new List<ProcessInfo>(Math.Max(Items.Count, next.Count));

        foreach (var current in Items)
        {
            if (pending.Remove(GetIdentity(current), out var replacement))
            {
                CopyProcessInfo(current, replacement);
                desired.Add(current);
            }
        }

        foreach (var candidate in next)
        {
            if (pending.Remove(GetIdentity(candidate), out _))
            {
                desired.Add(candidate);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            if (index < Items.Count && ReferenceEquals(Items[index], desired[index])) continue;

            if (index < Items.Count)
            {
                Items[index] = desired[index];
            }
            else
            {
                Items.Add(desired[index]);
            }
        }

        while (Items.Count > desired.Count)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    private static bool HasSameIdentity(ProcessInfo current, ProcessInfo next) =>
        GetIdentity(current).Equals(GetIdentity(next));

    private static ProcessListIdentity GetIdentity(ProcessInfo process) =>
        process.IsGroupParent
            ? new ProcessListIdentity(true, 0, 0, process.Name.ToUpperInvariant())
            : new ProcessListIdentity(false, process.Pid, GetStartTimeKey(process.StartTime), "");

    private static long GetStartTimeKey(DateTime startTime) =>
        startTime == default ? 0 : startTime.ToFileTimeUtc();

    private readonly record struct ProcessListIdentity(
        bool IsGroupParent,
        int Pid,
        long StartTimeFileTime,
        string Name);

    private static IEnumerable<ProcessInfo> ApplySorting(
        IEnumerable<ProcessInfo> list,
        ProcessListQuery query)
    {
        var ascending = query.SortDirection == ListSortDirection.Ascending;
        return query.SortColumn switch
        {
            // The visible process name is DisplayName (FriendlyName when available), so sorting
            // must use the same value the user sees. A case-insensitive, culture-invariant
            // comparison keeps "chrome.exe" next to "Chrome", instead of pushing every lowercase
            // entry after the uppercase ones.
            "Name" => ascending
                ? list.OrderBy(SortName, NameComparer).ThenBy(process => process.Pid)
                : list.OrderByDescending(SortName, NameComparer).ThenBy(process => process.Pid),
            "Status" => ascending
                ? list.OrderBy(process => process.StatusFormatted).ThenBy(SortName, NameComparer)
                : list.OrderByDescending(process => process.StatusFormatted).ThenBy(SortName, NameComparer),
            "Pid" => ascending ? list.OrderBy(process => process.Pid) : list.OrderByDescending(process => process.Pid),
            "CpuPercent" => ascending ? list.OrderBy(process => process.CpuPercent).ThenBy(SortName, NameComparer) : list.OrderByDescending(process => process.CpuPercent).ThenBy(SortName, NameComparer),
            "WorkingSetBytes" => ascending ? list.OrderBy(process => process.WorkingSetBytes).ThenBy(SortName, NameComparer) : list.OrderByDescending(process => process.WorkingSetBytes).ThenBy(SortName, NameComparer),
            "ThreadCount" => ascending ? list.OrderBy(process => process.ThreadCount).ThenBy(SortName, NameComparer) : list.OrderByDescending(process => process.ThreadCount).ThenBy(SortName, NameComparer),
            "Priority" => ascending ? list.OrderBy(process => process.Priority).ThenBy(SortName, NameComparer) : list.OrderByDescending(process => process.Priority).ThenBy(SortName, NameComparer),
            "ExePath" => ascending
                ? list.OrderBy(process => string.IsNullOrWhiteSpace(process.ExePath) ? 1 : 0).ThenBy(process => process.ExePath).ThenBy(SortName, NameComparer)
                : list.OrderBy(process => string.IsNullOrWhiteSpace(process.ExePath) ? 1 : 0).ThenByDescending(process => process.ExePath).ThenBy(SortName, NameComparer),
            _ => list.OrderByDescending(process => process.CpuPercent).ThenBy(SortName, NameComparer)
        };
    }

    private static readonly StringComparer NameComparer = StringComparer.InvariantCultureIgnoreCase;

    private static string SortName(ProcessInfo process) =>
        string.IsNullOrWhiteSpace(process.FriendlyName) ? process.Name : process.FriendlyName;

    private static void CopyProcessInfo(ProcessInfo target, ProcessInfo source)
    {
        target.Pid = source.Pid;
        target.ParentPid = source.ParentPid;
        target.Name = source.Name;
        target.FriendlyName = source.FriendlyName;
        target.ExePath = source.ExePath;
        target.UserName = source.UserName;
        target.Status = source.Status;
        target.CpuPercent = source.CpuPercent;
        target.WorkingSetBytes = source.WorkingSetBytes;
        target.PrivateBytes = source.PrivateBytes;
        target.ThreadCount = source.ThreadCount;
        target.StartTime = source.StartTime;
        target.CpuTimeTicks = source.CpuTimeTicks;
        target.Priority = source.Priority;
        target.MainWindowTitle = source.MainWindowTitle;
        target.IsGroupParent = source.IsGroupParent;
        target.IsGroupChild = source.IsGroupChild;
        target.IsExpanded = source.IsExpanded;
        target.InstanceCount = source.InstanceCount;
        target.Children = source.Children;
        target.DimWhenSuspended = source.DimWhenSuspended;
        target.IsNewlyCreated = source.IsNewlyCreated;
        target.IsTerminating = source.IsTerminating;
    }

    private static ProcessInfo FindRootProcess(
        ProcessInfo process,
        Dictionary<int, ProcessInfo> byPid)
    {
        var current = process;
        var visited = new HashSet<int> { current.Pid };

        while (current.ParentPid > 4 && byPid.TryGetValue(current.ParentPid, out var parent))
        {
            if (!visited.Add(parent.Pid)) break;
            if (SystemLaunchers.Contains(parent.Name)) break;

            if (!IsHelperOf(current, parent))
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    private static bool IsHelperOf(ProcessInfo child, ProcessInfo parent)
    {
        if (child.Name.Equals(parent.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (KnownHelperNames.Contains(child.Name))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(child.ExePath) &&
            !string.IsNullOrEmpty(parent.ExePath) &&
            !child.HasWindow)
        {
            try
            {
                var parentDir = Path.GetDirectoryName(parent.ExePath);
                if (!string.IsNullOrEmpty(parentDir) &&
                    child.ExePath.StartsWith(parentDir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return false;
    }

    private static bool MatchesSearch(ProcessInfo process, string search, bool isPid, int pid) =>
        process.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        process.FriendlyName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        process.ExePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        (isPid && process.Pid == pid);
}
