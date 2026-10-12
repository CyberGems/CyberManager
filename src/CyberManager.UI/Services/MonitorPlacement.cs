using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CyberManager.UI.Services;

/// <summary>
/// Monitor geometry helpers shared by the main window and the floating widget. WPF works in
/// device-independent units while the Win32 monitor APIs work in physical pixels, so every
/// conversion goes through the composition target of the window being placed. This keeps a
/// saved position usable when monitors are unplugged, rearranged or run at a different DPI.
/// </summary>
public static class MonitorPlacement
{
    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref NativeRect rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    /// <summary>The work area of the primary monitor, in device-independent units.</summary>
    public static Rect PrimaryWorkArea => SystemParameters.WorkArea;

    /// <summary>
    /// True when the given device-independent point falls inside a monitor. Used to detect a
    /// saved position that no longer points at any display.
    /// </summary>
    public static bool IsPointOnAnyMonitor(Visual? reference, double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top)) return false;

        var devicePoint = ToDevice(reference, left, top);

        var result = false;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeRect rect, IntPtr data) =>
            {
                if (devicePoint.X >= rect.Left && devicePoint.X < rect.Right &&
                    devicePoint.Y >= rect.Top && devicePoint.Y < rect.Bottom)
                {
                    result = true;
                    return false;
                }

                return true;
            },
            IntPtr.Zero);
        return result;
    }

    /// <summary>Work area of the monitor the window currently sits on, in device-independent units.</summary>
    public static Rect GetNearestWorkArea(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var area = TryGetWorkArea(window, MonitorFromWindow(hwnd, MonitorDefaultToNearest));
            if (area.HasValue) return area.Value;
        }

        return PrimaryWorkArea;
    }

    /// <summary>
    /// Work area of the monitor nearest the given point, in device-independent units. When the
    /// point lies outside every display the closest one wins, which is what brings a stale saved
    /// position back on screen.
    /// </summary>
    public static Rect GetWorkAreaForPoint(Visual? reference, double left, double top)
    {
        var devicePoint = ToDevice(reference, left, top);
        var monitor = MonitorFromPoint(
            new NativePoint
            {
                X = (int)Math.Round(devicePoint.X),
                Y = (int)Math.Round(devicePoint.Y)
            },
            MonitorDefaultToNearest);

        return TryGetWorkArea(reference, monitor) ?? PrimaryWorkArea;
    }

    /// <summary>
    /// Clamps a window position so the whole window fits inside the work area of the nearest
    /// monitor, leaving the given margin around it. Returns the position that should be applied.
    /// </summary>
    public static (double Left, double Top) ClampToNearestMonitor(
        Window window,
        double left,
        double top,
        double margin = 0)
    {
        var area = GetWorkAreaForPoint(window, left, top);
        if (area.Width <= 0 || area.Height <= 0) return (left, top);

        var width = ResolveDimension(window.ActualWidth, window.Width);
        var height = ResolveDimension(window.ActualHeight, window.Height);

        var minLeft = area.Left + margin;
        var minTop = area.Top + margin;
        var maxLeft = Math.Max(minLeft, area.Right - width - margin);
        var maxTop = Math.Max(minTop, area.Bottom - height - margin);

        return (
            Math.Clamp(double.IsFinite(left) ? left : minLeft, minLeft, maxLeft),
            Math.Clamp(double.IsFinite(top) ? top : minTop, minTop, maxTop));
    }

    /// <summary>
    /// Prefers the measured size and falls back to the requested one. A window using
    /// <c>SizeToContent</c> reports NaN as its requested height before it is measured.
    /// </summary>
    private static double ResolveDimension(double actual, double requested)
    {
        if (double.IsFinite(actual) && actual > 0) return actual;
        if (double.IsFinite(requested) && requested > 0) return requested;
        return 0;
    }

    private static Rect? TryGetWorkArea(Visual? reference, IntPtr monitor)
    {
        if (monitor == IntPtr.Zero) return null;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return null;

        var topLeft = FromDevice(reference, new Point(monitorInfo.Work.Left, monitorInfo.Work.Top));
        var bottomRight = FromDevice(reference, new Point(monitorInfo.Work.Right, monitorInfo.Work.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private static Point ToDevice(Visual? reference, double x, double y)
    {
        var transform = reference is not null
            ? PresentationSource.FromVisual(reference)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity
            : Matrix.Identity;
        return transform.Transform(new Point(x, y));
    }

    private static Point FromDevice(Visual? reference, Point point)
    {
        var transform = reference is not null
            ? PresentationSource.FromVisual(reference)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity
            : Matrix.Identity;
        return transform.Transform(point);
    }
}
