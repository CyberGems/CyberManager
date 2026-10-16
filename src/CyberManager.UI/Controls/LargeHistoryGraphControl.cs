using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CyberManager.UI.Controls;

public class LargeHistoryGraphControl : AnimatedHistoryGraph
{
    public static readonly DependencyProperty PrimaryValuesProperty =
        DependencyProperty.Register(
            nameof(PrimaryValues),
            typeof(float[]),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryValuesProperty =
        DependencyProperty.Register(
            nameof(SecondaryValues),
            typeof(float[]),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentValueProperty =
        DependencyProperty.Register(
            nameof(CurrentValue),
            typeof(double),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryCurrentValueProperty =
        DependencyProperty.Register(
            nameof(SecondaryCurrentValue),
            typeof(double),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue),
            typeof(double),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata("CPU", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UnitSuffixProperty =
        DependencyProperty.Register(
            nameof(UnitSuffix),
            typeof(string),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata("%", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PrimaryColorProperty =
        DependencyProperty.Register(
            nameof(PrimaryColor),
            typeof(Color),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(Color.FromRgb(16, 185, 129), FrameworkPropertyMetadataOptions.AffectsRender)); // Emerald / Green

    public static readonly DependencyProperty SecondaryColorProperty =
        DependencyProperty.Register(
            nameof(SecondaryColor),
            typeof(Color),
            typeof(LargeHistoryGraphControl),
            new FrameworkPropertyMetadata(Color.FromRgb(248, 113, 113), FrameworkPropertyMetadataOptions.AffectsRender)); // Red / Coral

    public static readonly DependencyProperty PlotBackgroundProperty = DependencyProperty.Register(
        nameof(PlotBackground), typeof(Brush), typeof(LargeHistoryGraphControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlotGridBrushProperty = DependencyProperty.Register(
        nameof(PlotGridBrush), typeof(Brush), typeof(LargeHistoryGraphControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? PlotBackground
    {
        get => (Brush?)GetValue(PlotBackgroundProperty);
        set => SetValue(PlotBackgroundProperty, value);
    }

    public Brush? PlotGridBrush
    {
        get => (Brush?)GetValue(PlotGridBrushProperty);
        set => SetValue(PlotGridBrushProperty, value);
    }

    public float[]? PrimaryValues
    {
        get => (float[]?)GetValue(PrimaryValuesProperty);
        set => SetValue(PrimaryValuesProperty, value);
    }

    public float[]? SecondaryValues
    {
        get => (float[]?)GetValue(SecondaryValuesProperty);
        set => SetValue(SecondaryValuesProperty, value);
    }

    public double CurrentValue
    {
        get => (double)GetValue(CurrentValueProperty);
        set => SetValue(CurrentValueProperty, value);
    }

    public double SecondaryCurrentValue
    {
        get => (double)GetValue(SecondaryCurrentValueProperty);
        set => SetValue(SecondaryCurrentValueProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string UnitSuffix
    {
        get => (string)GetValue(UnitSuffixProperty);
        set => SetValue(UnitSuffixProperty, value);
    }

    public Color PrimaryColor
    {
        get => (Color)GetValue(PrimaryColorProperty);
        set => SetValue(PrimaryColorProperty, value);
    }

    public Color SecondaryColor
    {
        get => (Color)GetValue(SecondaryColorProperty);
        set => SetValue(SecondaryColorProperty, value);
    }

    public int MaxSamples { get; set; } = 60;
    public bool ShowMeter { get; set; } = true;
    public string? PrimarySeriesLabel { get; set; }
    public string? SecondarySeriesLabel { get; set; }

    private Point? _hoverPos;
    private long _lastHoverRenderTimestamp;
    private readonly HistorySeriesDrawing _primaryDrawing = new();
    private readonly HistorySeriesDrawing _secondaryDrawing = new();
    private readonly ScaleTransform _primaryMeter = new(1, 0);
    private readonly ScaleTransform _secondaryMeter = new(1, 0);
    private double _meterStartPrimary;
    private double _meterStartSecondary;
    private double _meterTargetPrimary;
    private double _meterTargetSecondary;
    private Color? _paintPrimary;
    private Color? _paintSecondary;
    private Brush? _primaryMeterBrush;
    private Brush? _secondaryMeterBrush;
    private Brush? _primaryFill;
    private Brush? _secondaryFill;
    private Pen? _primaryPen;
    private Pen? _secondaryPen;
    private Pen? _plotGridPen;
    internal int GeometryBuildCount => _primaryDrawing.BuildCount + _secondaryDrawing.BuildCount;

    public void UpdateHistory(float[] primary, float[]? secondary, object sample, TimeSpan interval)
    {
        if (IsCurrentSample(sample)) return;
        PrimaryValues = primary;
        SecondaryValues = secondary;
        BeginSample(sample, interval, primary, secondary, Math.Max(2, MaxSamples));
    }

    protected override void OnAnimationStarted()
    {
        _meterStartPrimary = _primaryMeter.ScaleY;
        _meterStartSecondary = _secondaryMeter.ScaleY;
    }

    protected override void OnAnimationFrame(double elapsedSeconds, bool finished)
    {
        var progress = finished ? 1 : Math.Clamp(elapsedSeconds / 0.2, 0, 1);
        var eased = 1 - (1 - progress) * (1 - progress);
        _primaryMeter.ScaleY = _meterStartPrimary + (_meterTargetPrimary - _meterStartPrimary) * eased;
        _secondaryMeter.ScaleY = _meterStartSecondary + (_meterTargetSecondary - _meterStartSecondary) * eased;
    }

    private static readonly Typeface GraphTypeface = new(
        new FontFamily("Segoe UI, Arial"),
        FontStyles.Normal,
        FontWeights.SemiBold,
        FontStretches.Normal);
    private static readonly SolidColorBrush GraphBackgroundBrush = CreateBrush(Color.FromArgb(90, 8, 14, 26));
    private static readonly SolidColorBrush GraphBorderBrush = CreateBrush(Color.FromArgb(60, 255, 255, 255));
    private static readonly Pen GraphBorderPen = CreatePen(GraphBorderBrush, 1.0);
    private static readonly SolidColorBrush GridBrush = CreateBrush(Color.FromArgb(25, 255, 255, 255));
    private static readonly Pen GridPen = CreatePen(GridBrush, 1.0);
    private static readonly SolidColorBrush MeterTitleBrush = CreateBrush(Color.FromArgb(200, 255, 255, 255));
    private static readonly SolidColorBrush MeterValueBrush = CreateBrush(Color.FromRgb(240, 240, 240));
    private static readonly SolidColorBrush TooltipBackgroundBrush = CreateBrush(Color.FromArgb(220, 10, 16, 28));

    public LargeHistoryGraphControl()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _hoverPos = e.GetPosition(this);
        var now = Stopwatch.GetTimestamp();
        if ((now - _lastHoverRenderTimestamp) * 60 < Stopwatch.Frequency)
        {
            return;
        }

        _lastHoverRenderTimestamp = now;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverPos = null;
        InvalidateVisual();
    }

    [SuppressMessage("Naming", "CA1725", Justification = "The parameter name is part of the WPF override contract.")]
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 50 || h < 40) return;

        double meterWidth = 38;
        double gap = 12;
        double graphLeft = ShowMeter ? meterWidth + gap : 0;
        double graphWidth = Math.Max(10, w - graphLeft);
        double graphHeight = h - 24;
        double graphTop = 8;
        double graphBottom = graphTop + graphHeight;
        EnsurePaints();

        double max = MaxValue > 0 ? MaxValue : 100.0;
        if (ShowMeter)
        {
            // 1. Draw Left Vertical Meter Box
            var meterRect = new Rect(0, graphTop, meterWidth, graphHeight);
            dc.DrawRoundedRectangle(GraphBackgroundBrush, GraphBorderPen, meterRect, 4, 4);

            double curVal = Math.Clamp(CurrentValue, 0.0, max);
            double secVal = Math.Clamp(SecondaryCurrentValue, 0.0, curVal);

            double curNorm = curVal / max;
            double secNorm = secVal / max;

            _meterTargetPrimary = curNorm;
            _meterTargetSecondary = secNorm;
            _primaryMeter.CenterY = _secondaryMeter.CenterY = graphBottom;
            if (!IsAnimating)
            {
                _primaryMeter.ScaleY = curNorm;
                _secondaryMeter.ScaleY = secNorm;
            }

            // Draw primary bar fill
            dc.PushTransform(_primaryMeter);
            dc.DrawRectangle(_primaryMeterBrush, null, new Rect(1, graphTop + 2, meterWidth - 2, graphHeight - 2));
            dc.Pop();

            // Draw secondary bar fill (kernel)
            dc.PushTransform(_secondaryMeter);
            dc.DrawRectangle(_secondaryMeterBrush, null, new Rect(1, graphTop + 2, meterWidth - 2, graphHeight - 2));
            dc.Pop();

            // Meter Title & Value
            var titleFt = new FormattedText(
                Title,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GraphTypeface,
                10.5,
                MeterTitleBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(titleFt, new Point(meterWidth / 2.0 - titleFt.Width / 2.0, graphTop + 4));

            string curValStr = UnitSuffix == "%" ? $"{curVal:F1}%" : $"{curVal:F1}{UnitSuffix}";
            var valFt = new FormattedText(
                curValStr,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                GraphTypeface,
                9.5,
                MeterValueBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(valFt, new Point(meterWidth / 2.0 - valFt.Width / 2.0, graphBottom - valFt.Height - 4));
        }

        // 2. Draw Main Graph Box
        var graphRect = new Rect(graphLeft, graphTop, graphWidth, graphHeight);
        dc.DrawRoundedRectangle(PlotBackground ?? GraphBackgroundBrush, GraphBorderPen, graphRect, 4, 4);

        var gridPen = GridPen;
        if (PlotGridBrush is { } gridBrush)
        {
            if (_plotGridPen == null || !ReferenceEquals(_plotGridPen.Brush, gridBrush))
                _plotGridPen = new Pen(gridBrush, 1);
            gridPen = _plotGridPen;
        }

        // Grid Lines (25%, 50%, 75%)
        for (int step = 1; step <= 3; step++)
        {
            double gy = graphBottom - (step * 0.25 * graphHeight);
            dc.DrawLine(gridPen, new Point(graphLeft + 1, gy), new Point(graphLeft + graphWidth - 1, gy));
        }

        // Clip data to graph box
        dc.PushClip(new RectangleGeometry(new Rect(graphLeft + 1, graphTop + 1, graphWidth - 2, graphHeight - 2)));

        int capacity = Math.Max(2, MaxSamples);
        SetHistoryWidth(graphWidth, capacity);
        dc.PushTransform(HistoryTransform);

        // 3. Draw Primary Series
        var primary = PrimaryValues;
        if (primary != null && primary.Length > 0)
        {
            _primaryDrawing.Draw(dc, primary, graphRect, max, capacity, PrimaryPrefix, _primaryFill, _primaryPen);
        }

        // 4. Draw Secondary Series (Kernel / Commit)
        var secondary = SecondaryValues;
        if (secondary != null && secondary.Length > 0)
        {
            _secondaryDrawing.Draw(dc, secondary, graphRect, max, capacity, SecondaryPrefix, _secondaryFill, _secondaryPen);
        }

        // 5. Hover Indicator
        if (_hoverPos.HasValue && primary != null && primary.Length > 0)
        {
            double mx = _hoverPos.Value.X;
            if (mx >= graphLeft && mx <= graphLeft + graphWidth)
            {
                double stepX = graphWidth / (capacity - 1);
                int count = primary.Length;
                double startX = graphLeft + graphWidth - (count - 1) * stepX;
                int hoverIdx = (int)Math.Round((mx - HistoryOffset - startX) / stepX);
                if (hoverIdx >= 0 && hoverIdx < count)
                {
                    double ptX = startX + hoverIdx * stepX;
                    var hoverPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.0)
                    {
                        DashStyle = DashStyles.Dot
                    };
                    hoverPen.Freeze();
                    dc.DrawLine(hoverPen, new Point(ptX, graphTop), new Point(ptX, graphBottom));

                    float val = primary[hoverIdx];
                    float secV = secondary != null && hoverIdx < secondary.Length ? secondary[hoverIdx] : 0f;
                    string tipText = secondary != null && SecondarySeriesLabel != null
                        ? $"{PrimarySeriesLabel}: {val:F1} {UnitSuffix}\n{SecondarySeriesLabel}: {secV:F1} {UnitSuffix}"
                        : UnitSuffix == "%"
                        ? $"{(secondary != null ? $"Total: {val:F1}%\nKernel: {secV:F1}%" : $"{val:F1}%")}"
                        : $"{val:F1} {UnitSuffix}";

                    var tipFt = new FormattedText(
                        tipText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        GraphTypeface,
                        10.0,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double tipX = Math.Min(ptX + 8, graphLeft + graphWidth - HistoryOffset - tipFt.Width - 12);
                    double tipY = graphTop + 8;
                    var tipBg = new Rect(tipX - 4, tipY - 2, tipFt.Width + 8, tipFt.Height + 4);
                    dc.DrawRoundedRectangle(TooltipBackgroundBrush, GraphBorderPen, tipBg, 4, 4);
                    dc.DrawText(tipFt, new Point(tipX, tipY));
                }
            }
        }

        dc.Pop(); // history transform
        dc.Pop(); // clip
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private void EnsurePaints()
    {
        if (_paintPrimary != PrimaryColor)
        {
            var color = PrimaryColor;
            _paintPrimary = color;
            _primaryMeterBrush = CreateBrush(Color.FromArgb(160, color.R, color.G, color.B));
            _primaryFill = CreateGradient(color, 120, 25);
            _primaryPen = CreateSeriesPen(color, 1.6);
        }
        if (_paintSecondary != SecondaryColor)
        {
            var color = SecondaryColor;
            _paintSecondary = color;
            _secondaryMeterBrush = CreateBrush(Color.FromArgb(210, color.R, color.G, color.B));
            _secondaryFill = CreateGradient(color, 180, 50);
            _secondaryPen = CreateSeriesPen(color, 1.4);
        }
    }

    private static LinearGradientBrush CreateGradient(Color color, byte topAlpha, byte bottomAlpha)
    {
        var gradient = new LinearGradientBrush(Color.FromArgb(topAlpha, color.R, color.G, color.B),
            Color.FromArgb(bottomAlpha, color.R, color.G, color.B), 90);
        gradient.Freeze();
        return gradient;
    }

    private static Pen CreateSeriesPen(Color color, double thickness)
    {
        var pen = new Pen(CreateBrush(color), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }
}
