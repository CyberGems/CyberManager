using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
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
        VersionText.Text = $"Versión {UpdateService.GetCurrentVersionLabel()}";
        UpdateStatusText.Text = $"Estás al día con la versión {UpdateService.GetCurrentVersionLabel()}";
    }

    private async void UpdateCheck_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        UpdateBtn.Content = "Comprobando...";
        try
        {
            var r = await UpdateService.CheckForUpdatesAsync();
            UpdateStatusText.Text = r.StatusMessage;
            if (r.IsUpdateAvailable)
            {
                var res = System.Windows.MessageBox.Show($"{r.LatestVersionLabel} disponible.\n\n¿Abrir releases?", "Actualización", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes) OpenUrl(r.ReleaseUrl);
            }
            else
            {
                System.Windows.MessageBox.Show(r.StatusMessage, "CyberManager", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { UpdateBtn.IsEnabled = true; UpdateBtn.Content = "Comprobar ahora"; }
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
