using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CyberManager.Core.Engine;

public static class ProcessActions
{
    public static bool IsElevated
    {
        get
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    public static bool Kill(int pid)
    {
        try
        {
            var h = OpenProcess(0x0001 /* PROCESS_TERMINATE */, false, pid);
            if (h != IntPtr.Zero)
            {
                try
                {
                    if (TerminateProcess(h, 1)) return true;
                }
                finally
                {
                    CloseHandle(h);
                }
            }

            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: false);
            return true;
        }
        catch { return false; }
    }

    public static bool KillTree(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return Kill(pid);
        }
    }

    public static bool Suspend(int pid)
    {
        try
        {
            var h = OpenProcess(0x0800, false, pid);
            if (h == IntPtr.Zero) return false;
            try { return NtSuspendProcess(h) == 0; } finally { CloseHandle(h); }
        }
        catch { return false; }
    }

    public static bool Resume(int pid)
    {
        try
        {
            var h = OpenProcess(0x0800, false, pid);
            if (h == IntPtr.Zero) return false;
            try { return NtResumeProcess(h) == 0; } finally { CloseHandle(h); }
        }
        catch { return false; }
    }

    public static bool SetPriority(int pid, ProcessPriorityClass priority)
    {
        try
        {
            // PROCESS_SET_INFORMATION = 0x0200
            var h = OpenProcess(0x0200, false, pid);
            if (h != IntPtr.Zero)
            {
                try
                {
                    uint win32Priority = priority switch
                    {
                        ProcessPriorityClass.Idle => 0x00000040,
                        ProcessPriorityClass.BelowNormal => 0x00004000,
                        ProcessPriorityClass.Normal => 0x00000020,
                        ProcessPriorityClass.AboveNormal => 0x00008000,
                        ProcessPriorityClass.High => 0x00000080,
                        ProcessPriorityClass.RealTime => 0x00000100,
                        _ => 0x00000020
                    };
                    if (SetPriorityClass(h, win32Priority))
                        return true;
                }
                finally
                {
                    CloseHandle(h);
                }
            }

            using var p = Process.GetProcessById(pid);
            p.PriorityClass = priority;
            return true;
        }
        catch { return false; }
    }

    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr h);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(int acc, bool inh, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
}
