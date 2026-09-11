using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CyberManager.Core.Engine;

public static class ProcessActions
{
    public readonly record struct ActionResult(
        bool Succeeded,
        string? ErrorMessage = null,
        bool UsedFallback = false);

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
        return TryKill(pid).Succeeded;
    }

    public static ActionResult TryKill(int pid)
    {
        if (pid <= 0) return Failure("Invalid process ID.");

        try
        {
            var h = OpenProcess(0x0001 /* PROCESS_TERMINATE */, false, pid);
            if (h != IntPtr.Zero)
            {
                try
                {
                    if (TerminateProcess(h, 1)) return Success();
                }
                finally
                {
                    CloseHandle(h);
                }
            }

            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: false);
            return Success();
        }
        catch (Exception ex) { return Failure(ex.Message); }
    }

    public static bool KillTree(int pid)
    {
        return TryKillTree(pid).Succeeded;
    }

    public static ActionResult TryKillTree(int pid)
    {
        if (pid <= 0) return Failure("Invalid process ID.");

        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            return Success();
        }
        catch (Exception treeException)
        {
            var fallback = TryKill(pid);
            return fallback.Succeeded
                ? fallback with
                {
                    UsedFallback = true,
                    ErrorMessage = $"Process tree termination was unavailable; only the root process was terminated. {treeException.Message}"
                }
                : Failure(treeException.Message);
        }
    }

    public static bool Suspend(int pid)
    {
        return TrySuspend(pid).Succeeded;
    }

    public static ActionResult TrySuspend(int pid)
    {
        if (pid <= 0) return Failure("Invalid process ID.");
        try
        {
            var h = OpenProcess(0x0800, false, pid);
            if (h == IntPtr.Zero) return Failure(GetLastWin32ErrorMessage());
            try
            {
                var status = NtSuspendProcess(h);
                return status == 0
                    ? Success()
                    : Failure($"NtSuspendProcess failed with status {status}.");
            }
            finally { CloseHandle(h); }
        }
        catch (Exception ex) { return Failure(ex.Message); }
    }

    public static bool Resume(int pid)
    {
        return TryResume(pid).Succeeded;
    }

    public static ActionResult TryResume(int pid)
    {
        if (pid <= 0) return Failure("Invalid process ID.");
        try
        {
            var h = OpenProcess(0x0800, false, pid);
            if (h == IntPtr.Zero) return Failure(GetLastWin32ErrorMessage());
            try
            {
                var status = NtResumeProcess(h);
                return status == 0
                    ? Success()
                    : Failure($"NtResumeProcess failed with status {status}.");
            }
            finally { CloseHandle(h); }
        }
        catch (Exception ex) { return Failure(ex.Message); }
    }

    public static bool SetPriority(int pid, ProcessPriorityClass priority)
    {
        return TrySetPriority(pid, priority).Succeeded;
    }

    public static ActionResult TrySetPriority(int pid, ProcessPriorityClass priority)
    {
        if (pid <= 0) return Failure("Invalid process ID.");
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
                        return Success();
                }
                finally
                {
                    CloseHandle(h);
                }
            }

            using var p = Process.GetProcessById(pid);
            p.PriorityClass = priority;
            return Success();
        }
        catch (Exception ex) { return Failure(ex.Message); }
    }

    private static ActionResult Success() => new(true);

    private static ActionResult Failure(string message) => new(false, message);

    private static string GetLastWin32ErrorMessage()
    {
        var error = Marshal.GetLastWin32Error();
        return error == 0 ? "The process handle could not be opened." : new Win32Exception(error).Message;
    }

    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr h);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(int acc, bool inh, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetPriorityClass(IntPtr handle, uint priorityClass);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
}
