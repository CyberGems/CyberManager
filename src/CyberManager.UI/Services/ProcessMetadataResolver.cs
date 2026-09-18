using System.Diagnostics;
using System.IO;
using CyberManager.Common.Models;

namespace CyberManager.UI.Services;

public sealed record ProcessMetadata(
    string FriendlyName,
    string CompanyName,
    string ProductName,
    string FileVersion,
    string OriginalFilename);

public static class ProcessMetadataResolver
{
    public static ProcessMetadata Resolve(ProcessInfo process)
    {
        FileVersionInfo? versionInfo = null;
        if (!string.IsNullOrWhiteSpace(process.ExePath) && File.Exists(process.ExePath))
        {
            try
            {
                versionInfo = FileVersionInfo.GetVersionInfo(process.ExePath);
            }
            catch
            {
                // Access to a process image can be denied or disappear while
                // the process list is being refreshed.
            }
        }

        return new ProcessMetadata(
            SelectFriendlyName(
                versionInfo?.FileDescription,
                versionInfo?.ProductName,
                process.Name),
            versionInfo?.CompanyName ?? string.Empty,
            versionInfo?.ProductName ?? string.Empty,
            versionInfo?.FileVersion ?? string.Empty,
            versionInfo?.OriginalFilename ?? string.Empty);
    }

    public static string SelectFriendlyName(
        string? fileDescription,
        string? productName,
        string? imageName)
    {
        foreach (var candidate in new[] { fileDescription, productName, imageName })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return string.Empty;
    }
}
