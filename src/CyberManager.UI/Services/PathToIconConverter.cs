using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CyberManager.UI.Services;

public sealed class PathToIconConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;

        if (Cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            if (!File.Exists(path))
            {
                Cache[path] = null;
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null)
            {
                Cache[path] = null;
                return null;
            }

            var bs = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();

            Cache[path] = bs;
            return bs;
        }
        catch
        {
            Cache[path] = null;
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
