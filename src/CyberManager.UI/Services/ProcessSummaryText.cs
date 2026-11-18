using System.Globalization;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;

namespace CyberManager.UI.Services;

/// <summary>
/// Builds the plain-text process summary shared by the properties window (footer copy button,
/// Ctrl+C) and the main window (context menu item, Ctrl+Shift+C). Values mirror what the
/// properties window displays so what the user copies is exactly what they see.
/// </summary>
public static class ProcessSummaryText
{
    public static string Build(ProcessInfo process)
    {
        var metadata = ProcessMetadataResolver.Resolve(process);
        var unknown = Strings.T("Unknown");

        var lines = new List<string>
        {
            process.Pid > 0
                ? $"{Strings.T("ProcessProperties")}: {process.Name} (PID {process.Pid})"
                : $"{Strings.T("ProcessProperties")}: {process.Name}"
        };

        AddRow(lines, Strings.T("FriendlyName"), ValueOrUnknown(metadata.FriendlyName, unknown));
        AddRow(lines, Strings.T("ImageName"), ValueOrUnknown(process.Name, unknown));
        AddRow(lines, Strings.T("Pid"), process.Pid > 0 ? process.Pid.ToString(CultureInfo.CurrentCulture) : unknown);
        AddRow(lines, Strings.T("Path"), ValueOrUnknown(process.ExePath, unknown));
        AddRow(lines, Strings.T("Company"), ValueOrUnknown(metadata.CompanyName, unknown));
        AddRow(lines, Strings.T("Product"), ValueOrUnknown(metadata.ProductName, unknown));
        AddRow(lines, Strings.T("Version"), ValueOrUnknown(metadata.FileVersion, unknown));
        AddRow(lines, Strings.T("OriginalFilename"), ValueOrUnknown(metadata.OriginalFilename, unknown));
        AddRow(lines, Strings.T("User"), ValueOrUnknown(process.UserName, unknown));
        AddRow(lines, Strings.T("Status"), FormatStatus(process.Status, unknown));
        AddRow(lines, Strings.T("StartTime"), process.StartTime == default
            ? unknown
            : process.StartTime.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        AddRow(lines, Strings.T("Cpu"), process.CpuFormatted);
        AddRow(lines, Strings.T("Memory"), process.MemoryFormatted);
        AddRow(lines, Strings.T("Threads"), process.ThreadCount.ToString(CultureInfo.CurrentCulture));
        AddRow(lines, Strings.T("Priority"), process.PriorityFormatted);
        AddRow(lines, Strings.T("ParentProcess"), process.ParentPid > 0
            ? process.ParentPid.ToString(CultureInfo.CurrentCulture)
            : unknown);
        AddRow(lines, Strings.T("WindowTitle"), ValueOrUnknown(process.MainWindowTitle, unknown));
        AddRow(lines, Strings.T("Instances"), Math.Max(1, process.InstanceCount).ToString(CultureInfo.CurrentCulture));

        return string.Join(Environment.NewLine, lines);
    }

    public static string ValueOrUnknown(string? value, string unknown) =>
        string.IsNullOrWhiteSpace(value) ? unknown : value.Trim();

    public static string FormatStatus(string? status, string unknown)
    {
        if (string.IsNullOrWhiteSpace(status)) return unknown;

        return status switch
        {
            "Running" => Strings.T("Running"),
            "Suspended" => Strings.T("Suspended"),
            _ => status
        };
    }

    private static void AddRow(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
            lines.Add($"{label}: {value}");
    }
}
