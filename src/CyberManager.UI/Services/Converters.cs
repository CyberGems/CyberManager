using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
