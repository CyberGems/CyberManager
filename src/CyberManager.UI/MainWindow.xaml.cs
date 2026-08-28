using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CyberManager.Common.Models;
using CyberManager.Core.Engine;
using CyberManager.UI.Dialogs;
using CyberManager.UI.Services;

namespace CyberManager.UI;

public partial class MainWindow : Window
{
    private readonly ProcessCollector _collector = new();
    private readonly DispatcherTimer _timer = new();
    private List<ProcessInfo> _all = new();
    private List<ProcessInfo> _view = new();

    public MainWindow()
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);
        Loaded += OnLoaded;
        _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
        _timer.Tick += (_, _) => Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Topmost = App.Settings.AlwaysOnTop;
        TopmostCheck.IsChecked = Topmost;
        Refresh();
        _timer.Start();
    }

    private void Refresh()
    {
        try
        {
            var data = _collector.Collect().OrderByDescending(x => x.CpuPercent).ThenBy(x => x.Name).ToList();
            _all = data;
            ApplyFilter();
            var totalCpu = data.Sum(x => x.CpuPercent);
            var totalMem = data.Sum(x => (double)x.WorkingSetBytes) / (1024 * 1024 * 1024);
            StatsText.Text = $"{data.Count} procesos  •  CPU {totalCpu:F1}%  •  RAM {totalMem:F1} GB";
            FooterText.Text = $"{_view.Count} mostrados • Actualizado {DateTime.Now:HH:mm:ss}";
        }
        catch { }
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";
        SearchHint.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
        ClearBtn.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;
        if (string.IsNullOrEmpty(q)) _view = _all;
        else
        {
            var lower = q.ToLowerInvariant();
            bool isPid = int.TryParse(q, out var pid);
            _view = _all.Where(x => x.Name.ToLowerInvariant().Contains(lower) || x.ExePath.ToLowerInvariant().Contains(lower) || (isPid && x.Pid == pid)).ToList();
        }
        ProcGrid.ItemsSource = _view;
    }

    private ProcessInfo? Selected => ProcGrid.SelectedItem as ProcessInfo;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void About_Click(object sender, RoutedEventArgs e) { var w = new AboutWindow { Owner = this }; w.ShowDialog(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();
    private void ClearSearch_Click(object sender, RoutedEventArgs e) { SearchBox.Clear(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Topmost_Checked(object sender, RoutedEventArgs e) { Topmost = TopmostCheck.IsChecked == true; App.Settings.AlwaysOnTop = Topmost; App.Settings.Save(); }

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected; if (s == null) return;
        if (System.Windows.MessageBox.Show($"¿Finalizar {s.Name} (PID {s.Pid})?", "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        { ProcessActions.Kill(s.Pid); Refresh(); }
    }

    private void KillTree_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected; if (s == null) return;
        if (System.Windows.MessageBox.Show($"¿Finalizar árbol de {s.Name}?", "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        { ProcessActions.KillTree(s.Pid); Refresh(); }
    }

    private void Suspend_Click(object sender, RoutedEventArgs e) { var s = Selected; if (s != null) ProcessActions.Suspend(s.Pid); }
    private void Resume_Click(object sender, RoutedEventArgs e) { var s = Selected; if (s != null) ProcessActions.Resume(s.Pid); }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected; if (s == null || string.IsNullOrEmpty(s.ExePath)) return;
        System.Windows.Clipboard.SetText(s.ExePath);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected; if (s == null || string.IsNullOrEmpty(s.ExePath)) return;
        try { Process.Start("explorer.exe", $"/select,\"{s.ExePath}\""); } catch { }
    }

    private void SearchOnline_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected; if (s == null) return;
        try { Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(s.Name)}") { UseShellExecute = true }); } catch { }
    }

    private void ProcGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Kill_Click(sender, e);

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F5) Refresh();
        if (e.Key == Key.Delete) Kill_Click(this, new RoutedEventArgs());
        if (e.Key == Key.Escape) Close();
        base.OnKeyDown(e);
    }
}
