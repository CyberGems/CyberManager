using System.Diagnostics;
using System.Runtime.InteropServices;
using CyberManager.Common.Models;

namespace CyberManager.Core.Engine;

public sealed class ProcessCollector
{
    private readonly Dictionary<int, long> _prevCpu = new();
    private readonly Dictionary<int, long> _prevTime = new();
    private long _prevIdle;
    private long _prevTotal;

    public IReadOnlyList<ProcessInfo> Collect()
    {
        var result = new List<ProcessInfo>(350);
        var now = Environment.TickCount64;
        var procs = Process.GetProcesses();
        long totalCpu = 0;
        long idleCpu = 0;

        foreach (var p in procs)
        {
            try
            {
                var info = new ProcessInfo
                {
                    Pid = p.Id,
                    Name = p.ProcessName,
                    WorkingSetBytes = p.WorkingSet64,
                    PrivateBytes = p.PrivateMemorySize64,
                    ThreadCount = p.Threads.Count,
                    StartTime = SafeStart(p),
                    ExePath = SafePath(p),
                    Status = "Running",
                    CpuTimeTicks = p.TotalProcessorTime.Ticks
                };
                try { info.ParentPid = GetParentPid(p.Id); } catch { }
                result.Add(info);
                totalCpu += info.CpuTimeTicks;
                if (p.ProcessName.Equals("Idle", StringComparison.OrdinalIgnoreCase)) idleCpu = info.CpuTimeTicks;
            }
            catch { }
            finally { p.Dispose(); }
        }

        long curTotal = totalCpu;
        long curIdle = idleCpu;
        long deltaTotal = curTotal - _prevTotal;
        long deltaIdle = curIdle - _prevIdle;

        if (deltaTotal > 0 && _prevTotal != 0)
        {
            foreach (var r in result)
            {
                if (_prevCpu.TryGetValue(r.Pid, out var prev))
                {
                    long delta = r.CpuTimeTicks - prev;
                    r.CpuPercent = Math.Max(0, (double)delta / deltaTotal * 100 * Environment.ProcessorCount);
                    if (r.CpuPercent > 100) r.CpuPercent = 100;
                }
                _prevCpu[r.Pid] = r.CpuTimeTicks;
            }
        }
        else
        {
            foreach (var r in result) _prevCpu[r.Pid] = r.CpuTimeTicks;
        }

        _prevTotal = curTotal;
        _prevIdle = curIdle;

        var dead = _prevCpu.Keys.Except(result.Select(x => x.Pid)).ToList();
        foreach (var d in dead) _prevCpu.Remove(d);

        return result;
    }

    private static DateTime SafeStart(Process p)
    {
        try { return p.StartTime; } catch { return DateTime.MinValue; }
    }

    private static string SafePath(Process p)
    {
        try { return p.MainModule?.FileName ?? ""; } catch { return ""; }
    }

    private static int GetParentPid(int pid)
    {
        var handle = OpenProcess(0x0400, false, pid);
        if (handle == IntPtr.Zero) return 0;
        try
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            int len = 0;
            if (NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), ref len) == 0)
                return (int)pbi.InheritedFromUniqueProcessId;
        }
        finally { CloseHandle(handle); }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr h, int cls, ref PROCESS_BASIC_INFORMATION pbi, int len, ref int retLen);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);
}
