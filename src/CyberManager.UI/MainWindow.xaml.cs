using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.Common.Models;
using CyberManager.Common.Settings;
using CyberManager.Core.Engine;
using CyberManager.UI.Controls;
using CyberManager.UI.Dialogs;
using CyberManager.UI.Services;
using CyberManager.UI.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace CyberManager.UI;

public enum MainTab
{
    Processes,
    Performance,
    Settings
}

[SuppressMessage("Design", "CA1001", Justification = "WPF window-owned services are released from the window lifecycle.")]
public partial class MainWindow : Window
{
    public MainTab CurrentTab { get; private set; } = MainTab.Processes;

    public void SelectTab(MainTab tab, string? subTab = null)
    {
        CurrentTab = tab;

        TabProcessesBtn.IsChecked = tab == MainTab.Processes;
        TabPerformanceBtn.IsChecked = tab == MainTab.Performance;
        TabSettingsBtn.IsChecked = tab == MainTab.Settings;

        ProcessesTabContent.Visibility = tab == MainTab.Processes ? Visibility.Visible : Visibility.Collapsed;
        PerformanceTabContent.Visibility = tab == MainTab.Performance ? Visibility.Visible : Visibility.Collapsed;
        SettingsTabContent.Visibility = tab == MainTab.Settings ? Visibility.Visible : Visibility.Collapsed;

        var accentBrush = (Brush)FindResource("AccentBrush");
        var subTextBrush = (Brush)FindResource("SubTextBrush");

        TabProcessesIcon.Stroke = tab == MainTab.Processes ? accentBrush : subTextBrush;
        TabPerformanceIcon.Stroke = tab == MainTab.Performance ? accentBrush : subTextBrush;
        TabSettingsIcon.Stroke = tab == MainTab.Settings ? accentBrush : subTextBrush;

        if (tab == MainTab.Performance)
        {
            PerformanceTabContent.Activate();
            if (!string.IsNullOrEmpty(subTab))
            {
                PerformanceTabContent.SelectSubTab(subTab);
            }
        }
        else
        {
            PerformanceTabContent.Deactivate();
        }

        if (tab == MainTab.Settings)
        {
            SettingsTabContent.Activate();
        }
        else
        {
            SettingsTabContent.Deactivate();
        }
    }

    private void NavigateToTab(MainTab tab, string? subTab = null)
    {
        if (_isCompactMode)
        {
            ApplyViewMode(compact: false, restoreBounds: false);
        }

        SelectTab(tab, subTab);
    }

    private void TabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string tag)
        {
            var targetTab = tag switch
            {
                "Processes" => MainTab.Processes,
                "Performance" => MainTab.Performance,
                "Settings" => MainTab.Settings,
                _ => MainTab.Processes
            };
            if (CurrentTab != targetTab)
            {
                NavigateToTab(targetTab);
            }
        }
    }
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    private readonly ProcessCollector _collector = new();
    private readonly DispatcherTimer _timer = new();
    private readonly DispatcherTimer _searchDebounceTimer = new();
    private readonly DispatcherTimer _searchHistoryTimer = new();
    private readonly DispatcherTimer _settingsSaveTimer = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _processActionGate = new(1, 1);
    private readonly ProcessListViewModel _processList = new();
    private readonly HashSet<string> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, DateTime> _terminatedPids = new();
    private readonly GlobalHotkeyService _hotkeyService = new();
    private readonly TrayIconService _trayService = new();
    private FloatingMetricsWindow? _metricsWidget;
    private bool _isExplicitExit;
    private bool _isCloseDialogOpen;
    private bool _isLoaded;
    private bool _isClosed;
    private bool _detectionPaused;
    private int _iconRefreshPending;
    private List<ProcessInfo> _all = new();
    private List<ProcessInfo> _view = new();
    private string _pendingSearch = "";
    private int _refreshInFlight;
    private double _lastUsedRamGb;
    private double _lastTotalRamGb;
    private double _lastRamPercent;
    private double _lastCpuPercent;
    private string _lastCpuModel = "";

    // Fallbacks for the theme colours: only used if the resource is missing (design time, tests).
    private static readonly Color CpuFallback = Color.FromRgb(0x10, 0xB9, 0x81);
    private static readonly Color RamFallback = Color.FromRgb(0x24, 0xA6, 0xB5);

    private readonly MetricToolTipContent _cpuTip;
    private readonly MetricToolTipContent _ramTip;
    private ContextMenuTarget? _contextMenuTarget;
    private bool _contextMenuOpen;
    private bool _contextMenuKeepsSelection;
    private ProcessInfo? _primarySelection;
    private bool _refreshAfterContextMenu;
    private bool _contextMenuClosePending;
    private string? _pendingContextMenuMessage;
    private bool _isCompactMode;
    private bool _syncingSelection;
    private bool _suppressSelectionMirror;
    private bool _syncingHeavyFilter;
    private bool _syncingSystemFilter;

    // The list has no rows until the first collection completes, which takes noticeably longer
    // than a refresh on a cold start. Until then the branded loader owns the list area: without
    // this flag the empty state and the loader are two independent signals and the slower one
    // wins, which is how "No processes found" ended up replacing the loader on some starts.
    private bool _hasLoadedOnce;

    // Message of the last failed refresh, so an empty list can explain itself instead of
    // claiming there are no processes. Cleared as soon as a collection succeeds.
    private string? _lastLoadError;

    // Row the current Shift+Arrow run is anchored to. Stored as a process identity rather than a
    // ProcessInfo reference because the list is rebuilt on every refresh (default 800 ms), which
    // replaces the item objects. Null means the next arrow key starts a new run from the row that
    // is selected at that moment.
    private ProcessIdentity? _selectionAnchor;

    // Left-button press on the branding: About opens only if the pointer does not move
    // beyond the drag threshold, so dragging the window from the branding keeps working.
    private Point _brandingPressPoint;
    private bool _brandingPressed;
    private const double BrandingClickThreshold = 4.0;

    private string _sortColumn = "CpuPercent";
    private ListSortDirection _sortDirection = ListSortDirection.Descending;

    private sealed record ProcessIdentity(int Pid, DateTime StartTime);

    private sealed record ContextMenuTarget(
        ProcessInfo Item,
        int Pid,
        DateTime StartTime,
        bool IsGroupParent,
        string Name,
        IReadOnlyList<ProcessIdentity> GroupMembers);

    public MainWindow()
    {
        InitializeComponent();
        CpuSparklineBorder.ToolTip = MetricToolTipContent.Create(out _cpuTip);
        RamSparklineBorder.ToolTip = MetricToolTipContent.Create(out _ramTip);
        ProcGrid.ItemsSource = _processList.Items;
        CompactView.ItemsSource = _processList.Items;
        CompactView.ToggleRequested += CompactView_ToggleRequested;
        CompactView.EndTaskRequested += Kill_Click;
        CompactView.RestartRequested += Restart_Click;
        CompactView.PauseDetectionRequested += (_, _) => SetDetectionPaused(!_detectionPaused);
        CompactView.PinRequested += CompactView_PinRequested;
        CompactView.SettingsRequested += Settings_Click;
        CompactView.MinimizeRequested += Minimize_Click;
        CompactView.CloseRequested += Close_Click;
        CompactView.AboutRequested += About_Click;
        SettingsTabContent.AboutRequested += About_Click;
        CompactView.DragRequested += CompactView_DragRequested;
        CompactView.SearchChanged += CompactView_SearchChanged;
        CompactView.SearchSubmitted += CompactView_SearchSubmitted;
        CompactView.SearchHistoryRequested += SearchHistoryButton_Click;
        CompactView.ClearSelectionRequested += ClearSelectionBtn_Click;
        CompactView.HeavyFilterChanged += CompactView_HeavyFilterChanged;
        CompactView.MoreRequested += CompactView_MoreRequested;
        CompactView.ProcessGrid.SelectionChanged += ProcGrid_SelectionChanged;
        CompactView.ProcessGrid.Sorting += ProcGrid_Sorting;
        CompactView.ProcessGrid.PreviewMouseRightButtonDown += CompactGrid_PreviewMouseRightButtonDown;
        CompactView.ProcessGrid.PreviewKeyDown += ProcGrid_PreviewKeyDown;
        CyberManagerWindowChrome.Apply(this, 8);
        Loaded += OnLoaded;
        Closing += OnClosing;
        IsVisibleChanged += OnVisibilityChanged;
        StateChanged += OnWindowStateChanged;
        Closed += OnClosed;
        _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
        _timer.Tick += (_, _) => _ = RefreshAsync(cancellationToken: _lifetimeCts.Token);
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(150);
        _searchDebounceTimer.Tick += (_, _) => { _searchDebounceTimer.Stop(); ApplySortingAndFilter(); };
        _searchHistoryTimer.Interval = TimeSpan.FromMilliseconds(750);
        _searchHistoryTimer.Tick += (_, _) =>
        {
            _searchHistoryTimer.Stop();
            CommitSearchHistory(_pendingSearch);
        };
        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(350);
        _settingsSaveTimer.Tick += async (_, _) =>
        {
            _settingsSaveTimer.Stop();
            await App.Settings.SaveAsync(_lifetimeCts.Token);
        };
        PathToIconConverter.IconReady += OnIconReady;
        SettingsTabContent.HotkeyChangeRequested += SettingsView_HotkeyChangeRequested;
        SettingsTabContent.WidgetHotkeyChangeRequested += SettingsView_WidgetHotkeyChangeRequested;
        SettingsTabContent.DefaultsReset += SettingsView_DefaultsReset;
        SettingsTabContent.SettingsChanged += () =>
        {
            ApplySortingAndFilter();
            if (!string.Equals(_pendingSearch, App.Settings.SearchText, StringComparison.Ordinal))
            {
                SyncSearchFromSettings();
            }
            ApplyTheme();
            ApplyLanguage();
            if (_isCompactMode != App.Settings.CompactMode)
            {
                ApplyViewMode(App.Settings.CompactMode, restoreBounds: false);
            }
            _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
            Topmost = App.Settings.AlwaysOnTop;
            CompactView.SetPinned(Topmost);
            SetHeavyFilterControls(App.Settings.HeavyProcessesOnly);
            GroupToggleCheck.IsChecked = App.Settings.GroupProcesses;
            SystemFilterCheck.IsChecked = App.Settings.HideSystemProcesses;
            FontSizeSlider.Value = App.Settings.RowFontSize;
            ProcGrid.FontSize = App.Settings.RowFontSize;
            CompactView.RowFontSize = Math.Min(App.Settings.RowFontSize, 13);
            FontSizeLabel.Text = $"{App.Settings.RowFontSize:F0}px";
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        try
        {
            ApplyStoredSorting();
            ApplyLanguage();
            ApplyTheme();
            ApplyColumnVisibility();
            SyncSearchFromSettings();
            GroupToggleCheck.IsChecked = App.Settings.GroupProcesses;
            SystemFilterCheck.IsChecked = App.Settings.HideSystemProcesses;
            Topmost = App.Settings.AlwaysOnTop;
            CompactView.SetPinned(Topmost);
            SetHeavyFilterControls(App.Settings.HeavyProcessesOnly);
            KillBtn.IsEnabled = Selected != null;
            FontSizeSlider.Value = App.Settings.RowFontSize > 0 ? App.Settings.RowFontSize : 13;
            ProcGrid.FontSize = FontSizeSlider.Value;
            CompactView.RowFontSize = Math.Min(FontSizeSlider.Value, 13);
            FontSizeLabel.Text = $"{FontSizeSlider.Value:F0}px";
            SetStatus(Strings.T("Ready"));
            ApplyViewMode(App.Settings.CompactMode, restoreBounds: true);

            // Setup System Tray
            _trayService.Initialize(
                this,
                ToggleTrayVisibility,
                OpenSystemInfoFromTray,
                () => _ = RefreshAsync(force: true, cancellationToken: _lifetimeCts.Token),
                ToggleMetricsWidget,
                OpenSettingsFromTray,
                OpenAboutFromTray,
                ExitApplication);

            // Register Global Hotkeys. The widget shortcut is best-effort: failing to claim it
            // must not stop the app, so unlike the main one it does not report a status.
            if (!_hotkeyService.Register(this, GlobalHotkeyService.MainHotkeyId, App.Settings.GlobalHotkey))
            {
                SetStatus(Strings.T("GlobalHotkeyUnavailable"));
            }
            _hotkeyService.Register(this, GlobalHotkeyService.WidgetHotkeyId, App.Settings.WidgetHotkey);
            _hotkeyService.HotkeyPressed += OnGlobalHotkeyPressed;

            // Restore the floating widget when it was left open.
            if (App.Settings.WidgetVisible)
            {
                SetMetricsWidgetVisible(true);
            }

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
                                SetStatus($"⭐ {Strings.T("UpdateAvailable", r.LatestVersionLabel)}");
                            });
                        }
                    }
                    catch { }
                });
            }

            _ = RefreshAsync(cancellationToken: _lifetimeCts.Token);
            UpdateTimerState();
        }
        catch (Exception ex)
        {
            SetStatus($"Init error: {ex.Message}");
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // A user-driven close (X button, Alt+F4, taskbar close) always asks what to do
            // first, unless the choice has already been remembered. An explicit exit coming
            // from the tray menu or a shutdown skips the question entirely.
            if (!_isExplicitExit && !_isCloseDialogOpen)
            {
                e.Cancel = true;
                RequestCloseFromUser();
                return;
            }

            ClearSearchState();
            SaveCurrentWindowBounds();
            App.Settings.Save();

            if (App.Settings.MinimizeToTrayOnClose && !_isExplicitExit)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            _isClosed = true;
            _timer.Stop();
            _searchDebounceTimer.Stop();
            _searchHistoryTimer.Stop();
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

    /// <summary>
    /// Handles a close request coming from the user. When a choice has been remembered the
    /// stored action is applied directly; otherwise the close dialog is shown and its
    /// result is honored.
    /// </summary>
    private void RequestCloseFromUser()
    {
        if (_isCloseDialogOpen) return;

        var remembered = CloseWindowDialog.ReadRememberedAction(App.Settings);
        if (remembered != null)
        {
            ApplyCloseAction(remembered.Value);
            return;
        }

        _isCloseDialogOpen = true;
        CloseWindowAction action;
        try
        {
            var dlg = new CloseWindowDialog { Owner = this };
            dlg.ShowDialog();
            action = dlg.ChosenAction;
        }
        finally
        {
            _isCloseDialogOpen = false;
        }

        ApplyCloseAction(action);
    }

    /// <summary>Applies the outcome of the close dialog.</summary>
    private void ApplyCloseAction(CloseWindowAction action)
    {
        switch (action)
        {
            case CloseWindowAction.MinimizeToTray:
                // The explicit choice wins over the "send to tray on close" setting.
                ClearSearchState();
                SaveCurrentWindowBounds();
                App.Settings.Save();
                Hide();
                break;

            case CloseWindowAction.Quit:
                ExitApplication();
                break;

            default:
                // Cancel: leave the window open.
                break;
        }
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_isLoaded || _isClosed) return;

        if (IsVisible && WindowState != WindowState.Minimized)
        {
            _ = RefreshAsync(cancellationToken: _lifetimeCts.Token);
        }

        // The process list has nothing to show while hidden, but the floating widget may still
        // be on screen, so the timer state is recomputed rather than unconditionally stopped.
        UpdateTimerState();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_isLoaded || _isClosed) return;

        if (WindowState == WindowState.Minimized &&
            App.Settings.MinimizeToTrayOnMinimize &&
            IsVisible)
        {
            Hide();
            return;
        }

        UpdateMaximizeButtonIcon();
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            _ = RefreshAsync(cancellationToken: _lifetimeCts.Token);
        }

        UpdateTimerState();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _timer.Stop();
        _searchDebounceTimer.Stop();
        _searchHistoryTimer.Stop();
        _settingsSaveTimer.Stop();
        _lifetimeCts.Cancel();
        _metricsWidget?.Close();
        PathToIconConverter.IconReady -= OnIconReady;
        _lifetimeCts.Dispose();
    }

    private void ApplyViewMode(bool compact, bool restoreBounds)
    {
        if (!restoreBounds && _isCompactMode != compact)
        {
            SaveCurrentWindowBounds();
        }

        _isCompactMode = compact;
        App.Settings.CompactMode = compact;

        if (compact)
        {
            if (WorkAreaMaximize.IsFilled(this))
            {
                WorkAreaMaximize.Restore(this);
            }
            WindowState = WindowState.Normal;
            MinWidth = 500;
            MinHeight = 220;
            if (restoreBounds || App.Settings.CompactWindowBoundsSaved)
            {
                RestoreWindowBounds(compact: true);
            }
            else
            {
                Width = App.Settings.CompactWindowWidth;
                Height = App.Settings.CompactWindowHeight;
            }

            FullTitleBar.Visibility = Visibility.Collapsed;
            ProcessesTabContent.Visibility = Visibility.Collapsed;
            PerformanceTabContent.Visibility = Visibility.Collapsed;
            SettingsTabContent.Visibility = Visibility.Collapsed;
            CompactViewHost.Visibility = Visibility.Visible;
        }
        else
        {
            MinWidth = 900;
            MinHeight = 520;
            if (restoreBounds || App.Settings.MainWindowBoundsSaved)
            {
                RestoreWindowBounds(compact: false);
            }
            else
            {
                Width = App.Settings.MainWindowWidth;
                Height = App.Settings.MainWindowHeight;
            }

            FullTitleBar.Visibility = Visibility.Visible;
            CompactViewHost.Visibility = Visibility.Collapsed;
            SelectTab(CurrentTab);
        }

        AttachMoreMenuToActiveView();
        CompactView.RowFontSize = Math.Min(App.Settings.RowFontSize, 13);
        CompactView.SelectedItem = Selected;
        KeepWindowInWorkArea();
        UpdateMaximizeButtonIcon();
        if (_contextMenuOpen)
        {
            KeepContextTargetVisible();
        }
        if (!restoreBounds)
        {
            SaveCurrentWindowBounds();
            ThrottledSaveSettings();
        }
    }

    private void KeepWindowInWorkArea()
    {
        if (WindowState != WindowState.Normal) return;

        // A saved position may point at a monitor that is gone (unplugged, rearranged, or a
        // different DPI layout). When the top-left corner no longer falls on any monitor, recenter
        // on the primary one so the window cannot open where the user cannot reach it.
        if (!MonitorPlacement.IsPointOnAnyMonitor(this, Left, Top))
        {
            var primary = SystemParameters.WorkArea;
            Width = Math.Min(Width, primary.Width);
            Height = Math.Min(Height, primary.Height);
            Left = primary.Left + Math.Max(0, (primary.Width - Width) / 2);
            Top = primary.Top + Math.Max(0, (primary.Height - Height) / 2);
            return;
        }

        var workArea = MonitorPlacement.GetNearestWorkArea(this);
        if (workArea.Width <= 0 || workArea.Height <= 0) return;

        // A minimum size larger than the current monitor can make it impossible
        // to keep the full window on-screen, so lower it for this monitor.
        MinWidth = Math.Min(MinWidth, workArea.Width);
        MinHeight = Math.Min(MinHeight, workArea.Height);
        Width = Math.Min(Width, workArea.Width);
        Height = Math.Min(Height, workArea.Height);

        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
    }

    private void RestoreWindowBounds(bool compact)
    {
        if (compact)
        {
            if (double.IsFinite(App.Settings.CompactWindowWidth) && App.Settings.CompactWindowWidth >= MinWidth)
                Width = App.Settings.CompactWindowWidth;
            if (double.IsFinite(App.Settings.CompactWindowHeight) && App.Settings.CompactWindowHeight >= MinHeight)
                Height = App.Settings.CompactWindowHeight;
            if (double.IsFinite(App.Settings.CompactWindowLeft) && double.IsFinite(App.Settings.CompactWindowTop))
            {
                Left = App.Settings.CompactWindowLeft;
                Top = App.Settings.CompactWindowTop;
            }

            WindowState = WindowState.Normal;
            return;
        }

        if (double.IsFinite(App.Settings.MainWindowWidth) && App.Settings.MainWindowWidth >= MinWidth)
            Width = App.Settings.MainWindowWidth;
        if (double.IsFinite(App.Settings.MainWindowHeight) && App.Settings.MainWindowHeight >= MinHeight)
            Height = App.Settings.MainWindowHeight;
        if (double.IsFinite(App.Settings.MainWindowLeft) && double.IsFinite(App.Settings.MainWindowTop))
        {
            Left = App.Settings.MainWindowLeft;
            Top = App.Settings.MainWindowTop;
        }

        WindowState = App.Settings.MainWindowMaximized
            ? WindowState.Maximized
            : WindowState.Normal;
    }

    private void SaveCurrentWindowBounds()
    {
        if (_isCompactMode)
        {
            if (WindowState == WindowState.Normal)
            {
                App.Settings.CompactWindowLeft = Left;
                App.Settings.CompactWindowTop = Top;
                App.Settings.CompactWindowWidth = Width;
                App.Settings.CompactWindowHeight = Height;
            }

            App.Settings.CompactWindowBoundsSaved = true;
            return;
        }

        if (WorkAreaMaximize.IsFilled(this) || WindowState == WindowState.Maximized)
        {
            App.Settings.MainWindowMaximized = true;
            App.Settings.MainWindowBoundsSaved = true;
            return;
        }

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
    }

    private void OnIconReady()
    {
        if (!_isLoaded || _isClosed || Interlocked.Exchange(ref _iconRefreshPending, 1) == 1) return;
        Dispatcher.BeginInvoke(() =>
        {
            Volatile.Write(ref _iconRefreshPending, 0);
            if (!_isClosed)
            {
                ProcGrid.Items.Refresh();
                CompactView.RefreshItems();
            }
        }, DispatcherPriority.Background);
    }

    private void SetStatus(string status, string? toolTip = null, string? compactStatus = null)
    {
        FooterText.Text = status;
        FooterText.ToolTip = toolTip;
        CompactView.SetStatus(compactStatus ?? status);
        CompactView.SetStatusToolTip(toolTip);
    }

    /// <summary>
    /// Refreshes both metric card tooltips. The cards show a percentage and a sparkline, so the
    /// tooltip carries the absolute figures the cards cannot fit — the CPU model, the installed and
    /// used memory — plus the click affordance that opens System Information. The content is
    /// updated in place rather than rebuilt, so a tooltip that is already open keeps ticking along.
    /// </summary>
    private void UpdateMetricToolTips()
    {
        var cpuModel = string.IsNullOrWhiteSpace(_lastCpuModel) ? Strings.T("Unknown") : _lastCpuModel;
        var hint = $"{Strings.T("CpuHistory")} ({Strings.T("OpenSystemInfoTip")})";
        _cpuTip.SetContent(
            Strings.T("Cpu"),
            $"{_lastCpuPercent:F1}%",
            UsageHeat.Solid(_lastCpuPercent, UsageHeat.FromResource("CpuColor", CpuFallback)),
            $"{Strings.T("CpuModel")}: {cpuModel}",
            hint);

        _ramTip.SetContent(
            Strings.T("Ram"),
            $"{_lastRamPercent:F1}%",
            UsageHeat.Solid(_lastRamPercent, UsageHeat.FromResource("RamColor", RamFallback)),
            _lastTotalRamGb > 0
                ? $"{Strings.T("TotalRam", _lastTotalRamGb)}\n{Strings.T("MemTotal", _lastUsedRamGb)}"
                : string.Empty,
            $"{Strings.T("MemoryHistory")} ({Strings.T("OpenSystemInfoTip")})");
    }

    /// <summary>
    /// The metrics timer must run whenever something on screen is showing the samples: the main
    /// window itself, or the floating widget while the main window sits in the tray.
    /// </summary>
    private bool ShouldSampleMetrics =>
        (IsVisible && WindowState != WindowState.Minimized) || _metricsWidget?.IsVisible == true;

    private void UpdateTimerState()
    {
        if (_isClosed) return;

        if (ShouldSampleMetrics) _timer.Start();
        else _timer.Stop();
    }

    private void ToggleMetricsWidget() => SetMetricsWidgetVisible(_metricsWidget?.IsVisible != true);

    /// <summary>
    /// Shows or hides the floating widget and keeps the button, the tray check mark and the
    /// persisted setting in sync.
    /// </summary>
    private void SetMetricsWidgetVisible(bool visible)
    {
        if (_isClosed) return;

        if (visible)
        {
            EnsureMetricsWidget();
            _metricsWidget!.Show();
        }
        else
        {
            _metricsWidget?.Hide();
        }

        App.Settings.WidgetVisible = visible;
        WidgetToggleBtn.IsChecked = visible;
        _trayService.SetWidgetChecked(visible);
        ThrottledSaveSettings();
        UpdateTimerState();

        // Seed the widget right away instead of waiting for the next timer tick. With the main
        // window hidden this only refreshes the metrics, not the process list.
        if (visible) _ = RefreshAsync(cancellationToken: _lifetimeCts.Token);
    }

    private void EnsureMetricsWidget()
    {
        if (_metricsWidget != null) return;

        var widget = new FloatingMetricsWindow();
        widget.CloseRequested += () => SetMetricsWidgetVisible(false);
        widget.AlwaysOnTopChanged += onTop =>
        {
            if (_isClosed) return;

            App.Settings.WidgetAlwaysOnTop = onTop;
            widget.ApplyAlwaysOnTop(onTop);
            ThrottledSaveSettings();
        };
        widget.PositionChanged += (left, top) =>
        {
            if (_isClosed) return;

            App.Settings.WidgetLeft = left;
            App.Settings.WidgetTop = top;
            App.Settings.WidgetBoundsSaved = true;
            ThrottledSaveSettings();
        };
        widget.MenuActionRequested += OnMetricsWidgetMenuAction;

        _metricsWidget = widget;
    }

    private void OnMetricsWidgetMenuAction(WidgetMenuAction action)
    {
        switch (action)
        {
            case WidgetMenuAction.ShowMainWindow:
                ShowFromTray();
                break;
            case WidgetMenuAction.SystemInformation:
                OpenSystemInfoFromTray();
                break;
            case WidgetMenuAction.Settings:
                OpenSettingsFromTray();
                break;
            case WidgetMenuAction.About:
                OpenAboutFromTray(false);
                break;
            case WidgetMenuAction.Exit:
                ExitApplication();
                break;
        }
    }

    private static string FormatHotkeyForDisplay(string hotkey) =>
        string.Join(
            " + ",
            hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (_isClosed || Interlocked.Exchange(ref _refreshInFlight, 1) == 1) return;
        try
        {
            // When detection is frozen the process list keeps its last snapshot, but the
            // system metrics (CPU / RAM) must keep updating so the gauges stay live. An
            // explicit refresh (button, F5, tray menu) passes force: true so it always
            // collects the current state, even while frozen.
            //
            // Collecting the process list is also wasted work while it is off screen, which is
            // the case when the app sits in the tray with only the floating widget visible.
            var listOnScreen = IsVisible && WindowState != WindowState.Minimized;
            if (force || (!_detectionPaused && listOnScreen))
            {
                if (_all.Count == 0)
                {
                    SetStatus(Strings.T("CollectingProcesses"));
                }

                var now = DateTime.UtcNow;
                foreach (var kv in _terminatedPids)
                {
                    if (kv.Value < now) _terminatedPids.TryRemove(kv.Key, out _);
                }

                var data = await _collector.CollectAsync(cancellationToken);
                _all = data.Where(x => !_terminatedPids.ContainsKey(x.Pid)).ToList();
                _lastLoadError = null;
                _hasLoadedOnce = true;
                ApplySortingAndFilter();
                SetListLoading(false);
            }

            var sysMetrics = SystemMetricsCollector.Instance.Sample(_all.Count);
            var (cpuHistory, cpuKernelHistory) = SystemMetricsCollector.Instance.GetCpuHistory();
            var (ramGbHistory, ramPctHistory) = SystemMetricsCollector.Instance.GetRamHistory();

            CpuSparkline.Values = cpuHistory;
            CpuSparkline.SecondaryValues = cpuKernelHistory;
            CpuSparklineText.Text = $"{sysMetrics.CpuTotalPercent:F1}%";
            CompactView.SetSystemMetrics(
                sysMetrics.CpuTotalPercent,
                sysMetrics.MemoryLoadPercent,
                sysMetrics.UsedRamGb,
                sysMetrics.CpuModelName,
                sysMetrics.TotalRamGb);

            RamSparkline.Values = ramPctHistory;
            RamSparklineText.Text = $"{sysMetrics.MemoryLoadPercent:F1}%";

            _lastUsedRamGb = sysMetrics.UsedRamGb;
            _lastTotalRamGb = sysMetrics.TotalRamGb;
            _lastRamPercent = sysMetrics.MemoryLoadPercent;
            _lastCpuPercent = sysMetrics.CpuTotalPercent;
            _lastCpuModel = sysMetrics.CpuModelName ?? "";

            // Heat tint over each card's theme background, so the cards warm up towards red as the
            // machine fills instead of staying a flat colour. The compact pills share the ramp.
            CpuHeatOverlay.Background = UsageHeat.Tint(_lastCpuPercent, UsageHeat.FromResource("CpuColor", CpuFallback));
            RamHeatOverlay.Background = UsageHeat.Tint(_lastRamPercent, UsageHeat.FromResource("RamColor", RamFallback));
            UpdateMetricToolTips();

            if (_metricsWidget is { IsVisible: true } widget)
            {
                widget.UpdateMetrics(
                    sysMetrics.CpuTotalPercent,
                    cpuHistory,
                    cpuKernelHistory,
                    sysMetrics.MemoryLoadPercent,
                    ramPctHistory,
                    sysMetrics.UsedRamGb,
                    sysMetrics.TotalRamGb,
                    sysMetrics.CpuModelName);
            }

            // The footer pill shows the process count only. CPU and RAM already have their own
            // cards and sparklines above, so repeating them here (with a different RAM unit)
            // was redundant and crowded the status line into the controls on the right.
            StatsText.Text = Strings.T("ProcessesCount", _all.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Keep the last valid snapshot visible instead of showing a blank list
            // when a transient NT query or permission error occurs. The loader is dismissed
            // either way: a failure is an answer, and leaving the spinner running would hide
            // the error behind an endless "collecting processes" message.
            var message = $"{Strings.T("RefreshFailed")}: {ex.Message}";
            _lastLoadError = message;
            _hasLoadedOnce = true;
            SetListLoading(false);
            UpdateEmptyState();
            SetStatus(message, message);
            Debug.WriteLine($"Refresh error: {ex}");
        }
        finally
        {
            Volatile.Write(ref _refreshInFlight, 0);
        }
    }

    private void SystemInfo_Click(object sender, RoutedEventArgs e)
    {
        string? sub = null;
        if (sender == CpuSparklineBorder || sender == CpuSparkline) sub = "Cpu";
        else if (sender == RamSparklineBorder || sender == RamSparkline) sub = "Memory";
        NavigateToTab(MainTab.Performance, sub);
    }

    private void CpuCardSysInfo_Click(object sender, RoutedEventArgs e) =>
        NavigateToTab(MainTab.Performance, "Cpu");

    private void RamCardSysInfo_Click(object sender, RoutedEventArgs e) =>
        NavigateToTab(MainTab.Performance, "Memory");

    private void MetricsWidgetItem_Click(object sender, RoutedEventArgs e) => ToggleMetricsWidget();

    /// <summary>
    /// Reflects the widget state each time one of the metrics card menus opens. A checkable menu
    /// item keeps its own state, so it has to be resynced from the real window instead of trusted.
    /// </summary>
    private void MetricsCardMenu_Opened(object sender, RoutedEventArgs e)
    {
        var visible = _metricsWidget?.IsVisible == true;
        CpuMenuWidgetItem.IsChecked = visible;
        RamMenuWidgetItem.IsChecked = visible;
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

        App.Settings.SortColumn = _sortColumn;
        App.Settings.SortAscending = _sortDirection == ListSortDirection.Ascending;
        ThrottledSaveSettings();

        foreach (var col in ProcGrid.Columns.Concat(CompactView.ProcessGrid.Columns))
        {
            col.SortDirection = col.SortMemberPath == sortMember ? newDirection : null;
        }

        ApplySortingAndFilter();
    }

    /// <summary>
    /// Restores the column and direction the user last sorted by, so the ordering survives
    /// restarts. Runs once during load, before the first list build.
    /// </summary>
    private void ApplyStoredSorting()
    {
        if (!string.IsNullOrWhiteSpace(App.Settings.SortColumn))
        {
            _sortColumn = App.Settings.SortColumn;
        }

        _sortDirection = App.Settings.SortAscending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        foreach (var column in ProcGrid.Columns.Concat(CompactView.ProcessGrid.Columns))
        {
            column.SortDirection = column.SortMemberPath == _sortColumn ? _sortDirection : null;
        }
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
                _expandedGroups,
                App.Settings.HeavyProcessesOnly,
                HideSystemProcesses: App.Settings.HideSystemProcesses));

        // The refresh tick rebuilds the item collection every interval, which would otherwise
        // throw away a multi-selection. Snapshot the whole selection (by PID) before the rebuild
        // and re-apply it afterwards, so a batch the user is assembling survives refreshes.
        var previousSelection = GetMultiSelection(ProcGrid);
        if (previousSelection.Count == 0)
        {
            previousSelection = GetMultiSelection(CompactView.ProcessGrid);
        }

        if (_contextMenuOpen)
        {
            _processList.UpdatePreservingOrder(_view);
            _refreshAfterContextMenu = true;

            if (_contextMenuTarget is { } target && !_view.Any(process => MatchesContextTarget(process, target)))
            {
                CloseContextMenuForUnavailableProcess();
            }
            else
            {
                KeepContextTargetVisible();
            }
        }
        else
        {
            _processList.Update(_view);
        }

        RestoreSelection(previousSelection);

        UpdateEmptyState();
        KillBtn.IsEnabled = Selected != null;
        CompactView.SetEndTaskEnabled(Selected != null);
        UpdateRestartControls(Selected);

        string timeStr = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        string status;
        string toolTip;
        // The compact footer is the only status line in that mode, and its pill-free layout
        // hides the process count otherwise visible in the full window, so it always gets
        // the live list count prepended.
        string compactStatus = $"{Strings.T("ProcessesCount", _view.Count)}  •  {Strings.T("Updated")} {timeStr}";
        if (!_hasLoadedOnce)
        {
            // Nothing has been collected yet, so the footer must not claim the list was just
            // updated: the loader is still on screen.
            status = Strings.T("CollectingProcesses");
            toolTip = status;
            compactStatus = status;
        }
        else if (!string.IsNullOrEmpty(q))
        {
            status = Strings.T("ProcessesShown", _view.Count, _all.Count) + $" • {Strings.T("Updated")} {timeStr}";
            toolTip = Strings.T("ProcessesFilterTip", _view.Count, _all.Count, timeStr);
        }
        else
        {
            status = $"{Strings.T("Updated")} {timeStr}";
            toolTip = App.Settings.GroupProcesses
                ? Strings.T("GroupedProcessesTip", _view.Count, _all.Count, timeStr)
                : Strings.T("NormalProcessesTip", _all.Count, timeStr);
        }

        SetStatus(status, toolTip, compactStatus);
    }

    /// <summary>
    /// Shows or hides the branded loading overlay on both process lists. Each mode hosts its own
    /// instance, so routing every change through this single call is what stops a mode switch from
    /// exposing a list that is still loading.
    /// </summary>
    private void SetListLoading(bool loading)
    {
        LoaderOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        CompactView.SetLoading(loading);
    }

    /// <summary>
    /// Single decision point for the "nothing to show" state of both lists. The message is only
    /// allowed to appear once a collection has completed: before that the loader owns the list
    /// area, which is what previously let the empty state race the loader on a slow start.
    /// </summary>
    private void UpdateEmptyState()
    {
        if (!_hasLoadedOnce)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            ProcGrid.Visibility = Visibility.Collapsed;
            CompactView.SetEmptyState(false);
            return;
        }

        var message = ProcessListEmptyState.Resolve(_all.Count, _view.Count, _pendingSearch, _lastLoadError);
        if (message is null)
        {
            EmptyStateText.Visibility = Visibility.Collapsed;
            ProcGrid.Visibility = Visibility.Visible;
            CompactView.SetEmptyState(false);
            return;
        }

        EmptyStateText.Text = message;
        EmptyStateText.Visibility = Visibility.Visible;
        ProcGrid.Visibility = Visibility.Collapsed;
        CompactView.SetEmptyState(true, message);
    }

    private ProcessInfo? Selected => ProcGrid.SelectedItem as ProcessInfo;

    private void SetSelectedProcess(ProcessInfo? process)
    {
        // A click or programmatic selection starts a new navigation run, so the previous
        // Shift+Arrow anchor must not carry over.
        _selectionAnchor = process == null ? null : new ProcessIdentity(process.Pid, process.StartTime);

        _syncingSelection = true;
        try
        {
            ProcGrid.SelectedItem = process;
            CompactView.SelectedItem = process;
            EnsureCurrentCell(ProcGrid, process);
            EnsureCurrentCell(CompactView.ProcessGrid, process);
        }
        finally
        {
            _syncingSelection = false;
        }

        KillBtn.IsEnabled = process != null;
        CompactView.SetEndTaskEnabled(process != null);
        UpdateRestartControls(process);
        UpdateSelectionSummary(process == null
            ? Array.Empty<ProcessInfo>()
            : new[] { process });
    }

    /// <summary>
    /// Gives the grid a current cell for <paramref name="process"/>. A DataGrid whose
    /// <c>CurrentCell</c> is unset silently ignores every keyboard gesture aimed at its rows —
    /// arrow navigation, Shift+Arrow range extension and Ctrl+A all bail out early — so whenever
    /// the application programmatically chooses a row it must also anchor the current cell.
    /// Assigning <c>SelectedItem</c> alone is not enough, because that path does not touch
    /// <c>CurrentCell</c>.
    /// </summary>
    private static void EnsureCurrentCell(DataGrid grid, ProcessInfo? process)
    {
        if (process == null) return;
        if (grid.Columns.Count == 0) return;
        if (grid.ItemContainerGenerator.ContainerFromItem(process) is not DataGridRow) return;

        var column = grid.CurrentColumn ?? grid.Columns[0];
        if (!column.Visibility.Equals(Visibility.Visible))
        {
            column = grid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible) ?? column;
        }

        grid.CurrentCell = new DataGridCellInfo(process, column);
    }

    private void CompactView_ToggleRequested(object? sender, RoutedEventArgs e) =>
        ApplyViewMode(!_isCompactMode, restoreBounds: false);

    private void CompactView_PinRequested(object? sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        App.Settings.AlwaysOnTop = Topmost;
        CompactView.SetPinned(Topmost);
        ThrottledSaveSettings();
        _trayService.UpdateLocalization();
    }

    private void CompactView_DragRequested(object? sender, RoutedEventArgs e)
    {
        // The compact view already resolved double clicks into ToggleRequested, so any drag
        // request that reaches this point is a genuine window move.
        e.Handled = true;
        DragWindow();
    }

    private void CompactView_SearchChanged(object? sender, EventArgs e)
    {
        _pendingSearch = CompactView.SearchText;
        App.Settings.SearchText = _pendingSearch;
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
        ScheduleSearchHistory(_pendingSearch);
        ThrottledSaveSettings();
    }

    private void CompactView_SearchSubmitted(object? sender, EventArgs e) =>
        CommitSearchHistory(CompactView.SearchText);

    private void ScheduleSearchHistory(string query)
    {
        _searchHistoryTimer.Stop();
        if (!string.IsNullOrWhiteSpace(query))
        {
            _searchHistoryTimer.Start();
        }
    }

    private void CommitSearchHistory(string query)
    {
        _searchHistoryTimer.Stop();
        App.Settings.RecordSearch(query);
        ThrottledSaveSettings();
    }

    private void SearchHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement target) return;

        var menu = new ContextMenu
        {
            Style = (Style)FindResource("ModernContextMenu"),
            PlacementTarget = target,
            Placement = PlacementMode.Bottom
        };

        if (App.Settings.RecentSearches.Count == 0)
        {
            // The placeholder itself already says there are no recent searches, so adding a
            // "Recent searches" header above it was redundant noise.
            menu.Items.Add(new MenuItem
            {
                Header = Strings.T("NoRecentSearches"),
                IsEnabled = false,
                Style = (Style)FindResource("SearchHistoryHeaderItem")
            });
        }
        else
        {
            menu.Items.Add(new MenuItem
            {
                Header = Strings.T("RecentSearches"),
                IsEnabled = false,
                Style = (Style)FindResource("SearchHistoryHeaderItem")
            });

            foreach (var query in App.Settings.RecentSearches)
            {
                var item = new MenuItem
                {
                    Header = query,
                    Tag = query,
                    Icon = CreateSearchHistoryClockIcon(),
                    Style = (Style)FindResource("SearchHistoryMenuItem")
                };
                item.Click += SearchHistoryItem_Click;
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator
        {
            Style = (Style)FindResource("SearchHistorySeparator")
        });
        var clearItem = new MenuItem
        {
            Header = Strings.T("ClearSearchHistory"),
            Style = (Style)FindResource("SearchHistoryMenuItem"),
            // Nothing to clear yet: keep the action visible but inert instead of letting a
            // tap wipe an already empty history.
            IsEnabled = App.Settings.RecentSearches.Count > 0,
            Icon = CreateSearchHistoryBroomIcon()
        };
        clearItem.Click += ClearSearchHistory_Click;
        menu.Items.Add(clearItem);

        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>
    /// Builds the small clock glyph shown next to each recent search entry. Each item needs its
    /// own instance because a visual can only have one logical parent.
    /// </summary>
    private static Path CreateSearchHistoryClockIcon()
    {
        var icon = new Path
        {
            Data = Geometry.Parse("M 12,2 A 10,10 0 1 0 22,12 A 10,10 0 0 0 12,2 M 12,6 V 12 L 16,14"),
            StrokeThickness = 1.3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(Path.StrokeProperty, "SubTextBrush");
        return icon;
    }

    /// <summary>
    /// Builds the broom glyph for "Clear search history". It MUST be a single Path with explicit
    /// Width/Height (like the clock icon in the same menu): a wrapper Grid with several Paths
    /// renders each one at natural size and the pieces end up scrambled at 15px. The filled
    /// silhouette (long handle that merges into a wide, flat head with two bristle notches) is
    /// what stays readable at menu size.
    /// </summary>
    private static Path CreateSearchHistoryBroomIcon()
    {
        var icon = new Path
        {
            // Single filled silhouette (see CreateBroomGeometry): no FillRule games needed.
            Data = CreateBroomGeometry(),
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(Path.FillProperty, "SubTextBrush");
        return icon;
    }

    /// <summary>
    /// Broom silhouette in a 24x24 design box: a long handle bar that meets a wide, flat head
    /// whose bottom edge carries two V notches (three bristle fingers). The handle's bottom cut is
    /// horizontal and sits exactly on the head's top edge: the subpaths must only TOUCH, never
    /// overlap, because Geometry.Parse yields a frozen StreamGeometry that fills with EvenOdd (an
    /// overlap would punch a dark hole where the handle enters the head).
    /// </summary>
    private static Geometry CreateBroomGeometry() =>
        Geometry.Parse(
            "M 18.5 1.9 L 20.5 3.1 L 14.04 12 L 12.14 12 Z " +
            "M 9 12 L 13.4 12 L 17.2 19.6 L 14.9 19.6 L 13.8 17.4 L 12.7 19.6 " +
            "L 10 19.6 L 8.9 17.4 L 7.8 19.6 L 5.2 19.6 Z");

    private void SearchHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string query }) return;

        _searchHistoryTimer.Stop();
        SearchBox.Text = query;
        CompactView.SetSearchText(query);
        _pendingSearch = query;
        App.Settings.SearchText = query;
        _searchDebounceTimer.Stop();
        ApplySortingAndFilter();
        CommitSearchHistory(query);
    }

    private void ClearSearchHistory_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.ClearSearchHistory();
        ThrottledSaveSettings();
    }

    private void HeavyFilterToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_syncingHeavyFilter)
        {
            SetHeavyFilterControls(HeavyFilterToggle.IsChecked == true);
        }
    }

    private void CompactView_HeavyFilterChanged(object? sender, EventArgs e) =>
        SetHeavyFilterControls(CompactView.IsHeavyFilterEnabled);

    private void SetHeavyFilterControls(bool enabled)
    {
        var changed = App.Settings.HeavyProcessesOnly != enabled;
        App.Settings.HeavyProcessesOnly = enabled;

        _syncingHeavyFilter = true;
        try
        {
            HeavyFilterToggle.IsChecked = enabled;
            CompactView.SetHeavyFilter(enabled);
        }
        finally
        {
            _syncingHeavyFilter = false;
        }

        if (changed)
        {
            ThrottledSaveSettings();
            ApplySortingAndFilter();
        }
    }

    private ProcessInfo? GetActionTarget(object source)
    {
        if (source is not MenuItem || _contextMenuTarget is not { } target)
        {
            return Selected;
        }

        var currentTarget = _view.FirstOrDefault(process => MatchesContextTarget(process, target));
        if (currentTarget != null)
        {
            return currentTarget;
        }

        CloseContextMenuForUnavailableProcess();
        return null;
    }

    private static bool MatchesContextTarget(ProcessInfo process, ContextMenuTarget target)
    {
        if (process.IsGroupParent != target.IsGroupParent) return false;
        if (target.IsGroupParent)
        {
            return process.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase) &&
                   process.Children.Count == target.GroupMembers.Count &&
                   target.GroupMembers.All(member =>
                       process.Children.Any(child => SameProcessIdentity(child, member)));
        }

        return process.Pid == target.Pid && SameStartTime(process.StartTime, target.StartTime);
    }

    private static bool SameStartTime(DateTime left, DateTime right) =>
        left != default && right != default && left.ToFileTimeUtc() == right.ToFileTimeUtc();

    private static bool SameProcessIdentity(ProcessInfo process, ProcessIdentity target) =>
        process.Pid == target.Pid && SameStartTime(process.StartTime, target.StartTime);

    private void ProcGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Let the column header open its own visibility menu instead of the row context menu.
        if (FindVisualParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) != null) return;

        // The empty strip to the right of the last header is not covered by the column headers
        // presenter, so the hit reaches the grid's ScrollViewer and no menu would open. Compare the
        // pointer against the presenter bounds and open the column menu when it is on the header row.
        if (IsHeaderStripHit(e.OriginalSource as DependencyObject, e.GetPosition(ProcGrid)))
        {
            e.Handled = true;
            ShowColumnVisibilityMenu(ProcGrid);
            return;
        }

        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.DataContext is not ProcessInfo process)
        {
            if (ProcContextMenu.IsOpen) ProcContextMenu.IsOpen = false;
            e.Handled = true;
            return;
        }

        PrepareContextMenu(row, process, ProcGrid);
    }

    /// <summary>
    /// True when the pointer sits on the header row but not on a real column header, i.e. the empty
    /// strip that appears to the right of the last column when the columns do not fill the grid.
    /// </summary>
    private bool IsHeaderStripHit(DependencyObject? source, Point positionInGrid)
    {
        if (source is null) return false;
        // Anything inside a row is a process hit, never a header strip.
        if (FindVisualParent<DataGridRow>(source) != null) return false;

        var presenter = FindVisualChild<DataGridColumnHeadersPresenter>(ProcGrid);
        if (presenter is null || !presenter.IsVisible) return false;

        var origin = presenter.TransformToAncestor(ProcGrid).Transform(new Point(0, 0));
        var bounds = new Rect(origin, presenter.RenderSize);

        // The column headers presenter spans the whole grid width, but the columns only cover its
        // left part. The blank area to the right of the last column is the "dead strip": the pointer
        // lands on the presenter (or the grid's ScrollViewer) with no header under it. Treat any
        // pointer inside the header row band that is not on a header as the strip.
        if (positionInGrid.Y < bounds.Top - 2 || positionInGrid.Y > bounds.Bottom + 2) return false;

        var headerRight = GetLastVisibleHeaderRight();
        return positionInGrid.X > headerRight;
    }

    private double GetLastVisibleHeaderRight()
    {
        var presenter = FindVisualChild<DataGridColumnHeadersPresenter>(ProcGrid);
        if (presenter is null) return 0;

        var right = 0.0;
        var count = VisualTreeHelper.GetChildrenCount(presenter);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(presenter, i) is not DataGridColumnHeader header) continue;
            if (!header.IsVisible) continue;
            var edge = header.TransformToAncestor(ProcGrid).Transform(new Point(header.ActualWidth, 0)).X;
            if (edge > right) right = edge;
        }

        return right;
    }

    private void CompactGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The empty strip of the header row has no header and no row to raise a menu of its own.
        if (CompactView.TryOpenColumnMenuForStrip(
                e.OriginalSource as DependencyObject,
                e.GetPosition(CompactView.ProcessGrid)))
        {
            e.Handled = true;
            return;
        }

        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.DataContext is not ProcessInfo process)
        {
            if (ProcContextMenu.IsOpen) ProcContextMenu.IsOpen = false;
            e.Handled = true;
            return;
        }

        PrepareContextMenu(row, process, CompactView.ProcessGrid);
        ProcContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void ProcGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // A column header provides its own visibility menu. Marking the event handled here
        // would make WPF cancel that menu, so leave it alone and let the header's menu open.
        if (FindVisualParent<DataGridColumnHeader>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)
                  ?? ProcContextMenu.PlacementTarget as DataGridRow;
        if (row?.DataContext is not ProcessInfo process)
        {
            e.Handled = true;
            return;
        }

        PrepareContextMenu(row, process);
        _contextMenuOpen = true;
    }

    private void ProcContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _contextMenuOpen = true;
        if (_contextMenuTarget is { } target)
        {
            UpdateContextMenuAvailability(target.Item);
        }
        KeepContextTargetVisible();
    }

    private void ProcContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        ProcessInfo? primary = null;
        if (_contextMenuKeepsSelection && _primarySelection is { } pinned)
        {
            primary = _view.FirstOrDefault(item => SameProcessIdentity(item, new ProcessIdentity(pinned.Pid, pinned.StartTime)));
        }

        primary ??= Selected;

        if (primary == _contextMenuTarget?.Item || primary == null || !ReferenceEquals(ProcGrid.SelectedItem, primary))
        {
            SetSelectedProcess(primary);
        }

        _contextMenuKeepsSelection = false;
        _primarySelection = null;
        _contextMenuOpen = false;
        _contextMenuClosePending = false;
        ClearContextMenuTarget();

        if (_refreshAfterContextMenu)
        {
            _refreshAfterContextMenu = false;
            ApplySortingAndFilter();
        }

        if (_pendingContextMenuMessage is { } message)
        {
            SetStatus(message);
            _pendingContextMenuMessage = null;
        }
    }

    private void PrepareContextMenu(DataGridRow row, ProcessInfo process, DataGrid? sourceGrid = null)
    {
        sourceGrid ??= ProcGrid;

        // Right-clicking a row that is already part of a multi-selection must keep that
        // selection intact (Explorer behaviour) so batch items such as "End selected" can act on
        // the whole group. Assigning SelectedItem here would collapse it to the clicked row.
        _contextMenuKeepsSelection = IsPartOfMultiSelection(sourceGrid, process);

        if (_contextMenuTarget is { } previous && !ReferenceEquals(previous.Item, process))
        {
            previous.Item.IsContextTarget = false;
        }

        process.IsContextTarget = true;
        _contextMenuTarget = new ContextMenuTarget(
            process,
            process.Pid,
            process.StartTime,
            process.IsGroupParent,
            process.Name,
            process.IsGroupParent
                ? process.Children
                    .Select(child => new ProcessIdentity(child.Pid, child.StartTime))
                    .ToArray()
                : Array.Empty<ProcessIdentity>());

        if (_contextMenuKeepsSelection)
        {
            // Keep the batch but make the clicked row the primary target so keyboard actions and
            // the single-target context items still resolve to what the user pointed at.
            _primarySelection = process;
            sourceGrid.Focus();
            row.Focus();
        }
        else
        {
            SetSelectedProcess(process);
        }

        sourceGrid.ScrollIntoView(process);
        sourceGrid.UpdateLayout();

        var realizedRow = GetRowForItem(sourceGrid, process) ?? row;
        ProcContextMenu.PlacementTarget = realizedRow;
        ProcContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        ProcContextMenu.DataContext = process;
        UpdateContextMenuAvailability(process);
    }

    /// <summary>
    /// True when <paramref name="process"/> is one of several rows flagged as selected in
    /// <paramref name="grid"/>.
    /// </summary>
    private static bool IsPartOfMultiSelection(DataGrid grid, ProcessInfo process) =>
        grid.SelectedItems.Count > 1 && grid.SelectedItems.Contains(process);

    private void UpdateContextMenuAvailability(ProcessInfo process)
    {
        var hasProcessTree = process.IsGroupParent && process.Children.Count > 0;
        var hasSuspendedProcesses = process.IsGroupParent
            ? process.Children.Any(IsSuspended)
            : IsSuspended(process);
        var hasRunningProcesses = process.IsGroupParent
            ? process.Children.Any(child => !IsSuspended(child))
            : !IsSuspended(process);
        var hasExecutablePath = !string.IsNullOrWhiteSpace(process.ExePath);

        ContextKillItem.IsEnabled = process.Pid > 0;

        // Derive the batch state from the same grid the menu was opened on (the compact grid is
        // also mirrored into the full grid, so either one is authoritative). Using the visible
        // grid would be wrong while a view switch is still settling.
        var selection = GetActiveSelection();
        var batch = selection.Count > 1;
        ContextKillSelectionItem.Visibility = batch ? Visibility.Visible : Visibility.Collapsed;
        ContextKillSelectionItem.IsEnabled = batch;
        if (batch)
        {
            ContextKillSelectionItem.Header = Strings.T("EndSelected", selection.Count);
        }

        // "Properties" only ever describes one process, so with several rows selected it would
        // silently show the properties of whichever row happened to be the target. Disable it
        // instead of presenting a misleading dialog.
        ContextPropertiesItem.IsEnabled = !batch && (process.Pid > 0 || hasExecutablePath);
        ContextPropertiesItem.ToolTip = batch ? Strings.T("SingleProcessOnly") : null;

        ContextKillTreeItem.IsEnabled = !batch && hasProcessTree;
        var restartReason = GetRestartDisabledReason(process);
        ContextRestartItem.IsEnabled = !batch && restartReason == null;
        ContextRestartItem.ToolTip = batch ? Strings.T("SingleProcessOnly") : restartReason;
        ContextSuspendItem.IsEnabled = !batch && hasRunningProcesses;
        ContextResumeItem.IsEnabled = !batch && hasSuspendedProcesses;

        // The remaining actions each open a dialog or a shell window for exactly one process.
        // Running them against a batch would fire off several windows at once, which is both
        // confusing and hard to undo, so they are single-selection only as well.
        ContextSetPriorityItem.IsEnabled = !batch && process.Pid > 0;
        ContextCopyPathItem.IsEnabled = !batch && hasExecutablePath;
        ContextOpenFolderItem.IsEnabled = !batch && hasExecutablePath;
        ContextSearchOnlineItem.IsEnabled = !batch && !string.IsNullOrWhiteSpace(process.Name);

        if (batch)
        {
            var tooltip = Strings.T("SingleProcessOnly");
            ContextKillTreeItem.ToolTip = tooltip;
            ContextSuspendItem.ToolTip = tooltip;
            ContextResumeItem.ToolTip = tooltip;
            ContextSetPriorityItem.ToolTip = tooltip;
            ContextCopyPathItem.ToolTip = tooltip;
            ContextOpenFolderItem.ToolTip = tooltip;
            ContextSearchOnlineItem.ToolTip = tooltip;
        }
        else
        {
            ContextKillTreeItem.ToolTip = null;
            ContextSuspendItem.ToolTip = null;
            ContextResumeItem.ToolTip = null;
            ContextSetPriorityItem.ToolTip = null;
            ContextCopyPathItem.ToolTip = null;
            ContextOpenFolderItem.ToolTip = null;
            ContextSearchOnlineItem.ToolTip = null;
        }
    }

    private static bool IsSuspended(ProcessInfo process) =>
        string.Equals(process.Status, "Suspended", StringComparison.OrdinalIgnoreCase);

    private void ClearContextMenuTarget()
    {
        if (_contextMenuTarget is { } target)
        {
            target.Item.IsContextTarget = false;
        }

        _contextMenuTarget = null;
        ProcContextMenu.DataContext = null;
        ProcContextMenu.PlacementTarget = null;
    }

    private void CloseContextMenuForUnavailableProcess()
    {
        _pendingContextMenuMessage = Strings.T("ProcessUnavailable");
        if (_contextMenuClosePending) return;

        _contextMenuClosePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            _contextMenuClosePending = false;
            if (ProcContextMenu.IsOpen)
            {
                ProcContextMenu.IsOpen = false;
            }
            else if (_contextMenuTarget != null)
            {
                _contextMenuOpen = false;
                ClearContextMenuTarget();
            }
        }));
    }

    private void KeepContextTargetVisible()
    {
        if (!_contextMenuOpen || _contextMenuTarget is not { } target) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!_contextMenuOpen || !ReferenceEquals(_contextMenuTarget, target)) return;

            var grid = _isCompactMode ? CompactView.ProcessGrid : ProcGrid;
            grid.ScrollIntoView(target.Item);
            if (GetRowForItem(grid, target.Item) is { } row)
            {
                ProcContextMenu.PlacementTarget = row;
            }
        }));
    }

    private static DataGridRow? GetRowForItem(DataGrid grid, ProcessInfo process) =>
        grid.ItemContainerGenerator.ContainerFromItem(process) as DataGridRow;

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindVisualChild<T>(child) is { } found) return found;
        }

        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T match) return match;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void OuterBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (!IsSearchBarSource(source))
        {
            ClearSearchFocus();
        }

        if (!_isCompactMode || e.ChangedButton != MouseButton.Left) return;

        // The compact view owns its whole surface: its title bar decides "click opens About"
        // vs "drag moves the window", and its buttons, search box, grid and footer must keep
        // receiving their own clicks. Starting a drag here (this is a preview handler, so it
        // runs first) would swallow every one of those, so leave anything inside the compact
        // view to the control itself.
        if (IsDescendantOf(source, CompactView))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ApplyViewMode(!_isCompactMode, restoreBounds: false);
            e.Handled = true;
            return;
        }

        e.Handled = true;
        DragWindow();
    }

    private bool IsSearchBarSource(DependencyObject? source) =>
        IsDescendantOf(source, FullSearchBar) ||
        (_isCompactMode && CompactView.IsSearchBarSource(source));

    private void ClearSearchFocus()
    {
        if (SearchBox.IsKeyboardFocusWithin || CompactView.IsSearchFocused)
        {
            Keyboard.ClearFocus();
        }
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject ancestor)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ancestor)) return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var source = e.OriginalSource as DependencyObject;
        if (IsDescendantOf(source, FullBrandingPanel))
        {
            // Defer the decision to the matching mouse-up: a plain click opens About, while a
            // drag moves the window. Opening About here would make the branding undraggable.
            _brandingPressed = true;
            _brandingPressPoint = e.GetPosition(this);
            e.Handled = true;
            return;
        }

        if (FindVisualParent<ButtonBase>(source) != null ||
            FindVisualParent<TextBox>(source) != null ||
            FindVisualParent<Border>(source)?.Name == "NavTabBarBorder" ||
            FindVisualParent<Border>(source)?.Name == nameof(StatsBorder))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ApplyViewMode(!_isCompactMode, restoreBounds: false);
            e.Handled = true;
            return;
        }

        e.Handled = true;
        DragWindow();
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_brandingPressed) return;
        _brandingPressed = false;

        // A press that released without moving is a click; anything else was a drag attempt.
        var delta = e.GetPosition(this) - _brandingPressPoint;
        if (Math.Abs(delta.X) < BrandingClickThreshold && Math.Abs(delta.Y) < BrandingClickThreshold)
        {
            About_Click(this, e);
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_brandingPressed || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _brandingPressPoint;
        if (Math.Abs(delta.X) < BrandingClickThreshold && Math.Abs(delta.Y) < BrandingClickThreshold) return;

        // Past the threshold the gesture is a drag: hand it to Windows so the move feels native.
        _brandingPressed = false;
        e.Handled = true;
        DragWindow();
    }

    private void DragWindow()
    {
        var previousCursor = Mouse.OverrideCursor;
        try
        {
            Mouse.OverrideCursor = Cursors.SizeAll;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                DragMove();
                return;
            }

            // Let Windows perform the native caption drag immediately. This avoids
            // the small WPF DragMove threshold that is noticeable on busy views.
            ReleaseCapture();
            _ = SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        NavigateToTab(MainTab.Settings);
    }

    private bool SettingsView_HotkeyChangeRequested(string hotkey)
    {
        var previousHotkey = App.Settings.GlobalHotkey;
        if (_hotkeyService.Register(this, GlobalHotkeyService.MainHotkeyId, hotkey))
        {
            App.Settings.GlobalHotkey = hotkey;
            _trayService.UpdateLocalization();
            UpdateWidgetToggleHint();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(previousHotkey))
        {
            _hotkeyService.Register(this, GlobalHotkeyService.MainHotkeyId, previousHotkey);
        }

        SetStatus(Strings.T("GlobalHotkeyUnavailable"));
        return false;
    }

    private bool SettingsView_WidgetHotkeyChangeRequested(string hotkey)
    {
        var previousHotkey = App.Settings.WidgetHotkey;
        if (_hotkeyService.Register(this, GlobalHotkeyService.WidgetHotkeyId, hotkey))
        {
            App.Settings.WidgetHotkey = hotkey;
            UpdateWidgetToggleHint();
            return true;
        }

        if (!string.IsNullOrWhiteSpace(previousHotkey))
        {
            _hotkeyService.Register(this, GlobalHotkeyService.WidgetHotkeyId, previousHotkey);
        }

        SetStatus(Strings.T("GlobalHotkeyUnavailable"));
        return false;
    }

    /// <summary>
    /// Refreshes the toolbar widget button's accessible name and its shortcut hint, which is the
    /// only place the widget shortcut is surfaced outside the settings page.
    /// </summary>
    private void UpdateWidgetToggleHint()
    {
        var label = Strings.T("FloatingWidget");
        WidgetToggleBtn.ToolTip = string.IsNullOrWhiteSpace(App.Settings.WidgetHotkey)
            ? label
            : $"{label} ({FormatHotkeyForDisplay(App.Settings.WidgetHotkey)})";
        AutomationProperties.SetName(WidgetToggleBtn, label);
    }

    private void SettingsView_DefaultsReset()
    {
        WorkAreaMaximize.Restore(this);
        WindowState = WindowState.Normal;
        ApplyViewMode(compact: false, restoreBounds: true);
        Topmost = App.Settings.AlwaysOnTop;
        CompactView.SetPinned(Topmost);
        _timer.Interval = TimeSpan.FromMilliseconds(App.Settings.RefreshIntervalMs);
        ApplyStoredSorting();

        // Resetting the settings also clears the widget preferences, so the widget itself has to
        // follow instead of keeping a state the settings no longer describe.
        if (_metricsWidget is { } widget)
        {
            widget.ApplyAlwaysOnTop(App.Settings.WidgetAlwaysOnTop);
            if (widget.IsVisible != App.Settings.WidgetVisible)
            {
                SetMetricsWidgetVisible(App.Settings.WidgetVisible);
            }
        }
    }

    private void FullModeToggle_Click(object sender, RoutedEventArgs e) =>
        ApplyViewMode(!_isCompactMode, restoreBounds: false);

    // A ContextMenu dismisses itself when the pointer is pressed outside it, which is exactly
    // what happens when the user presses the button that opened it. By the time Click runs the
    // menu is already closed, so a plain IsOpen check would reopen it. Remember the dismissal
    // (only for a short window, so an unrelated later click is unaffected) and treat the
    // following click as a request to keep the menu closed.
    private DateTime _moreMenuDismissedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan MoreMenuReopenGuard = TimeSpan.FromMilliseconds(400);

    private void MoreOptionsMenu_Opened(object sender, RoutedEventArgs e) =>
        _moreMenuDismissedAtUtc = DateTime.MinValue;

    private void MoreOptionsMenu_Closed(object sender, RoutedEventArgs e)
    {
        // Menu items close the menu on mouse up, so a pressed button means an outside press.
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            _moreMenuDismissedAtUtc = DateTime.UtcNow;
        }
    }

    private void MoreBtn_Click(object sender, RoutedEventArgs e) => ToggleMoreMenu(MoreBtn);

    private void MoreExit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void CompactView_MoreRequested(object sender, RoutedEventArgs e) =>
        ToggleMoreMenu(sender as Button ?? MoreBtn);

    /// <summary>
    /// Opens the shared options menu below the given button, or closes it when it is already
    /// open or was just dismissed by the same press. Reusing one menu keeps the compact and the
    /// full view in sync without duplicating the items.
    /// </summary>
    private void ToggleMoreMenu(Button button)
    {
        if (button.ContextMenu is not { } menu) return;

        if (menu.IsOpen || DateTime.UtcNow - _moreMenuDismissedAtUtc < MoreMenuReopenGuard)
        {
            _moreMenuDismissedAtUtc = DateTime.MinValue;
            menu.IsOpen = false;
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>
    /// A ContextMenu can only belong to one button at a time, so the options menu follows the
    /// visible view: the compact title bar owns it in compact mode and the full title bar
    /// otherwise. Both views therefore share a single menu with the same items, handlers and
    /// highlighting, instead of duplicating it.
    /// </summary>
    private void AttachMoreMenuToActiveView()
    {
        var owner = _isCompactMode ? CompactView.MoreButton : MoreBtn;
        if (ReferenceEquals(owner.ContextMenu, MoreOptionsMenu)) return;

        if (ReferenceEquals(MoreBtn.ContextMenu, MoreOptionsMenu)) MoreBtn.ContextMenu = null;
        if (ReferenceEquals(CompactView.MoreButton.ContextMenu, MoreOptionsMenu))
        {
            CompactView.MoreButton.ContextMenu = null;
        }

        owner.ContextMenu = MoreOptionsMenu;
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void MoreDonate_Click(object sender, RoutedEventArgs e)
        => OpenExternalUrl("https://github.com/CyberGems/CyberManager#%EF%B8%8F-donate");

    private void MoreDocs_Click(object sender, RoutedEventArgs e)
        => OpenExternalUrl("https://github.com/CyberGems/CyberManager/wiki");

    private void MoreFaq_Click(object sender, RoutedEventArgs e)
        => OpenExternalUrl("https://github.com/CyberGems/CyberManager/wiki/FAQ");

    private void MoreChangelog_Click(object sender, RoutedEventArgs e)
        => OpenExternalUrl("https://github.com/CyberGems/CyberManager/releases");

    private void MoreWebsite_Click(object sender, RoutedEventArgs e)
        => OpenExternalUrl("https://cybergems.org");

    private void MoreCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutWindow(checkUpdatesNow: true) { Owner = this };
        dlg.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var w = new AboutWindow { Owner = this };
        w.ShowDialog();
    }

    private void SyncSearchFromSettings()
    {
        _pendingSearch = App.Settings.SearchText ?? "";
        SearchBox.Text = _pendingSearch;
        CompactView.SetSearchText(_pendingSearch);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _pendingSearch = SearchBox.Text ?? "";
        App.Settings.SearchText = _pendingSearch;
        CompactView.SetSearchText(_pendingSearch);
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
        ScheduleSearchHistory(_pendingSearch);
        ThrottledSaveSettings();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        ClearSearchState();
    }

    private void ClearSearchState()
    {
        _searchDebounceTimer.Stop();
        SearchBox.Clear();
        _searchDebounceTimer.Stop();
        _pendingSearch = "";
        CompactView.SetSearchText("");
        App.Settings.SearchText = "";
        ApplySortingAndFilter();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync(force: true);

    private void PauseDetection_Click(object sender, RoutedEventArgs e) => SetDetectionPaused(!_detectionPaused);

    private void WidgetToggle_Click(object sender, RoutedEventArgs e) => ToggleMetricsWidget();

    /// <summary>
    /// Freezes or resumes automatic process detection. While frozen the list keeps its last
    /// snapshot and only system metrics keep updating, so the gauges stay live. An explicit
    /// refresh (button, F5, tray menu) still collects the current state on demand.
    /// </summary>
    private void SetDetectionPaused(bool paused)
    {
        if (_detectionPaused == paused) return;
        _detectionPaused = paused;

        PauseDetectionBtn.IsChecked = paused;
        PauseDetectionIcon.Data = Geometry.Parse(paused
            ? "M 5,3 L 19,12 L 5,21 Z"
            : "M 5,3 H 9 V 21 H 5 Z M 15,3 H 19 V 21 H 15 Z");

        PauseDetectionBtn.ToolTip = paused
            ? $"{Strings.T("ResumeDetection")} (Ctrl+P)"
            : $"{Strings.T("PauseDetection")} (Ctrl+P)";
        AutomationProperties.SetName(PauseDetectionBtn, paused
            ? Strings.T("ResumeDetection")
            : Strings.T("PauseDetection"));

        CompactView.SetDetectionPaused(paused);
        SetStatus(paused ? Strings.T("DetectionPaused") : Strings.T("Ready"));
    }

    /// <summary>
    /// Attaches an empty, per-header context menu as soon as a header is realized. WPF resolves
    /// which ContextMenu to open before it raises <c>ContextMenuOpening</c>, so the header must
    /// already own one; otherwise the grid's row menu wins. Each header needs its own instance
    /// because a ContextMenu can only have a single logical parent.
    /// </summary>
    private void ColumnHeader_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGridColumnHeader header || header.Column is null) return;
        if (!ProcGrid.Columns.Contains(header.Column)) return;
        if (header.ContextMenu is not null) return;

        header.ContextMenu = new ContextMenu
        {
            PlacementTarget = header,
            Style = TryFindResource("ModernContextMenu") as Style
        };
    }

    /// <summary>
    /// Rebuilds the column visibility menu of the header that was clicked, the way Task Manager
    /// does it, so each column can be toggled on or off.
    /// </summary>
    private void ColumnHeader_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGridColumnHeader header || header.Column is null) return;
        // The implicit header style also covers any other grid in the window, so only build the
        // menu for the process grid (and let non-process headers keep any menu they define).
        if (!ProcGrid.Columns.Contains(header.Column)) return;

        var menu = header.ContextMenu ??= new ContextMenu
        {
            PlacementTarget = header,
            Style = TryFindResource("ModernContextMenu") as Style
        };

        PopulateColumnVisibilityMenu(menu, ProcGrid);
    }

    /// <summary>
    /// Opens the column visibility menu anchored to the grid, at the current pointer position. Used
    /// for hits on the header strip, where no column header exists to raise a menu of its own.
    /// </summary>
    private void ShowColumnVisibilityMenu(FrameworkElement anchor)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
            Style = TryFindResource("ModernContextMenu") as Style
        };

        PopulateColumnVisibilityMenu(menu, ProcGrid);
        menu.IsOpen = true;
    }

    /// <summary>
    /// Fills a context menu with one checkable item per process grid column, reflecting the
    /// current visibility and preventing the last visible column from being hidden.
    /// </summary>
    private void PopulateColumnVisibilityMenu(ContextMenu menu, DataGrid grid)
    {
        var visibleCount = grid.Columns.Count(column => column.Visibility == Visibility.Visible);
        menu.Items.Clear();

        foreach (var column in grid.Columns)
        {
            var headerText = column.Header?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(headerText)) continue;

            var item = new MenuItem
            {
                Header = headerText,
                IsCheckable = true,
                IsChecked = column.Visibility == Visibility.Visible,
                Style = TryFindResource("ModernMenuItem") as Style,
                // Hiding the last remaining column would leave an empty grid.
                IsEnabled = column.Visibility != Visibility.Visible || visibleCount > 1
            };
            item.Click += (_, _) => ToggleColumnVisibility(grid, column);
            menu.Items.Add(item);
        }
    }

    private void ToggleColumnVisibility(DataGrid grid, DataGridColumn column)
    {
        var visibleCount = grid.Columns.Count(c => c.Visibility == Visibility.Visible);
        if (column.Visibility == Visibility.Visible && visibleCount <= 1) return;

        column.Visibility = column.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        SaveHiddenColumns(grid);
    }

    private void SaveHiddenColumns(DataGrid grid)
    {
        App.Settings.HiddenProcessColumns = string.Join(",",
            grid.Columns
                .Where(column => column.Visibility != Visibility.Visible)
                .Select(column => column.SortMemberPath)
                .Where(path => !string.IsNullOrWhiteSpace(path)));
        ThrottledSaveSettings();
    }

    private void ApplyColumnVisibility()
    {
        var hidden = (App.Settings.HiddenProcessColumns ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var column in ProcGrid.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.SortMemberPath)) continue;
            column.Visibility = hidden.Contains(column.SortMemberPath)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // Never let a stale setting produce an empty grid.
        if (ProcGrid.Columns.All(column => column.Visibility != Visibility.Visible))
        {
            ProcGrid.Columns[0].Visibility = Visibility.Visible;
        }
    }

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

    private void SystemFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!_syncingSystemFilter)
        {
            SetSystemFilterControls(SystemFilterCheck.IsChecked == true);
        }
    }

    private void SystemFilterLabel_Click(object sender, MouseButtonEventArgs e)
    {
        SystemFilterCheck.IsChecked = !SystemFilterCheck.IsChecked;
    }

    private void SetSystemFilterControls(bool enabled)
    {
        var changed = App.Settings.HideSystemProcesses != enabled;
        App.Settings.HideSystemProcesses = enabled;

        _syncingSystemFilter = true;
        try
        {
            SystemFilterCheck.IsChecked = enabled;
        }
        finally
        {
            _syncingSystemFilter = false;
        }

        if (changed)
        {
            ThrottledSaveSettings();
            ApplySortingAndFilter();
        }
    }

    private void ProcGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var source = sender as DataGrid ?? ProcGrid;
        var selected = source.SelectedItem as ProcessInfo ?? Selected;
        var multiple = GetMultiSelection(source);

        // Shift+Arrow, rubber-band drag and Ctrl+A update the selection directly inside the grid.
        // Mirroring those back is unnecessary and actively harmful: ApplySelection replaces the
        // whole SelectedItems collection, which cancels the DataGrid's in-progress selection
        // tracking, so re-entrant changes are ignored. Only a change that the application itself
        // initiated (SetSelectedProcess / RestoreSelection) needs to be propagated.
        if (!_syncingSelection && !_suppressSelectionMirror)
        {
            _suppressSelectionMirror = true;
            _syncingSelection = true;
            try
            {
                MirrorSelection(multiple);
            }
            finally
            {
                _syncingSelection = false;
                _suppressSelectionMirror = false;
            }
        }

        KillBtn.IsEnabled = selected != null;
        UpdateRestartControls(selected);
        UpdateSelectionSummary(multiple);
    }

    /// <summary>
    /// Mirrors the selection of one grid onto the other so switching between full and compact mode
    /// keeps it. With <c>SelectionMode="Extended"</c> assigning <c>SelectedItem</c> alone would
    /// collapse a multi-selection down to that single row, so each additional row is added to
    /// <c>SelectedItems</c> explicitly.
    /// </summary>
    private void MirrorSelection(List<ProcessInfo> selection)
    {
        var last = selection.Count > 0 ? selection[^1] : null;
        foreach (var grid in new[] { ProcGrid, CompactView.ProcessGrid })
        {
            ApplySelection(grid, selection, last);
        }
    }

    /// <summary>
    /// Mirrors a selection onto the grid that is not currently being driven.
    /// </summary>
    /// <remarks>
    /// Used by arrow navigation: the source grid has just been updated row by row and holds the
    /// authoritative range plus the current cell, so re-applying the selection to it would clear
    /// and rebuild the very collection we just built. Only the other grid needs to follow.
    /// </remarks>
    private void MirrorSelectionToOther(DataGrid source, IReadOnlyList<ProcessInfo> selection)
    {
        var other = ReferenceEquals(source, ProcGrid) ? CompactView.ProcessGrid : ProcGrid;
        if (ReferenceEquals(other, source)) return;

        var last = selection.Count > 0 ? selection[^1] : null;
        ApplySelection(other, selection, last);
    }

    /// <summary>
    /// Re-applies a selection captured before an item-collection rebuild. Items are matched by
    /// process identity (PID plus start time) so a recycled row does not inherit another
    /// process's selection when PIDs are reused.
    /// </summary>
    private void RestoreSelection(List<ProcessInfo> previousSelection)
    {
        if (previousSelection.Count == 0)
        {
            // Nothing was selected before: clear any stale selection left by the rebuild.
            if (ProcGrid.SelectedItems.Count > 0)
            {
                _syncingSelection = true;
                try
                {
                    ProcGrid.SelectedItems.Clear();
                    CompactView.ProcessGrid.SelectedItems.Clear();
                }
                finally
                {
                    _syncingSelection = false;
                }
            }

            UpdateSelectionSummary(Array.Empty<ProcessInfo>());
            return;
        }

        var wanted = previousSelection
            .Select(item => new ProcessIdentity(item.Pid, item.StartTime))
            .ToArray();

        var restored = _processList.Items
            .Where(item => wanted.Any(identity => SameProcessIdentity(item, identity)))
            .Distinct()
            .ToList();

        if (restored.Count == 0)
        {
            UpdateSelectionSummary(Array.Empty<ProcessInfo>());
            return;
        }

        _syncingSelection = true;
        try
        {
            MirrorSelection(restored);
        }
        finally
        {
            _syncingSelection = false;
        }

        KillBtn.IsEnabled = true;
        UpdateRestartControls(restored[^1]);
        UpdateSelectionSummary(restored);
    }

    private static void ApplySelection(DataGrid grid, IReadOnlyList<ProcessInfo> selection, ProcessInfo? last)
    {
        // Never assign SelectedItem while a multi-selection is being rebuilt: on an Extended
        // grid that assignment collapses the selection to that single row. Only the empty and
        // single-row cases go through SelectedItem, which also keeps the current cell in sync.
        if (selection.Count <= 1)
        {
            if (!ReferenceEquals(grid.SelectedItem, last))
            {
                grid.SelectedItem = last;
            }

            EnsureCurrentCell(grid, last);
            return;
        }

        if (grid.SelectedItems.Count != selection.Count ||
            selection.Any(process => !grid.SelectedItems.Contains(process)))
        {
            grid.SelectedItems.Clear();
            foreach (var process in selection)
            {
                grid.SelectedItems.Add(process);
            }
        }
    }

    /// <summary>
    /// Snapshot of the rows currently selected in the given grid, in display order.
    /// </summary>
    private static List<ProcessInfo> GetMultiSelection(DataGrid grid) =>
        grid.SelectedItems.OfType<ProcessInfo>().ToList();

    /// <summary>
    /// Resolves a multi-selection into the flat set of PIDs a single "end selected" action should
    /// operate on. A group parent stands for every instance it aggregates, and any group child
    /// that is already covered by a selected parent is not added twice.
    /// </summary>
    private static List<int> ResolveSelectionPids(IReadOnlyList<ProcessInfo> selection)
    {
        var pids = new HashSet<int>();
        foreach (var process in selection)
        {
            if (process.IsGroupParent)
            {
                foreach (var child in process.Children)
                {
                    pids.Add(child.Pid);
                }
            }

            if (process.Pid > 0)
            {
                pids.Add(process.Pid);
            }
        }

        return pids.ToList();
    }

    /// <summary>
    /// Number of running instances a selection covers: a group parent counts as all of its
    /// instances, an individual process counts as one. Used for the counter and the confirmation.
    /// </summary>
    private static int CountSelectionInstances(IReadOnlyList<ProcessInfo> selection)
    {
        var count = 0;
        foreach (var process in selection)
        {
            count += process.IsGroupParent && process.InstanceCount > 0
                ? process.InstanceCount
                : 1;
        }

        return count;
    }

    /// <summary>
    /// Reflects the current multi-selection in both action bars: the shared counter label plus
    /// the "End Task" button, whose label advertises how many processes it will terminate.
    /// </summary>
    private void UpdateSelectionSummary(IReadOnlyList<ProcessInfo> selection)
    {
        var count = selection.Count;
        var instances = CountSelectionInstances(selection);

        SelectionCountText.Text = count > 1
            ? Strings.T("SelectionCount", count, instances)
            : string.Empty;
        SelectionCountBadge.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        ClearSelectionBtn.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;

        var hasSelection = count > 0;
        KillBtn.ToolTip = count > 1
            ? Strings.T("EndSelectedTooltip", count)
            : Strings.T("EndTaskTooltip");
        AutomationProperties.SetName(KillBtn, count > 1 ? Strings.T("EndSelected", count) : Strings.T("EndTask"));
        // WPF TextBlocks share their parent's UI Automation peer, so the badge count is exposed
        // through the clear button, which does own a peer of its own.
        AutomationProperties.SetName(ClearSelectionBtn, Strings.T("SelectionCountShort", count));

        CompactView.SetEndTaskEnabled(hasSelection);
        CompactView.SetSelectionSummary(count);
    }

    private void ClearSelectionBtn_Click(object sender, RoutedEventArgs e)
    {
        _syncingSelection = true;
        try
        {
            ProcGrid.SelectedItems.Clear();
            CompactView.ProcessGrid.SelectedItems.Clear();
        }
        finally
        {
            _syncingSelection = false;
        }

        SetSelectedProcess(null);
        UpdateSelectionSummary(Array.Empty<ProcessInfo>());
    }

    private void ThrottledSaveSettings()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async void Kill_Click(object sender, RoutedEventArgs e)
    {
        // The context-menu batch item is only shown for a multi-selection, so it always routes
        // through the batch path even though the click itself targets a single row.
        var batch = ReferenceEquals(sender, ContextKillSelectionItem) ? GetActiveSelection() : null;
        if (batch is { Count: > 1 })
        {
            await KillSelectionAsync(batch);
            return;
        }

        var selection = GetActiveSelection();
        if (selection.Count > 1)
        {
            await KillSelectionAsync(selection);
            return;
        }

        var s = GetActionTarget(sender);
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var msg = Strings.T("KillGroupConfirm", s.InstanceCount, s.Name);
            if (ConfirmDialog.ShowProcess(this, Strings.T("Kill"), msg, s.ExePath, Strings.T("Kill"), Strings.T("Cancel"), isDanger: true, confirmationKey: "process.kill"))
            {
                var pidsToKill = s.Children.Select(c => c.Pid).ToList();
                if (!pidsToKill.Contains(s.Pid)) pidsToKill.Add(s.Pid);
                var results = await RunProcessActionsAsync(pidsToKill, ProcessActions.TryKill);
                ApplyActionResults(results);
            }
            return;
        }

        if (ConfirmDialog.ShowProcess(this, Strings.T("Kill"), Strings.T("KillConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Kill"), Strings.T("Cancel"), isDanger: true, confirmationKey: "process.kill"))
        {
            var result = await Task.Run(() => ProcessActions.TryKill(s.Pid), _lifetimeCts.Token);
            ApplyActionResults(new Dictionary<int, ProcessActions.ActionResult> { [s.Pid] = result });
        }
    }

    /// <summary>
    /// Batch variant of "End Task": one confirmation summarises the whole selection, then every
    /// resolved PID is terminated in a single pass. Keeping it separate from <see cref="Kill_Click"/>
    /// preserves the per-process confirmations for the single-selection path.
    /// </summary>
    private async Task KillSelectionAsync(List<ProcessInfo> selection)
    {
        var pids = ResolveSelectionPids(selection);
        if (pids.Count == 0) return;

        var instances = CountSelectionInstances(selection);
        var message = Strings.T("KillSelectionConfirm", selection.Count, instances);
        if (!ConfirmDialog.ShowProcess(
                this,
                Strings.T("Kill"),
                message,
                exePath: null!,
                okText: Strings.T("Kill"),
                cancelText: Strings.T("Cancel"),
                isDanger: true,
                confirmationKey: "process.kill-selection"))
        {
            return;
        }

        var results = await RunProcessActionsAsync(pids, ProcessActions.TryKill);
        ApplyActionResults(results);
        SetStatus(Strings.T("KillSelectionDone", results.Values.Count(result => result.Succeeded)));
    }

    /// <summary>
    /// The rows the current action should apply to: the multi-selection of the grid that has
    /// focus, falling back to whichever grid is visible. A context-menu action keeps using its
    /// own target so right-clicking a row never surprises the user by acting on other rows.
    /// </summary>
    private List<ProcessInfo> GetActiveSelection()
    {
        var grid = _isCompactMode ? CompactView.ProcessGrid : ProcGrid;

        // A panel that lost keyboard focus (an open context menu, for instance) still holds the
        // authoritative selection, so a non-empty selection wins over the focus heuristic.
        if (grid.SelectedItems.Count > 0)
        {
            return GetMultiSelection(grid);
        }

        if (grid.IsKeyboardFocusWithin)
        {
            return GetMultiSelection(grid);
        }

        return GetMultiSelection(ProcGrid);
    }

    private async void KillTree_Click(object sender, RoutedEventArgs e)
    {
        var s = GetActionTarget(sender);
        if (s == null) return;
        if (ConfirmDialog.ShowProcess(this, Strings.T("KillTree"), Strings.T("KillTreeConfirm", s.Name), s.ExePath, Strings.T("KillTree"), Strings.T("Cancel"), isDanger: true, confirmationKey: "process.kill-tree"))
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
        var s = GetActionTarget(sender);
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var msg = Strings.T("SuspendGroupConfirm", s.InstanceCount, s.Name);
            if (ConfirmDialog.ShowProcess(this, Strings.T("Suspend"), msg, s.ExePath, Strings.T("Suspend"), Strings.T("Cancel"), isDanger: false, confirmationKey: "process.suspend"))
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

        if (ConfirmDialog.ShowProcess(this, Strings.T("Suspend"), Strings.T("SuspendConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Suspend"), Strings.T("Cancel"), isDanger: false, confirmationKey: "process.suspend"))
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
        var s = GetActionTarget(sender);
        if (s == null) return;

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            var msg = Strings.T("ResumeGroupConfirm", s.InstanceCount, s.Name);
            if (ConfirmDialog.ShowProcess(this, Strings.T("Resume"), msg, s.ExePath, Strings.T("Resume"), Strings.T("Cancel"), isDanger: false, confirmationKey: "process.resume"))
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

        if (ConfirmDialog.ShowProcess(this, Strings.T("Resume"), Strings.T("ResumeConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Resume"), Strings.T("Cancel"), isDanger: false, confirmationKey: "process.resume"))
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

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        var s = GetActionTarget(sender);
        if (s == null) return;

        if (IsPackagedApp(s.ExePath))
        {
            ShowRestartUnavailable(Strings.T("RestartPackagedUnsupported"));
            return;
        }

        if (string.IsNullOrWhiteSpace(s.ExePath))
        {
            ShowRestartUnavailable(Strings.T("RestartUnavailable"));
            return;
        }

        if (s.IsGroupParent && s.InstanceCount > 1)
        {
            // Group restart: relaunch each instance from its own executable, then terminate it.
            if (ConfirmDialog.ShowProcess(this, Strings.T("Restart"), Strings.T("RestartConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Restart"), Strings.T("Cancel"), isDanger: false, confirmationKey: "process.restart"))
            {
                var pidsToRestart = s.Children.Select(c => c.Pid).ToList();
                if (!pidsToRestart.Contains(s.Pid)) pidsToRestart.Add(s.Pid);
                var results = await RunProcessActionsAsync(pidsToRestart, pid =>
                {
                    var target = _all.FirstOrDefault(p => p.Pid == pid);
                    return ProcessActions.TryRestart(pid, target?.ExePath ?? s.ExePath);
                });
                ApplyActionResults(results);
            }
            return;
        }

        if (ConfirmDialog.ShowProcess(this, Strings.T("Restart"), Strings.T("RestartConfirm", s.Name, s.Pid), s.ExePath, Strings.T("Restart"), Strings.T("Cancel"), isDanger: false, confirmationKey: "process.restart"))
        {
            var exePath = s.ExePath;
            var result = await Task.Run(() => ProcessActions.TryRestart(s.Pid, exePath), _lifetimeCts.Token);
            ApplyActionResults(new Dictionary<int, ProcessActions.ActionResult> { [s.Pid] = result });
        }
    }

    /// <summary>
    /// A process image living under WindowsApps belongs to a packaged (UWP / Microsoft Store)
    /// app, which cannot be relaunched by executing its image path.
    /// </summary>
    private static bool IsPackagedApp(string? exePath) =>
        !string.IsNullOrEmpty(exePath) &&
        exePath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);

    private static bool CanRestart(ProcessInfo? process) => GetRestartDisabledReason(process) == null;

    /// <summary>
    /// Returns <c>null</c> when the process can be restarted, otherwise a localized reason
    /// explaining why the action is unavailable. Used to keep the restart controls' tooltips
    /// informative instead of leaving users guessing.
    /// </summary>
    private static string? GetRestartDisabledReason(ProcessInfo? process)
    {
        // No selection is not a limitation worth explaining: the caller falls back to the
        // plain informative tooltip in that case.
        if (process == null) return null;
        if (string.IsNullOrWhiteSpace(process.ExePath)) return Strings.T("RestartHintNoPath");
        if (IsPackagedApp(process.ExePath)) return Strings.T("RestartHintPackaged");
        if (IsSuspended(process)) return Strings.T("RestartHintSuspended");
        return null;
    }

    private void UpdateRestartControls(ProcessInfo? process)
    {
        // When nothing is selected the button stays disabled, but the tooltip remains purely
        // informative (name + shortcut) instead of showing a reason that would look confusing.
        var reason = GetRestartDisabledReason(process);
        var enabled = process != null && reason == null;
        var tooltip = reason ?? $"{Strings.T("Restart")} ({Strings.T("RestartShortcutHint")})";

        RestartBtn.IsEnabled = enabled;
        RestartBtn.ToolTip = tooltip;

        CompactView.SetRestartState(enabled, tooltip);
        if (ProcContextMenu != null)
        {
            ContextRestartItem.IsEnabled = enabled;
            ContextRestartItem.ToolTip = reason;
        }
    }

    private void ShowRestartUnavailable(string message)
    {
        SetStatus(message);
        ConfirmDialog.Show(
            this,
            Strings.T("ProcessActionFailed"),
            message,
            Strings.T("Ok"),
            null,
            ConfirmIconType.Warning);
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

    private void PriorityRealTime_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.RealTime, sender);
    private void PriorityHigh_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.High, sender);
    private void PriorityAboveNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.AboveNormal, sender);
    private void PriorityNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.Normal, sender);
    private void PriorityBelowNormal_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.BelowNormal, sender);
    private void PriorityIdle_Click(object sender, RoutedEventArgs e) => SetPriority(ProcessPriorityClass.Idle, sender);

    private async void SetPriority(ProcessPriorityClass priority, object source)
    {
        var s = GetActionTarget(source);
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

        SetStatus(issues[0]!);
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
        var s = GetActionTarget(sender);
        if (s == null) return;
        var textToCopy = !string.IsNullOrEmpty(s.ExePath) ? s.ExePath : s.Name;
        try
        {
            Clipboard.SetText(textToCopy);
            SetStatus(Strings.T("PathCopied"));
        }
        catch { }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var s = GetActionTarget(sender);
        if (s == null || string.IsNullOrEmpty(s.ExePath)) return;
        try { Process.Start("explorer.exe", $"/select,\"{s.ExePath}\""); } catch { }
    }

    private void SearchOnline_Click(object sender, RoutedEventArgs e)
    {
        var s = GetActionTarget(sender);
        if (s == null) return;
        try { Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(s.Name)}") { UseShellExecute = true }); } catch { }
    }

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        var process = GetActionTarget(sender);
        if (process == null) return;

        var dialog = new ProcessPropertiesWindow(process) { Owner = this };
        dialog.ShowDialog();
    }


    private void ApplyTheme()
    {
        ThemeManager.Apply(App.Settings.Theme);
        _trayService.UpdateTheme();

        var accentBrush = (Brush)FindResource("AccentBrush");
        var subTextBrush = (Brush)FindResource("SubTextBrush");

        if (TabProcessesIcon != null)
            TabProcessesIcon.Stroke = CurrentTab == MainTab.Processes ? accentBrush : subTextBrush;
        if (TabPerformanceIcon != null)
            TabPerformanceIcon.Stroke = CurrentTab == MainTab.Performance ? accentBrush : subTextBrush;
        if (TabSettingsIcon != null)
            TabSettingsIcon.Stroke = CurrentTab == MainTab.Settings ? accentBrush : subTextBrush;
    }

    private void ApplyLanguage()
    {
        try
        {
            TabProcessesText.Text = Strings.T("NavProcesses");
            TabPerformanceText.Text = Strings.T("NavPerformance");
            TabSettingsText.Text = Strings.T("NavSettings");
            TabProcessesBtn.ToolTip = Strings.T("NavProcessesTip");
            TabPerformanceBtn.ToolTip = Strings.T("NavPerformanceTip");
            TabSettingsBtn.ToolTip = Strings.T("NavSettingsTip");

            PerformanceTabContent?.RefreshLocalization();
            SettingsTabContent?.RefreshLocalization();

            SubtitleText.Text = Strings.T("AppSubtitle");
            FullBrandingPanel.ToolTip = Strings.T("About");
            AutomationProperties.SetName(FullBrandingPanel, Strings.T("About"));
            SearchHint.Text = Strings.T("SearchPlaceholder");
            LoaderOverlay.RefreshLocalization();
            GroupToggleLabel.Text = Strings.T("GroupByApp");
            SystemFilterLabel.Text = Strings.T("HideSystemProcesses");
            AutomationProperties.SetName(SystemFilterCheck, Strings.T("HideSystemProcesses"));
            FontSizeSlider.ToolTip = Strings.T("TextSize");
            RefreshBtn.ToolTip = $"{Strings.T("Refresh")} (F5)";
            UpdateWidgetToggleHint();
            _metricsWidget?.ApplyLocalization();
            CpuMenuWidgetItem.Header = Strings.T("FloatingWidget");
            RamMenuWidgetItem.Header = Strings.T("FloatingWidget");
            CpuMenuSysInfoItem.Header = $"{Strings.T("SystemInformation")}...";
            RamMenuSysInfoItem.Header = $"{Strings.T("SystemInformation")}...";
            PauseDetectionBtn.ToolTip = _detectionPaused
                ? $"{Strings.T("ResumeDetection")} (Ctrl+P)"
                : $"{Strings.T("PauseDetection")} (Ctrl+P)";
            AutomationProperties.SetName(PauseDetectionBtn, _detectionPaused
                ? Strings.T("ResumeDetection")
                : Strings.T("PauseDetection"));
            KillBtn.ToolTip = $"{Strings.T("Kill")} (Del)";
            RestartBtn.ToolTip = $"{Strings.T("Restart")} ({Strings.T("RestartShortcutHint")})";
            ClearBtn.ToolTip = Strings.T("Clear");
            HeavyFilterToggle.ToolTip = Strings.T("ShowHeavyProcesses");
            AutomationProperties.SetName(RefreshBtn, Strings.T("Refresh"));
            AutomationProperties.SetName(KillBtn, Strings.T("Kill"));
            AutomationProperties.SetName(RestartBtn, Strings.T("Restart"));
            AutomationProperties.SetName(ClearBtn, Strings.T("Clear"));
            AutomationProperties.SetName(HeavyFilterToggle, Strings.T("ShowHeavyProcesses"));
            FullModeToggleBtn.ToolTip = Strings.T("CompactMode");
            AutomationProperties.SetName(FullModeToggleBtn, Strings.T("CompactMode"));
            MoreBtn.ToolTip = Strings.T("MoreOptions");
            AutomationProperties.SetName(MoreBtn, Strings.T("MoreOptions"));
            if (MoreOptionsMenu != null)
            {
                MoreDonateItem.Header = Strings.T("Donate");
                MoreSysInfoItem.Header = $"{Strings.T("SystemInformation")}...";
                MoreRefreshItem.Header = Strings.T("RefreshProcesses");
                MoreCompactItem.Header = Strings.T("CompactMode");
                MoreDocsItem.Header = Strings.T("DocumentationWiki");
                MoreFaqItem.Header = Strings.T("Faq");
                MoreChangelogItem.Header = Strings.T("Changelog");
                MoreWebsiteItem.Header = Strings.T("Website");
                MoreCheckUpdatesItem.Header = Strings.T("CheckForUpdates");
                MoreAboutItem.Header = Strings.T("AboutCyberManager");
                MoreExitItem.Header = Strings.T("Exit");
            }
            MinimizeBtn.ToolTip = Strings.T("Minimize");
            MaximizeBtn.ToolTip = Strings.T("Maximize");
            CloseBtn.ToolTip = Strings.T("Close");
            UpdateMaximizeButtonIcon();
            StatsBorder.ToolTip = Strings.T("OpenSystemInfoTip");
            AutomationProperties.SetName(StatsBorder, Strings.T("SystemInformation"));
            UpdateMetricToolTips();
            SetStatus(Strings.T("Ready"));
            CompactView.ApplyLanguage();
            // Runs after the compact view re-localizes its own labels, so the resolved
            // empty-state message (no matches, filters, or a load error) is the one left on
            // screen instead of the generic "no processes found".
            UpdateEmptyState();
            CompactView.SetPinned(Topmost);
            _trayService.UpdateLocalization();

            if (ProcGrid.Columns.Count >= 7)
            {
                // Map by SortMemberPath instead of index so reordering or hiding columns
                // cannot mislabel the headers.
                var headerKeys = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Name"] = "Process",
                    ["CpuPercent"] = "Cpu",
                    ["Pid"] = "Pid",
                    ["WorkingSetBytes"] = "Memory",
                    ["ThreadCount"] = "Threads",
                    ["Priority"] = "Priority",
                    ["ExePath"] = "Path"
                };

                foreach (var column in ProcGrid.Columns)
                {
                    if (column.SortMemberPath is { } path && headerKeys.TryGetValue(path, out var key))
                    {
                        column.Header = Strings.T(key);
                    }
                }

                foreach (var c in ProcGrid.Columns)
                {
                    c.SortDirection = c.SortMemberPath == _sortColumn ? _sortDirection : null;
                }
            }
            foreach (var c in CompactView.ProcessGrid.Columns)
            {
                c.SortDirection = c.SortMemberPath == _sortColumn ? _sortDirection : null;
            }

            if (ProcContextMenu != null)
            {
                ContextKillItem.Header = Strings.T("Kill");
                ContextPropertiesItem.Header = Strings.T("ProcessProperties");
                ContextKillTreeItem.Header = Strings.T("KillTree");
                ContextRestartItem.Header = Strings.T("Restart");
                ContextSuspendItem.Header = Strings.T("Suspend");
                ContextResumeItem.Header = Strings.T("Resume");
                ContextSetPriorityItem.Header = Strings.T("SetPriority");
                ContextPriorityRealTimeItem.Header = Strings.T("PriorityRealTime");
                ContextPriorityHighItem.Header = Strings.T("PriorityHigh");
                ContextPriorityAboveNormalItem.Header = Strings.T("PriorityAboveNormal");
                ContextPriorityNormalItem.Header = Strings.T("PriorityNormal");
                ContextPriorityBelowNormalItem.Header = Strings.T("PriorityBelowNormal");
                ContextPriorityIdleItem.Header = Strings.T("PriorityIdle");
                ContextCopyPathItem.Header = Strings.T("CopyPath");
                ContextOpenFolderItem.Header = Strings.T("OpenFolder");
                ContextSearchOnlineItem.Header = Strings.T("SearchOnline");

                // Refresh state-dependent text that carries a localized reason. The selection
                // summary is re-applied afterwards because it rewrites the End Task labels that
                // the localization code above has just reset to their neutral form.
                UpdateRestartControls(Selected);
                UpdateSelectionSummary(GetActiveSelection());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyLanguage error: {ex}");
        }
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (MaximizeBtn == null || MaximizeIconPath == null) return;

        if (WorkAreaMaximize.IsFilled(this) || WindowState == WindowState.Maximized)
        {
            MaximizeBtn.ToolTip = Strings.T("Restore");
            AutomationProperties.SetName(MaximizeBtn, Strings.T("Restore"));
            MaximizeIconPath.Data = Geometry.Parse(
                "M 6 2 L 6 6 L 2 6 M 6 6 L 2.5 2.5 M 8 12 L 8 8 L 12 8 M 8 8 L 11.5 11.5");
        }
        else
        {
            MaximizeBtn.ToolTip = Strings.T("Maximize");
            AutomationProperties.SetName(MaximizeBtn, Strings.T("Maximize"));
            MaximizeIconPath.Data = Geometry.Parse(
                "M 3 7 L 3 3 L 7 3 M 3 3 L 6.5 6.5 M 11 7 L 11 11 L 7 11 M 11 11 L 7.5 7.5");
        }
    }

    #region Keyboard Navigation & Shortcuts

    public void FocusSearchBox()
    {
        if (_isCompactMode)
        {
            CompactView.FocusSearchBox();
        }
        else
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.M)
        {
            e.Handled = true;
            ApplyViewMode(!_isCompactMode, restoreBounds: false);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.D1 || e.Key == Key.NumPad1))
        {
            e.Handled = true;
            NavigateToTab(MainTab.Processes);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.D2 || e.Key == Key.NumPad2))
        {
            e.Handled = true;
            NavigateToTab(MainTab.Performance);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.D3 || e.Key == Key.NumPad3))
        {
            e.Handled = true;
            NavigateToTab(MainTab.Settings);
            return;
        }

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
            NavigateToTab(MainTab.Performance);
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemComma || e.Key == Key.OemPeriod))
        {
            e.Handled = true;
            NavigateToTab(MainTab.Settings);
            return;
        }

        // Refresh is F5 only. Ctrl+R belongs to the process grid, where it restarts the
        // selected process. Consuming it here would shadow that handler, because Preview
        // events tunnel from the window down to the grid before reaching it.
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            _ = RefreshAsync(force: true);
            return;
        }

        if (e.Key == Key.OemQuestion || (e.Key == Key.Divide && Keyboard.Modifiers == ModifierKeys.None))
        {
            if ((!_isCompactMode && !SearchBox.IsFocused) ||
                (_isCompactMode && !CompactView.IsSearchFocused))
            {
                e.Handled = true;
                FocusSearchBox();
                return;
            }
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitSearchHistory(SearchBox.Text);
        }

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
        var grid = sender as DataGrid ?? ProcGrid;

        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            // The grid's built-in navigation only moves the current cell (the focus rectangle),
            // leaving the selection behind, so Up/Down would visibly do nothing. Take over the
            // gesture and move the selection itself: plain arrows select a single row, Shift
            // extends the range from the existing anchor.
            if (MoveSelectionByArrow(grid, e.Key == Key.Down ? 1 : -1))
            {
                e.Handled = true;
                return;
            }
        }

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
                    SetSelectedProcess(parent);
                    grid.ScrollIntoView(parent);
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
        }        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.P)
        {
            SetDetectionPaused(!_detectionPaused);
            e.Handled = true;
            return;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R)
        {
            // Ctrl+R restarts the selected process. The window-level handler deliberately
            // leaves this gesture alone so it reaches the grid, and refresh stays on F5.
            if (Selected != null)
            {
                Restart_Click(sender, e);
                e.Handled = true;
                return;
            }
        }
        else if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            if (Selected is { } selected)
            {
                grid.ScrollIntoView(selected);
                grid.UpdateLayout();
                if (GetRowForItem(grid, selected) is { } row)
                {
                    PrepareContextMenu(row, selected, grid);
                    ProcContextMenu.IsOpen = true;
                    e.Handled = true;
                    return;
                }
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

        // Type-to-Search auto-focus. Shift must be excluded as well as Ctrl/Alt/Win: holding
        // Shift while pressing an arrow key extends the selection, and Shift+A..Z / Shift+digits
        // are selection gestures too, so they must reach the grid untouched.
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            if ((e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                FocusSearchBox();
            }
        }
    }

    /// <summary>
    /// Moves the selection one row up or down, the way a list control is expected to behave.
    /// </summary>
    /// <remarks>
    /// The grid's own navigation moves only the current cell, which is why arrow keys used to
    /// leave the selection untouched. Plain arrows collapse the selection onto the new row; while
    /// Shift is held the range is extended from the anchor that started the run, matching the
    /// usual Shift+Arrow contract. Returns false when there is nothing to move onto (empty grid or
    /// already at the end), so the caller can let the event fall through instead of swallowing it.
    /// </remarks>
    private bool MoveSelectionByArrow(DataGrid grid, int delta)
    {
        var items = grid.Items.OfType<ProcessInfo>().ToList();
        if (items.Count == 0) return false;

        var extending = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        // Plain arrows move from the row that is selected right now, so the caret follows the
        // selection. A Shift run instead grows from the row it started on, which is why the anchor
        // is only consulted while extending. Rows are matched by process identity because the
        // periodic refresh replaces the item objects and the default CPU sort reorders them.
        var from = grid.SelectedItem as ProcessInfo;
        if (extending && _selectionAnchor is { } identity)
        {
            from = items.FirstOrDefault(item => SameProcessIdentity(item, identity)) ?? from;
        }

        from ??= items[0];

        var fromIndex = items.IndexOf(from);
        if (fromIndex < 0) fromIndex = 0;

        var targetIndex = fromIndex + delta;
        if (targetIndex < 0 || targetIndex >= items.Count) return false;

        var target = items[targetIndex];

        if (!extending)
        {
            // Plain arrow: the new row becomes the whole selection and the fresh anchor.
            _selectionAnchor = new ProcessIdentity(target.Pid, target.StartTime);
            _syncingSelection = true;
            try
            {
                grid.SelectedItems.Clear();
                grid.SelectedItems.Add(target);
                EnsureCurrentCell(grid, target);
            }
            finally
            {
                _syncingSelection = false;
            }

            MirrorSelectionToOther(grid, new[] { target });
            SyncSelectionDependentUi(target, new[] { target });
        }
        else
        {
            // Shift+Arrow: keep the existing range and add or drop the row we just reached. The
            // anchor stays put so the selection grows and shrinks under the moving edge.
            _selectionAnchor ??= new ProcessIdentity(from.Pid, from.StartTime);
            var current = GetMultiSelection(grid);

            _syncingSelection = true;
            try
            {
                if (current.Contains(target))
                {
                    // Walking back onto a row that is already part of the range removes it, so
                    // shrinking the selection does not require releasing Shift.
                    if (current.Count > 1) grid.SelectedItems.Remove(target);
                }
                else
                {
                    grid.SelectedItems.Add(target);
                }

                EnsureCurrentCell(grid, target);
            }
            finally
            {
                _syncingSelection = false;
            }

            var updated = GetMultiSelection(grid);
            var displayOrder = items.Where(updated.Contains).ToList();
            MirrorSelectionToOther(grid, displayOrder);
            SyncSelectionDependentUi(target, displayOrder);
        }

        grid.ScrollIntoView(target);
        return true;
    }

    /// <summary>
    /// Refreshes the controls that depend on what is selected, without going through
    /// <see cref="ProcGrid_SelectionChanged"/> (which is suppressed while we drive the grid).
    /// </summary>
    private void SyncSelectionDependentUi(ProcessInfo? selected, IReadOnlyList<ProcessInfo> selection)
    {
        KillBtn.IsEnabled = selected != null;
        CompactView.SetEndTaskEnabled(selected != null);
        UpdateRestartControls(selected);
        UpdateSelectionSummary(selection);
    }

    #endregion

    #region Tray & Hotkey Management

    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
        {
            if (App.Settings.MinimizeToTrayOnMinimize)
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

    private void ToggleTrayVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            return;
        }

        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
        FocusSearchBox();
    }

    private void OnGlobalHotkeyPressed(int id)
    {
        if (id == GlobalHotkeyService.WidgetHotkeyId)
        {
            ToggleMetricsWidget();
            return;
        }

        ToggleWindowVisibility();
    }

    /// <summary>
    /// Brings the main window back from the tray, restoring it when it was minimized. Shared by
    /// every entry point that has to surface the window (tray menu and floating widget) so they
    /// all behave the same.
    /// </summary>
    private void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void OpenSystemInfoFromTray()
    {
        ShowFromTray();
        NavigateToTab(MainTab.Performance);
    }

    private void OpenSettingsFromTray()
    {
        ShowFromTray();
        NavigateToTab(MainTab.Settings);
    }

    private void OpenAboutFromTray(bool checkUpdatesNow)
    {
        ShowFromTray();
        var dlg = new AboutWindow(checkUpdatesNow) { Owner = this };
        dlg.ShowDialog();
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
        if (App.Settings.MinimizeToTrayOnMinimize)
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
        WorkAreaMaximize.Toggle(this);
        UpdateMaximizeButtonIcon();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Route through the same path as the window X so both trigger the close dialog.
        RequestCloseFromUser();
    }

    #endregion
}
