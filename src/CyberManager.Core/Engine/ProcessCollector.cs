using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CyberManager.Common.Models;

namespace CyberManager.Core.Engine;

[SuppressMessage("Design", "CA1001", Justification = "The collector gate is a process-lifetime synchronization primitive.")]
public sealed class ProcessCollector
{
    private const int InitialBufferSize = 1024 * 1024;
    private const int MaxBufferAttempts = 6;
    private const int MaxPathCacheEntries = 4096;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    private readonly ProcessCpuTracker _cpuTracker = new();
    private readonly ConcurrentDictionary<ProcessCacheKey, string> _pathCache = new();
    private readonly SemaphoreSlim _collectGate = new(1, 1);

    public IReadOnlyList<ProcessInfo> Collect()
    {
        _collectGate.Wait();
        try
        {
            return CollectCore(CancellationToken.None);
        }
        finally
        {
            _collectGate.Release();
        }
    }

    public async Task<IReadOnlyList<ProcessInfo>> CollectAsync(CancellationToken cancellationToken = default)
    {
        await _collectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => CollectCore(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _collectGate.Release();
        }
    }

    private List<ProcessInfo> CollectCore(CancellationToken cancellationToken)
    {
        long curTimestamp = Stopwatch.GetTimestamp();
        // 1. Get All Window Titles in one fast scan (~1ms)
        var windowTitles = GetTopLevelWindowTitles();

        // 2. Query NT Kernel for all processes in 1 single call (~5ms)
        var result = QueryNtProcesses(windowTitles, cancellationToken);

        // 3. Compute Delta CPU % for all processes
        _cpuTracker.Update(result, curTimestamp, Stopwatch.Frequency, Environment.ProcessorCount);

        // Clean dead PIDs from cache
        var currentProcesses = result
            .Select(x => new ProcessCacheKey(x.Pid, GetStartTimeKey(x.StartTime)))
            .ToHashSet();
        foreach (var key in _pathCache.Keys)
        {
            if (!currentProcesses.Contains(key))
            {
                _pathCache.TryRemove(key, out _);
            }
        }

        return result;
    }

    private List<ProcessInfo> QueryNtProcesses(
        Dictionary<int, string> windowTitles,
        CancellationToken cancellationToken)
    {
        var result = new List<ProcessInfo>(500);

        int size = InitialBufferSize;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            int status = 0;
            int returnLength = 0;
            for (var attempt = 0; attempt < MaxBufferAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer = Marshal.AllocHGlobal(size);
                status = NtQuerySystemInformation(5, buffer, size, out returnLength);
                if (status == 0) break;

                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;

                if (status != StatusInfoLengthMismatch)
                {
                    throw new InvalidOperationException(
                        $"NtQuerySystemInformation failed with NTSTATUS 0x{status:X8}.");
                }

                var requestedSize = returnLength > 0 ? returnLength + 128 * 1024 : size * 2;
                size = Math.Max(size * 2, requestedSize);
            }

            if (status != 0 || buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"NtQuerySystemInformation did not provide a large enough buffer after {MaxBufferAttempts} attempts.");
            }

            IntPtr current = buffer;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    try
                    {
                        info.StartTime = DateTime.FromFileTimeUtc(spi.CreateTime);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Trace.WriteLine($"Invalid process start time for PID {pid}: {ex.Message}");
                    }
                }

                // Window Title from fast 1ms cache
                if (windowTitles.TryGetValue(pid, out var title) && !string.IsNullOrWhiteSpace(title))
                {
                    info.MainWindowTitle = title;
                }

                // Exe Path with bounded identity-aware caching
                info.ExePath = GetOrResolveExePath(pid, name, info.StartTime);

                result.Add(info);

                if (spi.NextEntryOffset == 0) break;
                current = IntPtr.Add(current, (int)spi.NextEntryOffset);
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }

        return result;
    }

    private string GetOrResolveExePath(int pid, string processName, DateTime startTime)
    {
        if (pid <= 4) return "";
        var key = new ProcessCacheKey(pid, GetStartTimeKey(startTime));
        if (_pathCache.TryGetValue(key, out var cachedPath))
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
        catch (Exception ex)
        {
            Trace.WriteLine($"Unable to resolve executable path for PID {pid}: {ex.Message}");
        }

        if (string.IsNullOrEmpty(path) && processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback to System32 path if standard system binary
            var sysPath = Path.Combine(Environment.SystemDirectory, processName);
            if (File.Exists(sysPath)) path = sysPath;
        }

        if (_pathCache.Count >= MaxPathCacheEntries)
        {
            foreach (var oldKey in _pathCache.Keys.Take(64))
            {
                _pathCache.TryRemove(oldKey, out _);
            }
        }

        _pathCache[key] = path;
        return path;
    }

    private static long GetStartTimeKey(DateTime startTime) =>
        startTime == default ? 0 : startTime.ToFileTimeUtc();

    private readonly record struct ProcessCacheKey(int Pid, long StartTimeFileTime);

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
                        if (GetWindowThreadProcessId(hWnd, out uint pid) == 0) return true;
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

    [SuppressMessage("Performance", "CA1838", Justification = "StringBuilder is required by this Win32 API.")]
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

    [SuppressMessage("Performance", "CA1838", Justification = "StringBuilder is required by this Win32 API.")]
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, [Out] StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);

    #endregion

}
