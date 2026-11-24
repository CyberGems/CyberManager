using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Views;

/// <summary>
/// About surface hosted as its own main-window tab (replaces the former About dialog).
/// Identity and description, update checking, the sister-app row and the
/// external-links footer all live here.
/// </summary>
public partial class AboutView : UserControl
{
    private const string RepoUrl = "https://github.com/CyberGems/CyberManager";
    private const string WebsiteUrl = "https://cybergems.org";
    private const string WikiUrl = "https://github.com/CyberGems/CyberManager/wiki";
    private const string DonateUrl = "https://github.com/CyberGems/CyberManager#%EF%B8%8F-donate";

    private const double SuiteIdleOpacity = 0.7;

    private bool _checkingForUpdates;
    private UpdateCheckResult? _lastUpdateCheck;
    private bool _suiteBuilt;
    private DispatcherTimer? _diagTipTimer;

    private Window? OwnerWindow => Window.GetWindow(this);

    public AboutView()
    {
        InitializeComponent();
    }

    public void Activate()
    {
        LoadContent();
    }

    public void Deactivate()
    {
        _diagTipTimer?.Stop();
    }

    private void LoadContent()
    {
        RefreshLocalization();
        EnsureSuiteApps();
    }

    public void RefreshLocalization()
    {
        var es = Strings.Current == Lang.Es;
        AboutTitleText.Text = $"{Strings.T("AboutSubtitle")} ᐧ CyberManager";
        var currentVerLabel = UpdateService.GetCurrentVersionLabel();
        AboutVersionText.Text = (es ? "Versión " : "Version ") + currentVerLabel;
        AboutDescriptionText.Text = Strings.T("Description");
        UpdatesSectionLbl.Text = Strings.T("UpdatesAndMaintenance");
        CheckUpdateTitleLbl.Text = Strings.T("CheckUpdatesAction");
        RefreshUpdateStatus();
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

    private async void UpdateCheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (_checkingForUpdates || OwnerWindow is not MainWindow main) return;
        _checkingForUpdates = true;
        RefreshUpdateStatus();
        try
        {
            _lastUpdateCheck = await main.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not check for updates from About: {ex}");
            _lastUpdateCheck = new UpdateCheckResult(UpdateService.GetCurrentVersion(), null, "", "", null, null,
                null, null, false, "UpdateCheckFailed");
        }
        finally
        {
            _checkingForUpdates = false;
            RefreshUpdateStatus();
        }
    }

    private void RefreshUpdateStatus()
    {
        var checking = Strings.T("CheckingUpdates");
        UpdateBtn.IsEnabled = !_checkingForUpdates;
        UpdateBtn.Content = _checkingForUpdates ? checking : Strings.T("CheckUpdates");
        CheckUpdateDescLbl.Text = _checkingForUpdates ? checking :
            _lastUpdateCheck?.StatusMessage ?? Strings.T("CheckUpdatesDesc");
    }

    private void SuiteMoreBtn_Click(object sender, RoutedEventArgs e) => OpenUrl(WebsiteUrl);

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
                Width = 28,
                Height = 28,
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
        var dpiLine = "Unknown";
        try
        {
            if (OwnerWindow is { } win)
            {
                var dpi = VisualTreeHelper.GetDpi(win);
                dpiLine = $"{SystemParameters.PrimaryScreenWidth:0}x{SystemParameters.PrimaryScreenHeight:0}, DPI {dpi.PixelsPerInchX:0} ({dpi.DpiScaleX * 100:0}%)";
            }
        }
        catch { }

        return string.Join(Environment.NewLine,
            "CyberManager diagnostic report",
            "",
            $"- Version: {UpdateService.GetCurrentVersionLabel()}",
            $"- Install: {DescribeInstall()}",
            $"- OS: {RuntimeInformation.OSDescription}",
            $"- Display: {dpiLine}",
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

    private void AboutFooterWebsite_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(WebsiteUrl);
    }

    private void AboutFooterWebsiteBtn_Click(object sender, RoutedEventArgs e) => OpenUrl(WebsiteUrl);

    private void AboutFooterDocs_Click(object sender, RoutedEventArgs e) => OpenUrl(WikiUrl);

    private void AboutFooterGithub_Click(object sender, RoutedEventArgs e) => OpenUrl(RepoUrl);

    private void AboutFooterIssues_Click(object sender, RoutedEventArgs e) => OpenUrl($"{RepoUrl}/issues");

    private void AboutFooterReleases_Click(object sender, RoutedEventArgs e) => OpenUrl($"{RepoUrl}/releases");

    private void AboutFooterDonate_Click(object sender, RoutedEventArgs e) => OpenUrl(DonateUrl);

    private void AboutFooterDiag_Click(object sender, RoutedEventArgs e)
    {
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
