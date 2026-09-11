using System.Collections.Concurrent;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CyberManager.UI.Services;

public sealed class PathToIconConverter : IValueConverter
{
    private const int MaxCacheEntries = 512;
    private static readonly TimeSpan NegativeCacheLifetime = TimeSpan.FromSeconds(10);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> Pending = new(StringComparer.OrdinalIgnoreCase);

    public static event Action? IconReady;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim();
        var lastWrite = GetLastWriteTime(path);

        if (Cache.TryGetValue(path, out var cached) &&
            cached.LastWriteUtc == lastWrite &&
            (cached.Source != null || DateTime.UtcNow - cached.LastAccessUtc < NegativeCacheLifetime))
        {
            cached.LastAccessUtc = DateTime.UtcNow;
            return cached.Source;
        }

        _ = PrefetchAsync(path);
        return null;
    }

    public static async Task PrefetchAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !Pending.TryAdd(path, 0)) return;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = await Task.Run(() => LoadIcon(path), cancellationToken).ConfigureAwait(false);
            Cache[path] = new CacheEntry(entry, GetLastWriteTime(path), DateTime.UtcNow);
            EvictIfNeeded();
            IconReady?.Invoke();
        }
        catch
        {
        }
        finally
        {
            Pending.TryRemove(path, out _);
        }
    }

    private static BitmapSource? LoadIcon(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon != null)
            {
                return CreateBitmapSource(icon.Handle);
            }
        }
        catch
        {
        }

        try
        {
            var shinfo = new SHFILEINFO();
            var res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON);
            if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero) return null;

            try
            {
                return CreateBitmapSource(shinfo.hIcon);
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource CreateBitmapSource(IntPtr iconHandle)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            iconHandle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }

    private static DateTime GetLastWriteTime(string path)
    {
        try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue; }
        catch (IOException) { return DateTime.MinValue; }
    }

    private static void EvictIfNeeded()
    {
        if (Cache.Count <= MaxCacheEntries) return;
        foreach (var key in Cache
                     .OrderBy(pair => pair.Value.LastAccessUtc)
                     .Take(Math.Max(1, Cache.Count - MaxCacheEntries))
                     .Select(pair => pair.Key)
                     .ToList())
        {
            Cache.TryRemove(key, out _);
        }
    }

    private sealed class CacheEntry(ImageSource? source, DateTime lastWriteUtc, DateTime lastAccessUtc)
    {
        public ImageSource? Source { get; } = source;
        public DateTime LastWriteUtc { get; } = lastWriteUtc;
        public DateTime LastAccessUtc { get; set; } = lastAccessUtc;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
