using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CyberManager.Common.Models;

public sealed class ProcessInfo : INotifyPropertyChanged
{
    private int _pid;
    private int _parentPid;
    private string _name = "";
    private string _exePath = "";
    private string _userName = "";
    private string _status = "Running";
    private double _cpuPercent;
    private long _workingSetBytes;
    private long _privateBytes;
    private int _threadCount;
    private DateTime _startTime;
    private long _cpuTimeTicks;
    private ProcessPriorityClass _priority;
    private string _mainWindowTitle = "";
    private bool _isGroupParent;
    private bool _isGroupChild;
    private bool _isExpanded;
    private int _instanceCount = 1;
    private List<ProcessInfo> _children = new();
    private bool _dimWhenSuspended;
    private bool _isContextTarget;

    public int Pid { get => _pid; set => Set(ref _pid, value); }
    public int ParentPid { get => _parentPid; set => Set(ref _parentPid, value); }
    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value)) OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string ExePath { get => _exePath; set => Set(ref _exePath, value); }
    public string UserName { get => _userName; set => Set(ref _userName, value); }
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public double CpuPercent
    {
        get => _cpuPercent;
        set
        {
            if (Set(ref _cpuPercent, value)) OnPropertyChanged(nameof(CpuFormatted));
        }
    }

    public long WorkingSetBytes
    {
        get => _workingSetBytes;
        set
        {
            if (Set(ref _workingSetBytes, value)) OnPropertyChanged(nameof(MemoryFormatted));
        }
    }

    public long PrivateBytes { get => _privateBytes; set => Set(ref _privateBytes, value); }
    public int ThreadCount { get => _threadCount; set => Set(ref _threadCount, value); }
    public DateTime StartTime { get => _startTime; set => Set(ref _startTime, value); }
    public long CpuTimeTicks { get => _cpuTimeTicks; set => Set(ref _cpuTimeTicks, value); }

    public ProcessPriorityClass Priority
    {
        get => _priority;
        set
        {
            if (Set(ref _priority, value)) OnPropertyChanged(nameof(PriorityFormatted));
        }
    }

    public string MainWindowTitle
    {
        get => _mainWindowTitle;
        set
        {
            if (Set(ref _mainWindowTitle, value))
            {
                OnPropertyChanged(nameof(HasWindow));
                OnPropertyChanged(nameof(RoleBadge));
            }
        }
    }

    public bool HasWindow => !string.IsNullOrWhiteSpace(MainWindowTitle);

    public bool IsGroupParent
    {
        get => _isGroupParent;
        set
        {
            if (Set(ref _isGroupParent, value)) OnPropertyChanged(nameof(DisplayName));
        }
    }

    public bool IsGroupChild
    {
        get => _isGroupChild;
        set
        {
            if (Set(ref _isGroupChild, value)) OnPropertyChanged(nameof(RoleBadge));
        }
    }

    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    public int InstanceCount
    {
        get => _instanceCount;
        set
        {
            if (Set(ref _instanceCount, value)) OnPropertyChanged(nameof(DisplayName));
        }
    }

    public List<ProcessInfo> Children { get => _children; set => Set(ref _children, value); }

    public bool DimWhenSuspended { get => _dimWhenSuspended; set => Set(ref _dimWhenSuspended, value); }

    public bool IsContextTarget { get => _isContextTarget; set => Set(ref _isContextTarget, value); }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
