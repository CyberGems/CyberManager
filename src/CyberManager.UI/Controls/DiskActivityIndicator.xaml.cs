using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CyberManager.Common.I18n;
using CyberManager.Core.Engine;

namespace CyberManager.UI.Controls;

public partial class DiskActivityIndicator : UserControl
{
    private readonly MetricToolTipContent _tip;
    private readonly ContentControl _actionHint = new() { Margin = new Thickness(0, 8, 0, 0) };
    private DiskTelemetrySnapshot _snapshot = new();
    private Color? _paint;

    public DiskActivityIndicator()
    {
        InitializeComponent();
        var tip = MetricToolTipContent.Create(out _tip);
        tip.Content = null;
        var panel = new StackPanel();
        panel.Children.Add(_tip);
        panel.Children.Add(_actionHint);
        tip.Content = panel;
        ActivityButton.ToolTip = tip;
        RefreshLocalization();
    }

    public event RoutedEventHandler Click
    {
        add => ActivityButton.Click += value;
        remove => ActivityButton.Click -= value;
    }

    public void UpdateMetrics(DiskTelemetrySnapshot snapshot)
    {
        _snapshot = snapshot;
        UpdateVisuals();
    }

    public void RefreshLocalization()
    {
        _actionHint.Content = HotkeyToolTipContent.WithShortcut(Strings.T("OpenDiskPerformance"), KeyboardShortcuts.Disks);
        AutomationProperties.SetName(ActivityButton, Strings.T("OpenDiskPerformance"));
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var disk = _snapshot.Combined;
        var ready = _snapshot.Status == DiskMetricsStatus.Ready;
        var reading = ready && disk.ReadBytesPerSecond > 0;
        var writing = ready && disk.WriteBytesPerSecond > 0;
        var color = reading && writing ? Color.FromRgb(245, 158, 11)
            : writing ? Color.FromRgb(239, 68, 68)
            : reading ? Color.FromRgb(16, 185, 129) : Color.FromRgb(65, 77, 94);
        if (_paint != color)
        {
            _paint = color;
            Led.Fill = new RadialGradientBrush(
                Color.FromRgb((byte)Math.Min(255, color.R + 50), (byte)Math.Min(255, color.G + 50), (byte)Math.Min(255, color.B + 50)),
                Color.FromRgb((byte)(color.R / 2), (byte)(color.G / 2), (byte)(color.B / 2)))
            { GradientOrigin = new Point(0.3, 0.25), Center = new Point(0.35, 0.35), RadiusX = 0.75, RadiusY = 0.75 };
            Glow.Color = color;
            Glow.Opacity = reading || writing ? 0.7 : 0;
        }

        var detail = ready
            ? Strings.T("DiskLedRates", DiskMetricFormatting.Rate(disk.ReadBytesPerSecond), DiskMetricFormatting.Rate(disk.WriteBytesPerSecond)) + "\n" +
              Strings.T(reading && writing ? "DiskReadingWriting" : reading ? "DiskReading" : writing ? "DiskWriting" : "DiskIdle")
            : Strings.T(_snapshot.Status == DiskMetricsStatus.WarmingUp ? "DiskWarmingUp" : "DiskUnavailable");
        _tip.SetContent(Strings.T("DiskActivity"), ready ? $"{disk.ActivePercent:F1}%" : "—",
            ready && (reading || writing) ? new SolidColorBrush(color) : TryFindResource("SubTextBrush") as Brush ?? Brushes.Gray,
            detail);
        AutomationProperties.SetHelpText(ActivityButton, detail);
    }
}
