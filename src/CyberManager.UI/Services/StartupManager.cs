using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using Microsoft.Win32;

namespace CyberManager.UI.Services;

public static class StartupManager
{
    private const string TaskName = "CyberManager";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CyberManager";
    private const string StartupArgument = "--minimized";

    public static bool IsAutoStartEnabled()
    {
        var executablePath = GetExecutablePath();
        return !string.IsNullOrWhiteSpace(executablePath) &&
               (IsScheduledTaskConfigured(executablePath) || IsRunEntryConfigured(executablePath));
    }

    public static bool SetAutoStart(bool enable, bool requestElevation = true)
    {
        if (!enable)
        {
            return DeleteScheduledTask(requestElevation) && DeleteRunEntry();
        }

        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath)) return false;

        if (IsScheduledTaskConfigured(executablePath))
        {
            return DeleteRunEntry();
        }

        _ = DeleteScheduledTask(requestElevation);
        if (TryCreateScheduledTask(executablePath, startMinimized: true, requestElevation))
        {
            return DeleteRunEntry();
        }

        // A per-user Run entry keeps startup functional when UAC is denied or
        // Task Scheduler is unavailable on the current Windows installation.
        return SetRunEntry(executablePath, startMinimized: true);
    }

    /// <summary>
    /// Repairs the selected startup preference without prompting for UAC.
    /// This runs during application startup so a missing entry is restored
    /// silently, while enabling the elevated task remains an explicit action.
    /// </summary>
    public static bool EnsureAutoStart(bool enable, bool startMinimized = true)
    {
        if (!enable)
        {
            return DeleteScheduledTask(requestElevation: false) && DeleteRunEntry();
        }

        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath)) return false;

        if (IsScheduledTaskConfigured(executablePath))
        {
            _ = DeleteRunEntry();
            return true;
        }

        _ = DeleteScheduledTask(requestElevation: false);
        return SetRunEntry(executablePath, startMinimized);
    }

    private static string? GetExecutablePath()
    {
        var candidates = new[]
        {
            Environment.ProcessPath,
            Process.GetCurrentProcess().MainModule?.FileName
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) continue;
            if (Path.GetFileNameWithoutExtension(candidate)
                .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Path.GetFullPath(candidate);
        }

        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            var publishedExecutable = Path.Combine(
                Path.GetDirectoryName(assemblyPath) ?? string.Empty,
                $"{AppName}.exe");
            if (File.Exists(publishedExecutable)) return Path.GetFullPath(publishedExecutable);
        }

        return null;
    }

    private static bool IsRunEntryConfigured(string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var command = key?.GetValue(AppName) as string;
            var commandPath = ExtractCommandPath(command);
            return !string.IsNullOrWhiteSpace(commandPath) &&
                   PathsEqual(commandPath, executablePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool SetRunEntry(string executablePath, bool startMinimized)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null) return false;

            var arguments = startMinimized ? $" {StartupArgument}" : string.Empty;
            key.SetValue(AppName, $"\"{executablePath}\"{arguments}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool DeleteRunEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractCommandPath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        var trimmed = command.Trim();
        if (trimmed[0] == '"')
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            return closingQuote > 1 ? trimmed[1..closingQuote] : null;
        }

        var separator = trimmed.IndexOfAny([' ', '\t']);
        return separator > 0 ? trimmed[..separator] : trimmed;
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsScheduledTaskConfigured(string executablePath)
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null) return false;

            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();
            dynamic rootFolder = service.GetFolder(@"\");
            dynamic task = rootFolder.GetTask(TaskName);
            dynamic actions = task.Definition.Actions;
            if (actions.Count < 1) return false;

            dynamic action = actions[1];
            var commandPath = Convert.ToString(action.Path);
            return !string.IsNullOrWhiteSpace(commandPath) &&
                   PathsEqual(commandPath, executablePath) &&
                   ContainsStartupArgument(Convert.ToString(action.Arguments));
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsStartupArgument(string? arguments) =>
        !string.IsNullOrWhiteSpace(arguments) &&
        arguments.Contains(StartupArgument, StringComparison.OrdinalIgnoreCase);

    private static bool IsScheduledTaskPresent()
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType != null)
            {
                dynamic service = Activator.CreateInstance(serviceType)!;
                service.Connect();
                dynamic rootFolder = service.GetFolder(@"\");
                try
                {
                    _ = rootFolder.GetTask(TaskName);
                    return true;
                }
                catch
                {
                    // Fall through to schtasks for installations where COM
                    // can connect but cannot enumerate the root task.
                }
            }
        }
        catch
        {
            // Fall through to schtasks.
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"\\{TaskName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return false;
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateScheduledTask(
        string executablePath,
        bool startMinimized,
        bool requestElevation)
    {
        var xml = BuildTaskXml(executablePath, startMinimized);
        if (TryRegisterViaCom(xml)) return true;
        return TryRegisterViaSchtasks(xml, requestElevation);
    }

    private static string BuildTaskXml(string executablePath, bool startMinimized)
    {
        var escapedPath = SecurityElement.Escape(executablePath);
        var arguments = startMinimized ? $"<Arguments>{StartupArgument}</Arguments>" : string.Empty;

        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>CyberGems</Author>
    <Description>CyberManager startup</Description>
    <URI>\{TaskName}</URI>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedPath}</Command>
      {arguments}
    </Exec>
  </Actions>
</Task>";
    }

    private static bool TryRegisterViaCom(string xml)
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null) return false;

            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();
            dynamic rootFolder = service.GetFolder(@"\");
            // 6 = TASK_CREATE_OR_UPDATE, 3 = TASK_LOGON_INTERACTIVE_TOKEN.
            rootFolder.RegisterTask(TaskName, xml, 6, null, null, 3, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRegisterViaSchtasks(string xml, bool requestElevation)
    {
        string? tempFile = null;
        try
        {
            tempFile = Path.Combine(
                Path.GetTempPath(),
                $"{AppName}_Task_{Guid.NewGuid():N}.xml");
            File.WriteAllText(tempFile, xml, System.Text.Encoding.Unicode);

            var processInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = requestElevation,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (requestElevation) processInfo.Verb = "runas";
            processInfo.ArgumentList.Add("/Create");
            processInfo.ArgumentList.Add("/TN");
            processInfo.ArgumentList.Add($@"\{TaskName}");
            processInfo.ArgumentList.Add("/XML");
            processInfo.ArgumentList.Add(tempFile);
            processInfo.ArgumentList.Add("/F");

            using var process = Process.Start(processInfo);
            if (process == null) return false;
            process.WaitForExit(15000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static bool DeleteScheduledTask(bool requestElevation)
    {
        if (!IsScheduledTaskPresent()) return true;
        if (TryDeleteTaskViaCom()) return true;
        if (TryDeleteTaskViaSchtasks(requestElevation: false)) return true;
        return requestElevation && TryDeleteTaskViaSchtasks(requestElevation: true);
    }

    private static bool TryDeleteTaskViaCom()
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null) return false;

            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();
            dynamic rootFolder = service.GetFolder(@"\");
            rootFolder.DeleteTask(TaskName, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeleteTaskViaSchtasks(bool requestElevation)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = requestElevation,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (requestElevation) processInfo.Verb = "runas";
            processInfo.ArgumentList.Add("/Delete");
            processInfo.ArgumentList.Add("/TN");
            processInfo.ArgumentList.Add($@"\{TaskName}");
            processInfo.ArgumentList.Add("/F");

            using var process = Process.Start(processInfo);
            if (process == null) return false;
            process.WaitForExit(15000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
