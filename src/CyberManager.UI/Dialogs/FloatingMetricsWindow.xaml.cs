using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Shapes;
using CyberManager.Common.I18n;
using CyberManager.UI.Services;

namespace CyberManager.UI.Dialogs;

/// <summary>
/// Small always-on-top companion window that mirrors the CPU and memory sparklines of the main
/// window so the metrics stay visible while the main window is hidden in the tray.
/// It is a passive view: the main window owns the sampling and pushes values in through
/// <see cref="UpdateMetrics"/>, which keeps the shared history ring buffer advancing once.
/// </summary>
public partial class FloatingMetricsWindow : Window
{
    /// <summary>Raised when the user asks to close the widget.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when the user toggles always-on-top.</summary>
    public event Action<bool>? AlwaysOnTopChanged;

    /// <summary>Raised when the widget is moved, with its new screen position.</summary>
    public event Action<double, double>? PositionChanged;

    public FloatingMetricsWindow()
    {
        InitializeComponent();
        CyberManagerWindowChrome.Apply(this, 12);
        ApplyLocalization();
        ApplyAlwaysOnTop(App.Settings.WidgetAlwaysOnTop);
    }

    /// <summary>Applies the current position without echoing it back through <see cref="PositionChanged"/>.</summary>
    public void SetPosition(double left, double top)
    {
        if (double.IsFinite(left) && double.IsFinite(top))
        {
            Left = left;
            Top = top;
        }
    }

    public void ApplyLocalization()
    {
        CpuLabel.Text = Strings.T("Cpu");
        RamLabel.Text = Strings.T("Ram");
        CloseButton.ToolTip = Strings.T("CloseWidget");
        AutomationProperties.SetName(CloseButton, Strings.T("CloseWidget"));
        AutomationProperties.SetName(PinButton, Strings.T("AlwaysOnTop"));
        UpdatePinToolTip();
    }

    public void ApplyAlwaysOnTop(bool onTop)
    {
        Topmost = onTop;
        PinButton.IsChecked = onTop;
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

    private void UpdatePinToolTip()
    {
        var text = Strings.T("AlwaysOnTop");
        PinButton.ToolTip = text;
    }

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

    private void Window_LocationChanged(object sender, EventArgs e) => PositionChanged?.Invoke(Left, Top);
}
