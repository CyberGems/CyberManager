using System.Diagnostics;
using System.Globalization;

namespace CyberManager.Common.Models;

public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public int ParentPid { get; set; }
    public string Name { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Status { get; set; } = "Running";
    public double CpuPercent { get; set; }
    public long WorkingSetBytes { get; set; }
    public long PrivateBytes { get; set; }
    public int ThreadCount { get; set; }
    public DateTime StartTime { get; set; }
    public long CpuTimeTicks { get; set; }
    public ProcessPriorityClass Priority { get; set; }

    public string MainWindowTitle { get; set; } = "";
    public bool HasWindow => !string.IsNullOrWhiteSpace(MainWindowTitle);
    public bool IsGroupParent { get; set; }
    public bool IsGroupChild { get; set; }
    public bool IsExpanded { get; set; }
    public int InstanceCount { get; set; } = 1;
    public List<ProcessInfo> Children { get; set; } = new();

    public string DisplayName => InstanceCount > 1 && IsGroupParent ? $"{Name} ({InstanceCount})" : Name;

    public string RoleBadge => HasWindow ? "UI" : (IsGroupChild ? "Worker" : "");

    public string MemoryFormatted
    {
        get
        {
            double mb = WorkingSetBytes / (1024.0 * 1024.0);
            return mb >= 1024 ? $"{mb / 1024:F1} GB" : $"{mb:F0} MB";
        }
    }

    public string CpuFormatted => CpuPercent.ToString("F1", CultureInfo.CurrentCulture) + "%";

    public string PriorityFormatted => Priority.ToString();
}
