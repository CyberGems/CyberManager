using System.Diagnostics;
using System.IO;
using System.Windows;
using CyberManager.Common.I18n;
using CyberManager.UI.Dialogs;
using CyberManager.UI.Services;

namespace CyberManager.UI;

public partial class MainWindow
{
    private Task<UpdateCheckResult>? _updateCheck;
    private UpdateCheckResult? _pendingUpdate;
    private UpdateNoticeWindow? _updateNotice;
    private UpdatePhase _updatePhase;
    private UpdateDownloadProgress _updateProgress;
    private CancellationTokenSource? _updateDownload;
    private string? _updatePackage;
    private string? _updateError;

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            await Task.Delay(3000, _lifetimeCts.Token);
            if (App.Settings.AutoCheckForUpdates)
                await CheckForUpdateAsync(manual: false);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested) { }
        catch (Exception ex) { Debug.WriteLine($"Startup update check failed: {ex}"); }
    }

    internal async Task<UpdateCheckResult> CheckForUpdateAsync(bool manual = true)
    {
        var check = _pendingUpdate is not null && _updatePhase != UpdatePhase.Available
            ? Task.FromResult(_pendingUpdate)
            : _updateCheck ??= UpdateService.CheckForUpdatesAsync(_lifetimeCts.Token);
        UpdateCheckResult result;
        try { result = await check; }
        finally
        {
            if (ReferenceEquals(_updateCheck, check)) _updateCheck = null;
        }

        if (_isClosed || (!manual && !App.Settings.AutoCheckForUpdates)) return result;
        if (_pendingUpdate is not null && _updatePhase != UpdatePhase.Available)
            result = _pendingUpdate;

        if (!result.IsUpdateAvailable)
        {
            if (result.LatestVersion is not null) ClearPendingUpdate();
            return result;
        }
        if (!manual && string.Equals(result.LatestVersionLabel, App.Settings.SkippedUpdateVersion, StringComparison.OrdinalIgnoreCase))
            return result;

        bool newVersion = _pendingUpdate?.LatestVersionLabel != result.LatestVersionLabel;
        if (newVersion)
        {
            _pendingUpdate = result;
            _updatePhase = UpdatePhase.Available;
            _updateProgress = default;
            _updateError = null;
            _updatePackage = null;
            RefreshUpdateShortcuts();
        }
        if (manual || (newVersion && IsVisible && WindowState != WindowState.Minimized))
            ShowUpdateNotice(activate: manual);
        return result;
    }

    private async void CheckForUpdatesFromShell()
    {
        if (_pendingUpdate is not null)
        {
            ShowUpdateNotice(activate: true);
            return;
        }

        try
        {
            var result = await CheckForUpdateAsync();
            if (!_isClosed && !result.IsUpdateAvailable)
                ConfirmDialog.Show(IsVisible && WindowState != WindowState.Minimized ? this : null,
                    Strings.T("CheckUpdatesAction"), result.StatusMessage, Strings.T("Ok"), null,
                    result.IsUpToDate ? ConfirmIconType.Check : ConfirmIconType.Warning);
        }
        catch (Exception ex) { Debug.WriteLine($"Update check failed: {ex}"); }
    }

    private void UpdatePill_UpdateRequested(object? sender, EventArgs e) => CheckForUpdatesFromShell();

    private void ShowUpdateNotice(bool activate)
    {
        if (_isClosed || _pendingUpdate is null) return;
        if (_updateNotice is null)
        {
            var notice = new UpdateNoticeWindow();
            notice.OpenReleaseRequested += OpenUpdateRelease;
            notice.SkipRequested += SkipPendingUpdate;
            notice.PrimaryRequested += ActOnPendingUpdateAsync;
            notice.CancelDownloadRequested += CancelUpdateDownload;
            notice.Closed += (_, _) => _updateNotice = null;
            _updateNotice = notice;
        }
        RefreshUpdateNotice();
        _updateNotice.Present(MonitorPlacement.GetNearestWorkArea(this), activate);
    }

    private void RefreshUpdateNotice()
    {
        if (_pendingUpdate is { } update)
            _updateNotice?.Render(update, _updatePhase, _updateProgress,
                _updateDownload?.IsCancellationRequested == true, _updateError);
    }

    private void RefreshUpdateShortcuts()
    {
        var version = _pendingUpdate?.LatestVersionLabel;
        UpdatePill.SetUpdate(version);
        CompactView.SetPendingUpdate(version);
        _trayService.SetUpdateAvailable(version);
    }

    private void ClearPendingUpdate()
    {
        _updateNotice?.Close();
        _pendingUpdate = null;
        RefreshUpdateShortcuts();
    }

    private void SkipPendingUpdate()
    {
        if (_pendingUpdate is not { } update || _updatePhase != UpdatePhase.Available) return;
        var previous = App.Settings.SkippedUpdateVersion;
        App.Settings.SkippedUpdateVersion = update.LatestVersionLabel;
        if (!App.Settings.TrySave())
        {
            App.Settings.SkippedUpdateVersion = previous;
            _updateError = "UpdateSkipFailed";
            RefreshUpdateNotice();
            return;
        }
        ClearPendingUpdate();
    }

    private void CancelUpdateDownload()
    {
        _updateDownload?.Cancel();
        RefreshUpdateNotice();
    }

    private async Task ActOnPendingUpdateAsync()
    {
        if (_updatePhase == UpdatePhase.Available) await DownloadPendingUpdateAsync();
        else if (_updatePhase == UpdatePhase.Ready) FinishPendingUpdate();
    }

    private async Task DownloadPendingUpdateAsync()
    {
        if (_pendingUpdate is not { DownloadUrl: { } url, AssetName: { } name } update) return;
        using var download = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        _updateDownload = download;
        _updatePhase = UpdatePhase.Downloading;
        _updateProgress = default;
        _updateError = null;
        RefreshUpdateNotice();

        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            if (_updateDownload != download || download.IsCancellationRequested) return;
            _updateProgress = value;
            _updateNotice?.RenderProgress(value);
        });
        try
        {
            if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The release asset name is not a file name.");
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CyberManager", "Updates", name);
            await UpdateService.DownloadUpdateAsync(url, path, progress, update.Sha256, download.Token);
            _updatePackage = path;
            _updatePhase = UpdatePhase.Ready;
        }
        catch (OperationCanceledException) when (download.IsCancellationRequested)
        {
            _updatePhase = UpdatePhase.Available;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update download failed: {ex}");
            _updatePhase = UpdatePhase.Available;
            _updateError = "UpdateDownloadFailed";
        }
        finally
        {
            _updateDownload = null;
            if (!_isClosed) RefreshUpdateNotice();
        }
        if (_updateNotice is null && (_updatePhase == UpdatePhase.Ready || _updateError is not null))
            ShowUpdateNotice(activate: false);
    }

    private void FinishPendingUpdate()
    {
        if (_updatePackage is not { } path || !File.Exists(path))
        {
            _updatePhase = UpdatePhase.Available;
            _updateError = "UpdatePackageMissing";
            RefreshUpdateNotice();
            return;
        }
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try { Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)!) { UseShellExecute = true })?.Dispose(); }
            catch (Exception ex) { ReportUpdateActionError("UpdateOpenFolderFailed", ex); }
            return;
        }

        try
        {
            UpdateService.LaunchInstaller(path);
            ExitApplication();
        }
        catch (Exception ex) { ReportUpdateActionError("UpdateInstallFailed", ex); }
    }

    private void OpenUpdateRelease()
    {
        if (_pendingUpdate is not { } update) return;
        try { UpdateService.LaunchReleasesPage(update.ReleaseUrl); }
        catch (Exception ex) { ReportUpdateActionError("UpdateOpenReleaseFailed", ex); }
    }

    private void ReportUpdateActionError(string key, Exception ex)
    {
        Debug.WriteLine($"Update action failed: {ex}");
        _updateError = key;
        RefreshUpdateNotice();
    }
}
