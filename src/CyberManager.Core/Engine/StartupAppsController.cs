using System.Diagnostics;
using System.Text.Json;
using CyberManager.Common.Models;
using Microsoft.Win32;

namespace CyberManager.Core.Engine;

/// <summary>Outcome of an enable/disable request.</summary>
public sealed record StartupToggleResult(bool Success, string? Error = null)
{
    public static StartupToggleResult Ok { get; } = new(true);
    public static StartupToggleResult Fail(string error) => new(false, error);
}

/// <summary>
/// Enables or disables startup entries the same way Task Manager does: by writing the per-user
/// record under <c>Explorer\StartupApproved</c>. That location is user-scoped even for entries
/// registered in HKLM, so no elevation is needed. The original value is saved to a backup log
/// before every change, and a failed backup aborts the change so nothing is modified unbacked.
/// </summary>
public static class StartupAppsController
{
    private const string ApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private const string BackupFileName = "startup-approvals-backup.log";

    private static readonly string[] ValidCategories = { "Run", "Run32", "StartupFolder" };

    /// <summary>Test seam: redirects the backup log away from the real user profile.</summary>
    internal static string? BackupDirectoryOverride { get; set; }

    public static StartupToggleResult SetEnabled(StartupAppInfo entry, bool enabled)
    {
        if (entry is null) return StartupToggleResult.Fail("missing entry");

        var category = entry.ApprovalCategory;
        if (!ValidCategories.Contains(category, StringComparer.Ordinal))
        {
            return StartupToggleResult.Fail($"unsupported approval category '{category}'");
        }

        if (string.IsNullOrWhiteSpace(entry.ApprovalValueName))
        {
            return StartupToggleResult.Fail("missing approval value name");
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"{ApprovedKeyPath}\{category}", writable: true);
            if (key is null) return StartupToggleResult.Fail("approval key could not be opened");

            var original = key.GetValue(entry.ApprovalValueName) as byte[];

            if (!TryBackup(entry, original, enabled, out var backupError))
            {
                return StartupToggleResult.Fail(backupError);
            }

            if (enabled)
            {
                // No record is what Windows treats as "no explicit decision", and that means the
                // entry starts normally. Deleting the record is therefore the exact inverse of a
                // previous disable and avoids inventing a value Windows never wrote.
                if (original is not null)
                {
                    key.DeleteValue(entry.ApprovalValueName, throwOnMissingValue: false);
                }
            }
            else
            {
                var value = original is { Length: > 0 } ? (byte[])original.Clone() : new byte[12];

                // Low bit of the first byte is the disabled flag Windows reads (0x03/0x07).
                value[0] |= 0x01;
                key.SetValue(entry.ApprovalValueName, value, RegistryValueKind.Binary);
            }

            return StartupToggleResult.Ok;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup entry '{entry.Name}' could not be updated: {ex.Message}");
            return StartupToggleResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Appends a JSON line with the pre-change state. Returns false when the backup could not be
    /// written: the caller must then leave the registry untouched.
    /// </summary>
    private static bool TryBackup(StartupAppInfo entry, byte[]? original, bool newEnabled, out string error)
    {
        error = string.Empty;

        try
        {
            var directory = BackupDirectoryOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CyberManager");
            Directory.CreateDirectory(directory);

            var record = new
            {
                timestamp = DateTimeOffset.UtcNow,
                entry.ApprovalCategory,
                entry.ApprovalValueName,
                entry.Name,
                entry.Command,
                previousEnabled = entry.IsEnabled,
                newEnabled,
                originalValue = original is null ? null : Convert.ToBase64String(original)
            };

            File.AppendAllText(
                Path.Combine(directory, BackupFileName),
                JsonSerializer.Serialize(record) + Environment.NewLine);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup backup could not be written: {ex.Message}");
            error = ex.Message;
            return false;
        }
    }
}
