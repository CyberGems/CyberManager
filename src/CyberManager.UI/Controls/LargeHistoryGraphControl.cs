using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CyberManager.UI.Controls;

public class LargeHistoryGraphControl : FrameworkElement
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

    private Point? _hoverPos;

    public LargeHistoryGraphControl()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _hoverPos = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverPos = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 50 || h < 40) return;

        double meterWidth = 38;
        double gap = 12;
        double graphLeft = meterWidth + gap;
        double graphWidth = Math.Max(10, w - graphLeft);
        double graphHeight = h - 24;
        double graphTop = 8;
        double graphBottom = graphTop + graphHeight;

        var bgBrush = new SolidColorBrush(Color.FromArgb(90, 8, 14, 26));
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1.0);
        borderPen.Freeze();

        // 1. Draw Left Vertical Meter Box
        var meterRect = new Rect(0, graphTop, meterWidth, graphHeight);
        dc.DrawRoundedRectangle(bgBrush, borderPen, meterRect, 4, 4);

        double max = MaxValue > 0 ? MaxValue : 100.0;
        double curVal = Math.Clamp(CurrentValue, 0.0, max);
        double secVal = Math.Clamp(SecondaryCurrentValue, 0.0, curVal);

        double curNorm = curVal / max;
        double secNorm = secVal / max;

        double totalBarH = curNorm * (graphHeight - 2);
        double secBarH = secNorm * (graphHeight - 2);

        // Draw primary bar fill
        if (totalBarH > 0)
        {
            var pFill = new SolidColorBrush(Color.FromArgb(160, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B));
            pFill.Freeze();
            var pRect = new Rect(1, graphBottom - totalBarH, meterWidth - 2, totalBarH);
            dc.DrawRectangle(pFill, null, pRect);
        }

        // Draw secondary bar fill (kernel)
        if (secBarH > 0)
        {
            var sFill = new SolidColorBrush(Color.FromArgb(210, SecondaryColor.R, SecondaryColor.G, SecondaryColor.B));
            sFill.Freeze();
            var sRect = new Rect(1, graphBottom - secBarH, meterWidth - 2, secBarH);
            dc.DrawRectangle(sFill, null, sRect);
        }

        // Meter Title & Value
        var typeFace = new Typeface(new FontFamily("Segoe UI, Arial"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var titleFt = new FormattedText(
            Title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeFace,
            10.5,
            new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(titleFt, new Point(meterWidth / 2.0 - titleFt.Width / 2.0, graphTop + 4));

        string curValStr = UnitSuffix == "%" ? $"{curVal:F1}%" : $"{curVal:F1}{UnitSuffix}";
        var valFt = new FormattedText(
            curValStr,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeFace,
            9.5,
            new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(valFt, new Point(meterWidth / 2.0 - valFt.Width / 2.0, graphBottom - valFt.Height - 4));

        // 2. Draw Main Graph Box
        var graphRect = new Rect(graphLeft, graphTop, graphWidth, graphHeight);
        dc.DrawRoundedRectangle(bgBrush, borderPen, graphRect, 4, 4);

        // Grid Lines (25%, 50%, 75%)
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1.0);
        gridPen.Freeze();
        for (int step = 1; step <= 3; step++)
        {
            double gy = graphBottom - (step * 0.25 * graphHeight);
            dc.DrawLine(gridPen, new Point(graphLeft + 1, gy), new Point(graphLeft + graphWidth - 1, gy));
        }

        // Clip data to graph box
        dc.PushClip(new RectangleGeometry(new Rect(graphLeft + 1, graphTop + 1, graphWidth - 2, graphHeight - 2)));

        int capacity = Math.Max(2, MaxSamples);

        // 3. Draw Primary Series
        var primary = PrimaryValues;
        if (primary != null && primary.Length > 0)
        {
            var pFillGrad = new LinearGradientBrush(
                Color.FromArgb(120, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B),
                Color.FromArgb(25, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B),
                90.0);
            pFillGrad.Freeze();
            var pStroke = new SolidColorBrush(PrimaryColor);
            pStroke.Freeze();
            DrawArea(dc, primary, graphLeft, graphTop, graphWidth, graphHeight, max, capacity, pFillGrad, pStroke, 1.6);
        }

        // 4. Draw Secondary Series (Kernel / Commit)
        var secondary = SecondaryValues;
        if (secondary != null && secondary.Length > 0)
        {
            var sFillGrad = new LinearGradientBrush(
                Color.FromArgb(180, SecondaryColor.R, SecondaryColor.G, SecondaryColor.B),
                Color.FromArgb(50, SecondaryColor.R, SecondaryColor.G, SecondaryColor.B),
                90.0);
            sFillGrad.Freeze();
            var sStroke = new SolidColorBrush(SecondaryColor);
            sStroke.Freeze();
            DrawArea(dc, secondary, graphLeft, graphTop, graphWidth, graphHeight, max, capacity, sFillGrad, sStroke, 1.4);
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
                int hoverIdx = (int)Math.Round((mx - startX) / stepX);
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
                    string tipText = UnitSuffix == "%" 
                        ? $"{(secondary != null ? $"Total: {val:F1}%\nKernel: {secV:F1}%" : $"{val:F1}%")}"
                        : $"{val:F1} {UnitSuffix}";

                    var tipFt = new FormattedText(
                        tipText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeFace,
                        10.0,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double tipX = Math.Min(ptX + 8, graphLeft + graphWidth - tipFt.Width - 12);
                    double tipY = graphTop + 8;
                    var tipBg = new Rect(tipX - 4, tipY - 2, tipFt.Width + 8, tipFt.Height + 4);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 10, 16, 28)), borderPen, tipBg, 4, 4);
                    dc.DrawText(tipFt, new Point(tipX, tipY));
                }
            }
        }

        dc.Pop(); // pop clip
    }

    private static void DrawArea(
        DrawingContext dc,
        float[] data,
        double left,
        double top,
        double width,
        double height,
        double max,
        int capacity,
        Brush fillBrush,
        Brush strokeBrush,
        double strokeThickness)
    {
        int count = data.Length;
        if (count < 1) return;

        double bottom = top + height;
        double stepX = width / (capacity - 1);
        double startX = left + width - (count - 1) * stepX;

        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double x = startX + i * stepX;
            double norm = Math.Clamp(data[i] / max, 0.0, 1.0);
            double y = bottom - (norm * (height - 2.0)) - 1.0;
            points[i] = new Point(x, y);
        }

        if (count >= 2)
        {
            var fillGeo = new StreamGeometry();
            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, bottom), true, true);
                for (int i = 0; i < count; i++)
                {
                    ctx.LineTo(points[i], true, false);
                }
                ctx.LineTo(new Point(points[count - 1].X, bottom), true, false);
            }
            fillGeo.Freeze();
            dc.DrawGeometry(fillBrush, null, fillGeo);
        }

        var lineGeo = new StreamGeometry();
        using (var ctx = lineGeo.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            for (int i = 1; i < count; i++)
            {
                ctx.LineTo(points[i], true, true);
            }
        }
        lineGeo.Freeze();
        var pen = new Pen(strokeBrush, strokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, lineGeo);
    }
}
