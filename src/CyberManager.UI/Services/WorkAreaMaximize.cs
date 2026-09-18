using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CyberManager.UI.Services;

/// <summary>
/// Fills the monitor work area for borderless windows instead of using
/// WindowState.Maximized, which can cover the taskbar.
/// </summary>
public static class WorkAreaMaximize
{
    private static readonly DependencyProperty IsFilledProperty =
        DependencyProperty.RegisterAttached("IsFilled", typeof(bool), typeof(WorkAreaMaximize));

    private static readonly DependencyProperty RestoreProperty =
        DependencyProperty.RegisterAttached("Restore", typeof(Rect), typeof(WorkAreaMaximize));

    private static readonly DependencyProperty ApplyingProperty =
        DependencyProperty.RegisterAttached("Applying", typeof(bool), typeof(WorkAreaMaximize));

    public static bool IsFilled(Window window) =>
        window != null && (bool)window.GetValue(IsFilledProperty);

    public static void Attach(Window window)
    {
        window.LocationChanged += (_, _) => RememberNormalBounds(window);
        window.SizeChanged += (_, _) => RememberNormalBounds(window);
        window.StateChanged += (_, _) =>
        {
            if ((bool)window.GetValue(ApplyingProperty) || window.WindowState != WindowState.Maximized)
            {
                return;
            }

            window.SetValue(ApplyingProperty, true);
            try
            {
                window.WindowState = WindowState.Normal;
                Fill(window);
            }
            finally
            {
                window.SetValue(ApplyingProperty, false);
            }
        };
    }

    public static void Toggle(Window window)
    {
        if (IsFilled(window) || window.WindowState == WindowState.Maximized)
        {
            Restore(window);
        }
        else
        {
            Fill(window);
        }
    }

    public static void Fill(Window window)
    {
        RememberNormalBounds(window);
        var nested = (bool)window.GetValue(ApplyingProperty);
        window.SetValue(ApplyingProperty, true);
        try
        {
            var area = GetWorkAreaDip(window);
            window.WindowState = WindowState.Normal;
            window.Left = area.Left;
            window.Top = area.Top;
            window.Width = Math.Max(window.MinWidth, area.Width);
            window.Height = Math.Max(window.MinHeight, area.Height);
            window.SetValue(IsFilledProperty, true);
        }
        finally
        {
            if (!nested)
            {
                window.SetValue(ApplyingProperty, false);
            }
        }
    }

    public static void Restore(Window window)
    {
        var restore = (Rect)window.GetValue(RestoreProperty);
        window.SetValue(ApplyingProperty, true);
        try
        {
            window.SetValue(IsFilledProperty, false);
            window.WindowState = WindowState.Normal;
            if (restore.Width <= 0 || restore.Height <= 0) return;

            window.Left = restore.Left;
            window.Top = restore.Top;
            window.Width = restore.Width;
            window.Height = restore.Height;
        }
        finally
        {
            window.SetValue(ApplyingProperty, false);
        }
    }

    private static void RememberNormalBounds(Window window)
    {
        if (IsFilled(window) ||
            window.WindowState != WindowState.Normal ||
            (bool)window.GetValue(ApplyingProperty) ||
            window.Width <= 0 ||
            window.Height <= 0)
        {
            return;
        }

        window.SetValue(
            RestoreProperty,
            new Rect(window.Left, window.Top, window.Width, window.Height));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectPixels
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectPixels Monitor;
        public RectPixels Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private static Rect GetWorkAreaDip(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var monitor = MonitorFromWindow(hwnd, 2);
            if (monitor != IntPtr.Zero)
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    var source = PresentationSource.FromVisual(window);
                    if (source?.CompositionTarget != null)
                    {
                        var transform = source.CompositionTarget.TransformFromDevice;
                        var topLeft = transform.Transform(new Point(info.Work.Left, info.Work.Top));
                        var bottomRight = transform.Transform(new Point(info.Work.Right, info.Work.Bottom));
                        return new Rect(
                            topLeft.X,
                            topLeft.Y,
                            Math.Max(100, bottomRight.X - topLeft.X),
                            Math.Max(100, bottomRight.Y - topLeft.Y));
                    }

                    var dpi = VisualTreeHelper.GetDpi(window);
                    return new Rect(
                        info.Work.Left / dpi.DpiScaleX,
                        info.Work.Top / dpi.DpiScaleY,
                        Math.Max(100, (info.Work.Right - info.Work.Left) / dpi.DpiScaleX),
                        Math.Max(100, (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY));
                }
            }
        }

        return SystemParameters.WorkArea;
    }
}
