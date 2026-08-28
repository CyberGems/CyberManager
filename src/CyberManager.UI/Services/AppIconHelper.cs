using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using WpfImageSource = System.Windows.Media.ImageSource;

namespace CyberManager.UI.Services;

public static class AppIconHelper
{
    private static Icon? _trayIcon;
    private static WpfImageSource? _cached;

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateManagerIcon(int size = 32)
    {
        if (_trayIcon != null) return _trayIcon;
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/CyberWall.ico", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri != null) { using var s = sri.Stream; var ic = new Icon(s, size, size); _trayIcon = ic; return ic; }
        }
        catch { }
        try
        {
            var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "CyberWall.ico");
            if (File.Exists(p)) { var ic = new Icon(p, size, size); _trayIcon = ic; return ic; }
        }
        catch { }
        using var bmp = GenerateBitmap(size);
        var h = bmp.GetHicon(); var fb = (Icon)Icon.FromHandle(h).Clone(); DestroyIcon(h); _trayIcon = fb; return fb;
    }

    public static WpfImageSource CreateManagerImageSource(int size = 256)
    {
        if (_cached != null) return _cached;
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/CyberWall.ico", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri != null) { using var s = sri.Stream; var dec = new IconBitmapDecoder(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); var best = dec.Frames.OrderByDescending(f => f.Width).FirstOrDefault(); if (best != null) { _cached = best; return best; } }
        }
        catch { }
        try { var uri = new Uri("pack://application:,,,/Assets/CyberWall.png", UriKind.Absolute); var bi = BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad); _cached = bi; return bi; } catch { }
        using var bmp2 = GenerateBitmap(size); using var ms = new MemoryStream(); bmp2.Save(ms, ImageFormat.Png); ms.Position = 0;
        var fb2 = new BitmapImage(); fb2.BeginInit(); fb2.StreamSource = ms; fb2.CacheOption = BitmapCacheOption.OnLoad; fb2.EndInit(); fb2.Freeze(); _cached = fb2; return fb2;
    }

    private static Bitmap GenerateBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
        float r = size * 0.22f;
        using var path = RoundedRect(new RectangleF(0.5f, 0.5f, size - 1f, size - 1f), r);
        using var bg = new SolidBrush(Color.FromArgb(255, 11, 19, 34));
        using var pen = new Pen(Color.FromArgb(255, 30, 48, 80), 1.2f);
        g.FillPath(bg, path); g.DrawPath(pen, path);
        using var sp = new GraphicsPath();
        float cx = size / 2f, cy = size / 2f, sw = size * 0.52f, sh = size * 0.58f;
        var pTop = new PointF(cx, cy - sh * 0.48f); var pLeft = new PointF(cx - sw * 0.48f, cy - sh * 0.32f);
        var pLeftMid = new PointF(cx - sw * 0.48f, cy + sh * 0.05f); var pBottom = new PointF(cx, cy + sh * 0.48f);
        var pRightMid = new PointF(cx + sw * 0.48f, cy + sh * 0.05f); var pRight = new PointF(cx + sw * 0.48f, cy - sh * 0.32f);
        sp.AddLine(pTop, pLeft); sp.AddLine(pLeft, pLeftMid);
        sp.AddBezier(pLeftMid, new PointF(cx - sw * 0.45f, cy + sh * 0.32f), new PointF(cx - sw * 0.2f, cy + sh * 0.45f), pBottom);
        sp.AddBezier(pBottom, new PointF(cx + sw * 0.2f, cy + sh * 0.45f), new PointF(cx + sw * 0.45f, cy + sh * 0.32f), pRightMid);
        sp.AddLine(pRightMid, pRight); sp.CloseFigure();
        using var br = new LinearGradientBrush(new PointF(cx, cy - sh / 2f), new PointF(cx, cy + sh / 2f), Color.FromArgb(255, 0, 229, 255), Color.FromArgb(255, 0, 160, 220));
        g.FillPath(br, sp);
        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath(); float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
}
