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

public sealed class CpuHeatmapBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush[] Brushes = new[]
    {
        System.Windows.Media.Brushes.Transparent,
        CreateFrozenBrush(0x20, 0xF5, 0x9E, 0x0B),
        CreateFrozenBrush(0x3E, 0xF5, 0x9E, 0x0B),
        CreateFrozenBrush(0x60, 0xF5, 0x9E, 0x0B),
        CreateFrozenBrush(0x8A, 0xEF, 0x44, 0x44)
    };

    private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double cpu)
        {
            if (cpu <= 1.0) return Brushes[0];
            if (cpu <= 5.0) return Brushes[1];
            if (cpu <= 15.0) return Brushes[2];
            if (cpu <= 30.0) return Brushes[3];
            return Brushes[4];
        }
        return Brushes[0];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class MemoryHeatmapBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush[] Brushes = new[]
    {
        System.Windows.Media.Brushes.Transparent,
        CreateFrozenBrush(0x1A, 0xF5, 0x9E, 0x0B),
        CreateFrozenBrush(0x36, 0xF5, 0x9E, 0x0B),
        CreateFrozenBrush(0x56, 0xF5, 0x9E, 0x0B),
        CreateFrozenBrush(0x82, 0xEF, 0x44, 0x44)
    };

    private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            double mb = bytes / (1024.0 * 1024.0);
            if (mb < 300) return Brushes[0];
            if (mb < 700) return Brushes[1];
            if (mb < 1500) return Brushes[2];
            if (mb < 3000) return Brushes[3];
            return Brushes[4];
        }
        return Brushes[0];
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
