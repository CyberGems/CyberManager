using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.Common.Settings;
using CyberManager.Core.Engine;
using CyberManager.UI.Dialogs;
using CyberManager.UI.Services;
using CyberManager.UI.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace CyberManager.UI;

[SuppressMessage("Design", "CA1001", Justification = "WPF window-owned services are released from the window lifecycle.")]
public partial class MainWindow : Window
{
    private readonly ProcessCollector _collector = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _searchDebounceTimer = new();
    private readonly DispatcherTimer _settingsSaveTimer = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _processActionGate = new(1, 1);
    private readonly ProcessListViewModel _processList = new();
    private readonly HashSet<string> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, DateTime> _terminatedPids = new();
    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly TrayIconService _trayService = new();
    private bool _isExplicitExit;
    private bool _isLoaded;
    private bool _isClosed;
    private int _iconRefreshPending;
    private List<ProcessInfo> _all = new();
    private List<ProcessInfo> _view = new();
    private string _pendingSearch = "";
    private int _refreshInFlight;

    private string _sortColumn = "CpuPercent";
    private ListSortDirection _sortDirection = ListSortDirection.Descending;

    public MainWindow()
    {
        InitializeComponent();
        ProcGrid.ItemsSource = _processList.Items;
        CyberManagerWindowChrome.Apply(this, 12);
        Loaded += OnLoaded;
        Closing += OnClosing;
        IsVisibleChanged += OnVisibilityChanged;
        Closed += OnClosed;
        _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
        _timer.Tick += (_, _) => _ = RefreshAsync(_lifetimeCts.Token);
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(150);
        _searchDebounceTimer.Tick += (_, _) => { _searchDebounceTimer.Stop(); ApplySortingAndFilter(); };
        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(350);
        _settingsSaveTimer.Tick += async (_, _) =>
        {
            _settingsSaveTimer.Stop();
            await App.Settings.SaveAsync(_lifetimeCts.Token);
        };
        PathToIconConverter.IconReady += OnIconReady;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
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
            GroupToggleCheck.IsChecked = App.Settings.GroupProcesses;
            Topmost = App.Settings.AlwaysOnTop;
            TopmostCheck.IsChecked = Topmost;
            KillBtn.IsEnabled = Selected != null;
            FontSizeSlider.Value = App.Settings.RowFontSize > 0 ? App.Settings.RowFontSize : 13;
            ProcGrid.FontSize = FontSizeSlider.Value;
            FontSizeLabel.Text = $"{FontSizeSlider.Value:F0}px";
            FooterText.Text = Strings.T("Ready");

            // Setup System Tray
            _trayService.Initialize(
                this,
                ToggleWindowVisibility,
                OpenSystemInfoFromTray,
                () => _ = RefreshAsync(_lifetimeCts.Token),
                OnToggleAlwaysOnTop,
                OnToggleGroupByApp,
                OnToggleStartWithWindows,
                OnToggleMinimizeToTray,
                OpenSettingsFromTray,
                OpenAboutFromTray,
                ExitApplication);

            // Register Global Hotkey
            _hotkeyService.Register(this, App.Settings.GlobalHotkey);
            _hotkeyService.HotkeyPressed += OnGlobalHotkeyPressed;

            // Start Minimized Check
            var args = Environment.GetCommandLineArgs();
            if (App.Settings.StartMinimized || args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
            {
                Hide();
            }

            // Background update check
            if (App.Settings.AutoCheckForUpdates)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000);
                        var r = await UpdateService.CheckForUpdatesAsync();
                        if (r.IsUpdateAvailable)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                FooterText.Text = $"⭐ {Strings.T("UpdateAvailable", r.LatestVersionLabel)}";
                            });
                        }
                    }
                    catch { }
                });
            }

            _ = RefreshAsync(_lifetimeCts.Token);
            if (IsVisible) _timer.Start();
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

            if (App.Settings.MinimizeToTray && !_isExplicitExit)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            _isClosed = true;
            _timer.Stop();
            _searchDebounceTimer.Stop();
            _settingsSaveTimer.Stop();
            _lifetimeCts.Cancel();
            _hotkeyService.Dispose();
            _trayService.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Close error: {ex}");
        }
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_isLoaded || _isClosed) return;

        if (IsVisible)
        {
            _timer.Start();
            _ = RefreshAsync(_lifetimeCts.Token);
        }
        else
        {
            // There is no useful UI to update while the app is in the tray.
            _timer.Stop();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _timer.Stop();
        _searchDebounceTimer.Stop();
        _settingsSaveTimer.Stop();
        _lifetimeCts.Cancel();
        PathToIconConverter.IconReady -= OnIconReady;
        _lifetimeCts.Dispose();
    }

    private void OnIconReady()
    {
        if (!_isLoaded || _isClosed || Interlocked.Exchange(ref _iconRefreshPending, 1) == 1) return;
        Dispatcher.BeginInvoke(() =>
        {
            Volatile.Write(ref _iconRefreshPending, 0);
            if (!_isClosed) ProcGrid.Items.Refresh();
        }, DispatcherPriority.Background);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_isClosed || Interlocked.Exchange(ref _refreshInFlight, 1) == 1) return;
        try
        {
            if (_all.Count == 0)
            {
                FooterText.Text = "Collecting...";
            }

            var now = DateTime.UtcNow;
            foreach (var kv in _terminatedPids)
            {
                if (kv.Value < now) _terminatedPids.TryRemove(kv.Key, out _);
            }

            var data = await _collector.CollectAsync(cancellationToken);
            _all = data.Where(x => !_terminatedPids.ContainsKey(x.Pid)).ToList();
            ApplySortingAndFilter();

            if (LoaderOverlay.Visibility == Visibility.Visible)
            {
                LoaderOverlay.Visibility = Visibility.Collapsed;
            }

            var sysMetrics = SystemMetricsCollector.Instance.Sample(_all.Count);
            var (cpuHistory, cpuKernelHistory) = SystemMetricsCollector.Instance.GetCpuHistory();
            var (ramGbHistory, ramPctHistory) = SystemMetricsCollector.Instance.GetRamHistory();

            CpuSparkline.Values = cpuHistory;
            CpuSparkline.SecondaryValues = cpuKernelHistory;
            CpuSparklineText.Text = $"{sysMetrics.CpuTotalPercent:F1}%";

            RamSparkline.Values = ramPctHistory;
            RamSparklineText.Text = $"{sysMetrics.UsedRamGb:F1} GB";

            StatsText.Text = $"{Strings.T("ProcessesCount", _all.Count)}  •  {Strings.T("CpuTotal", sysMetrics.CpuTotalPercent)}  •  {Strings.T("MemTotal", sysMetrics.UsedRamGb)}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Keep the last valid snapshot visible instead of showing a blank list
            // when a transient NT query or permission error occurs.
            FooterText.Text = $"{Strings.T("RefreshFailed")}: {ex.Message}";
            Debug.WriteLine($"Refresh error: {ex}");
        }
        finally
        {
            Volatile.Write(ref _refreshInFlight, 0);
        }
    }

    private void SystemInfo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SystemInfoWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void SystemInfoBorder_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            SystemInfo_Click(sender, e);
        }
    }



    private void GroupChevron_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement fe && fe.DataContext is ProcessInfo p && p.IsGroupParent)
        {
            if (_expandedGroups.Contains(p.Name))
            {
                _expandedGroups.Remove(p.Name);
            }
            else
            {
                _expandedGroups.Add(p.Name);
            }
            ApplySortingAndFilter();
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

        _view = ProcessListViewModel.Build(
            _all,
            new ProcessListQuery(
                q,
                App.Settings.ShowIdleProcess,
                App.Settings.GroupProcesses,
                App.Settings.ShowSuspended,
                _sortColumn,
                _sortDirection,
                _expandedGroups));

        var prevSelectedPid = Selected?.Pid;
        _processList.Update(_view);
        if (prevSelectedPid.HasValue)
        {
            var matched = _processList.Items.FirstOrDefault(x => x.Pid == prevSelectedPid.Value);
            if (matched != null)
            {
                ProcGrid.SelectedItem = matched;
            }
        }

        EmptyStateText.Visibility = _view.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ProcGrid.Visibility = _view.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        KillBtn.IsEnabled = Selected != null;

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

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow
        {
            Owner = this,
            OnSettingsChanged = () =>
            {
                ApplySortingAndFilter();
                ApplyTheme();
                ApplyLanguage();
                _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
                Topmost = App.Settings.AlwaysOnTop;
                TopmostCheck.IsChecked = Topmost;
                GroupToggleCheck.IsChecked = App.Settings.GroupProcesses;
                FontSizeSlider.Value = App.Settings.RowFontSize;
                ProcGrid.FontSize = App.Settings.RowFontSize;
                FontSizeLabel.Text = $"{App.Settings.RowFontSize:F0}px";
            }
        };
        dlg.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var w = new AboutWindow { Owner = this };
        w.ShowDialog();
    }

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

    private void GroupToggle_Checked(object sender, RoutedEventArgs e)
    {
        App.Settings.GroupProcesses = GroupToggleCheck.IsChecked == true;
        ThrottledSaveSettings();
        ApplySortingAndFilter();
    }

    private void GroupToggleLabel_Click(object sender, MouseButtonEventArgs e)
    {
        GroupToggleCheck.IsChecked = !GroupToggleCheck.IsChecked;
    }

    private void Topmost_Checked(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostCheck.IsChecked == true;
        App.Settings.AlwaysOnTop = Topmost;
        ThrottledSaveSettings();
    }

    private void TopmostLabel_Click(object sender, MouseButtonEventArgs e)
    {
        TopmostCheck.IsChecked = !TopmostCheck.IsChecked;
    }

    private void ProcGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        KillBtn.IsEnabled = Selected != null;
    }

    private void ThrottledSaveSettings()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async void Kill_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var msg = Strings.T("KillGroupConfirm", s.InstanceCount, s.Name);
            if (ConfirmDialog.ShowProcess(this, Strings.T("Kill"), msg, s.ExePath, Strings.T("Kill"), Strings.T("Cancel"), isDanger: true))
            {
                var pidsToKill = s.Children.Select(c => c.Pid).ToList();
                if (!pidsToKill.Contains(s.Pid)) pidsToKill.Add(s.Pid);
                var results = await RunProcessActionsAsync(pidsToKill, ProcessActions.TryKill);
                ApplyActionResults(results);
            }
            return;
        }

        if (ConfirmDialog.ShowProcess(this, Strings.T("Kill"), Strings.T("KillConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Kill"), Strings.T("Cancel"), isDanger: true))
        {
            var result = await Task.Run(() => ProcessActions.TryKill(s.Pid), _lifetimeCts.Token);
            ApplyActionResults(new Dictionary<int, ProcessActions.ActionResult> { [s.Pid] = result });
        }
    }

    private async void KillTree_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        if (ConfirmDialog.ShowProcess(this, Strings.T("KillTree"), Strings.T("KillTreeConfirm", s.Name), s.ExePath, Strings.T("KillTree"), Strings.T("Cancel"), isDanger: true))
        {
            int rootPid = s.Pid;
            var pidsToKill = s.IsGroupParent && s.Children.Count > 0 ? s.Children.Select(c => c.Pid).ToList() : new List<int> { rootPid };
            if (!pidsToKill.Contains(rootPid)) pidsToKill.Add(rootPid);
            var result = await Task.Run(() => ProcessActions.TryKillTree(rootPid), _lifetimeCts.Token);
            var results = pidsToKill.ToDictionary(pid => pid, _ => result);
            ApplyActionResults(results);
        }
    }

    private async void Suspend_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var msg = Strings.T("SuspendGroupConfirm", s.InstanceCount, s.Name);
            if (ConfirmDialog.ShowProcess(this, Strings.T("Suspend"), msg, s.ExePath, Strings.T("Suspend"), Strings.T("Cancel"), isDanger: false))
            {
                var results = await RunProcessActionsAsync(s.Children.Select(c => c.Pid), ProcessActions.TrySuspend);
                foreach (var c in s.Children)
                {
                    if (results.TryGetValue(c.Pid, out var result) && result.Succeeded)
                    {
                        c.Status = "Suspended";
                    }
                }
                s.Status = s.Children.All(c => c.Status == "Suspended") ? "Suspended" : "Running";
                s.DimWhenSuspended = App.Settings.ShowSuspended && s.Status == "Suspended";
                ApplySortingAndFilter();
                ShowActionFailures(results.Values);
            }
            return;
        }

        if (ConfirmDialog.ShowProcess(this, Strings.T("Suspend"), Strings.T("SuspendConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Suspend"), Strings.T("Cancel"), isDanger: false))
        {
            var result = await Task.Run(() => ProcessActions.TrySuspend(s.Pid), _lifetimeCts.Token);
            if (result.Succeeded)
            {
                s.Status = "Suspended";
                s.DimWhenSuspended = App.Settings.ShowSuspended;
            }
            else ShowActionFailures(new[] { result });
        }
    }

    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var msg = Strings.T("ResumeGroupConfirm", s.InstanceCount, s.Name);
            if (ConfirmDialog.ShowProcess(this, Strings.T("Resume"), msg, s.ExePath, Strings.T("Resume"), Strings.T("Cancel"), isDanger: false))
            {
                var results = await RunProcessActionsAsync(s.Children.Select(c => c.Pid), ProcessActions.TryResume);
                foreach (var c in s.Children)
                {
                    if (results.TryGetValue(c.Pid, out var result) && result.Succeeded)
                    {
                        c.Status = "Running";
                        c.DimWhenSuspended = false;
                    }
                }
                s.Status = "Running";
                s.DimWhenSuspended = false;
                ApplySortingAndFilter();
                ShowActionFailures(results.Values);
            }
            return;
        }

        if (ConfirmDialog.ShowProcess(this, Strings.T("Resume"), Strings.T("ResumeConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Resume"), Strings.T("Cancel"), isDanger: false))
        {
            var result = await Task.Run(() => ProcessActions.TryResume(s.Pid), _lifetimeCts.Token);
            if (result.Succeeded)
            {
                s.Status = "Running";
                s.DimWhenSuspended = false;
            }
            else ShowActionFailures(new[] { result });
        }
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProcGrid == null || FontSizeLabel == null) return;
        double val = Math.Round(e.NewValue);
        App.Settings.RowFontSize = val;
        ProcGrid.FontSize = val;
        FontSizeLabel.Text = $"{val:F0}px";
        ThrottledSaveSettings();
    }

    private void PriorityRealTime_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.RealTime);
    private void PriorityHigh_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.High);
    private void PriorityAboveNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.AboveNormal);
    private void PriorityNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.Normal);
    private void PriorityBelowNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.BelowNormal);
    private void PriorityIdle_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.Idle);

    private async void SetPriority(ProcessPriorityClass priority)
    {
        var s = Selected;
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var results = await RunProcessActionsAsync(s.Children.Select(c => c.Pid), pid => ProcessActions.TrySetPriority(pid, priority));
            foreach (var c in s.Children)
            {
                if (results.TryGetValue(c.Pid, out var result) && result.Succeeded)
                {
                    c.Priority = priority;
                    var inAll = _all.FirstOrDefault(x => x.Pid == c.Pid);
                    if (inAll != null) inAll.Priority = priority;
                }
            }
            ApplySortingAndFilter();
            ShowActionFailures(results.Values);
            return;
        }

        var singleResult = await Task.Run(() => ProcessActions.TrySetPriority(s.Pid, priority), _lifetimeCts.Token);
        if (singleResult.Succeeded)
        {
            s.Priority = priority;
            var inAll = _all.FirstOrDefault(x => x.Pid == s.Pid);
            if (inAll != null) inAll.Priority = priority;
        }
        else
        {
            ShowActionFailures(new[] { singleResult });
        }
    }

    private async Task<Dictionary<int, ProcessActions.ActionResult>> RunProcessActionsAsync(
        IEnumerable<int> processIds,
        Func<int, ProcessActions.ActionResult> action)
    {
        await _processActionGate.WaitAsync(_lifetimeCts.Token);
        try
        {
            var results = new System.Collections.Concurrent.ConcurrentDictionary<int, ProcessActions.ActionResult>();
            var ids = processIds.Distinct().ToArray();
            await Task.Run(() =>
            {
                Parallel.ForEach(
                    ids,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 4,
                        CancellationToken = _lifetimeCts.Token
                    },
                    pid => results[pid] = action(pid));
            }, _lifetimeCts.Token);
            return results.ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        finally
        {
            _processActionGate.Release();
        }
    }

    private void ApplyActionResults(IReadOnlyDictionary<int, ProcessActions.ActionResult> results)
    {
        var successfulPids = results
            .Where(pair => pair.Value.Succeeded)
            .Select(pair => pair.Key)
            .ToHashSet();

        foreach (var pid in successfulPids)
        {
            _terminatedPids[pid] = DateTime.UtcNow.AddSeconds(2);
        }

        if (successfulPids.Count > 0)
        {
            _all.RemoveAll(process => successfulPids.Contains(process.Pid));
            ApplySortingAndFilter();
        }

        ShowActionFailures(results.Values);
    }

    private void ShowActionFailures(IEnumerable<ProcessActions.ActionResult> results)
    {
        var issues = results
            .Where(result => !result.Succeeded || result.UsedFallback)
            .Select(result => result.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct()
            .ToList();

        if (issues.Count == 0) return;

        FooterText.Text = issues[0]!;
        ConfirmDialog.Show(
            this,
            Strings.T("ProcessActionFailed"),
            string.Join(Environment.NewLine, issues.Take(3)),
            Strings.T("Ok"),
            null,
            ConfirmIconType.Warning);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var s = Selected;
        if (s == null) return;
        var textToCopy = !string.IsNullOrEmpty(s.ExePath) ? s.ExePath : s.Name;
        try
        {
            Clipboard.SetText(textToCopy);
            FooterText.Text = Strings.T("PathCopied");
        }
        catch { }
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


    private static void ApplyTheme()
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
            LoaderTitle.Text = Strings.T("CollectingProcesses");
            LoaderSub.Text = Strings.T("InitializingNtEngine");
            GroupToggleLabel.Text = Strings.T("GroupByApp");
            TopmostLabel.Text = Strings.T("AlwaysOnTop");
            FontSizeSlider.ToolTip = Strings.T("TextSize");
            RefreshBtnText.Text = Strings.T("Refresh");
            RefreshBtn.ToolTip = $"{Strings.T("Refresh")} (F5)";
            KillBtnText.Text = Strings.T("Kill");
            KillBtn.ToolTip = $"{Strings.T("Kill")} (Del)";
            SettingsBtn.ToolTip = $"{Strings.T("Settings")} (Ctrl+,)";
            AboutBtn.ToolTip = Strings.T("About");
            MinimizeBtn.ToolTip = Strings.T("Minimize");
            MaximizeBtn.ToolTip = Strings.T("Maximize");
            CloseBtn.ToolTip = Strings.T("Close");
            CpuSparklineBorder.ToolTip = $"{Strings.T("CpuHistory")} ({Strings.T("OpenSystemInfoTip")})";
            RamSparklineBorder.ToolTip = $"{Strings.T("MemoryHistory")} ({Strings.T("OpenSystemInfoTip")})";
            FooterText.Text = Strings.T("Ready");
            _trayService.UpdateLocalization();

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
                if (((MenuItem)cm.Items[6]).Items.Count >= 6)
                {
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[0]).Header = Strings.T("PriorityRealTime");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[1]).Header = Strings.T("PriorityHigh");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[2]).Header = Strings.T("PriorityAboveNormal");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[3]).Header = Strings.T("PriorityNormal");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[4]).Header = Strings.T("PriorityBelowNormal");
                    ((MenuItem)((MenuItem)cm.Items[6]).Items[5]).Header = Strings.T("PriorityIdle");
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

    #region Keyboard Navigation & Shortcuts

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            e.Handled = true;
            FocusSearchBox();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E)
        {
            e.Handled = true;
            FocusSearchBox();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.G)
        {
            e.Handled = true;
            GroupToggleCheck.IsChecked = !GroupToggleCheck.IsChecked;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
        {
            e.Handled = true;
            SystemInfo_Click(sender, e);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemComma || e.Key == Key.OemPeriod))
        {
            e.Handled = true;
            Settings_Click(sender, e);
            return;
        }

        if (e.Key == Key.F5 || (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R))
        {
            e.Handled = true;
            _ = RefreshAsync();
            return;
        }

        if (e.Key == Key.OemQuestion || (e.Key == Key.Divide && Keyboard.Modifiers == ModifierKeys.None))
        {
            if (!SearchBox.IsFocused)
            {
                e.Handled = true;
                FocusSearchBox();
                return;
            }
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down || e.Key == Key.Enter)
        {
            if (ProcGrid.Items.Count > 0)
            {
                e.Handled = true;
                if (ProcGrid.SelectedIndex < 0) ProcGrid.SelectedIndex = 0;
                ProcGrid.Focus();
                var item = ProcGrid.SelectedItem;
                if (item != null)
                {
                    ProcGrid.ScrollIntoView(item);
                    ProcGrid.UpdateLayout();
                    if (ProcGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                    {
                        row.Focus();
                    }
                }
            }
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = "";
            }
            else
            {
                ProcGrid.Focus();
            }
        }
    }

    private void ProcGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Right)
        {
            if (Selected is { IsGroupParent: true, IsExpanded: false } p)
            {
                _expandedGroups.Add(p.Name);
                ApplySortingAndFilter();
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Left)
        {
            if (Selected is { IsGroupParent: true, IsExpanded: true } p)
            {
                _expandedGroups.Remove(p.Name);
                ApplySortingAndFilter();
                e.Handled = true;
                return;
            }
            else if (Selected is { IsGroupChild: true } c)
            {
                var parent = _view.FirstOrDefault(x => x.IsGroupParent && x.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
                if (parent != null)
                {
                    ProcGrid.SelectedItem = parent;
                    ProcGrid.ScrollIntoView(parent);
                }
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Space || e.Key == Key.Enter)
        {
            if (Selected is { IsGroupParent: true } p)
            {
                if (_expandedGroups.Contains(p.Name)) _expandedGroups.Remove(p.Name);
                else _expandedGroups.Add(p.Name);
                ApplySortingAndFilter();
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                KillTree_Click(sender, e);
            }
            else
            {
                Kill_Click(sender, e);
            }
            return;
        }
        else if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            if (ProcGrid.ContextMenu != null && ProcGrid.SelectedItem != null)
            {
                ProcGrid.ContextMenu.PlacementTarget = ProcGrid;
                ProcGrid.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                ProcGrid.ContextMenu.IsOpen = true;
                e.Handled = true;
                return;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            if (Selected != null)
            {
                CopyPath_Click(sender, e);
                e.Handled = true;
                return;
            }
        }

        // Type-to-Search auto-focus
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            if ((e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                SearchBox.Focus();
            }
        }
    }

    #endregion

    #region Tray & Hotkey Management

    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
        {
            if (App.Settings.MinimizeToTray)
            {
                Hide();
            }
            else
            {
                WindowState = WindowState.Minimized;
            }
        }
        else
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
            FocusSearchBox();
        }
    }

    private void OnGlobalHotkeyPressed()
    {
        ToggleWindowVisibility();
    }

    private void OpenSystemInfoFromTray()
    {
        if (!IsVisible)
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }
        var dlg = new SystemInfoWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void OpenSettingsFromTray()
    {
        if (!IsVisible)
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }
        Settings_Click(this, new RoutedEventArgs());
    }

    private void OpenAboutFromTray(bool checkUpdatesNow)
    {
        if (!IsVisible)
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }
        var dlg = new AboutWindow(checkUpdatesNow) { Owner = this };
        dlg.ShowDialog();
    }

    private void OnToggleAlwaysOnTop(bool enable)
    {
        App.Settings.AlwaysOnTop = enable;
        Topmost = enable;
        TopmostCheck.IsChecked = enable;
        App.Settings.Save();
        _trayService.UpdateLocalization();
    }

    private void OnToggleGroupByApp(bool enable)
    {
        App.Settings.GroupProcesses = enable;
        GroupToggleCheck.IsChecked = enable;
        App.Settings.Save();
        ApplySortingAndFilter();
        _trayService.UpdateLocalization();
    }

    private void OnToggleStartWithWindows(bool enable)
    {
        App.Settings.StartWithWindows = enable;
        if (!StartupManager.SetAutoStart(enable))
        {
            App.Settings.StartWithWindows = StartupManager.IsAutoStartEnabled();
            FooterText.Text = Strings.T("AutoStartFailed");
        }
        App.Settings.Save();
        _trayService.UpdateLocalization();
    }

    private void OnToggleMinimizeToTray(bool enable)
    {
        App.Settings.MinimizeToTray = enable;
        App.Settings.Save();
        _trayService.UpdateLocalization();
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        _hotkeyService.Dispose();
        _trayService.Dispose();
        Close();
        Application.Current.Shutdown();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (App.Settings.MinimizeToTray)
        {
            Hide();
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}
