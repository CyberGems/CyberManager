using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CyberManager.UI.Services;

public sealed class ChildIndentMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? new Thickness(18, 0, 0, 0) : new Thickness(0, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class ExpandedChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "▼" : "▶";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class ParentFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class WorkerBadgeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is "Worker" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Builds a rounded <see cref="RectangleGeometry"/> from an element's actual size so it can be used
/// as a <c>UIElement.Clip</c>. WPF's <c>ClipToBounds</c> only clips to the rectangular bounds and
/// ignores <c>CornerRadius</c>, so a rounded container still shows square corners painted by its
/// children (the grid background, the rows, the header band). Binding the grid's Clip to this
/// geometry makes its content follow the container's rounded shape exactly. Values are
/// [ActualWidth, ActualHeight]; the radius comes from <c>ConverterParameter</c>.
/// </summary>
public sealed class RoundedClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double w || values[1] is not double h || w <= 0 || h <= 0)
            return Binding.DoNothing;

        var radius = 0.0;
        if (parameter is string s)
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out radius);

        radius = Math.Max(0, Math.Min(radius, Math.Min(w, h) / 2.0));
        return new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Five-step heat ramp shared by the CPU and memory columns: transparent for the lowest bucket,
/// three increasing strengths of the theme accent, then red for the top one.
/// <para>
/// The accent is read from the theme resources instead of being hard-coded, so the ramp follows
/// the palette (and the RAM sparkline) in every theme. It also stays cool until the top bucket on
/// purpose: painting a translucent warm hue over the dark card is what used to read as muddy
/// brown, because amber at low opacity over a navy surface composites to sepia.
/// </para>
/// </summary>
internal static class HeatRamp
{
    private static readonly Color TopColor = Color.FromRgb(0xEF, 0x44, 0x44);
    private static readonly double[] AccentAlphas = { 0.16, 0.32, 0.50 };
    private static readonly Color FallbackAccent = Color.FromRgb(0x24, 0xA6, 0xB5);
    private const double TopAlpha = 0.60;

    private sealed record Entry(Color Accent, SolidColorBrush[] Brushes);

    private static Entry? _entry;

    /// <summary>Ramp for the accent of the active theme, rebuilt whenever the theme changes.</summary>
    public static SolidColorBrush[] Current
    {
        get
        {
            var accent = ResolveAccent();
            var entry = _entry;
            if (entry is not null && entry.Accent == accent) return entry.Brushes;

            // A single reference assignment keeps the accent and its ramp consistent for readers,
            // which avoids taking a lock on a path that runs once per rendered cell.
            var built = new Entry(accent, Build(accent));
            _entry = built;
            return built.Brushes;
        }
    }

    private static Color ResolveAccent() =>
        Application.Current?.TryFindResource("RamColor") is Color accent ? accent : FallbackAccent;

    private static SolidColorBrush[] Build(Color accent) => new[]
    {
        System.Windows.Media.Brushes.Transparent,
        Blend(accent, AccentAlphas[0]),
        Blend(accent, AccentAlphas[1]),
        Blend(accent, AccentAlphas[2]),
        Blend(TopColor, TopAlpha)
    };

    private static SolidColorBrush Blend(Color color, double alpha)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(alpha * 255), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}

internal static class HeatmapThresholds
{
    public static int CpuBucket(double cpuPercent)
    {
        if (cpuPercent <= 1.0) return 0;
        if (cpuPercent <= 5.0) return 1;
        if (cpuPercent <= 15.0) return 2;
        if (cpuPercent <= 30.0) return 3;
        return 4;
    }

    public static int MemoryBucket(long workingSetBytes)
    {
        double mb = workingSetBytes / (1024.0 * 1024.0);
        if (mb < 300) return 0;
        if (mb < 700) return 1;
        if (mb < 1500) return 2;
        if (mb < 3000) return 3;
        return 4;
    }
}

/// <summary>
/// Heat tint for a system-wide percentage (0-100), shared by the CPU and RAM badges in both
/// windows: the title bar pills, the compact window and the metrics cards all warm up the same way.
/// <para>
/// The opacity scales smoothly with the value while the hue switches to red once usage turns
/// critical. The two are kept apart on purpose: blending green or cyan into red passes through
/// olive and grey, and a translucent warm hue over the dark card is exactly what used to read as
/// muddy brown. Each metric keeps its own base colour (CPU green, RAM from the palette), so the
/// badge still looks like itself at rest and only changes as it fills.
/// </para>
/// </summary>
public static class UsageHeat
{
    private static readonly Color HotColor = Color.FromRgb(0xEF, 0x44, 0x44);

    /// <summary>Usage at which the hue flips to red. Opacity stays continuous across the flip.</summary>
    private const double HotFrom = 0.80;

    private const double MinAlpha = 0.06;
    private const double MaxAlpha = 0.58;
    private const double Curve = 1.8;

    public static double Alpha(double percent)
    {
        // Math.Clamp passes NaN through, and a NaN would reach Math.Pow and poison the brush.
        if (double.IsNaN(percent)) return MinAlpha;

        var t = Math.Clamp(percent / 100.0, 0, 1);
        return MinAlpha + (MaxAlpha - MinAlpha) * Math.Pow(t, Curve);
    }

    /// <summary>Solid colour for text and glyphs: the metric's colour, red once usage is critical.</summary>
    public static Color ColorFor(double percent, Color baseColor) =>
        Math.Clamp(percent / 100.0, 0, 1) >= HotFrom ? HotColor : baseColor;

    /// <summary>Frozen solid brush of <see cref="ColorFor"/>, for values and glyphs.</summary>
    public static SolidColorBrush Solid(double percent, Color baseColor)
    {
        var brush = new SolidColorBrush(ColorFor(percent, baseColor));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Translucent overlay meant to be painted on top of the badge's own theme background, so a
    /// theme switch keeps working without rebuilding the brush.
    /// </summary>
    public static SolidColorBrush Tint(double percent, Color baseColor)
    {
        var c = ColorFor(percent, baseColor);
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(Alpha(percent) * 255), c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>Reads a <see cref="Color"/> from the theme resources, falling back when it is missing.</summary>
    public static Color FromResource(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) is Color c ? c : fallback;
}

public sealed class CpuHeatmapBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var ramp = HeatRamp.Current;
        return value is double cpu ? ramp[HeatmapThresholds.CpuBucket(cpu)] : ramp[0];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class MemoryHeatmapBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var ramp = HeatRamp.Current;
        return value is long bytes ? ramp[HeatmapThresholds.MemoryBucket(bytes)] : ramp[0];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class HighCpuWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is double cpu && cpu > 5.0) ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class HighMemoryWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is long bytes && bytes > 700L * 1024 * 1024) ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class SystemProcessIconConverter : IValueConverter
{
    private static readonly Geometry Generic = CreateGeometry(
        "M 7 4 H 17 V 20 H 7 Z M 9 1 V 4 M 15 1 V 4 M 9 20 V 23 M 15 20 V 23 M 1 9 H 7 M 17 9 H 23 M 1 15 H 7 M 17 15 H 23");
    private static readonly Geometry Shield = CreateGeometry(
        "M 12 2 L 20 5 V 11 C 20 16 16.8 20 12 22 C 7.2 20 4 16 4 11 V 5 Z");
    private static readonly Geometry Lightning = CreateGeometry(
        "M 13 2 L 5 13 H 11 L 10 22 L 19 10 H 13 Z");
    private static readonly Geometry Database = CreateGeometry(
        "M 4 5 C 4 3.3 7.6 2 12 2 C 16.4 2 20 3.3 20 5 V 19 C 20 20.7 16.4 22 12 22 C 7.6 22 4 20.7 4 19 Z M 4 5 C 4 6.7 7.6 8 12 8 C 16.4 8 20 6.7 20 5 M 4 12 C 4 13.7 7.6 15 12 15 C 16.4 15 20 13.7 20 12");
    private static readonly Geometry Moon = CreateGeometry(
        "M 16 3 A 8 8 0 1 0 18 17 A 7 7 0 1 1 16 3 Z");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string name)
        {
            if (name.Contains("Memory Compression", StringComparison.OrdinalIgnoreCase)) return Lightning;
            if (name.Equals("Registry", StringComparison.OrdinalIgnoreCase)) return Database;
            if (name.Equals("Secure System", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("MsMpEng", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("SecurityHealth", StringComparison.OrdinalIgnoreCase))
            {
                return Shield;
            }

            if (name.Contains("Idle", StringComparison.OrdinalIgnoreCase)) return Moon;
        }

        return Generic;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private static Geometry CreateGeometry(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}

public sealed class ProcessDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string name)
        {
            if (name.Contains("Memory Compression", StringComparison.OrdinalIgnoreCase))
                return CyberManager.Common.I18n.Strings.T("MemoryCompressionDesc");
            if (name.Equals("Registry", StringComparison.OrdinalIgnoreCase))
                return CyberManager.Common.I18n.Strings.T("RegistryDesc");
            if (name.Equals("Secure System", StringComparison.OrdinalIgnoreCase))
                return CyberManager.Common.I18n.Strings.T("SecureSystemDesc");
            if (name.Equals("System", StringComparison.OrdinalIgnoreCase))
                return CyberManager.Common.I18n.Strings.T("SystemDesc");
            if (name.Contains("Idle", StringComparison.OrdinalIgnoreCase))
                return CyberManager.Common.I18n.Strings.T("IdleDesc");
        }
        return CyberManager.Common.I18n.Strings.T("ProtectedSystemProcess");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
