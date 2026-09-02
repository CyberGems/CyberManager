using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CyberManager.Core.Engine;

public sealed class SystemMetricsSnapshot
{
    public double CpuTotalPercent { get; set; }
    public double CpuKernelPercent { get; set; }
    public double CpuUserPercent { get; set; }

    public long TotalPhysicalBytes { get; set; }
    public long AvailablePhysicalBytes { get; set; }
    public long UsedPhysicalBytes { get; set; }
    public double MemoryLoadPercent { get; set; }

    public long CommitTotalBytes { get; set; }
    public long CommitLimitBytes { get; set; }
    public long CommitPeakBytes { get; set; }

    public long PagedPoolBytes { get; set; }
    public long NonPagedPoolBytes { get; set; }

    public int HandleCount { get; set; }
    public int ProcessCount { get; set; }
    public int ThreadCount { get; set; }

    public int LogicalProcessors { get; set; }
    public int PhysicalCores { get; set; }
    public int Sockets { get; set; }
    public string CpuModelName { get; set; } = "";

    public double UsedRamGb => UsedPhysicalBytes / (1024.0 * 1024.0 * 1024.0);
    public double TotalRamGb => TotalPhysicalBytes / (1024.0 * 1024.0 * 1024.0);
    public double AvailableRamGb => AvailablePhysicalBytes / (1024.0 * 1024.0 * 1024.0);

    public double CommitTotalGb => CommitTotalBytes / (1024.0 * 1024.0 * 1024.0);
    public double CommitLimitGb => CommitLimitBytes / (1024.0 * 1024.0 * 1024.0);
    public double CommitPeakGb => CommitPeakBytes / (1024.0 * 1024.0 * 1024.0);

    public double PagedPoolMb => PagedPoolBytes / (1024.0 * 1024.0);
    public double NonPagedPoolMb => NonPagedPoolBytes / (1024.0 * 1024.0);
}

public sealed class SystemMetricsCollector
{
    private static readonly Lazy<SystemMetricsCollector> _instance = new(() => new SystemMetricsCollector());
    public static SystemMetricsCollector Instance => _instance.Value;

    private readonly object _lock = new();
    private const int HistoryCapacity = 60;

    private readonly float[] _cpuTotalHistory = new float[HistoryCapacity];
    private readonly float[] _cpuKernelHistory = new float[HistoryCapacity];
    private readonly float[] _cpuUserHistory = new float[HistoryCapacity];
    private readonly float[] _ramPercentHistory = new float[HistoryCapacity];
    private readonly float[] _ramUsedGbHistory = new float[HistoryCapacity];
    private readonly float[] _commitGbHistory = new float[HistoryCapacity];

    private int _historyCount;
    private int _historyHead;

    private long _prevIdleTime;
    private long _prevKernelTime;
    private long _prevUserTime;
    private bool _hasPrevTimes;

    private readonly int _logicalProcessors;
    private int _physicalCores;
    private int _sockets;
    private string _cpuModelName = "";

    public SystemMetricsSnapshot Latest { get; private set; } = new();

    public SystemMetricsCollector()
    {
        _logicalProcessors = Math.Max(1, Environment.ProcessorCount);
        InitTopology();
    }

    private void InitTopology()
    {
        try
        {
            _cpuModelName = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null)?.ToString()?.Trim() ?? "";
        }
        catch { }

        if (string.IsNullOrEmpty(_cpuModelName))
        {
            _cpuModelName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "x64 Processor";
        }

        _sockets = 1;
        _physicalCores = Math.Max(1, _logicalProcessors / 2);
        try
        {
            int coreCount = 0;
            int socketCount = 0;
            GetProcessorInformation(out coreCount, out socketCount);
            if (coreCount > 0) _physicalCores = coreCount;
            if (socketCount > 0) _sockets = socketCount;
        }
        catch { }
    }

    public SystemMetricsSnapshot Sample(int fallbackProcessCount = 0)
    {
        var snap = new SystemMetricsSnapshot
        {
            LogicalProcessors = _logicalProcessors,
            PhysicalCores = _physicalCores,
            Sockets = _sockets,
            CpuModelName = _cpuModelName
        };

        // 1. CPU Times (User vs Kernel)
        if (GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
        {
            long idle = FileTimeToLong(idleFt);
            long kernel = FileTimeToLong(kernelFt);
            long user = FileTimeToLong(userFt);

            if (_hasPrevTimes)
            {
                long idleDelta = idle - _prevIdleTime;
                long kernelDelta = kernel - _prevKernelTime;
                long userDelta = user - _prevUserTime;

                long totalDelta = kernelDelta + userDelta;
                if (totalDelta > 0)
                {
                    // Total CPU = (1 - idleDelta / totalDelta)
                    double totalPct = (1.0 - (double)idleDelta / totalDelta) * 100.0;
                    // Kernel time reported by GetSystemTimes includes idle time!
                    long actualKernelDelta = Math.Max(0, kernelDelta - idleDelta);
                    double kernelPct = ((double)actualKernelDelta / totalDelta) * 100.0;
                    double userPct = ((double)userDelta / totalDelta) * 100.0;

                    snap.CpuTotalPercent = Math.Clamp(totalPct, 0.0, 100.0);
                    snap.CpuKernelPercent = Math.Clamp(kernelPct, 0.0, snap.CpuTotalPercent);
                    snap.CpuUserPercent = Math.Clamp(userPct, 0.0, 100.0);
                }
            }

            _prevIdleTime = idle;
            _prevKernelTime = kernel;
            _prevUserTime = user;
            _hasPrevTimes = true;
        }

        // 2. Global Memory Status Ex
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            snap.TotalPhysicalBytes = (long)memStatus.ullTotalPhys;
            snap.AvailablePhysicalBytes = (long)memStatus.ullAvailPhys;
            snap.UsedPhysicalBytes = Math.Max(0, snap.TotalPhysicalBytes - snap.AvailablePhysicalBytes);
            snap.MemoryLoadPercent = memStatus.dwMemoryLoad;
            snap.CommitTotalBytes = (long)memStatus.ullTotalPageFile - (long)memStatus.ullAvailPageFile;
            snap.CommitLimitBytes = (long)memStatus.ullTotalPageFile;
        }

        // 3. Performance Info
        var perfInfo = new PERFORMANCE_INFORMATION { cb = Marshal.SizeOf<PERFORMANCE_INFORMATION>() };
        if (GetPerformanceInfo(out perfInfo, perfInfo.cb))
        {
            snap.HandleCount = (int)perfInfo.HandleCount;
            snap.ProcessCount = (int)perfInfo.ProcessCount;
            snap.ThreadCount = (int)perfInfo.ThreadCount;

            long pageSize = (long)perfInfo.PageSize;
            if (pageSize <= 0) pageSize = 4096;

            snap.PagedPoolBytes = (long)perfInfo.KernelPaged * pageSize;
            snap.NonPagedPoolBytes = (long)perfInfo.KernelNonpaged * pageSize;
            snap.CommitTotalBytes = (long)perfInfo.CommitTotal * pageSize;
            snap.CommitLimitBytes = (long)perfInfo.CommitLimit * pageSize;
            snap.CommitPeakBytes = (long)perfInfo.CommitPeak * pageSize;
        }
        else if (fallbackProcessCount > 0)
        {
            snap.ProcessCount = fallbackProcessCount;
        }

        // 4. Update Ring Buffer
        lock (_lock)
        {
            _cpuTotalHistory[_historyHead] = (float)snap.CpuTotalPercent;
            _cpuKernelHistory[_historyHead] = (float)snap.CpuKernelPercent;
            _cpuUserHistory[_historyHead] = (float)snap.CpuUserPercent;
            _ramPercentHistory[_historyHead] = (float)snap.MemoryLoadPercent;
            _ramUsedGbHistory[_historyHead] = (float)snap.UsedRamGb;
            _commitGbHistory[_historyHead] = (float)snap.CommitTotalGb;

            _historyHead = (_historyHead + 1) % HistoryCapacity;
            if (_historyCount < HistoryCapacity) _historyCount++;
            Latest = snap;
        }

        return snap;
    }

    public (float[] Total, float[] Kernel) GetCpuHistory()
    {
        lock (_lock)
        {
            int count = _historyCount;
            var total = new float[count];
            var kernel = new float[count];

            int start = (_historyHead - count + HistoryCapacity) % HistoryCapacity;
            for (int i = 0; i < count; i++)
            {
                int idx = (start + i) % HistoryCapacity;
                total[i] = _cpuTotalHistory[idx];
                kernel[i] = _cpuKernelHistory[idx];
            }
            return (total, kernel);
        }
    }

    public (float[] UsedGb, float[] Percent) GetRamHistory()
    {
        lock (_lock)
        {
            int count = _historyCount;
            var gb = new float[count];
            var pct = new float[count];

            int start = (_historyHead - count + HistoryCapacity) % HistoryCapacity;
            for (int i = 0; i < count; i++)
            {
                int idx = (start + i) % HistoryCapacity;
                gb[i] = _ramUsedGbHistory[idx];
                pct[i] = _ramPercentHistory[idx];
            }
            return (gb, pct);
        }
    }

    public float[] GetCommitHistory()
    {
        lock (_lock)
        {
            int count = _historyCount;
            var commit = new float[count];
            int start = (_historyHead - count + HistoryCapacity) % HistoryCapacity;
            for (int i = 0; i < count; i++)
            {
                int idx = (start + i) % HistoryCapacity;
                commit[i] = _commitGbHistory[idx];
            }
            return commit;
        }
    }

    private static long FileTimeToLong(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    private static void GetProcessorInformation(out int cores, out int sockets)
    {
        cores = 0;
        sockets = 0;
        uint returnLength = 0;
        GetLogicalProcessorInformation(IntPtr.Zero, ref returnLength);
        if (returnLength == 0) return;

        int structSize = Marshal.SizeOf<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>();
        int count = (int)(returnLength / structSize);
        IntPtr buffer = Marshal.AllocHGlobal((int)returnLength);
        try
        {
            if (GetLogicalProcessorInformation(buffer, ref returnLength))
            {
                for (int i = 0; i < count; i++)
                {
                    IntPtr ptr = IntPtr.Add(buffer, i * structSize);
                    var info = Marshal.PtrToStructure<SYSTEM_LOGICAL_PROCESSOR_INFORMATION>(ptr);
                    if (info.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                    {
                        cores++;
                    }
                    else if (info.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorPackage)
                    {
                        sockets++;
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    #region Win32 P/Invoke

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public int cb;
        public IntPtr CommitTotal;
        public IntPtr CommitLimit;
        public IntPtr CommitPeak;
        public IntPtr PhysicalTotal;
        public IntPtr PhysicalAvailable;
        public IntPtr SystemCache;
        public IntPtr KernelTotal;
        public IntPtr KernelPaged;
        public IntPtr KernelNonpaged;
        public IntPtr PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, int cb);

    private enum LOGICAL_PROCESSOR_RELATIONSHIP
    {
        RelationProcessorCore = 0,
        RelationNumaNode = 1,
        RelationCache = 2,
        RelationProcessorPackage = 3,
        RelationGroup = 4,
        RelationAll = 0xffff
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
    {
        public UIntPtr ProcessorMask;
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public ProcessorCoreUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct ProcessorCoreUnion
    {
        [FieldOffset(0)]
        public byte Flags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnedLength);

    #endregion
}
