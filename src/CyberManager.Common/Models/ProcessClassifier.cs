using System.IO;

namespace CyberManager.Common.Models;

/// <summary>
/// Categorizes processes so the UI can offer category based filtering.
/// A process is treated as a Windows system process when it is the kernel idle or
/// system task (PID 0 / 4), when it has no executable path (driver backed kernel
/// components such as System, Registry, Memory Compression or Secure System), or when
/// its image lives inside the Windows installation directory (System32, SysWOW64,
/// WinSxS and friends).
/// A short exception list keeps the interactive shell out of the system bucket, because
/// it lives under the Windows directory yet is a user-facing process.
/// </summary>
public static class ProcessClassifier
{
    private static readonly string WindowsDirectoryPrefix = BuildWindowsDirectoryPrefix();

    /// <summary>
    /// Processes that ship inside the Windows directory but that the user interacts with
    /// directly (windows, tray, taskbar). They stay visible even when system processes are
    /// hidden so the user can still restart the shell. Extend this list if other shell
    /// components should be treated the same way.
    /// </summary>
    private static readonly HashSet<string> InteractiveShellProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer.exe"
        };

    public static bool IsSystemProcess(ProcessInfo? process)
    {
        if (process is null) return false;

        // PID 0 is the System Idle Process and PID 4 hosts the NT kernel image.
        if (process.Pid is 0 or 4) return true;

        // Kernel mode / driver backed components expose no executable image path.
        if (string.IsNullOrWhiteSpace(process.ExePath)) return true;

        // The desktop shell is user-facing: keep it out of the system category.
        if (IsInteractiveShell(process.ExePath)) return false;

        return IsUnderWindowsDirectory(process.ExePath);
    }

    private static bool IsInteractiveShell(string exePath)
    {
        var fileName = Path.GetFileName(exePath);
        return !string.IsNullOrEmpty(fileName) && InteractiveShellProcesses.Contains(fileName);
    }

    private static bool IsUnderWindowsDirectory(string path)
    {
        var prefix = WindowsDirectoryPrefix;
        if (prefix.Length == 0) return false;

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildWindowsDirectoryPrefix()
    {
        try
        {
            var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(windowsDirectory)) return string.Empty;

            return windowsDirectory.TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        }
        catch
        {
            return string.Empty;
        }
    }
}
