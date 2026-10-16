using System.ComponentModel;
using System.Diagnostics;

namespace CyberManager.Core.Engine;

public enum TaskLaunchStatus { Started, Cancelled, InvalidCommand, Failed }

public sealed record TaskLaunchResult(TaskLaunchStatus Status, string ErrorMessage = "");

/// <summary>Opens a program, document, folder or URI through the Windows shell.</summary>
public static class TaskLauncher
{
    public static TaskLaunchResult Launch(string command, bool asAdministrator)
    {
        try
        {
            using var process = Process.Start(CreateStartInfo(command, asAdministrator));
            // Shell activation may reuse an existing process and return no Process object.
            return new(TaskLaunchStatus.Started);
        }
        catch (FormatException) { return new(TaskLaunchStatus.InvalidCommand); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new(TaskLaunchStatus.Cancelled); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or
            NotSupportedException or System.Security.SecurityException or IOException)
        {
            return new(TaskLaunchStatus.Failed, ex.Message);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(string command, bool asAdministrator)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Length > 32767 || command.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new FormatException("Invalid task command.");
        var expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (string.IsNullOrWhiteSpace(expanded) || expanded.Length > 32767 || expanded.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new FormatException("Invalid expanded task command.");
        var (target, arguments) = SplitCommand(expanded);
        var directory = File.Exists(target) ? Path.GetDirectoryName(Path.GetFullPath(target)) : Environment.CurrentDirectory;
        return new ProcessStartInfo
        {
            FileName = target,
            Arguments = arguments,
            WorkingDirectory = directory ?? Environment.CurrentDirectory,
            UseShellExecute = true,
            Verb = asAdministrator ? "runas" : string.Empty,
            ErrorDialog = false
        };
    }

    private static (string Target, string Arguments) SplitCommand(string command)
    {
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end <= 1 || (end + 1 < command.Length && !char.IsWhiteSpace(command[end + 1])))
                throw new FormatException("The quoted target is incomplete.");
            return (command[1..end], command[(end + 1)..].TrimStart());
        }

        if (File.Exists(command) || Directory.Exists(command)) return (Path.GetFullPath(command), string.Empty);
        if (Uri.TryCreate(command, UriKind.Absolute, out var uri) && !uri.IsFile) return (command, string.Empty);

        var firstSpace = -1;
        for (var index = 0; index < command.Length; index++)
        {
            if (!char.IsWhiteSpace(command[index])) continue;
            if (firstSpace < 0) firstSpace = index;
            // Resolve an existing full file name before passing a shortened path to the shell.
            // This also accepts pasted, unquoted paths with spaces followed by arguments.
            var candidate = command[..index];
            if (File.Exists(candidate)) return (Path.GetFullPath(candidate), command[index..].TrimStart());
        }
        if (firstSpace < 0) return (command, string.Empty);
        if (Path.IsPathRooted(command)) throw new FormatException("Quote paths that contain spaces.");
        return (command[..firstSpace], command[firstSpace..].TrimStart());
    }
}
