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
/// Outcome of an undo request. <paramref name="Found"/> is false when the backup log has no
/// change for the entry, which is a normal result rather than a failure.
/// </summary>
public sealed record StartupUndoResult(bool Success, bool Found, bool RestoredEnabled, string? Error = null);

/// <summary>One line of the backup log.</summary>
internal sealed record StartupApprovalBackupRecord(
    DateTimeOffset Timestamp,
    string? Category,
    string? ValueName,
    string? Name,
    string? Command,
    bool PreviousEnabled,
    bool NewEnabled,
    string? OriginalValue,
    bool IsUndo);

/// <summary>
/// Enables, disables or restores startup entries the same way Task Manager does: by writing the
/// per-user record under <c>Explorer\StartupApproved</c>. That location is user-scoped even for
/// entries registered in HKLM, so no elevation is needed. The original value is saved to a backup
/// log before every change, and a failed backup aborts the change so nothing is modified unbacked.
/// </summary>
public static class StartupAppsController
{
    private const string ApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private const string BackupFileName = "startup-approvals-backup.log";

    /// <summary>Size at which the log is trimmed back to its most recent <see cref="BackupLinesToKeep"/> lines.</summary>
    internal const long MaxBackupBytes = 256 * 1024;

    private const int BackupLinesToKeep = 500;

    private static readonly string[] ValidCategories = { "Run", "Run32", "StartupFolder" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Test seam: redirects the backup log away from the real user profile.</summary>
    internal static string? BackupDirectoryOverride { get; set; }

    private static string BackupFilePath => Path.Combine(
        BackupDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CyberManager"),
        BackupFileName);

    public static StartupToggleResult SetEnabled(StartupAppInfo entry, bool enabled)
    {
        if (entry is null) return StartupToggleResult.Fail("missing entry");
        if (!TryValidate(entry, out var validationError)) return StartupToggleResult.Fail(validationError);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"{ApprovedKeyPath}\{entry.ApprovalCategory}", writable: true);
            if (key is null) return StartupToggleResult.Fail("approval key could not be opened");

            var original = key.GetValue(entry.ApprovalValueName) as byte[];

            if (!TryAppendBackup(entry, original, enabled, isUndo: false, out var backupError))
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

    /// <summary>True when the backup log holds a change for this entry that can be undone.</summary>
    public static bool HasUndoableChange(StartupAppInfo entry) =>
        entry is not null && FindLastChange(entry) is not null;

    /// <summary>
    /// Restores the startup record exactly as it was before the most recent change to this entry.
    /// The current value is backed up first as an undo record, so restoring is itself reversible
    /// and the log stays a complete account of every write.
    /// </summary>
    public static StartupUndoResult UndoLastChange(StartupAppInfo entry)
    {
        if (entry is null) return new StartupUndoResult(false, false, false, "missing entry");
        if (!TryValidate(entry, out var validationError))
        {
            return new StartupUndoResult(false, false, false, validationError);
        }

        var record = FindLastChange(entry);
        if (record is null) return new StartupUndoResult(true, false, false);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"{ApprovedKeyPath}\{entry.ApprovalCategory}", writable: true);
            if (key is null)
            {
                return new StartupUndoResult(false, true, false, "approval key could not be opened");
            }

            var current = key.GetValue(entry.ApprovalValueName) as byte[];
            if (!TryAppendBackup(entry, current, record.PreviousEnabled, isUndo: true, out var backupError))
            {
                return new StartupUndoResult(false, true, false, backupError);
            }

            var previous = record.OriginalValue is null ? null : Convert.FromBase64String(record.OriginalValue);
            if (previous is null || previous.Length == 0)
            {
                key.DeleteValue(entry.ApprovalValueName, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(entry.ApprovalValueName, previous, RegistryValueKind.Binary);
            }

            return new StartupUndoResult(true, true, record.PreviousEnabled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup entry '{entry.Name}' could not be restored: {ex.Message}");
            return new StartupUndoResult(false, true, false, ex.Message);
        }
    }

    private static bool TryValidate(StartupAppInfo entry, out string error)
    {
        error = string.Empty;

        if (!ValidCategories.Contains(entry.ApprovalCategory, StringComparer.Ordinal))
        {
            error = $"unsupported approval category '{entry.ApprovalCategory}'";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.ApprovalValueName))
        {
            error = "missing approval value name";
            return false;
        }

        return true;
    }

    private static StartupApprovalBackupRecord? FindLastChange(StartupAppInfo entry)
    {
        try
        {
            var path = BackupFilePath;
            if (!File.Exists(path)) return null;

            var lines = File.ReadAllLines(path);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                StartupApprovalBackupRecord? record;
                try
                {
                    record = JsonSerializer.Deserialize<StartupApprovalBackupRecord>(lines[i], JsonOptions);
                }
                catch (JsonException)
                {
                    // A malformed or older line must not stop the search for a usable one.
                    continue;
                }

                if (record is null || record.IsUndo) continue;
                if (!string.Equals(record.Category, entry.ApprovalCategory, StringComparison.Ordinal)) continue;
                if (!string.Equals(record.ValueName, entry.ApprovalValueName, StringComparison.Ordinal)) continue;

                return record;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup backup could not be read: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Appends a JSON line with the pre-change state. Returns false when the backup could not be
    /// written: the caller must then leave the registry untouched.
    /// </summary>
    private static bool TryAppendBackup(
        StartupAppInfo entry,
        byte[]? original,
        bool newEnabled,
        bool isUndo,
        out string error)
    {
        error = string.Empty;

        try
        {
            var path = BackupFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            RotateIfNeeded(path);

            var record = new StartupApprovalBackupRecord(
                DateTimeOffset.UtcNow,
                entry.ApprovalCategory,
                entry.ApprovalValueName,
                entry.Name,
                entry.Command,
                entry.IsEnabled,
                newEnabled,
                original is null ? null : Convert.ToBase64String(original),
                isUndo);

            File.AppendAllText(path, JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup backup could not be written: {ex.Message}");
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Keeps the log from growing without bound by dropping everything but the newest lines once
    /// it passes <see cref="MaxBackupBytes"/>. A rotation failure is not fatal: the entry is still
    /// appended and the next write tries again.
    /// </summary>
    private static void RotateIfNeeded(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= MaxBackupBytes) return;

            var lines = File.ReadAllLines(path);
            if (lines.Length <= BackupLinesToKeep) return;

            File.WriteAllLines(path, lines[^BackupLinesToKeep..]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup backup could not be rotated: {ex.Message}");
        }
    }
}
