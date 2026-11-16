namespace CyberManager.Common.Models;

/// <summary>Where a startup entry is registered.</summary>
public enum StartupEntrySource
{
    RegistryUser,
    RegistryMachine,
    RegistryMachine32,
    StartupFolderUser,
    StartupFolderCommon
}

/// <summary>
/// A single application Windows launches when the user signs in.
/// <paramref name="ApprovalCategory"/> and <paramref name="ApprovalValueName"/> identify the
/// per-user StartupApproved record that holds its enabled state, which is what enabling or
/// disabling the entry writes.
/// </summary>
public sealed record StartupAppInfo(
    string Name,
    string Publisher,
    string Command,
    StartupEntrySource Source,
    bool IsEnabled,
    string ApprovalCategory,
    string ApprovalValueName);
