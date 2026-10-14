using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Controls;

public partial class CompactProcessView : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(CompactProcessView),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(CompactProcessView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty RowFontSizeProperty =
        DependencyProperty.Register(
            nameof(RowFontSize),
            typeof(double),
            typeof(CompactProcessView),
            new PropertyMetadata(12.0));

    private bool _syncingSearch;
    private bool _syncingHeavyFilter;
    private double _cpuPercent;
    private double _ramPercent;
    private double _usedRamGb;
    private double _totalRamGb;
    private string _cpuModelName = "";

    // Fallbacks for the theme colours: only used if the resource is missing (design time, tests).
    private static readonly Color CpuFallback = Color.FromRgb(0x10, 0xB9, 0x81);
    private static readonly Color RamFallback = Color.FromRgb(0x24, 0xA6, 0xB5);

    private readonly MetricToolTipContent _cpuTip;
    private readonly MetricToolTipContent _ramTip;

    // Left-button press on the branding: About opens only if the pointer does not move beyond
    // the drag threshold, so dragging the window from the branding keeps working.
    private Point _brandingPressPoint;
    private bool _brandingPressed;
    private const double BrandingClickThreshold = 4.0;

    public CompactProcessView()
    {
        InitializeComponent();
        CompactCpuMetricHost.ToolTip = MetricToolTipContent.Create(out _cpuTip);
        CompactRamMetricHost.ToolTip = MetricToolTipContent.Create(out _ramTip);
        ApplyLanguage();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CompactProcessView view) return;
        if (e.OldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= view.OnSourceCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += view.OnSourceCollectionChanged;
        view.QueueListFit();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        QueueListFit();

    private void QueueListFit() =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateListFit));

    private void CompactGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateListFit();

    /// <summary>
    /// Keeps the compact list from hugging the top of the window when it holds fewer rows
    /// than fit: with few processes (a quiet system or an active filter) the rows used to
    /// sit at the very top and leave an empty void below them, which read as broken layout.
    /// When the content is shorter than the available height the grid shrinks to its rows
    /// and centers itself vertically; otherwise it stretches and scrolls as usual.
    /// </summary>
    private void UpdateListFit()
    {
        if (!IsLoaded) return;

        int rows = CompactGrid.Items?.Count ?? 0;
        double available = CompactGrid.ActualHeight;
        if (rows == 0 || available <= 0) return;

        // 26 matches the DataGridRow Height setter in this view's row style.
        double content = CompactGrid.ColumnHeaderHeight + (rows * CompactRowHeight) + 2;
        bool fits = available > content + 8;
        CompactGrid.Height = fits ? content : double.NaN;
        CompactGrid.VerticalAlignment = fits ? VerticalAlignment.Center : VerticalAlignment.Stretch;
    }

    private const double CompactRowHeight = 26;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public double RowFontSize
    {
        get => (double)GetValue(RowFontSizeProperty);
        set => SetValue(RowFontSizeProperty, value);
    }

    public DataGrid ProcessGrid => CompactGrid;

    public string SearchText => SearchBox.Text;

    public bool IsSearchFocused => SearchBox.IsFocused;

    public bool IsHeavyFilterEnabled => HeavyFilterToggle.IsChecked == true;

    public bool IsSearchBarSource(DependencyObject? source) =>
        IsDescendantOf(source, CompactSearchBar);

    public event EventHandler? SearchChanged;
    public event EventHandler? SearchSubmitted;
    public event RoutedEventHandler? SearchHistoryRequested;
    public event EventHandler? HeavyFilterChanged;
    public event RoutedEventHandler? ToggleRequested;
    public event RoutedEventHandler? EndTaskRequested;
    public event RoutedEventHandler? RestartRequested;
    public event RoutedEventHandler? PauseDetectionRequested;
    public event RoutedEventHandler? PinRequested;
    public event RoutedEventHandler? SettingsRequested;
    public event RoutedEventHandler? MoreRequested;
    public event RoutedEventHandler? AboutRequested;
    public event RoutedEventHandler? MinimizeRequested;
    public event RoutedEventHandler? CloseRequested;
    public event RoutedEventHandler? DragRequested;
    public event RoutedEventHandler? ClearSelectionRequested;

    public void ApplyLanguage()
    {
        EndTaskButton.ToolTip = Strings.T("Kill");
        RestartButton.ToolTip = Strings.T("Restart");
        AutomationProperties.SetName(RestartButton, Strings.T("Restart"));
        ToolTipService.SetShowOnDisabled(RestartButton, true);
        PauseDetectionButton.ToolTip = Strings.T("PauseDetection");
        AutomationProperties.SetName(PauseDetectionButton, Strings.T("PauseDetection"));
        PinButton.ToolTip = Strings.T("AlwaysOnTop");
        EngineBadgeText.Text = Strings.T("WfpEngineBadge");
        CompactCpuLabel.Text = Strings.T("Cpu");
        CompactRamLabel.Text = Strings.T("Ram");
        UpdateMetricToolTips();
        ModeButton.ToolTip = Strings.T("MoreDetails");
        AutomationProperties.SetName(ModeButton, Strings.T("MoreDetails"));
        BrandingPanel.ToolTip = Strings.T("About");
        AutomationProperties.SetName(BrandingPanel, Strings.T("About"));
        SettingsButton.ToolTip = Strings.T("Settings");
        MoreButton.ToolTip = Strings.T("MoreOptions");
        CloseButton.ToolTip = Strings.T("Close");
        SearchBox.ToolTip = Strings.T("SearchPlaceholder");
        SearchHint.Text = Strings.T("SearchPlaceholder");
        ClearSearchButton.ToolTip = Strings.T("Clear");
        SearchHistoryButton.ToolTip = Strings.T("RecentSearches");
        HeavyFilterToggle.ToolTip = Strings.T("ShowHeavyProcesses");
        AutomationProperties.SetName(PinButton, Strings.T("AlwaysOnTop"));
        AutomationProperties.SetName(SettingsButton, Strings.T("Settings"));
        AutomationProperties.SetName(MoreButton, Strings.T("MoreOptions"));
        MinimizeButton.ToolTip = Strings.T("Minimize");
        AutomationProperties.SetName(MinimizeButton, Strings.T("Minimize"));
        AutomationProperties.SetName(CloseButton, Strings.T("Close"));
        AutomationProperties.SetName(SearchBox, Strings.T("SearchProcesses"));
        AutomationProperties.SetName(ClearSearchButton, Strings.T("Clear"));
        AutomationProperties.SetName(SearchHistoryButton, Strings.T("RecentSearches"));
        AutomationProperties.SetName(HeavyFilterToggle, Strings.T("ShowHeavyProcesses"));
        AutomationProperties.SetName(EndTaskButton, Strings.T("Kill"));
        AutomationProperties.SetName(CompactGrid, Strings.T("Processes"));
        ContextHintText.Text = Strings.T("CompactContextHint");
        EmptyStateText.Text = Strings.T("NoProcesses");
        LoaderOverlay.RefreshLocalization();
        // Localize by sort key instead of column index, so hiding or reordering columns
        // never mislabels a header.
        LocalizeColumn("Name", "Process");
        LocalizeColumn("Pid", "Pid");
        LocalizeColumn("CpuPercent", "Cpu");
        LocalizeColumn("WorkingSetBytes", "Memory");
    }

    private void LocalizeColumn(string sortMemberPath, string stringKey)
    {
        var column = CompactGrid.Columns.FirstOrDefault(c =>
            string.Equals(c.SortMemberPath, sortMemberPath, StringComparison.Ordinal));
        if (column is not null) column.Header = Strings.T(stringKey);
    }

    public void SetSearchText(string value)
    {
        if (string.Equals(SearchBox.Text, value, StringComparison.Ordinal))
        {
            UpdateSearchVisuals();
            return;
        }

        _syncingSearch = true;
        try
        {
            SearchBox.Text = value;
            SearchBox.CaretIndex = SearchBox.Text.Length;
        }
        finally
        {
            _syncingSearch = false;
        }

        UpdateSearchVisuals();
    }

    public void SetStatus(string value) => StatusText.Text = value;
    public void SetStatusToolTip(string? value) => StatusText.ToolTip = value;

    public void SetSystemMetrics(double cpuPercent, double ramPercent, double usedRamGb, string? cpuModelName, double totalRamGb)
    {
        _cpuPercent = cpuPercent;
        _ramPercent = ramPercent;
        _usedRamGb = usedRamGb;
        _cpuModelName = cpuModelName?.Trim() ?? "";
        _totalRamGb = totalRamGb;

        CompactCpuText.Text = $"{cpuPercent:F1}%";
        CompactRamText.Text = $"{ramPercent:F1}%";

        // Tint layered over each pill's theme background: the badges stay recognisable at rest and
        // warm up towards red as the machine fills, the same ramp the metric cards and rows use.
        CompactCpuHeat.Background = UsageHeat.Tint(cpuPercent, UsageHeat.FromResource("CpuColor", CpuFallback));
        CompactRamHeat.Background = UsageHeat.Tint(ramPercent, UsageHeat.FromResource("RamColor", RamFallback));

        UpdateMetricToolTips();
    }

    private void UpdateMetricToolTips()
    {
        var cpuModel = string.IsNullOrWhiteSpace(_cpuModelName) ? Strings.T("Unknown") : _cpuModelName;
        _cpuTip.SetContent(
            Strings.T("Cpu"),
            $"{_cpuPercent:F1}%",
            UsageHeat.Solid(_cpuPercent, UsageHeat.FromResource("CpuColor", CpuFallback)),
            $"{Strings.T("CpuModel")}: {cpuModel}");
        _ramTip.SetContent(
            Strings.T("Ram"),
            $"{_ramPercent:F1}%",
            UsageHeat.Solid(_ramPercent, UsageHeat.FromResource("RamColor", RamFallback)),
            $"{Strings.T("TotalRam", _totalRamGb)}\n{Strings.T("MemTotal", _usedRamGb)}");
    }

    public void SetEmptyState(bool empty, string? message = null)
    {
        EmptyStateText.Text = message ?? Strings.T("NoProcesses");
        CompactGrid.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Shows or hides the loading overlay. The main window drives this together with the full
    /// view's overlay, so both modes always agree on whether the list is still loading.
    /// </summary>
    public void SetLoading(bool loading) =>
        LoaderOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

    public void SetEndTaskEnabled(bool enabled) => EndTaskButton.IsEnabled = enabled;

    /// <summary>
    /// Reflects the current multi-selection on the compact action bar: a badge with the selected
    /// row count, and an "End Task" button whose tooltip advertises the batch size. The compact
    /// bar is narrow, so it shows the row count only.
    /// </summary>
    public void SetSelectionSummary(int count)
    {
        SelectionBadge.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SelectionCountText.Text = count > 1 ? Strings.T("SelectionCountShort", count) : string.Empty;
        EndTaskButton.ToolTip = count > 1
            ? Strings.T("EndSelectedTooltip", count)
            : Strings.T("EndTaskTooltip");
        AutomationProperties.SetName(
            EndTaskButton,
            count > 1 ? Strings.T("EndSelected", count) : Strings.T("EndTask"));
        // The badge text itself is not exposed to UI Automation (TextBlocks share their parent's
        // peer), so the count is also published through the clear button, which owns a peer.
        AutomationProperties.SetName(ClearSelectionButton, Strings.T("SelectionCountShort", count));
    }

    public void SetRestartEnabled(bool enabled) => RestartButton.IsEnabled = enabled;

    /// <summary>
    /// Enables or disables the restart button and sets its tooltip. When the reason is
    /// <c>null</c> the plain informative tooltip (name + shortcut) is shown, which covers the
    /// "nothing selected" case. A disabled control keeps its tooltip visible only when
    /// <c>ToolTipService.ShowOnDisabled</c> is set, which <see cref="ApplyLanguage"/> handles.
    /// </summary>
    public void SetRestartState(bool enabled, string? tooltip)
    {
        RestartButton.IsEnabled = enabled;
        RestartButton.ToolTip = string.IsNullOrWhiteSpace(tooltip)
            ? $"{Strings.T("Restart")} ({Strings.T("RestartShortcutHint")})"
            : tooltip;
    }

    /// <summary>
    /// Reflects the freeze state on the compact pause button: a play icon while frozen and a
    /// pause icon while running, plus the matching tooltip.
    /// </summary>
    public void SetDetectionPaused(bool paused)
    {
        PauseDetectionButton.IsChecked = paused;
        PauseDetectionIcon.Data = Geometry.Parse(paused
            ? "M 5,3 L 19,12 L 5,21 Z"
            : "M 5,3 H 9 V 21 H 5 Z M 15,3 H 19 V 21 H 15 Z");

        PauseDetectionButton.ToolTip = paused
            ? $"{Strings.T("ResumeDetection")} ({Strings.T("RestartShortcutHint")})"
            : $"{Strings.T("PauseDetection")} ({Strings.T("RestartShortcutHint")})";
        AutomationProperties.SetName(PauseDetectionButton, paused
            ? Strings.T("ResumeDetection")
            : Strings.T("PauseDetection"));
    }

    public void SetHeavyFilter(bool enabled)
    {
        if (IsHeavyFilterEnabled == enabled) return;

        _syncingHeavyFilter = true;
        try
        {
            HeavyFilterToggle.IsChecked = enabled;
        }
        finally
        {
            _syncingHeavyFilter = false;
        }
    }

    public void SetPinned(bool pinned)
    {
        // Borderless buttons: the pinned state is conveyed by icon opacity and the tooltip,
        // not by a colored plate painted behind the icon.
        PinButton.Opacity = pinned ? 1.0 : 0.65;
        PinButton.ToolTip = pinned
            ? $"{Strings.T("AlwaysOnTop")} ✓"
            : Strings.T("AlwaysOnTop");
        AutomationProperties.SetName(PinButton, Strings.T("AlwaysOnTop"));
    }

    public void RefreshItems() => CompactGrid.Items.Refresh();

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchVisuals();
        if (!_syncingSearch) SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSearchVisuals()
    {
        var isEmpty = string.IsNullOrEmpty(SearchBox.Text);
        ClearSearchButton.Visibility = isEmpty
            ? Visibility.Collapsed
            : Visibility.Visible;
        SearchHint.Visibility = isEmpty
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void SearchHistoryButton_Click(object sender, RoutedEventArgs e) =>
        SearchHistoryRequested?.Invoke(sender, e);

    private void HeavyFilterToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_syncingHeavyFilter) HeavyFilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter)
        {
            if (e.Key == Key.Enter)
            {
                SearchSubmitted?.Invoke(this, EventArgs.Empty);
            }

            if (CompactGrid.Items.Count > 0)
            {
                e.Handled = true;
                if (CompactGrid.SelectedIndex < 0) CompactGrid.SelectedIndex = 0;
                CompactGrid.Focus();
                var item = CompactGrid.SelectedItem;
                if (item != null)
                {
                    CompactGrid.ScrollIntoView(item);
                    CompactGrid.UpdateLayout();
                    if (CompactGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
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
                SearchBox.Clear();
            }
            else
            {
                CompactGrid.Focus();
            }
        }
    }

    private void MoreDetailsButton_Click(object sender, RoutedEventArgs e) =>
        ToggleRequested?.Invoke(sender, e);

    /// <summary>
    /// Attaches an empty, per-header context menu as soon as a header is realized. WPF resolves
    /// which ContextMenu to open before it raises <c>ContextMenuOpening</c>, so the header must
    /// already own one. Each header needs its own instance because a ContextMenu can only have a
    /// single logical parent.
    /// </summary>
    private void ColumnHeader_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGridColumnHeader header || header.Column is null) return;
        if (!CompactGrid.Columns.Contains(header.Column)) return;
        if (header.ContextMenu is not null) return;

        header.ContextMenu = new ContextMenu
        {
            PlacementTarget = header,
            Style = TryFindResource("ModernContextMenu") as Style
        };
    }

    /// <summary>
    /// Rebuilds the column visibility menu of the compact grid header that was clicked.
    /// </summary>
    private void ColumnHeader_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not DataGridColumnHeader header || header.Column is null) return;
        if (!CompactGrid.Columns.Contains(header.Column)) return;

        var menu = header.ContextMenu ??= new ContextMenu
        {
            PlacementTarget = header,
            Style = TryFindResource("ModernContextMenu") as Style
        };

        PopulateColumnVisibilityMenu(menu);
    }

    /// <summary>
    /// Opens the column visibility menu when the pointer landed on the empty strip of the header
    /// row, where no column header exists to raise a menu by itself. Returns true when it handled
    /// the hit. The strip is not covered by the headers presenter, so the pointer is compared
    /// against the presenter bounds instead of relying on the hit element.
    /// </summary>
    public bool TryOpenColumnMenuForStrip(DependencyObject? source, Point positionInGrid)
    {
        if (source is null) return false;
        if (FindVisualParent<DataGridRow>(source) != null) return false;
        if (FindVisualParent<DataGridColumnHeader>(source) != null) return false;

        var presenter = FindVisualChild<DataGridColumnHeadersPresenter>(CompactGrid);
        if (presenter is null || !presenter.IsVisible) return false;

        var origin = presenter.TransformToAncestor(CompactGrid).Transform(new Point(0, 0));
        var bounds = new Rect(origin, presenter.RenderSize);

        // The presenter spans the whole grid width; the columns only cover its left part. The blank
        // area to the right of the last column is the dead strip. Any pointer inside the header row
        // band that is not on a header counts as the strip.
        if (positionInGrid.Y < bounds.Top - 2 || positionInGrid.Y > bounds.Bottom + 2) return false;
        if (positionInGrid.X <= GetLastVisibleHeaderRight()) return false;

        var menu = new ContextMenu
        {
            PlacementTarget = CompactGrid,
            Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
            Style = TryFindResource("ModernContextMenu") as Style
        };

        PopulateColumnVisibilityMenu(menu);
        menu.IsOpen = true;
        return true;
    }

    private double GetLastVisibleHeaderRight()
    {
        var presenter = FindVisualChild<DataGridColumnHeadersPresenter>(CompactGrid);
        if (presenter is null) return 0;

        var right = 0.0;
        var count = VisualTreeHelper.GetChildrenCount(presenter);
        for (var i = 0; i < count; i++)
        {
            if (VisualTreeHelper.GetChild(presenter, i) is not DataGridColumnHeader header) continue;
            if (!header.IsVisible) continue;
            var edge = header.TransformToAncestor(CompactGrid).Transform(new Point(header.ActualWidth, 0)).X;
            if (edge > right) right = edge;
        }

        return right;
    }

    /// <summary>
    /// Fills a context menu with one checkable item per compact grid column, reflecting the
    /// current visibility and preventing the last visible column from being hidden.
    /// </summary>
    private void PopulateColumnVisibilityMenu(ContextMenu menu)
    {
        var visibleCount = CompactGrid.Columns.Count(column => column.Visibility == Visibility.Visible);
        menu.Items.Clear();

        foreach (var column in CompactGrid.Columns)
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
            item.Click += (_, _) => ToggleColumnVisibility(column);
            menu.Items.Add(item);
        }
    }

    private void ToggleColumnVisibility(DataGridColumn column)
    {
        var visibleCount = CompactGrid.Columns.Count(c => c.Visibility == Visibility.Visible);
        if (column.Visibility == Visibility.Visible && visibleCount <= 1) return;

        column.Visibility = column.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void EndTaskButton_Click(object sender, RoutedEventArgs e) =>
        EndTaskRequested?.Invoke(sender, e);

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e) =>
        ClearSelectionRequested?.Invoke(sender, e);

    private void RestartButton_Click(object sender, RoutedEventArgs e) =>
        RestartRequested?.Invoke(sender, e);

    private void PauseDetectionButton_Click(object sender, RoutedEventArgs e) =>
        PauseDetectionRequested?.Invoke(sender, e);

    private void PinButton_Click(object sender, RoutedEventArgs e) =>
        PinRequested?.Invoke(sender, e);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(sender, e);

    private void MoreButton_Click(object sender, RoutedEventArgs e) =>
        MoreRequested?.Invoke(sender, e);

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        MinimizeRequested?.Invoke(sender, e);

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(sender, e);

    private void InteractiveControl_MouseEnter(object sender, MouseEventArgs e) =>
        AnimateInteraction(sender, 1.05);

    private void InteractiveControl_MouseLeave(object sender, MouseEventArgs e) =>
        AnimateInteraction(sender, 1.0);

    private static void AnimateInteraction(object sender, double scale)
    {
        if (sender is not UIElement element) return;

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        if (element.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1, 1);
            element.RenderTransform = transform;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(130));
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, duration)
        {
            EasingFunction = easing
        });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, duration)
        {
            EasingFunction = easing
        });
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (IsDescendantOf(source, BrandingPanel))
        {
            // Defer the decision to the matching mouse-up: a plain click opens About, while a
            // drag moves the window. Opening About here would make the branding undraggable.
            _brandingPressed = true;
            _brandingPressPoint = e.GetPosition(this);
            e.Handled = true;
            return;
        }

        if (!IsTitleBarDragSource(source)) return;

        // A double click on the title bar toggles between compact and full mode.
        if (e.ClickCount == 2)
        {
            e.Handled = true;
            ToggleRequested?.Invoke(this, e);
            return;
        }

        DragRequested?.Invoke(this, e);
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_brandingPressed) return;
        _brandingPressed = false;

        // A press that released without moving is a click; anything else was a drag attempt.
        var delta = e.GetPosition(this) - _brandingPressPoint;
        if (Math.Abs(delta.X) < BrandingClickThreshold && Math.Abs(delta.Y) < BrandingClickThreshold)
        {
            AboutRequested?.Invoke(this, e);
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_brandingPressed || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(this) - _brandingPressPoint;
        if (Math.Abs(delta.X) < BrandingClickThreshold && Math.Abs(delta.Y) < BrandingClickThreshold) return;

        // Past the threshold the gesture is a drag: hand it to the host so the move feels native.
        _brandingPressed = false;
        e.Handled = true;
        DragRequested?.Invoke(this, e);
    }

    private bool IsTitleBarDragSource(DependencyObject? source) =>
        IsDescendantOf(source, CompactTitleBar) &&
        FindVisualParent<Button>(source) == null &&
        FindVisualParent<TextBox>(source) == null &&
        FindVisualParent<Border>(source)?.Name is not (nameof(PinButtonHost) or nameof(ModeButtonHost));

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject ancestor)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ancestor)) return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T match) return match;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }

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

}
