using System.Windows;
using System.Windows.Media;

namespace CyberManager.UI.Controls;

/// <summary>Caches frozen geometry until the samples, scale or drawing bounds change.</summary>
internal sealed class HistorySeriesDrawing
{
    private float[]? _data;
    private Rect _bounds;
    private double _maximum;
    private int _capacity;
    private IReadOnlyList<float>? _prefix;
    private StreamGeometry? _fill;
    private StreamGeometry? _line;
    internal int BuildCount { get; private set; }

    internal void Draw(DrawingContext context, float[]? data, Rect bounds, double maximum, int capacity,
        IReadOnlyList<float> prefix, Brush? fill, Pen? pen)
    {
        if (data is not { Length: > 0 }) return;
        if (!ReferenceEquals(_data, data) || _bounds != bounds || _maximum != maximum ||
            _capacity != capacity || !ReferenceEquals(_prefix, prefix))
        {
            _data = data;
            _bounds = bounds;
            _maximum = maximum;
            _capacity = capacity;
            _prefix = prefix;
            Build(data, bounds, maximum, capacity, prefix);
        }
        if (fill is not null && data.Length + prefix.Count >= 2) context.DrawGeometry(fill, null, _fill);
        if (pen is not null) context.DrawGeometry(null, pen, _line);
    }

    private void Build(float[] data, Rect bounds, double maximum, int capacity, IReadOnlyList<float> prefix)
    {
        var count = data.Length + prefix.Count;
        var step = bounds.Width / Math.Max(1, capacity - 1);
        var start = bounds.Right - (count - 1) * step;
        _fill = new StreamGeometry();
        _line = new StreamGeometry();
        using (var fill = _fill.Open())
        using (var line = _line.Open())
        {
            fill.BeginFigure(new Point(start, bounds.Bottom), true, true);
            for (var index = 0; index < count; index++)
            {
                var value = index < prefix.Count ? prefix[index] : data[index - prefix.Count];
                var normalized = Math.Clamp(value / maximum, 0, 1);
                var point = new Point(start + index * step, bounds.Bottom - normalized * (bounds.Height - 2) - 1);
                fill.LineTo(point, true, false);
                if (index == 0) line.BeginFigure(point, false, false);
                else line.LineTo(point, true, true);
            }
            fill.LineTo(new Point(bounds.Right, bounds.Bottom), true, false);
        }
        _fill.Freeze();
        _line.Freeze();
        BuildCount++;
    }
}
