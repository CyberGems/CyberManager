namespace CyberManager.Common.Models;

/// <summary>Where a startup entry is registered.</summary>
public enum StartupEntrySource
{
    RegistryUser,
    RegistryMachine,
    RegistryUser32,
    RegistryMachine32,
    StartupFolderUser,
    StartupFolderCommon
}

/// <summary>
/// A single application Windows launches when the user signs in. Read-only for now: the second
/// phase will add enable/disable through the StartupApproved keys.
/// </summary>
public sealed record StartupAppInfo(
    string Name,
    string Publisher,
    string Command,
    StartupEntrySource Source,
    bool IsEnabled);
