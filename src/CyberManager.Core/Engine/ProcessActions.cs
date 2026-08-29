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
        try { using var p = Process.GetProcessById(pid); p.Kill(entireProcessTree: false); return true; } catch { return false; }
    }

    public static bool KillTree(int pid)
    {
        try { using var p = Process.GetProcessById(pid); p.Kill(entireProcessTree: true); return true; } catch { return false; }
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
            using var p = Process.GetProcessById(pid);
            p.PriorityClass = priority;
            return true;
        }
        catch { return false; }
    }

    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr h);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(int acc, bool inh, int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
}
