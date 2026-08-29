using System.Diagnostics;
using System.Runtime.InteropServices;
using CyberManager.Common.Models;

namespace CyberManager.Core.Engine;

public sealed class ProcessCollector
{
    private readonly Dictionary<int, long> _prevCpu = new();
    private long _prevTimestamp;

    public IReadOnlyList<ProcessInfo> Collect()
    {
        var result = new List<ProcessInfo>(350);
        long curTimestamp = Stopwatch.GetTimestamp();
        double elapsedSeconds = _prevTimestamp == 0 ? 0 : (double)(curTimestamp - _prevTimestamp) / Stopwatch.Frequency;
        int processorCount = Math.Max(1, Environment.ProcessorCount);

        var procs = Process.GetProcesses();
        foreach (var p in procs)
        {
            try
            {
                var info = new ProcessInfo
                {
                    Pid = p.Id,
                    Name = p.ProcessName,
                    Status = "Running"
                };

                info.CpuTimeTicks = SafeCpuTicks(p);
                info.WorkingSetBytes = SafeWorkingSet(p);
                info.PrivateBytes = SafePrivateBytes(p);
                info.ThreadCount = SafeThreadCount(p);
                info.StartTime = SafeStart(p);
                info.ExePath = SafePath(p);
                info.Priority = SafePriority(p);
                info.MainWindowTitle = SafeMainWindowTitle(p);
                try { info.ParentPid = GetParentPid(p.Id); } catch { }

                result.Add(info);
            }
            catch
            {
                // Ignore any completely unreadable or already exited process
            }
            finally
            {
                p.Dispose();
            }
        }

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

        var currentPids = new HashSet<int>(result.Select(x => x.Pid));
        var dead = _prevCpu.Keys.Where(pid => !currentPids.Contains(pid)).ToList();
        foreach (var d in dead)
        {
            _prevCpu.Remove(d);
        }

        return result;
    }

    public async Task<IReadOnlyList<ProcessInfo>> CollectAsync()
    {
        return await Task.Run(() => Collect()).ConfigureAwait(false);
    }

    private static long SafeCpuTicks(Process p)
    {
        try { return p.TotalProcessorTime.Ticks; } catch { return 0; }
    }

    private static long SafeWorkingSet(Process p)
    {
        try { return p.WorkingSet64; } catch { return 0; }
    }

    private static long SafePrivateBytes(Process p)
    {
        try { return p.PrivateMemorySize64; } catch { return 0; }
    }

    private static int SafeThreadCount(Process p)
    {
        try { return p.Threads.Count; } catch { return 0; }
    }

    private static ProcessPriorityClass SafePriority(Process p)
    {
        try { return p.PriorityClass; } catch { return ProcessPriorityClass.Normal; }
    }

    private static DateTime SafeStart(Process p)
    {
        try { return p.StartTime; } catch { return DateTime.MinValue; }
    }

    private static string SafePath(Process p)
    {
        try { return p.MainModule?.FileName ?? ""; } catch { return ""; }
    }

    private static string SafeMainWindowTitle(Process p)
    {
        try { return p.MainWindowTitle ?? ""; } catch { return ""; }
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
