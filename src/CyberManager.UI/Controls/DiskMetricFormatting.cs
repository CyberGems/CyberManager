using System.Globalization;

namespace CyberManager.UI.Controls;

internal static class DiskMetricFormatting
{
    internal static string Rate(double bytesPerSecond) => $"{Bytes(bytesPerSecond)}/s";

    internal static string Bytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var unit = 0;
        while (bytes >= 1000 && unit < units.Length - 1) { bytes /= 1000; unit++; }
        return string.Create(CultureInfo.CurrentCulture, $"{bytes:0.#} {units[unit]}");
    }
}
