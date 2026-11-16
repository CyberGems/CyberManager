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

    private static void DeleteRecord(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApprovedRunKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
