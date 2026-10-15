using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class AboutWindow : Window, IModalAttentionWindow
{
    private const string RepoUrl = "https://github.com/CyberGems/CyberManager";
    private const string WebsiteUrl = "https://cybergems.org";
    private const string WikiUrl = "https://github.com/CyberGems/CyberManager/wiki";
    private const string DonateUrl = "https://github.com/CyberGems/CyberManager#%EF%B8%8F-donate";

    private const double SuiteIdleOpacity = 0.38;
    private const double MonitorEdgeMargin = 16;

    private bool _suppressAutoCheckUpdateChange;
    private DateTime _lastAttentionTime = DateTime.MinValue;
    private bool _suiteBuilt;
    private DispatcherTimer? _diagTipTimer;

    public void TriggerAttention()
    {
        ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);
    }

    public AboutWindow()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as System.Windows.Media.ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);
        SourceInitialized += AboutWindow_SourceInitialized;
        LoadContent();
    }

    private void AboutWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var workArea = MonitorPlacement.GetNearestWorkArea(Owner ?? this);
        if (workArea.Width <= 0 || workArea.Height <= 0)
            return;

        var maxWidth = Math.Max(1, workArea.Width - MonitorEdgeMargin * 2);
        var maxHeight = Math.Max(1, workArea.Height - MonitorEdgeMargin * 2);

        // Lower the minimums before assigning maximums so even very small work areas
        // can constrain the window without violating WPF's Min/Max size relationship.
        MinWidth = Math.Min(MinWidth, maxWidth);
        MinHeight = Math.Min(MinHeight, maxHeight);
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        Width = Math.Min(Width, maxWidth);
        Height = Math.Min(Height, maxHeight);
    }

    private void LoadContent()
    {
        _suppressAutoCheckUpdateChange = true;
        try
        {
            AutoCheckUpdateCheck.IsChecked = App.Settings.AutoCheckForUpdates;
            RefreshLocalization();
            EnsureSuiteApps();
            ApplySuiteVisibility();
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
        AboutFooterDiagBtn.ToolTip = Strings.T("DiagnosticInfo");
        SuiteSectionLbl.Text = Strings.T("MoreAppsFromCyberGems");
        SuiteMoreBtn.Content = Strings.T("SeeMore");
        SuiteMoreBtn.ToolTip = Strings.T("VisitCyberGems");
        System.Windows.Automation.AutomationProperties.SetName(SuiteMoreBtn, Strings.T("SeeMore"));
        RefreshSuiteTooltips();
        ApplySuiteVisibility();
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
                var choice = ConfirmDialog.ShowUpdate(
                    this, UpdateService.GetCurrentVersionLabel(), result.LatestVersionLabel);

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

    private void AboutFooterDiag_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        try
        {
            Clipboard.SetText(BuildDiagnosticReport());
        }
        catch
        {
            return;
        }

        AboutFooterDiagBtn.ToolTip = Strings.T("DiagnosticInfoCopied");
        _diagTipTimer?.Stop();
        _diagTipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _diagTipTimer.Tick += (_, _) =>
        {
            _diagTipTimer?.Stop();
            AboutFooterDiagBtn.ToolTip = Strings.T("DiagnosticInfo");
        };
        _diagTipTimer.Start();
    }

    private void SuiteMoreBtn_Click(object sender, RoutedEventArgs e) => OpenUrl(WebsiteUrl);

    /// <summary>Shows or hides the sister-app row from the current setting.</summary>
    public void ApplySuiteVisibility()
    {
        SuiteSection.Visibility = App.Settings.ShowSuiteRecommendations
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void EnsureSuiteApps()
    {
        if (_suiteBuilt)
        {
            RefreshSuiteTooltips();
            return;
        }

        _suiteBuilt = true;
        SuiteAppsHost.Children.Clear();
        if (FindResource("SuiteAppButton") is not Style style)
            return;

        foreach (var app in SuiteCatalog.Pick(5))
        {
            BitmapImage bitmap;
            try
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Suite/{app.Slug}.png", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            catch
            {
                continue;
            }

            var image = new System.Windows.Controls.Image
            {
                Width = 30,
                Height = 30,
                Stretch = Stretch.Uniform,
                Opacity = SuiteIdleOpacity,
                IsHitTestVisible = false,
                Source = bitmap
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var button = new System.Windows.Controls.Button
            {
                Style = style,
                Content = image,
                Tag = app
            };
            System.Windows.Automation.AutomationProperties.SetName(button, app.Name);
            button.ToolTip = SuiteTooltip(app);
            button.MouseEnter += (_, _) => FadeSuiteIcon(image, 1);
            button.MouseLeave += (_, _) => FadeSuiteIcon(image, SuiteIdleOpacity);
            button.Click += (_, _) => OpenUrl(app.Site);
            SuiteAppsHost.Children.Add(button);
        }
    }

    private void RefreshSuiteTooltips()
    {
        foreach (var child in SuiteAppsHost.Children)
        {
            if (child is System.Windows.Controls.Button button && button.Tag is SuiteApp app)
                button.ToolTip = SuiteTooltip(app);
        }
    }

    private static string SuiteTooltip(SuiteApp app)
    {
        var pitch = Strings.Current == Lang.Es ? app.PitchEs : app.PitchEn;
        return $"{app.Name}: {pitch}";
    }

    private static void FadeSuiteIcon(System.Windows.Controls.Image image, double to)
    {
        image.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private string BuildDiagnosticReport()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return string.Join(Environment.NewLine,
            "CyberManager diagnostic report",
            "",
            $"- Version: {UpdateService.GetCurrentVersionLabel()}",
            $"- Install: {DescribeInstall()}",
            $"- OS: {RuntimeInformation.OSDescription}",
            $"- Display: {SystemParameters.PrimaryScreenWidth:0}x{SystemParameters.PrimaryScreenHeight:0}, DPI {dpi.PixelsPerInchX:0} ({dpi.DpiScaleX * 100:0}%)",
            $"- Language: {App.Settings.Language}",
            $"- Theme: {App.Settings.Theme}",
            $"- Check for updates on startup: {OnOff(App.Settings.AutoCheckForUpdates)}",
            $"- Suite suggestions: {OnOff(App.Settings.ShowSuiteRecommendations)}",
            $"- Global hotkey: {DescribeHotkey(App.Settings.GlobalHotkey)}",
            $"- Widget hotkey: {DescribeHotkey(App.Settings.WidgetHotkey)}",
            $"- Refresh interval: {App.Settings.RefreshIntervalMs:0} ms",
            $"- Runtime: {RuntimeInformation.FrameworkDescription}",
            $"- Local time: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} ({TimeZoneInfo.Local.DisplayName})");
    }

    private static string DescribeHotkey(string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? "not set" : hotkey;

    private static string OnOff(bool value) => value ? "on" : "off";

    private static string DescribeInstall()
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var installed =
                (!string.IsNullOrEmpty(programFiles) && dir.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(programFilesX86) && dir.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase));
            return installed ? "installed" : "portable or development";
        }
        catch
        {
            return "unknown";
        }
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
        else if (border == AboutFooterDiagBtn)
        {
            AboutFooterDiagBody.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterDiagClip.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
            AboutFooterDiagLines.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
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
