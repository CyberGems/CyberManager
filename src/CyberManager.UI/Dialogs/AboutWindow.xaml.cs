using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

public partial class AboutWindow : Window, IModalAttentionWindow
{
    private const string RepoUrl = "https://github.com/CyberGems/CyberManager";
    private const string WebsiteUrl = "https://cybergems.org";
    private DateTime _lastAttentionTime = DateTime.MinValue;

    public void TriggerAttention() => ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);

    public AboutWindow()
    {
        InitializeComponent();
        Icon = AppIconHelper.CreateManagerImageSource(64) as System.Windows.Media.ImageSource;
        CyberManagerWindowChrome.Apply(this, 12);
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        TitleText.Text = $"{Strings.T("AboutSubtitle")} — CyberManager";
        VersionText.Text = $"{Strings.T("Version")} {UpdateService.GetCurrentVersionLabel()}";
        UpdateStatusText.Text = Strings.T("UpToDate", UpdateService.GetCurrentVersionLabel());
        UpdateBtn.Content = Strings.T("CheckUpdates");
        DescriptionText.Text = Strings.T("Description");
        UpdatesTitle.Text = Strings.T("UpdatesAndMaintenance");
        CheckUpdatesLabel.Text = Strings.T("CheckUpdatesAction");
        CopyrightText.Text = Strings.T("Copyright");
    }

    private async void UpdateCheck_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        UpdateBtn.Content = Strings.T("CheckingUpdates");
        try
        {
            var r = await UpdateService.CheckForUpdatesAsync();
            UpdateStatusText.Text = r.StatusMessage;
            if (r.IsUpdateAvailable)
            {
                var res = MessageBox.Show($"{r.LatestVersionLabel} disponible.\n\n{Strings.T("OpenReleases")}", "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes) OpenUrl(r.ReleaseUrl);
            }
            else
            {
                MessageBox.Show(r.StatusMessage, "CyberManager", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { UpdateBtn.IsEnabled = true; UpdateBtn.Content = Strings.T("CheckUpdates"); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Website_Click(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenUrl(WebsiteUrl); }
    private void Github_Click(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenUrl(RepoUrl); }
    private void Issues_Click(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenUrl($"{RepoUrl}/issues"); }
    private void Releases_Click(object sender, MouseButtonEventArgs e) { e.Handled = true; OpenUrl($"{RepoUrl}/releases"); }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
