using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class AboutWindow : Window, IModalAttentionWindow
{
    private const string RepoUrl = "https://github.com/CyberGems/CyberManager";
    private const string WebsiteUrl = "https://cybergems.org";

    private bool _suppressAutoCheckUpdateChange;
    private DateTime _lastAttentionTime = DateTime.MinValue;

    public void TriggerAttention()
    {
        ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);
    }

    public AboutWindow(bool checkUpdatesNow = false)
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as System.Windows.Media.ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);
        LoadContent();
        if (checkUpdatesNow)
        {
            Loaded += (_, _) => UpdateCheckButton_Click(this, new RoutedEventArgs());
        }
    }

    private void LoadContent()
    {
        _suppressAutoCheckUpdateChange = true;
        try
        {
            AutoCheckUpdateCheck.IsChecked = App.Settings.AutoCheckForUpdates;
            RefreshLocalization();
        }
        finally
        {
            _suppressAutoCheckUpdateChange = false;
        }
    }

    public void RefreshLocalization()
    {
        var es = Strings.Current == Lang.Es;
        Title = $"{Strings.T("AboutSubtitle")} ᐧ CyberManager";
        AboutTitleText.Text = $"{Strings.T("AboutSubtitle")} ᐧ CyberManager";
        var currentVerLabel = UpdateService.GetCurrentVersionLabel();
        AboutVersionText.Text = (es ? "Versión " : "Version ") + currentVerLabel;
        AboutDescriptionText.Text = Strings.T("Description");
        UpdatesSectionLbl.Text = Strings.T("UpdatesAndMaintenance");
        AutoUpdateTitleLbl.Text = Strings.T("AutoUpdateTitle");
        AutoUpdateDescLbl.Text = Strings.T("AutoUpdateDesc");
        CheckUpdateTitleLbl.Text = Strings.T("CheckUpdatesAction");
        CheckUpdateDescLbl.Text = Strings.T("CheckUpdatesDesc");
        UpdateBtn.Content = Strings.T("CheckUpdates");
        AboutFooterCopyright.Text = Strings.T("Copyright");
        AboutFooterCopyright.ToolTip = Strings.T("Website");
        AboutFooterWebsiteBtn.ToolTip = Strings.T("Website");
        AboutFooterGithubBtn.ToolTip = Strings.T("GitHub");
        AboutFooterIssuesBtn.ToolTip = Strings.T("Issues");
        AboutFooterReleasesBtn.ToolTip = Strings.T("OpenReleases");
    }

    private void AutoCheckUpdateCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppressAutoCheckUpdateChange) return;
        App.Settings.AutoCheckForUpdates = AutoCheckUpdateCheck.IsChecked == true;
        App.Settings.Save();
    }

    private async void UpdateCheckButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        UpdateBtn.Content = Strings.T("CheckingUpdates");

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            UpdateBtn.IsEnabled = true;
            UpdateBtn.Content = Strings.T("CheckUpdates");

            if (result.IsUpdateAvailable)
            {
                var currentLabel = Strings.T("Current");
                var latestLabel = Strings.T("Latest");
                var promptMessage = Strings.T("UpdatePrompt");
                var currentVerLabel = UpdateService.GetCurrentVersionLabel();
                var msg = $"{currentLabel} {currentVerLabel}\n{latestLabel} {result.LatestVersionLabel}\n\n{promptMessage}";

                var choice = ConfirmDialog.Show(
                    this,
                    Strings.T("UpdateAvailable", result.LatestVersionLabel),
                    msg,
                    Strings.T("Download"),
                    Strings.T("Later"),
                    ConfirmIconType.Info);

                if (choice)
                {
                    await StartUpdateDownloadAsync(result);
                }
            }
            else
            {
                ConfirmDialog.Show(
                    this,
                    Strings.T("CheckUpdatesAction"),
                    result.StatusMessage,
                    Strings.T("Ok"),
                    null,
                    ConfirmIconType.Check);
            }
        }
        catch (Exception ex)
        {
            UpdateBtn.IsEnabled = true;
            UpdateBtn.Content = Strings.T("CheckUpdates");
            ConfirmDialog.Show(
                this,
                Strings.T("CheckUpdatesAction"),
                ex.Message,
                Strings.T("Ok"),
                null,
                ConfirmIconType.Warning);
        }
    }

    public async Task StartUpdateDownloadAsync(UpdateCheckResult result)
    {
        UpdateProgressPanel.Visibility = Visibility.Visible;
        UpdateBtn.IsEnabled = false;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var updatesFolder = Path.Combine(appData, "CyberManager", "Updates");
        var filename = result.AssetName ?? $"CyberManager_setup_{UpdateService.GetRuntimeChannel()}.exe";
        var installerPath = Path.Combine(updatesFolder, filename);

        var progress = new Progress<double>(val =>
        {
            UpdateProgressBar.Value = val;
            UpdateProgressText.Text = string.Format(Strings.T("DownloadingUpdate"), val);
        });

        try
        {
            UpdateProgressBar.Value = 0;
            UpdateProgressText.Text = string.Format(Strings.T("DownloadingUpdate"), 0.0);

            if (string.IsNullOrEmpty(result.DownloadUrl))
                throw new Exception("Direct download link is not available for this release.");

            await UpdateService.DownloadUpdateAsync(result.DownloadUrl, installerPath, progress);

            UpdateProgressText.Text = Strings.T("DownloadComplete");

            ConfirmDialog.Show(
                this,
                Strings.T("DownloadComplete"),
                Strings.T("DownloadCompleteDesc"),
                Strings.T("Ok"),
                null,
                ConfirmIconType.Check);

            UpdateService.LaunchInstallerAndExit(installerPath);
        }
        catch (Exception ex)
        {
            UpdateProgressPanel.Visibility = Visibility.Collapsed;
            UpdateBtn.IsEnabled = true;

            var errorChoice = ConfirmDialog.Show(
                this,
                Strings.T("DownloadFailed"),
                $"{ex.Message}\n\n{(Strings.Current == Lang.Es ? "¿Deseas abrir la página de releases de GitHub en el navegador?" : "Would you like to open the GitHub releases page in your browser?")}",
                Strings.T("OpenBrowser"),
                Strings.T("Cancel"),
                ConfirmIconType.Warning);

            if (errorChoice && !string.IsNullOrEmpty(result.ReleaseUrl))
            {
                OpenUrl(result.ReleaseUrl);
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void AboutFooterWebsite_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(WebsiteUrl);
    }

    private void AboutFooterGithub_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(RepoUrl);
    }

    private void AboutFooterIssues_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl($"{RepoUrl}/issues");
    }

    private void AboutFooterReleases_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl($"{RepoUrl}/releases");
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }
}
