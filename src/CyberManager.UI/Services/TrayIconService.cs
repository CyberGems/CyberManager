using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CyberManager.Common.I18n;

namespace CyberManager.UI.Services;

public sealed class TrayIconService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private IntPtr _hwnd;
    private HwndSource? _source;
    private NOTIFYICONDATA _nid;
    private bool _isAdded;
    private IntPtr _hIcon = IntPtr.Zero;

    private ContextMenu? _contextMenu;
    private MenuItem? _headerMenuItem;
    private MenuItem? _showMenuItem;
    private MenuItem? _startWithWindowsMenuItem;
    private MenuItem? _minimizeToTrayMenuItem;
    private MenuItem? _exitMenuItem;

    private Window? _ownerWindow;
    private Action? _onToggleShow;
    private Action<bool>? _onToggleStartWithWindows;
    private Action<bool>? _onToggleMinimizeToTray;
    private Action? _onExit;

    public void Initialize(
        Window ownerWindow,
        Action onToggleShow,
        Action<bool> onToggleStartWithWindows,
        Action<bool> onToggleMinimizeToTray,
        Action onExit)
    {
        _ownerWindow = ownerWindow;
        _onToggleShow = onToggleShow;
        _onToggleStartWithWindows = onToggleStartWithWindows;
        _onToggleMinimizeToTray = onToggleMinimizeToTray;
        _onExit = onExit;

        var helper = new WindowInteropHelper(ownerWindow);
        _hwnd = helper.Handle;
        if (_hwnd == IntPtr.Zero)
        {
            helper.EnsureHandle();
            _hwnd = helper.Handle;
        }

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);

        // Build WPF ContextMenu
        BuildContextMenu();

        // Extract Icon
        _hIcon = ExtractAppIcon();

        _nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "CyberManager — Ultra-Light Task Manager"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenu();
        if (_ownerWindow?.TryFindResource("ModernContextMenu") is Style cmStyle)
        {
            _contextMenu.Style = cmStyle;
        }

        Style? miStyle = _ownerWindow?.TryFindResource("ModernMenuItem") as Style;
        Style? sepStyle = _ownerWindow?.TryFindResource("ModernMenuSeparator") as Style;

        // Header Title Item (CyberViewer Card Style)
        _headerMenuItem = new MenuItem
        {
            Header = $"◈  CyberManager {UpdateService.GetCurrentVersionLabel()}",
            IsEnabled = false,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 0, 229, 255)),
            Opacity = 0.95
        };
        if (miStyle != null) _headerMenuItem.Style = miStyle;

        // Show CyberManager
        _showMenuItem = new MenuItem
        {
            Header = Strings.T("ShowCyberManager"),
            InputGestureText = "Ctrl+Alt+M",
            Icon = CreatePathIcon("M 2 12 L 12 2 L 22 12 M 4 10 V 20 H 20 V 10")
        };
        if (miStyle != null) _showMenuItem.Style = miStyle;
        _showMenuItem.Click += (_, _) => _onToggleShow?.Invoke();

        // Start with Windows
        _startWithWindowsMenuItem = new MenuItem
        {
            Header = Strings.T("StartWithWindows"),
            IsCheckable = true,
            IsChecked = App.Settings.StartWithWindows,
            Icon = CreatePathIcon("M 3 3 H 10 V 10 H 3 Z M 14 3 H 21 V 10 H 14 Z M 3 14 H 10 V 21 H 3 Z M 14 14 H 21 V 21 H 14 Z")
        };
        if (miStyle != null) _startWithWindowsMenuItem.Style = miStyle;
        _startWithWindowsMenuItem.Click += (_, _) =>
        {
            if (_startWithWindowsMenuItem != null)
            {
                _onToggleStartWithWindows?.Invoke(_startWithWindowsMenuItem.IsChecked);
            }
        };

        // Minimize to Tray
        _minimizeToTrayMenuItem = new MenuItem
        {
            Header = Strings.T("MinimizeToTray"),
            IsCheckable = true,
            IsChecked = App.Settings.MinimizeToTray,
            Icon = CreatePathIcon("M 4 14 L 12 22 L 20 14 M 12 2 V 20")
        };
        if (miStyle != null) _minimizeToTrayMenuItem.Style = miStyle;
        _minimizeToTrayMenuItem.Click += (_, _) =>
        {
            if (_minimizeToTrayMenuItem != null)
            {
                _onToggleMinimizeToTray?.Invoke(_minimizeToTrayMenuItem.IsChecked);
            }
        };

        // Exit Application
        _exitMenuItem = new MenuItem
        {
            Header = Strings.T("Exit"),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)),
            Icon = CreatePathIcon("M 18 6 L 6 18 M 6 6 L 18 18", System.Windows.Media.Color.FromRgb(248, 113, 113))
        };
        if (miStyle != null) _exitMenuItem.Style = miStyle;
        _exitMenuItem.Click += (_, _) => _onExit?.Invoke();

        _contextMenu.Items.Add(_headerMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_showMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_startWithWindowsMenuItem);
        _contextMenu.Items.Add(_minimizeToTrayMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_exitMenuItem);
    }

    private static System.Windows.Shapes.Path CreatePathIcon(string data, System.Windows.Media.Color? strokeColor = null)
    {
        return new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = new SolidColorBrush(strokeColor ?? System.Windows.Media.Color.FromArgb(200, 0, 229, 255)),
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
            Fill = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public void UpdateLocalization()
    {
        if (_headerMenuItem != null)
        {
            _headerMenuItem.Header = $"◈  CyberManager {UpdateService.GetCurrentVersionLabel()}";
        }
        if (_showMenuItem != null)
        {
            _showMenuItem.Header = Strings.T("ShowCyberManager");
        }
        if (_startWithWindowsMenuItem != null)
        {
            _startWithWindowsMenuItem.Header = Strings.T("StartWithWindows");
            _startWithWindowsMenuItem.IsChecked = App.Settings.StartWithWindows;
        }
        if (_minimizeToTrayMenuItem != null)
        {
            _minimizeToTrayMenuItem.Header = Strings.T("MinimizeToTray");
            _minimizeToTrayMenuItem.IsChecked = App.Settings.MinimizeToTray;
        }
        if (_exitMenuItem != null)
        {
            _exitMenuItem.Header = Strings.T("Exit");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            var eventCode = lParam.ToInt32();
            if (eventCode == WM_LBUTTONUP || eventCode == WM_LBUTTONDBLCLK)
            {
                handled = true;
                _onToggleShow?.Invoke();
            }
            else if (eventCode == WM_RBUTTONUP)
            {
                handled = true;
                ShowContextMenu();
            }
        }
        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        if (_contextMenu == null) return;
        UpdateLocalization();

        GetCursorPos(out var pt);

        // Convert physical screen pixels to WPF DIPs accurately across 100%, 125%, 150%, 200% scaling
        double dipX = pt.X;
        double dipY = pt.Y;

        var targetVisual = _ownerWindow ?? System.Windows.Application.Current.MainWindow;
        if (targetVisual != null)
        {
            var source = PresentationSource.FromVisual(targetVisual);
            if (source?.CompositionTarget != null)
            {
                var matrix = source.CompositionTarget.TransformFromDevice;
                var dipPoint = matrix.Transform(new Point(pt.X, pt.Y));
                dipX = dipPoint.X;
                dipY = dipPoint.Y;
            }
        }

        SetForegroundWindow(_hwnd);
        _contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
        _contextMenu.HorizontalOffset = dipX;
        _contextMenu.VerticalOffset = dipY;
        _contextMenu.IsOpen = true;
    }

    private static IntPtr ExtractAppIcon()
    {
        try
        {
            var icon = AppIconHelper.CreateManagerIcon(32);
            if (icon != null && icon.Handle != IntPtr.Zero)
            {
                return icon.Handle;
            }
        }
        catch { }

        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var hIcon = ExtractAssociatedIconHandle(exePath);
                if (hIcon != IntPtr.Zero) return hIcon;
            }
        }
        catch { }

        return LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private static IntPtr ExtractAssociatedIconHandle(string path)
    {
        try
        {
            var h = ExtractIcon(IntPtr.Zero, path, 0);
            if (h != IntPtr.Zero && h != (IntPtr)1) return h;
        }
        catch { }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_isAdded)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            _isAdded = false;
        }

        if (_source != null)
        {
            try { _source.RemoveHook(HwndHook); } catch { }
            _source = null;
        }

        if (_hIcon != IntPtr.Zero)
        {
            try { DestroyIcon(_hIcon); } catch { }
            _hIcon = IntPtr.Zero;
        }
    }
}
