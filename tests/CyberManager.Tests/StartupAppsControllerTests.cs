using CyberManager.Common.Models;
using CyberManager.Core.Engine;
using Microsoft.Win32;
using Xunit;

namespace CyberManager.Tests;

public class StartupAppsControllerTests
{
    private const string ApprovedRunKey =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    [Fact]
    public void SetEnabled_WritesAndRemovesTheApprovalRecord()
    {
        const string name = "CyberManager.SelfTest.Approval";
        var backupDirectory = Path.Combine(Path.GetTempPath(), "cybermanager-startup-" + Guid.NewGuid().ToString("N"));
        var previousOverride = StartupAppsController.BackupDirectoryOverride;
        StartupAppsController.BackupDirectoryOverride = backupDirectory;

        var entry = new StartupAppInfo(
            name, string.Empty, @"C:\self-test.exe", StartupEntrySource.RegistryUser, true, "Run", name);

        try
        {
            var disable = StartupAppsController.SetEnabled(entry, false);
            Assert.True(disable.Success, disable.Error);
            Assert.False(StartupAppsCollector.IsEnabledFromApprovedValue(ReadRecord(name)));

            var enable = StartupAppsController.SetEnabled(entry, true);
            Assert.True(enable.Success, enable.Error);
            Assert.Null(ReadRecord(name));

            var backupFile = Path.Combine(backupDirectory, "startup-approvals-backup.log");
            Assert.True(File.Exists(backupFile));
            Assert.Equal(2, File.ReadAllLines(backupFile).Length);
        }
        finally
        {
            DeleteRecord(name);
            StartupAppsController.BackupDirectoryOverride = previousOverride;
            try
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of the test directory.
            }
        }
    }

    [Fact]
    public void SetEnabled_AbortsWhenTheBackupCannotBeWritten()
    {
        const string name = "CyberManager.SelfTest.NoBackup";
        var blockedPath = Path.Combine(Path.GetTempPath(), "cybermanager-blocked-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockedPath, "not a directory");

        var previousOverride = StartupAppsController.BackupDirectoryOverride;
        StartupAppsController.BackupDirectoryOverride = blockedPath;

        var entry = new StartupAppInfo(
            name, string.Empty, @"C:\self-test.exe", StartupEntrySource.RegistryUser, true, "Run", name);

        try
        {
            var result = StartupAppsController.SetEnabled(entry, false);

            Assert.False(result.Success);
            // The registry must be untouched when the backup failed.
            Assert.Null(ReadRecord(name));
        }
        finally
        {
            DeleteRecord(name);
            StartupAppsController.BackupDirectoryOverride = previousOverride;
            try
            {
                File.Delete(blockedPath);
            }
            catch
            {
                // Best-effort cleanup of the test file.
            }
        }
    }

    [Fact]
    public void UndoLastChange_RestoresTheExactOriginalBytes()
    {
        const string name = "CyberManager.SelfTest.Undo";
        var backupDirectory = Path.Combine(Path.GetTempPath(), "cybermanager-undo-" + Guid.NewGuid().ToString("N"));
        var previousOverride = StartupAppsController.BackupDirectoryOverride;
        StartupAppsController.BackupDirectoryOverride = backupDirectory;

        var entry = new StartupAppInfo(
            name, string.Empty, @"C:\self-test.exe", StartupEntrySource.RegistryUser, true, "Run", name);
        var original = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        try
        {
            WriteRecord(name, original);

            var disable = StartupAppsController.SetEnabled(entry, false);
            Assert.True(disable.Success, disable.Error);
            Assert.True(StartupAppsController.HasUndoableChange(entry));

            var undo = StartupAppsController.UndoLastChange(entry);

            Assert.True(undo.Success, undo.Error);
            Assert.True(undo.Found);
            Assert.True(undo.RestoredEnabled);
            // Byte-exact restore: a previous value that Windows wrote is never rewritten from scratch.
            Assert.Equal(original, ReadRecord(name));
        }
        finally
        {
            DeleteRecord(name);
            StartupAppsController.BackupDirectoryOverride = previousOverride;
            try
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of the test directory.
            }
        }
    }

    [Fact]
    public void UndoLastChange_ReportsWhenNothingWasBackedUp()
    {
        const string name = "CyberManager.SelfTest.Nothing";
        var backupDirectory = Path.Combine(Path.GetTempPath(), "cybermanager-undo-empty-" + Guid.NewGuid().ToString("N"));
        var previousOverride = StartupAppsController.BackupDirectoryOverride;
        StartupAppsController.BackupDirectoryOverride = backupDirectory;

        var entry = new StartupAppInfo(
            name, string.Empty, @"C:\self-test.exe", StartupEntrySource.RegistryUser, true, "Run", name);

        try
        {
            Assert.False(StartupAppsController.HasUndoableChange(entry));

            var undo = StartupAppsController.UndoLastChange(entry);

            Assert.True(undo.Success);
            Assert.False(undo.Found);
            Assert.Null(ReadRecord(name));
        }
        finally
        {
            DeleteRecord(name);
            StartupAppsController.BackupDirectoryOverride = previousOverride;
            try
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of the test directory.
            }
        }
    }

    [Fact]
    public void SetEnabled_RotatesAnOversizedBackupLog()
    {
        const string name = "CyberManager.SelfTest.Rotation";
        var backupDirectory = Path.Combine(Path.GetTempPath(), "cybermanager-rotate-" + Guid.NewGuid().ToString("N"));
        var previousOverride = StartupAppsController.BackupDirectoryOverride;
        StartupAppsController.BackupDirectoryOverride = backupDirectory;

        var entry = new StartupAppInfo(
            name, string.Empty, @"C:\self-test.exe", StartupEntrySource.RegistryUser, true, "Run", name);

        try
        {
            Directory.CreateDirectory(backupDirectory);
            var path = Path.Combine(backupDirectory, "startup-approvals-backup.log");
            File.WriteAllLines(path, Enumerable.Repeat(new string('x', 100), 4000));

            var result = StartupAppsController.SetEnabled(entry, false);

            Assert.True(result.Success, result.Error);
            var lines = File.ReadAllLines(path);
            Assert.True(lines.Length <= 502, $"log kept {lines.Length} lines");
            Assert.Contains(lines, line => line.Contains(name, StringComparison.Ordinal));
        }
        finally
        {
            DeleteRecord(name);
            StartupAppsController.BackupDirectoryOverride = previousOverride;
            try
            {
                Directory.Delete(backupDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of the test directory.
            }
        }
    }

    [Fact]
    public void SetEnabled_RejectsUnknownCategories()
    {
        var entry = new StartupAppInfo(
            "Anything", string.Empty, @"C:\app.exe", StartupEntrySource.RegistryUser, true, "RunOther", "Anything");

        var result = StartupAppsController.SetEnabled(entry, false);

        Assert.False(result.Success);
    }

    private static byte[]? ReadRecord(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedRunKey, writable: false);
        return key?.GetValue(name) as byte[];
    }

    private static void WriteRecord(string name, byte[] value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedRunKey, writable: true);
        key!.SetValue(name, value, RegistryValueKind.Binary);
    }

    private static void DeleteRecord(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedRunKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
