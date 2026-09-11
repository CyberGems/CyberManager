using System.Collections.ObjectModel;
using System.ComponentModel;
using CyberManager.Common.Models;

namespace CyberManager.UI.ViewModels;

public sealed record ProcessListQuery(
    string Search,
    bool ShowIdleProcess,
    bool GroupProcesses,
    bool DimSuspended,
    string SortColumn,
    ListSortDirection SortDirection,
    IReadOnlySet<string> ExpandedGroups);

public sealed class ProcessListViewModel
{
    public ObservableCollection<ProcessInfo> Items { get; } = new();

    public static List<ProcessInfo> Build(IReadOnlyList<ProcessInfo> all, ProcessListQuery query)
    {
        IEnumerable<ProcessInfo> filtered = all;
        if (!query.ShowIdleProcess)
        {
            filtered = filtered.Where(process => process.Pid != 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var isPid = int.TryParse(search, out var pid);
            filtered = filtered.Where(process =>
                process.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                process.ExePath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (isPid && process.Pid == pid));
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

            return ApplySorting(filtered, query).ToList();
        }

        var topLevel = new List<ProcessInfo>();
        foreach (var group in filtered.GroupBy(process => process.Name, StringComparer.OrdinalIgnoreCase))
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

            var mainProcess = processes.FirstOrDefault(process => process.HasWindow) ??
                              processes.OrderBy(process => process.Pid).First();
            var parent = new ProcessInfo
            {
                Pid = mainProcess.Pid,
                ParentPid = mainProcess.ParentPid,
                Name = group.Key,
                ExePath = !string.IsNullOrEmpty(mainProcess.ExePath)
                    ? mainProcess.ExePath
                    : processes.FirstOrDefault(process => !string.IsNullOrEmpty(process.ExePath))?.ExePath ?? "",
                UserName = mainProcess.UserName,
                Status = processes.Any(process => process.Status == "Running") ? "Running" : "Suspended",
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

        var result = new List<ProcessInfo>();
        foreach (var item in ApplySorting(topLevel, query))
        {
            result.Add(item);
            if (item.IsGroupParent && item.IsExpanded)
            {
                result.AddRange(item.Children);
            }
        }

        return result;
    }

    public void Update(IReadOnlyList<ProcessInfo> next)
    {
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

    private static bool HasSameIdentity(ProcessInfo current, ProcessInfo next) =>
        current.IsGroupParent == next.IsGroupParent &&
        (current.IsGroupParent
            ? current.Name.Equals(next.Name, StringComparison.OrdinalIgnoreCase)
            : current.Pid == next.Pid);

    private static IEnumerable<ProcessInfo> ApplySorting(
        IEnumerable<ProcessInfo> list,
        ProcessListQuery query)
    {
        var ascending = query.SortDirection == ListSortDirection.Ascending;
        return query.SortColumn switch
        {
            "Name" => ascending ? list.OrderBy(process => process.Name).ThenBy(process => process.Pid) : list.OrderByDescending(process => process.Name).ThenBy(process => process.Pid),
            "Pid" => ascending ? list.OrderBy(process => process.Pid) : list.OrderByDescending(process => process.Pid),
            "CpuPercent" => ascending ? list.OrderBy(process => process.CpuPercent).ThenBy(process => process.Name) : list.OrderByDescending(process => process.CpuPercent).ThenBy(process => process.Name),
            "WorkingSetBytes" => ascending ? list.OrderBy(process => process.WorkingSetBytes).ThenBy(process => process.Name) : list.OrderByDescending(process => process.WorkingSetBytes).ThenBy(process => process.Name),
            "ThreadCount" => ascending ? list.OrderBy(process => process.ThreadCount).ThenBy(process => process.Name) : list.OrderByDescending(process => process.ThreadCount).ThenBy(process => process.Name),
            "Priority" => ascending ? list.OrderBy(process => process.Priority).ThenBy(process => process.Name) : list.OrderByDescending(process => process.Priority).ThenBy(process => process.Name),
            "ExePath" => ascending
                ? list.OrderBy(process => string.IsNullOrWhiteSpace(process.ExePath) ? 1 : 0).ThenBy(process => process.ExePath).ThenBy(process => process.Name)
                : list.OrderBy(process => string.IsNullOrWhiteSpace(process.ExePath) ? 1 : 0).ThenByDescending(process => process.ExePath).ThenBy(process => process.Name),
            _ => list.OrderByDescending(process => process.CpuPercent).ThenBy(process => process.Name)
        };
    }

    private static void CopyProcessInfo(ProcessInfo target, ProcessInfo source)
    {
        target.Pid = source.Pid;
        target.ParentPid = source.ParentPid;
        target.Name = source.Name;
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
    }
}
