using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;

namespace CyberManager.UI.Services;

public static class CyberManagerWindowChrome
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_DONOTROUND = 1;

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int x1,int y1,int x2,int y2,int cx,int cy);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool r);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

    public static void Apply(Window w, double radius = 12)
    {
        bool canResize = w.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        WindowChrome.SetWindowChrome(w, new WindowChrome { CaptionHeight = 0, CornerRadius = new CornerRadius(radius), GlassFrameThickness = new Thickness(0), ResizeBorderThickness = canResize ? new Thickness(8) : new Thickness(0), UseAeroCaptionButtons = false });
        ApplyRounded(w, radius);
    }

    private static void ApplyRounded(Window w, double radius)
    {
        void Do()
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd == IntPtr.Zero) return;
            if (OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))
            {
                int pref = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
                return;
            }
            if (w.ActualWidth <= 0 || w.ActualHeight <= 0) return;
            var src = PresentationSource.FromVisual(w);
            var m = src?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
            int width = (int)Math.Ceiling(w.ActualWidth * m.M11);
            int height = (int)Math.Ceiling(w.ActualHeight * m.M22);
            int d = (int)Math.Round(radius * 2 * m.M11);
            var rgn = CreateRoundRectRgn(0,0,width+1,height+1,d,d);
            if (rgn != IntPtr.Zero && SetWindowRgn(hwnd, rgn, true) == 0) DeleteObject(rgn);
        }
        w.SourceInitialized += (_, _) => Do();
        w.SizeChanged += (_, _) => Do();
    }
}
