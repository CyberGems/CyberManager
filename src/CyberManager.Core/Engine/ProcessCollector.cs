using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CyberManager.Common.Models;

namespace CyberManager.Core.Engine;

public sealed class ProcessCollector
{
    private readonly Dictionary<int, long> _prevCpu = new();
    private readonly ConcurrentDictionary<int, string> _pathCache = new();
    private long _prevTimestamp;

    public IReadOnlyList<ProcessInfo> Collect()
    {
        long curTimestamp = Stopwatch.GetTimestamp();
        double elapsedSeconds = _prevTimestamp == 0 ? 0 : (double)(curTimestamp - _prevTimestamp) / Stopwatch.Frequency;
        int processorCount = Math.Max(1, Environment.ProcessorCount);

        // 1. Get All Window Titles in one fast scan (~1ms)
        var windowTitles = GetTopLevelWindowTitles();

        // 2. Query NT Kernel for all processes in 1 single call (~5ms)
        var result = QueryNtProcesses(windowTitles);

        // 3. Compute Delta CPU % for all processes
        if (elapsedSeconds > 0 && _prevCpu.Count > 0)
        {
            foreach (var r in result)
            {
                if (_prevCpu.TryGetValue(r.Pid, out var prevTicks) && r.CpuTimeTicks >= prevTicks)
                {
                    long delta = r.CpuTimeTicks - prevTicks;
                    double cpuSecs = delta / 10_000_000.0;
                    double pct = (cpuSecs / (elapsedSeconds * processorCount)) * 100.0;
                    r.CpuPercent = Math.Clamp(pct, 0.0, 100.0);
                }
                _prevCpu[r.Pid] = r.CpuTimeTicks;
            }
        }
        else
        {
            foreach (var r in result)
            {
                _prevCpu[r.Pid] = r.CpuTimeTicks;
            }
        }

        _prevTimestamp = curTimestamp;

        // Clean dead PIDs from cache
        var currentPids = new HashSet<int>(result.Select(x => x.Pid));
        var dead = _prevCpu.Keys.Where(pid => !currentPids.Contains(pid)).ToList();
        foreach (var d in dead)
        {
            _prevCpu.Remove(d);
            _pathCache.TryRemove(d, out _);
        }

        return result;
    }

    public async Task<IReadOnlyList<ProcessInfo>> CollectAsync()
    {
        return await Task.Run(() => Collect()).ConfigureAwait(false);
    }

    private List<ProcessInfo> QueryNtProcesses(Dictionary<int, string> windowTitles)
    {
        var result = new List<ProcessInfo>(500);

        int size = 1024 * 1024; // 1 MB initial buffer
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            int returnLength;
            int status = NtQuerySystemInformation(5, buffer, size, out returnLength); // 5 = SystemProcessInformation
            if (status == -1073741820) // STATUS_INFO_LENGTH_MISMATCH
            {
                Marshal.FreeHGlobal(buffer);
                size = returnLength + 128 * 1024;
                buffer = Marshal.AllocHGlobal(size);
                status = NtQuerySystemInformation(5, buffer, size, out returnLength);
            }

            if (status == 0)
            {
                IntPtr current = buffer;
                while (true)
                {
                    var spi = Marshal.PtrToStructure<SYSTEM_PROCESS_INFORMATION>(current);
                    int pid = spi.UniqueProcessId.ToInt32();
                    int ppid = spi.InheritedFromUniqueProcessId.ToInt32();

                    string name = "";
                    if (spi.ImageName.Buffer != IntPtr.Zero && spi.ImageName.Length > 0)
                    {
                        name = Marshal.PtrToStringUni(spi.ImageName.Buffer, spi.ImageName.Length / 2) ?? "";
                    }
                    else if (pid == 0)
                    {
                        name = "System Idle Process";
                    }
                    else if (pid == 4)
                    {
                        name = "System";
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        name = $"PID_{pid}";
                    }

                    var info = new ProcessInfo
                    {
                        Pid = pid,
                        ParentPid = ppid,
                        Name = name,
                        Status = "Running",
                        ThreadCount = (int)spi.NumberOfThreads,
                        WorkingSetBytes = (long)spi.WorkingSetSize,
                        PrivateBytes = (long)spi.PrivatePageCount,
                        CpuTimeTicks = spi.UserTime + spi.KernelTime,
                        Priority = MapBasePriority(spi.BasePriority)
                    };

                    // Safe Start Time from NT CreateTime without throwing Win32Exception
                    if (spi.CreateTime > 0)
                    {
                        try { info.StartTime = DateTime.FromFileTimeUtc(spi.CreateTime); } catch { }
                    }

                    // Window Title from fast 1ms cache
                    if (windowTitles.TryGetValue(pid, out var title) && !string.IsNullOrWhiteSpace(title))
                    {
                        info.MainWindowTitle = title;
                    }

                    // Exe Path with zero-allocation caching
                    info.ExePath = GetOrResolveExePath(pid, name);

                    result.Add(info);

                    if (spi.NextEntryOffset == 0) break;
                    current = IntPtr.Add(current, (int)spi.NextEntryOffset);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return result;
    }

    private string GetOrResolveExePath(int pid, string processName)
    {
        if (pid <= 4) return "";
        if (_pathCache.TryGetValue(pid, out var cachedPath))
        {
            return cachedPath;
        }

        string path = "";
        try
        {
            // PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
            var handle = OpenProcess(0x1000, false, pid);
            if (handle != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (QueryFullProcessImageName(handle, 0, sb, ref size))
                    {
                        path = sb.ToString();
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }
        catch { }

        if (string.IsNullOrEmpty(path) && processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback to System32 path if standard system binary
            var sysPath = Path.Combine(Environment.SystemDirectory, processName);
            if (File.Exists(sysPath)) path = sysPath;
        }

        _pathCache[pid] = path;
        return path;
    }

    private static ProcessPriorityClass MapBasePriority(int basePriority)
    {
        return basePriority switch
        {
            <= 4 => ProcessPriorityClass.Idle,
            <= 6 => ProcessPriorityClass.BelowNormal,
            <= 8 => ProcessPriorityClass.Normal,
            <= 10 => ProcessPriorityClass.AboveNormal,
            <= 13 => ProcessPriorityClass.High,
            _ => ProcessPriorityClass.RealTime
        };
    }

    #region Window Title Enumeration

    private static Dictionary<int, string> GetTopLevelWindowTitles()
    {
        var dict = new Dictionary<int, string>();
        EnumWindows((hWnd, _) =>
        {
            if (IsWindowVisible(hWnd))
            {
                int len = GetWindowTextLength(hWnd);
                if (len > 0)
                {
                    var sb = new StringBuilder(len + 1);
                    if (GetWindowText(hWnd, sb, sb.Capacity) > 0)
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        if (pid > 0 && !dict.ContainsKey((int)pid))
                        {
                            var title = sb.ToString();
                            if (!string.IsNullOrWhiteSpace(title))
                            {
                                dict[(int)pid] = title;
                            }
                        }
                    }
                }
            }
            return true;
        }, IntPtr.Zero);
        return dict;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion

    #region NT Process Structs & P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_PROCESS_INFORMATION
    {
        public uint NextEntryOffset;
        public uint NumberOfThreads;
        public long WorkingSetPrivateSize;
        public uint HardFaultCount;
        public uint NumberOfThreadsHighWatermark;
        public ulong CycleTime;
        public long CreateTime;
        public long UserTime;
        public long KernelTime;
        public UNICODE_STRING ImageName;
        public int BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
        public uint HandleCount;
        public uint SessionId;
        public UIntPtr UniqueProcessKey;
        public UIntPtr PeakVirtualSize;
        public UIntPtr VirtualSize;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivatePageCount;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, [Out] StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    #endregion
}
