using System.Diagnostics;
using System.Globalization;
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
    private const string WikiUrl = "https://cybergems.org/apps/cybermanager";
    private const string DonateUrl = "https://ko-fi.com/cybergems";

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
        AboutFooterDocsBtn.ToolTip = Strings.T("OnlineDocs");
        AboutFooterGithubBtn.ToolTip = Strings.T("GitHub");
        AboutFooterIssuesBtn.ToolTip = Strings.T("Issues");
        AboutFooterReleasesBtn.ToolTip = Strings.T("OpenReleases");
        AboutFooterDonateBtn.ToolTip = Strings.T("DonateProject");
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
            UpdateProgressText.Text = string.Format(CultureInfo.CurrentCulture, Strings.T("DownloadingUpdate"), val);
        });

        try
        {
            UpdateProgressBar.Value = 0;
            UpdateProgressText.Text = string.Format(CultureInfo.CurrentCulture, Strings.T("DownloadingUpdate"), 0.0);

            if (string.IsNullOrEmpty(result.DownloadUrl))
                throw new InvalidOperationException("Direct download link is not available for this release.");

            await UpdateService.DownloadUpdateAsync(result.DownloadUrl, installerPath, progress, result.Sha256);

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

    private void AboutFooterDocs_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(WikiUrl);
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

    private void AboutFooterDonate_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(DonateUrl);
    }

    private void AboutFooterCopyright_MouseEnter(object sender, MouseEventArgs e)
    {
        AboutFooterCopyright.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
    }

    private void AboutFooterCopyright_MouseLeave(object sender, MouseEventArgs e)
    {
        AboutFooterCopyright.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SubTextBrush");
    }

    private void AboutFooterIcon_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border border) return;
        border.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "CardSecondaryBrush");
        SetFooterIconAccent(border, primary: true);
    }

    private void AboutFooterIcon_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border border) return;
        border.Background = System.Windows.Media.Brushes.Transparent;
        SetFooterIconAccent(border, primary: false);
    }

    private void SetFooterIconAccent(System.Windows.Controls.Border border, bool primary)
    {
        var brushKey = primary ? "TextBrush" : "SubTextBrush";
        if (border == AboutFooterWebsiteBtn)
        {
            AboutFooterWebsiteIcon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
        }
        else if (border == AboutFooterDocsBtn)
        {
            AboutFooterDocsBody.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
        }
        else if (border == AboutFooterGithubBtn)
        {
            AboutFooterGithubIcon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brushKey);
        }
        else if (border == AboutFooterIssuesBtn)
        {
            AboutFooterIssuesIcon1.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterIssuesIcon2.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterIssuesIcon3.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterIssuesIcon4.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterIssuesIcon5.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
        }
        else if (border == AboutFooterReleasesBtn)
        {
            AboutFooterTagBody.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterTagDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brushKey);
        }
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
