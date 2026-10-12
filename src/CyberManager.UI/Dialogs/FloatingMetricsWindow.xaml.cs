using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

/// <summary>Actions the widget context menu asks the host window to perform.</summary>
public enum WidgetMenuAction
{
    ShowMainWindow,
    Refresh,
    SystemInformation,
    Settings,
    About,
    Exit
}

/// <summary>
/// Small always-on-top companion window that mirrors the CPU and memory sparklines of the main
/// window so the metrics stay visible while the main window is hidden in the tray.
/// It is a passive view: the main window owns the sampling and pushes values in through
/// <see cref="UpdateMetrics"/>, which keeps the shared history ring buffer advancing once.
/// </summary>
public partial class FloatingMetricsWindow : Window
{
    private const int WM_DISPLAYCHANGE = 0x007E;

    /// <summary>Gap kept from the work area edge when the widget is placed for the first time.</summary>
    private const double DefaultEdgeMargin = 24;

    private bool _positionApplied;

    /// <summary>Raised when the user asks to close the widget.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when the user toggles always-on-top, from the pin or the context menu.</summary>
    public event Action<bool>? AlwaysOnTopChanged;

    /// <summary>Raised when the widget is moved, with its new screen position.</summary>
    public event Action<double, double>? PositionChanged;

    /// <summary>Raised when the user picks an entry from the widget context menu.</summary>
    public event Action<WidgetMenuAction>? MenuActionRequested;

    public FloatingMetricsWindow()
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);
        ApplyLocalization();
        ApplyAlwaysOnTop(App.Settings.WidgetAlwaysOnTop);
        Loaded += (_, _) => ApplyInitialPosition();
        IsVisibleChanged += Window_IsVisibleChanged;
    }

    public void ApplyLocalization()
    {
        CpuLabel.Text = Strings.T("Cpu");
        RamLabel.Text = Strings.T("Ram");
        CloseButton.ToolTip = Strings.T("CloseWidget");
        AutomationProperties.SetName(CloseButton, Strings.T("CloseWidget"));
        AutomationProperties.SetName(PinButton, Strings.T("AlwaysOnTop"));
        UpdatePinToolTip();

        WidgetPinItem.Header = Strings.T("AlwaysOnTop");
        WidgetShowItem.Header = Strings.T("ShowMainWindow");
        WidgetShowItem.InputGestureText = App.Settings.GlobalHotkey;
        WidgetRefreshItem.Header = Strings.T("RefreshProcesses");
        WidgetSysInfoItem.Header = $"{Strings.T("SystemInformation")}...";
        WidgetSettingsItem.Header = $"{Strings.T("Settings")}...";
        WidgetAboutItem.Header = Strings.T("AboutCyberManager");
        WidgetExitItem.Header = Strings.T("Exit");
    }

    public void ApplyAlwaysOnTop(bool onTop)
    {
        Topmost = onTop;
        PinButton.IsChecked = onTop;
        WidgetPinItem.IsChecked = onTop;
        PinIcon.SetResourceReference(Shape.StrokeProperty, onTop ? "AccentBrush" : "SubTextBrush");
        UpdatePinToolTip();
    }

    public void UpdateMetrics(
        double cpuPercent,
        float[] cpuHistory,
        float[] cpuKernelHistory,
        double ramPercent,
        float[] ramHistory,
        double usedRamGb,
        double totalRamGb,
        string? cpuModelName)
    {
        CpuValueText.Text = $"{cpuPercent:F1}%";
        CpuSparkline.Values = cpuHistory;
        CpuSparkline.SecondaryValues = cpuKernelHistory;

        RamValueText.Text = $"{ramPercent:F1}%";
        RamSparkline.Values = ramHistory;

        var cpuModel = string.IsNullOrWhiteSpace(cpuModelName) ? Strings.T("Unknown") : cpuModelName;
        CpuRow.ToolTip = $"{Strings.T("CpuModel")}: {cpuModel}\n{Strings.T("CpuTotal", cpuPercent)}";
        RamRow.ToolTip = $"{Strings.T("MemTotal", usedRamGb)}\n{Strings.T("TotalRam", totalRamGb)}";
    }

    /// <summary>
    /// Places the widget for the first time, once its measured size is known. A window using
    /// <c>SizeToContent</c> reports NaN as its height before the first layout pass.
    /// </summary>
    private void ApplyInitialPosition()
    {
        if (_positionApplied) return;
        _positionApplied = true;

        if (App.Settings.WidgetBoundsSaved)
        {
            MoveOnScreen(App.Settings.WidgetLeft, App.Settings.WidgetTop);
            return;
        }

        var area = MonitorPlacement.PrimaryWorkArea;
        MoveOnScreen(area.Right - Width - DefaultEdgeMargin, area.Bottom - DefaultEdgeMargin - 160);
    }

    /// <summary>
    /// Moves the widget to the requested position, clamped so the whole window stays inside the
    /// work area of the nearest monitor. A position saved on a monitor that is now gone therefore
    /// lands on the closest existing display instead of off screen.
    /// </summary>
    private void MoveOnScreen(double left, double top)
    {
        var (clampedLeft, clampedTop) = MonitorPlacement.ClampToNearestMonitor(this, left, top);
        Left = clampedLeft;
        Top = clampedTop;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
        }
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Coming back from a hidden period, the display layout may have changed meanwhile.
        if (IsVisible && _positionApplied) MoveOnScreen(Left, Top);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE)
        {
            // A monitor was added, removed or rearranged while the widget is on screen.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_positionApplied) MoveOnScreen(Left, Top);
            }));
        }

        return IntPtr.Zero;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // SizeToContent means taller content (a longer translation, a different DPI) could push
        // the widget past the bottom edge, so re-clamp once the new size is known.
        if (_positionApplied && e.HeightChanged) MoveOnScreen(Left, Top);
    }

    private void UpdatePinToolTip() => PinButton.ToolTip = Strings.T("AlwaysOnTop");

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove throws when the button was released between the event and the call.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void PinButton_Click(object sender, RoutedEventArgs e) =>
        AlwaysOnTopChanged?.Invoke(PinButton.IsChecked == true);

    private void WidgetPinItem_Click(object sender, RoutedEventArgs e)
    {
        var onTop = WidgetPinItem.IsChecked;
        ApplyAlwaysOnTop(onTop);
        AlwaysOnTopChanged?.Invoke(onTop);
    }

    private void WidgetShowItem_Click(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(WidgetMenuAction.ShowMainWindow);

    private void WidgetRefreshItem_Click(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(WidgetMenuAction.Refresh);

    private void WidgetSysInfoItem_Click(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(WidgetMenuAction.SystemInformation);

    private void WidgetSettingsItem_Click(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(WidgetMenuAction.Settings);

    private void WidgetAboutItem_Click(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(WidgetMenuAction.About);

    private void WidgetExitItem_Click(object sender, RoutedEventArgs e) =>
        MenuActionRequested?.Invoke(WidgetMenuAction.Exit);

    private void Window_LocationChanged(object sender, EventArgs e) => PositionChanged?.Invoke(Left, Top);
}
