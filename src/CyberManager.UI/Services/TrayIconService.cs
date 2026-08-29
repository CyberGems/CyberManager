using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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

        _showMenuItem = new MenuItem { Header = Strings.T("ShowCyberManager") };
        if (miStyle != null) _showMenuItem.Style = miStyle;
        _showMenuItem.Click += (_, _) => _onToggleShow?.Invoke();

        _startWithWindowsMenuItem = new MenuItem
        {
            Header = Strings.T("StartWithWindows"),
            IsCheckable = true,
            IsChecked = App.Settings.StartWithWindows
        };
        if (miStyle != null) _startWithWindowsMenuItem.Style = miStyle;
        _startWithWindowsMenuItem.Click += (_, _) =>
        {
            if (_startWithWindowsMenuItem != null)
            {
                _onToggleStartWithWindows?.Invoke(_startWithWindowsMenuItem.IsChecked);
            }
        };

        _minimizeToTrayMenuItem = new MenuItem
        {
            Header = Strings.T("MinimizeToTray"),
            IsCheckable = true,
            IsChecked = App.Settings.MinimizeToTray
        };
        if (miStyle != null) _minimizeToTrayMenuItem.Style = miStyle;
        _minimizeToTrayMenuItem.Click += (_, _) =>
        {
            if (_minimizeToTrayMenuItem != null)
            {
                _onToggleMinimizeToTray?.Invoke(_minimizeToTrayMenuItem.IsChecked);
            }
        };

        _exitMenuItem = new MenuItem { Header = Strings.T("Exit") };
        if (miStyle != null) _exitMenuItem.Style = miStyle;
        _exitMenuItem.Click += (_, _) => _onExit?.Invoke();

        _contextMenu.Items.Add(_showMenuItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_startWithWindowsMenuItem);
        _contextMenu.Items.Add(_minimizeToTrayMenuItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_exitMenuItem);
    }

    public void UpdateLocalization()
    {
        if (_showMenuItem != null) _showMenuItem.Header = Strings.T("ShowCyberManager");
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
        _contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
        _contextMenu.HorizontalOffset = pt.X;
        _contextMenu.VerticalOffset = pt.Y;
        _contextMenu.IsOpen = true;
    }

    private static IntPtr ExtractAppIcon()
    {
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
