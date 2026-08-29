using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.Common.Settings;
using CyberManager.Core.Engine;
using CyberManager.UI.Dialogs;
using CyberManager.UI.Services;

namespace CyberManager.UI;

public partial class MainWindow : Window
{
    private readonly ProcessCollector _collector = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _searchDebounceTimer = new();
    private List<ProcessInfo> _all = new();
    private List<ProcessInfo> _view = new();
    private string _pendingSearch = "";
    private bool _isRefreshing;
    private DateTime _lastSettingsSave = DateTime.MinValue;

    private string _sortColumn = "CpuPercent";
    private ListSortDirection _sortDirection = ListSortDirection.Descending;

    public MainWindow()
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);
        Loaded += OnLoaded;
        Closing += OnClosing;
        _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
        _timer.Tick += (_, _) => _ = RefreshAsync();
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(150);
        _searchDebounceTimer.Tick += (_, _) => { _searchDebounceTimer.Stop(); ApplySortingAndFilter(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (App.Settings.MainWindowBoundsSaved)
            {
                if (App.Settings.MainWindowWidth >= MinWidth) Width = App.Settings.MainWindowWidth;
                if (App.Settings.MainWindowHeight >= MinHeight) Height = App.Settings.MainWindowHeight;
                if (App.Settings.MainWindowLeft > 0 && App.Settings.MainWindowTop > 0)
                {
                    Left = App.Settings.MainWindowLeft;
                    Top = App.Settings.MainWindowTop;
                }
                if (App.Settings.MainWindowMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }

            ApplyLanguage();
            ApplyTheme();
            Topmost = App.Settings.AlwaysOnTop;
            TopmostCheck.IsChecked = Topmost;
            FooterText.Text = Strings.T("Ready");
            _ = RefreshAsync();
            _timer.Start();
        }
        catch (Exception ex)
        {
            FooterText.Text = $"Init error: {ex.Message}";
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (WindowState == WindowState.Normal)
            {
                App.Settings.MainWindowLeft = Left;
                App.Settings.MainWindowTop = Top;
                App.Settings.MainWindowWidth = Width;
                App.Settings.MainWindowHeight = Height;
                App.Settings.MainWindowMaximized = false;
            }
            else if (WindowState == WindowState.Maximized)
            {
                App.Settings.MainWindowMaximized = true;
            }
            App.Settings.MainWindowBoundsSaved = true;
            App.Settings.Save();
        }
        catch { }
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            if (_all.Count == 0)
            {
                FooterText.Text = "Collecting...";
            }

            var data = await _collector.CollectAsync();
            _all = data.ToList();
            ApplySortingAndFilter();

            var totalCpu = _all.Sum(x => x.CpuPercent);
            var totalMem = _all.Sum(x => (double)x.WorkingSetBytes) / (1024.0 * 1024.0 * 1024.0);
            StatsText.Text = $"{_all.Count} {Strings.T("ProcessesCount", _all.Count).Split(' ')[0]}  •  {Strings.T("CpuTotal", totalCpu)}  •  {Strings.T("MemTotal", totalMem)}";
        }
        catch (Exception ex)
        {
            FooterText.Text = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Refresh error: {ex}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void ProcGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var sortMember = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(sortMember)) return;

        ListSortDirection newDirection;
        if (_sortColumn == sortMember)
        {
            newDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            newDirection = (sortMember is "CpuPercent" or "WorkingSetBytes" or "ThreadCount")
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        _sortColumn = sortMember;
        _sortDirection = newDirection;

        foreach (var col in ProcGrid.Columns)
        {
            col.SortDirection = col.SortMemberPath == sortMember ? newDirection : null;
        }

        ApplySortingAndFilter();
    }

    private void ApplySortingAndFilter()
    {
        var q = _pendingSearch.Trim();
        SearchHint.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
        ClearBtn.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;

        IEnumerable<ProcessInfo> filtered;
        if (string.IsNullOrEmpty(q))
        {
            filtered = _all;
        }
        else
        {
            bool isPid = int.TryParse(q, out var pid);
            filtered = _all.Where(x =>
                x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.ExePath.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (isPid && x.Pid == pid));
        }

        bool asc = _sortDirection == ListSortDirection.Ascending;
        _view = (_sortColumn switch
        {
            "Name" => asc ? filtered.OrderBy(x => x.Name).ThenBy(x => x.Pid) : filtered.OrderByDescending(x => x.Name).ThenBy(x => x.Pid),
            "Pid" => asc ? filtered.OrderBy(x => x.Pid) : filtered.OrderByDescending(x => x.Pid),
            "CpuPercent" => asc ? filtered.OrderBy(x => x.CpuPercent).ThenBy(x => x.Name) : filtered.OrderByDescending(x => x.CpuPercent).ThenBy(x => x.Name),
            "WorkingSetBytes" => asc ? filtered.OrderBy(x => x.WorkingSetBytes).ThenBy(x => x.Name) : filtered.OrderByDescending(x => x.WorkingSetBytes).ThenBy(x => x.Name),
            "ThreadCount" => asc ? filtered.OrderBy(x => x.ThreadCount).ThenBy(x => x.Name) : filtered.OrderByDescending(x => x.ThreadCount).ThenBy(x => x.Name),
            "Priority" => asc ? filtered.OrderBy(x => x.Priority).ThenBy(x => x.Name) : filtered.OrderByDescending(x => x.Priority).ThenBy(x => x.Name),
            "ExePath" => asc ? filtered.OrderBy(x => x.ExePath).ThenBy(x => x.Name) : filtered.OrderByDescending(x => x.ExePath).ThenBy(x => x.Name),
            _ => filtered.OrderByDescending(x => x.CpuPercent).ThenBy(x => x.Name)
        }).ToList();

        var prevSelectedPid = Selected?.Pid;
        ProcGrid.ItemsSource = _view;
        if (prevSelectedPid.HasValue)
        {
            var matched = _view.FirstOrDefault(x => x.Pid == prevSelectedPid.Value);
            if (matched != null)
            {
                ProcGrid.SelectedItem = matched;
            }
        }

        EmptyStateText.Visibility = _view.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ProcGrid.Visibility = _view.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (!string.IsNullOrEmpty(q))
        {
            FooterText.Text = Strings.T("ProcessesShown", _view.Count, _all.Count) + $" • {Strings.T("Updated")} {DateTime.Now:HH:mm:ss}";
        }
        else
        {
            FooterText.Text = $"{_view.Count} {Strings.T("Updated")} {DateTime.Now:HH:mm:ss}";
        }
    }

    private ProcessInfo? Selected => ProcGrid.SelectedItem as ProcessInfo;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var w = new AboutWindow { Owner = this };
        w.ShowDialog();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _pendingSearch = SearchBox.Text ?? "";
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        _pendingSearch = "";
        ApplySortingAndFilter();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private void Topmost_Checked(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostCheck.IsChecked == true;
        App.Settings.AlwaysOnTop = Topmost;
        ThrottledSaveSettings();
    }

    private void ThrottledSaveSettings()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastSettingsSave).TotalSeconds < 2)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(2000);
                App.Settings.Save();
                _lastSettingsSave = DateTime.UtcNow;
            }, DispatcherPriority.Background);
            return;
        }
        App.Settings.Save();
        _lastSettingsSave = now;
    }

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        if (MessageBox.Show(Strings.T("KillConfirm", s.Name, s.Pid), "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            ProcessActions.Kill(s.Pid);
            _ = RefreshAsync();
        }
    }

    private void KillTree_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        if (MessageBox.Show(Strings.T("KillTreeConfirm", s.Name), "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            ProcessActions.KillTree(s.Pid);
            _ = RefreshAsync();
        }
    }

    private void Suspend_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        if (MessageBox.Show(Strings.T("SuspendConfirm", s.Name, s.Pid), "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            if (ProcessActions.Suspend(s.Pid))
            {
                s.Status = "Suspended";
                ProcGrid.Items.Refresh();
            }
        }
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        if (MessageBox.Show(Strings.T("ResumeConfirm", s.Name, s.Pid), "CyberManager", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            if (ProcessActions.Resume(s.Pid))
            {
                s.Status = "Running";
                ProcGrid.Items.Refresh();
            }
        }
    }

    private void PriorityNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.Normal);
    private void PriorityAboveNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.AboveNormal);
    private void PriorityHigh_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.High);
    private void PriorityRealTime_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.RealTime);

    private void SetPriority(ProcessPriorityClass priority)
    {
        var s = Selected;
        if (s == null) return;
        if (ProcessActions.SetPriority(s.Pid, priority))
        {
            s.Priority = priority;
            ProcGrid.Items.Refresh();
        }
        else
        {
            MessageBox.Show(Strings.T("ElevationRequired"), Strings.T("ConfirmAction"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null || string.IsNullOrEmpty(s.ExePath)) return;
        try { Clipboard.SetText(s.ExePath); } catch { }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null || string.IsNullOrEmpty(s.ExePath)) return;
        try { Process.Start("explorer.exe", $"/select,\"{s.ExePath}\""); } catch { }
    }

    private void SearchOnline_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        try { Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(s.Name)}") { UseShellExecute = true }); } catch { }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var themes = new[] { AppTheme.CyberManager, AppTheme.Dark, AppTheme.Light };
        var current = App.Settings.Theme;
        var next = themes[(Array.IndexOf(themes, current) + 1) % themes.Length];
        App.Settings.Theme = next;
        ApplyTheme();
        ThrottledSaveSettings();
    }

    private void LangToggle_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Language = App.Settings.Language == Lang.Es ? Lang.En : Lang.Es;
        Strings.Current = App.Settings.Language;
        ApplyLanguage();
        ThrottledSaveSettings();
    }

    private void ApplyTheme()
    {
        ThemeManager.Apply(App.Settings.Theme);
    }

    private void ApplyLanguage()
    {
        try
        {
            SubtitleText.Text = Strings.T("AppSubtitle");
            SearchHint.Text = Strings.T("SearchPlaceholder");
            EmptyStateText.Text = Strings.T("NoProcesses");
            TopmostCheck.Content = Strings.T("AlwaysOnTop");
            RefreshBtnText.Text = $"↻  {Strings.T("Refresh")}";
            RefreshBtn.ToolTip = $"{Strings.T("Refresh")} (F5)";
            KillBtnText.Text = $"✕  {Strings.T("Kill")}";
            KillBtn.ToolTip = $"{Strings.T("Kill")} (Del)";
            FooterText.Text = Strings.T("Ready");

            if (ProcGrid.Columns.Count >= 7)
            {
                ProcGrid.Columns[0].Header = Strings.T("Process");
                ProcGrid.Columns[1].Header = Strings.T("Pid");
                ProcGrid.Columns[2].Header = Strings.T("Cpu");
                ProcGrid.Columns[3].Header = Strings.T("Memory");
                ProcGrid.Columns[4].Header = Strings.T("Threads");
                ProcGrid.Columns[5].Header = Strings.T("Priority");
                ProcGrid.Columns[6].Header = Strings.T("Path");

                foreach (var c in ProcGrid.Columns)
                {
                    c.SortDirection = c.SortMemberPath == _sortColumn ? _sortDirection : null;
                }
            }

            if (ProcGrid.ContextMenu is { } cm && cm.Items.Count >= 10)
            {
                ((MenuItem)cm.Items[0]).Header = Strings.T("Kill");
                ((MenuItem)cm.Items[1]).Header = Strings.T("KillTree");
                ((MenuItem)cm.Items[3]).Header = Strings.T("Suspend");
                ((MenuItem)cm.Items[4]).Header = Strings.T("Resume");
                ((MenuItem)cm.Items[6]).Header = Strings.T("SetPriority");
                if (((MenuItem)cm.Items[6]).Items.Count >= 4)
                {
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[0]).Header = Strings.T("PriorityNormal");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[1]).Header = Strings.T("PriorityAboveNormal");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[2]).Header = Strings.T("PriorityHigh");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[3]).Header = Strings.T("PriorityRealTime");
                }
                ((MenuItem)cm.Items[8]).Header = Strings.T("CopyPath");
                ((MenuItem)cm.Items[9]).Header = Strings.T("OpenFolder");
                if (cm.Items.Count > 10)
                    ((MenuItem)cm.Items[10]).Header = Strings.T("SearchOnline");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyLanguage error: {ex}");
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F5) _ = RefreshAsync();
        if (e.Key == System.Windows.Input.Key.Delete) Kill_Click(this, new RoutedEventArgs());
        if (e.Key == System.Windows.Input.Key.Escape) Close();
        base.OnKeyDown(e);
    }
}
