using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CyberManager.UI.Controls;

public class SparklineControl : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(float[]),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryValuesProperty =
        DependencyProperty.Register(
            nameof(SecondaryValues),
            typeof(float[]),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeBrushProperty =
        DependencyProperty.Register(
            nameof(StrokeBrush),
            typeof(Brush),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 229, 255)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty =
        DependencyProperty.Register(
            nameof(FillBrush),
            typeof(Brush),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryStrokeBrushProperty =
        DependencyProperty.Register(
            nameof(SecondaryStrokeBrush),
            typeof(Brush),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(248, 113, 113)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SecondaryFillBrushProperty =
        DependencyProperty.Register(
            nameof(SecondaryFillBrush),
            typeof(Brush),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue),
            typeof(double),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueLabelProperty =
        DependencyProperty.Register(
            nameof(ValueLabel),
            typeof(string),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridLinesBrushProperty =
        DependencyProperty.Register(
            nameof(GridLinesBrush),
            typeof(Brush),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent(nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SparklineControl));

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public float[]? Values
    {
        get => (float[]?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public float[]? SecondaryValues
    {
        get => (float[]?)GetValue(SecondaryValuesProperty);
        set => SetValue(SecondaryValuesProperty, value);
    }

    public Brush StrokeBrush
    {
        get => (Brush)GetValue(StrokeBrushProperty);
        set => SetValue(StrokeBrushProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public Brush SecondaryStrokeBrush
    {
        get => (Brush)GetValue(SecondaryStrokeBrushProperty);
        set => SetValue(SecondaryStrokeBrushProperty, value);
    }

    public Brush? SecondaryFillBrush
    {
        get => (Brush?)GetValue(SecondaryFillBrushProperty);
        set => SetValue(SecondaryFillBrushProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public string ValueLabel
    {
        get => (string)GetValue(ValueLabelProperty);
        set => SetValue(ValueLabelProperty, value);
    }

    public Brush GridLinesBrush
    {
        get => (Brush)GetValue(GridLinesBrushProperty);
        set => SetValue(GridLinesBrushProperty, value);
    }

    public int MaxSamples { get; set; } = 60;

    public SparklineControl()
    {
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 2 || h <= 2) return;

        // Clip to rounded rectangle
        var clipGeo = new RectangleGeometry(new Rect(0, 0, w, h), 6, 6);
        dc.PushClip(clipGeo);

        // Draw faint horizontal grid line at 50%
        if (GridLinesBrush != null)
        {
            var gridPen = new Pen(GridLinesBrush, 1.0);
            gridPen.Freeze();
            dc.DrawLine(gridPen, new Point(0, h * 0.5), new Point(w, h * 0.5));
            dc.DrawLine(gridPen, new Point(0, h - 0.5), new Point(w, h - 0.5));
        }

        double max = MaxValue > 0 ? MaxValue : 100.0;
        int capacity = Math.Max(2, MaxSamples);

        // 1. Draw Primary Series (Total / User)
        var values = Values;
        if (values != null && values.Length > 0)
        {
            DrawSeries(dc, values, w, h, max, capacity, FillBrush, StrokeBrush, 1.4);
        }

        // 2. Draw Secondary Series (e.g. Kernel CPU)
        var secValues = SecondaryValues;
        if (secValues != null && secValues.Length > 0)
        {
            DrawSeries(dc, secValues, w, h, max, capacity, SecondaryFillBrush, SecondaryStrokeBrush, 1.2);
        }

        dc.Pop(); // pop clip
    }

    private static void DrawSeries(
        DrawingContext dc,
        float[] data,
        double w,
        double h,
        double max,
        int capacity,
        Brush? fillBrush,
        Brush? strokeBrush,
        double strokeThickness)
    {
        int count = data.Length;
        if (count < 1) return;

        double stepX = w / (capacity - 1);
        double startX = w - (count - 1) * stepX;

        var points = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double x = startX + i * stepX;
            double norm = Math.Clamp(data[i] / max, 0.0, 1.0);
            double y = h - (norm * (h - 2.0)) - 1.0;
            points[i] = new Point(x, y);
        }

        // Draw Fill Geometry
        if (fillBrush != null && count >= 2)
        {
            var fillGeo = new StreamGeometry();
            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, h), true, true);
                for (int i = 0; i < count; i++)
                {
                    ctx.LineTo(points[i], true, false);
                }
                ctx.LineTo(new Point(points[count - 1].X, h), true, false);
            }
            fillGeo.Freeze();
            dc.DrawGeometry(fillBrush, null, fillGeo);
        }

        // Draw Line Stroke
        if (strokeBrush != null)
        {
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
}
