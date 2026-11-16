using System.Diagnostics;
using CyberManager.Common.Models;
using Microsoft.Win32;

namespace CyberManager.Core.Engine;

/// <summary>
/// Reads the applications Windows launches at sign-in from the Run keys and the Startup folders.
/// The enabled state comes from the same StartupApproved records Task Manager writes, so the
/// values shown here match Windows. Read-only: nothing is written.
///
/// Cost is small and bounded: two registry keys plus two folder listings, and one cached
/// FileVersionInfo lookup per executable path. Publisher lookups are cached across refreshes
/// because the same binary shows up under several entries.
/// </summary>
public sealed class StartupAppsCollector
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private static readonly string[] StartupFileExtensions =
    {
        ".lnk", ".exe", ".bat", ".cmd", ".com", ".url"
    };

    private readonly Dictionary<string, string> _publisherCache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<StartupAppInfo> Collect()
    {
        var result = new List<StartupAppInfo>();

        var runApproved = ReadApprovalState("Run");
        var run32Approved = ReadApprovalState("Run32");
        var folderApproved = ReadApprovalState("StartupFolder");

        AddRegistryEntries(result, RegistryHive.CurrentUser, RegistryView.Registry64,
            StartupEntrySource.RegistryUser, "Run", runApproved);
        AddRegistryEntries(result, RegistryHive.LocalMachine, RegistryView.Registry64,
            StartupEntrySource.RegistryMachine, "Run", runApproved);
        AddRegistryEntries(result, RegistryHive.LocalMachine, RegistryView.Registry32,
            StartupEntrySource.RegistryMachine32, "Run32", run32Approved);

        AddFolderEntries(result, Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupEntrySource.StartupFolderUser, folderApproved);
        AddFolderEntries(result, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            StartupEntrySource.StartupFolderCommon, folderApproved);

        return result
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Interprets a StartupApproved value. Windows stores a small header whose low bit of the
    /// first byte is the disabled flag (0x02/0x06 enabled, 0x03/0x07 disabled). A missing value
    /// means there is no explicit decision, which Windows treats as enabled.
    /// </summary>
    public static bool IsEnabledFromApprovedValue(byte[]? value)
    {
        if (value is null || value.Length == 0) return true;
        return (value[0] & 0x01) == 0;
    }

    /// <summary>
    /// Extracts the executable path from a Run command. Quoted commands are unambiguous; an
    /// unquoted command falls back to its first token, which is the usual Windows convention.
    /// Environment variables are expanded because Run values commonly use them.
    /// </summary>
    public static string? ExtractExecutablePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return null;

        var command = commandLine.Trim();
        string path;

        if (command[0] == '"')
        {
            var closing = command.IndexOf('"', 1);
            if (closing <= 1) return null;
            path = command[1..closing];
        }
        else
        {
            var space = command.IndexOf(' ');
            path = space < 0 ? command : command[..space];
        }

        path = Environment.ExpandEnvironmentVariables(path).Trim();
        return path.Length == 0 ? null : path;
    }

    private static Dictionary<string, bool> ReadApprovalState(string category)
    {
        var state = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ApprovedKeyPath}\{category}", writable: false);
            if (key is null) return state;

            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is byte[] bytes)
                {
                    state[valueName] = IsEnabledFromApprovedValue(bytes);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StartupApproved\\{category} could not be read: {ex.Message}");
        }

        return state;
    }

    private void AddRegistryEntries(
        List<StartupAppInfo> target,
        RegistryHive hive,
        RegistryView view,
        StartupEntrySource source,
        string approvalCategory,
        Dictionary<string, bool> approved)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var runKey = baseKey.OpenSubKey(RunKeyPath, writable: false);
            if (runKey is null) return;

            foreach (var name in runKey.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (runKey.GetValue(name) is not string command || command.Length == 0) continue;

                target.Add(new StartupAppInfo(
                    name,
                    ResolvePublisher(command),
                    command,
                    source,
                    !approved.TryGetValue(name, out var enabled) || enabled,
                    approvalCategory,
                    name));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{hive} {view} Run key could not be read: {ex.Message}");
        }
    }

    private void AddFolderEntries(
        List<StartupAppInfo> target,
        string folder,
        StartupEntrySource source,
        Dictionary<string, bool> approved)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            foreach (var path in Directory.EnumerateFiles(folder))
            {
                var extension = Path.GetExtension(path);
                if (!StartupFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;

                var fileName = Path.GetFileName(path);
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fileName)) continue;

                // Windows keys these records by file name, extension included. Fall back to the
                // bare name for environments that wrote it without one.
                var isEnabled = true;
                if (approved.TryGetValue(fileName, out var byFileName)) isEnabled = byFileName;
                else if (approved.TryGetValue(name, out var byName)) isEnabled = byName;

                target.Add(new StartupAppInfo(
                    name,
                    ResolvePublisher(path),
                    path,
                    source,
                    isEnabled,
                    "StartupFolder",
                    fileName));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup folder {folder} could not be read: {ex.Message}");
        }
    }

    private string ResolvePublisher(string commandOrPath)
    {
        var path = ExtractExecutablePath(commandOrPath);
        if (string.IsNullOrEmpty(path)) return string.Empty;

        if (_publisherCache.TryGetValue(path, out var cached)) return cached;

        var publisher = string.Empty;
        try
        {
            // Shortcuts are not executables; resolving their target needs shell COM, which is
            // out of scope for this read-only phase and would slow the listing down.
            if (File.Exists(path) && !string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                publisher = info.CompanyName?.Trim() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Publisher for {path} could not be read: {ex.Message}");
        }

        _publisherCache[path] = publisher;
        return publisher;
    }
}
