using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;

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
    private MenuItem? _sysInfoMenuItem;
    private MenuItem? _refreshMenuItem;
    private MenuItem? _alwaysOnTopMenuItem;
    private MenuItem? _groupByAppMenuItem;
    private MenuItem? _startWithWindowsMenuItem;
    private MenuItem? _minimizeToTrayMenuItem;
    private MenuItem? _helpMenuItem;
    private MenuItem? _subHelpItem;
    private MenuItem? _subFaqItem;
    private MenuItem? _subChangelogItem;
    private MenuItem? _subWebsiteItem;
    private MenuItem? _subDonateItem;
    private MenuItem? _subAboutItem;
    private MenuItem? _subCheckUpdateItem;
    private MenuItem? _exitMenuItem;

    private Window? _ownerWindow;
    private Action? _onToggleShow;
    private Action? _onOpenSystemInfo;
    private Action? _onRefresh;
    private Action<bool>? _onToggleAlwaysOnTop;
    private Action<bool>? _onToggleGroupByApp;
    private Action<bool>? _onToggleStartWithWindows;
    private Action<bool>? _onToggleMinimizeToTray;
    private Action<bool>? _onOpenAbout;
    private Action? _onExit;

    public void Initialize(
        Window ownerWindow,
        Action onToggleShow,
        Action onOpenSystemInfo,
        Action onRefresh,
        Action<bool> onToggleAlwaysOnTop,
        Action<bool> onToggleGroupByApp,
        Action<bool> onToggleStartWithWindows,
        Action<bool> onToggleMinimizeToTray,
        Action<bool> onOpenAbout,
        Action onExit)
    {
        _ownerWindow = ownerWindow;
        _onToggleShow = onToggleShow;
        _onOpenSystemInfo = onOpenSystemInfo;
        _onRefresh = onRefresh;
        _onToggleAlwaysOnTop = onToggleAlwaysOnTop;
        _onToggleGroupByApp = onToggleGroupByApp;
        _onToggleStartWithWindows = onToggleStartWithWindows;
        _onToggleMinimizeToTray = onToggleMinimizeToTray;
        _onOpenAbout = onOpenAbout;
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
            szTip = $"CyberManager {UpdateService.GetCurrentVersionLabel()}"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenu();
        if (Application.Current.TryFindResource("ModernContextMenu") is Style cmStyle)
        {
            _contextMenu.Style = cmStyle;
        }

        Style? miStyle = Application.Current.TryFindResource("ModernMenuItem") as Style;
        Style? sepStyle = Application.Current.TryFindResource("ModernMenuSeparator") as Style;

        // 1. Branding Header Item: [Logo] CyberManager v1.0.0
        var logoImg = new Image
        {
            Source = AppIconHelper.CreateManagerImageSource(16),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        RenderOptions.SetBitmapScalingMode(logoImg, BitmapScalingMode.HighQuality);

        _headerMenuItem = new MenuItem
        {
            Header = $"CyberManager {UpdateService.GetCurrentVersionLabel()}",
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255)),
            Icon = logoImg,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (miStyle != null) _headerMenuItem.Style = miStyle;
        _headerMenuItem.Click += (_, _) => _onOpenAbout?.Invoke(false);

        // 2. Show / Hide CyberManager
        _showMenuItem = new MenuItem
        {
            Header = Strings.T("ShowCyberManager"),
            InputGestureText = App.Settings.GlobalHotkey,
            Icon = CreatePathIcon("M 2 4 H 22 V 18 H 2 Z M 2 8 H 22")
        };
        if (miStyle != null) _showMenuItem.Style = miStyle;
        _showMenuItem.Click += (_, _) => _onToggleShow?.Invoke();

        // 3. System Information...
        _sysInfoMenuItem = new MenuItem
        {
            Header = $"{Strings.T("SystemInformation")}...",
            InputGestureText = "Ctrl+I",
            Icon = CreatePathIcon("M 2 12 L 6 12 L 9 5 L 14 19 L 17 12 L 22 12")
        };
        if (miStyle != null) _sysInfoMenuItem.Style = miStyle;
        _sysInfoMenuItem.Click += (_, _) => _onOpenSystemInfo?.Invoke();

        // 4. Refresh Processes
        _refreshMenuItem = new MenuItem
        {
            Header = Strings.T("RefreshProcesses"),
            InputGestureText = "F5",
            Icon = CreatePathIcon("M 4 4 V 9 H 9 M 20 20 V 15 H 15 M 20 9 A 9 9 0 0 0 5.64 5.64 L 4 9 M 4 15 A 9 9 0 0 0 18.36 18.36 L 20 15")
        };
        if (miStyle != null) _refreshMenuItem.Style = miStyle;
        _refreshMenuItem.Click += (_, _) => _onRefresh?.Invoke();

        // 5. Always on Top
        _alwaysOnTopMenuItem = new MenuItem
        {
            Header = Strings.T("AlwaysOnTop"),
            IsCheckable = true,
            IsChecked = App.Settings.AlwaysOnTop,
            Icon = CreatePathIcon("M 12 2 V 6 M 12 18 V 22 M 5 12 H 19 M 5 5 L 19 19")
        };
        if (miStyle != null) _alwaysOnTopMenuItem.Style = miStyle;
        _alwaysOnTopMenuItem.Click += (_, _) =>
        {
            if (_alwaysOnTopMenuItem != null)
            {
                _onToggleAlwaysOnTop?.Invoke(_alwaysOnTopMenuItem.IsChecked);
            }
        };

        // 6. Group by Application
        _groupByAppMenuItem = new MenuItem
        {
            Header = Strings.T("GroupByApp"),
            IsCheckable = true,
            IsChecked = App.Settings.GroupProcesses,
            Icon = CreatePathIcon("M 3 6 H 21 M 3 12 H 21 M 3 18 H 21")
        };
        if (miStyle != null) _groupByAppMenuItem.Style = miStyle;
        _groupByAppMenuItem.Click += (_, _) =>
        {
            if (_groupByAppMenuItem != null)
            {
                _onToggleGroupByApp?.Invoke(_groupByAppMenuItem.IsChecked);
            }
        };

        // 7. Start with Windows
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

        // 8. Minimize to Tray
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

        // 9. Help Submenu (Matching CyberFeeds standard)
        _helpMenuItem = new MenuItem
        {
            Header = Strings.T("Help"),
            Icon = CreatePathIcon("M 12 22 A 10 10 0 1 0 12 2 A 10 10 0 0 0 12 22 M 9 9 A 3 3 0 0 1 12 6 A 3 3 0 0 1 15 9 C 15 11 12 11.5 12 14 M 12 18 H 12.01")
        };
        if (miStyle != null) _helpMenuItem.Style = miStyle;

        // Submenu: Help
        _subHelpItem = new MenuItem
        {
            Header = Strings.T("Help"),
            Icon = CreatePathIcon("M 12 22 A 10 10 0 1 0 12 2 A 10 10 0 0 0 12 22 M 9 9 A 3 3 0 0 1 12 6 A 3 3 0 0 1 15 9 C 15 11 12 11.5 12 14 M 12 18 H 12.01")
        };
        if (miStyle != null) _subHelpItem.Style = miStyle;
        _subHelpItem.Click += (_, _) => OpenUrl("https://github.com/CyberGems/CyberManager/wiki");

        // Submenu: FAQ
        _subFaqItem = new MenuItem
        {
            Header = Strings.T("Faq"),
            Icon = CreatePathIcon("M 21 15 A 2 2 0 0 1 19 17 H 7 L 3 21 V 5 A 2 2 0 0 1 5 3 H 19 A 2 2 0 0 1 21 5 Z")
        };
        if (miStyle != null) _subFaqItem.Style = miStyle;
        _subFaqItem.Click += (_, _) => OpenUrl("https://github.com/CyberGems/CyberManager/wiki/FAQ");

        // Submenu: Changelog
        _subChangelogItem = new MenuItem
        {
            Header = Strings.T("Changelog"),
            Icon = CreatePathIcon("M 9 6 H 20 M 9 12 H 20 M 9 18 H 20 M 4 6 H 5 M 4 12 H 5 M 4 18 H 5")
        };
        if (miStyle != null) _subChangelogItem.Style = miStyle;
        _subChangelogItem.Click += (_, _) => OpenUrl("https://github.com/CyberGems/CyberManager/releases");

        // Submenu: Website
        _subWebsiteItem = new MenuItem
        {
            Header = Strings.T("Website"),
            Icon = CreatePathIcon("M 12 2 A 10 10 0 1 0 12 22 A 10 10 0 0 0 12 2 M 2 12 H 22 M 12 2 A 15 15 0 0 0 12 22 A 15 15 0 0 0 12 2")
        };
        if (miStyle != null) _subWebsiteItem.Style = miStyle;
        _subWebsiteItem.Click += (_, _) => OpenUrl("https://cybergems.org");

        // Submenu: Donate
        _subDonateItem = new MenuItem
        {
            Header = Strings.T("Donate"),
            Icon = CreatePathIcon("M 12 21.35 L 10.55 20.03 C 5.4 15.36 2 12.28 2 8.5 C 2 5.42 4.42 3 7.5 3 C 9.24 3 10.91 3.81 12 5.09 C 13.09 3.81 14.76 3 16.5 3 C 19.58 3 22 5.42 22 8.5 C 22 12.28 18.6 15.36 13.45 20.04 L 12 21.35 Z", Color.FromRgb(244, 63, 94))
        };
        if (miStyle != null) _subDonateItem.Style = miStyle;
        _subDonateItem.Click += (_, _) => OpenUrl("https://github.com/CyberGems/CyberManager#%EF%B8%8F-donate");

        // Submenu: About
        _subAboutItem = new MenuItem
        {
            Header = $"{Strings.T("About")}...",
            Icon = CreatePathIcon("M 12 22 A 10 10 0 1 0 12 2 A 10 10 0 0 0 12 22 M 12 16 V 12 M 12 8 H 12.01")
        };
        if (miStyle != null) _subAboutItem.Style = miStyle;
        _subAboutItem.Click += (_, _) => _onOpenAbout?.Invoke(false);

        // Submenu: Check for Updates
        _subCheckUpdateItem = new MenuItem
        {
            Header = Strings.T("CheckForUpdates"),
            Icon = CreatePathIcon("M 21.5 2 V 7 H 16.5 M 2.5 22 V 17 H 7.5 M 20.49 15 A 9 9 0 0 1 5.64 5.64 L 2.5 9 M 3.51 9 A 9 9 0 0 1 18.36 18.36 L 21.5 15")
        };
        if (miStyle != null) _subCheckUpdateItem.Style = miStyle;
        _subCheckUpdateItem.Click += (_, _) => _onOpenAbout?.Invoke(true);

        _helpMenuItem.Items.Add(_subHelpItem);
        _helpMenuItem.Items.Add(_subFaqItem);
        _helpMenuItem.Items.Add(_subChangelogItem);
        _helpMenuItem.Items.Add(_subWebsiteItem);
        _helpMenuItem.Items.Add(_subDonateItem);
        _helpMenuItem.Items.Add(new Separator { Style = sepStyle });
        _helpMenuItem.Items.Add(_subAboutItem);
        _helpMenuItem.Items.Add(_subCheckUpdateItem);

        // 10. Exit Application
        _exitMenuItem = new MenuItem
        {
            Header = Strings.T("Exit"),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
            Icon = CreatePathIcon("M 18.36 6.64 A 9 9 0 1 1 5.63 6.64 M 12 2 V 12", Color.FromRgb(248, 113, 113))
        };
        if (miStyle != null) _exitMenuItem.Style = miStyle;
        _exitMenuItem.Click += (_, _) => _onExit?.Invoke();

        // Assemble Menu in CyberFeeds Standard Order
        _contextMenu.Items.Add(_headerMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_showMenuItem);
        _contextMenu.Items.Add(_sysInfoMenuItem);
        _contextMenu.Items.Add(_refreshMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_alwaysOnTopMenuItem);
        _contextMenu.Items.Add(_groupByAppMenuItem);
        _contextMenu.Items.Add(_startWithWindowsMenuItem);
        _contextMenu.Items.Add(_minimizeToTrayMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_helpMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_exitMenuItem);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private static System.Windows.Shapes.Path CreatePathIcon(string data, Color? strokeColor = null)
    {
        return new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = new SolidColorBrush(strokeColor ?? Color.FromArgb(200, 0, 229, 255)),
            StrokeThickness = 1.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 14,
            Height = 14,
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
            _headerMenuItem.Header = $"CyberManager {UpdateService.GetCurrentVersionLabel()}";
        }
        if (_showMenuItem != null)
        {
            bool isVis = _ownerWindow?.IsVisible == true;
            _showMenuItem.Header = isVis ? Strings.T("HideCyberManager") : Strings.T("ShowCyberManager");
            _showMenuItem.InputGestureText = App.Settings.GlobalHotkey;
        }
        if (_sysInfoMenuItem != null)
        {
            _sysInfoMenuItem.Header = $"{Strings.T("SystemInformation")}...";
        }
        if (_refreshMenuItem != null)
        {
            _refreshMenuItem.Header = Strings.T("RefreshProcesses");
        }
        if (_alwaysOnTopMenuItem != null)
        {
            _alwaysOnTopMenuItem.Header = Strings.T("AlwaysOnTop");
            _alwaysOnTopMenuItem.IsChecked = App.Settings.AlwaysOnTop;
        }
        if (_groupByAppMenuItem != null)
        {
            _groupByAppMenuItem.Header = Strings.T("GroupByApp");
            _groupByAppMenuItem.IsChecked = App.Settings.GroupProcesses;
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
        if (_helpMenuItem != null) _helpMenuItem.Header = Strings.T("Help");
        if (_subHelpItem != null) _subHelpItem.Header = Strings.T("Help");
        if (_subFaqItem != null) _subFaqItem.Header = Strings.T("Faq");
        if (_subChangelogItem != null) _subChangelogItem.Header = Strings.T("Changelog");
        if (_subWebsiteItem != null) _subWebsiteItem.Header = Strings.T("Website");
        if (_subDonateItem != null) _subDonateItem.Header = Strings.T("Donate");
        if (_subAboutItem != null) _subAboutItem.Header = $"{Strings.T("AboutSubtitle")}...";
        if (_subCheckUpdateItem != null) _subCheckUpdateItem.Header = Strings.T("CheckForUpdates");
        if (_exitMenuItem != null) _exitMenuItem.Header = Strings.T("Exit");
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

        double dipX = pt.X;
        double dipY = pt.Y;

        var targetVisual = _ownerWindow ?? Application.Current.MainWindow;
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
