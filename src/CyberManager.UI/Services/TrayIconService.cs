using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CyberManager.Common.I18n;
using CyberManager.Common.Settings;
using CyberManager.UI;

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

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
    private uint _taskbarCreatedMessage;

    private ContextMenu? _contextMenu;
    private MenuItem? _headerMenuItem;
    private MenuItem? _showMenuItem;
    private MenuItem? _sysInfoMenuItem;
    private MenuItem? _refreshMenuItem;
    private MenuItem? _widgetMenuItem;
    private MenuItem? _helpMenuItem;
    private MenuItem? _suiteMenuItem;
    private MenuItem? _subHelpItem;
    private MenuItem? _subFaqItem;
    private MenuItem? _subChangelogItem;
    private MenuItem? _subWebsiteItem;
    private MenuItem? _subDonateItem;
    private MenuItem? _subAboutItem;
    private MenuItem? _subCheckUpdateItem;
    private MenuItem? _settingsMenuItem;
    private MenuItem? _exitMenuItem;

    private Window? _ownerWindow;
    private Action? _onToggleShow;
    private Action? _onOpenSystemInfo;
    private Action? _onRefresh;
    private Action? _onToggleWidget;
    private bool _widgetChecked;
    private Action? _onOpenSettings;
    private Action? _onOpenAbout;
    private Action? _onCheckForUpdates;
    private Action? _onExit;

    /// <summary>Version the last completed check found, or null when the build is current.</summary>
    private string? _updateVersionLabel;
    private MenuItem? _updateMenuItem;

    public void Initialize(
        Window ownerWindow,
        Action onToggleShow,
        Action onOpenSystemInfo,
        Action onRefresh,
        Action onToggleWidget,
        Action onOpenSettings,
        Action onOpenAbout,
        Action onCheckForUpdates,
        Action onExit)
    {
        _ownerWindow = ownerWindow;
        _onToggleShow = onToggleShow;
        _onOpenSystemInfo = onOpenSystemInfo;
        _onRefresh = onRefresh;
        _onToggleWidget = onToggleWidget;
        _onOpenSettings = onOpenSettings;
        _onOpenAbout = onOpenAbout;
        _onCheckForUpdates = onCheckForUpdates;
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
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

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
            szTip = BuildTooltip()
        };

        _isAdded = AddTrayIcon();
    }

    private bool AddTrayIcon() =>
        _hIcon != IntPtr.Zero && Shell_NotifyIcon(NIM_ADD, ref _nid);

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
            Icon = logoImg,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _headerMenuItem.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
        if (miStyle != null) _headerMenuItem.Style = miStyle;
        _headerMenuItem.Click += (_, _) => _onOpenAbout?.Invoke();

        // 1b. Update notice: the tray counterpart of the titlebar pill, so the announcement still
        // reaches the user when the window is hidden or minimized to the tray. Collapsed until a
        // check actually finds a newer version.
        _updateMenuItem = new MenuItem
        {
            Header = Strings.T("Update"),
            Icon = CreatePathIcon("M 12 4 V 15 M 7 10 L 12 15 L 17 10 M 5 20 H 19"),
            Visibility = Visibility.Collapsed
        };
        _updateMenuItem.SetResourceReference(Control.ForegroundProperty, "AccentBrush");
        if (miStyle != null) _updateMenuItem.Style = miStyle;
        _updateMenuItem.Click += (_, _) => _onCheckForUpdates?.Invoke();

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

        // 5. Floating Widget
        _widgetMenuItem = new MenuItem
        {
            Header = Strings.T("FloatingWidget"),
            IsCheckable = true,
            IsChecked = _widgetChecked,
            InputGestureText = App.Settings.WidgetHotkey,
            Icon = CreatePathIcon("M 3 5 H 21 V 19 H 3 Z M 12 11 H 19 V 17 H 12 Z")
        };
        if (miStyle != null) _widgetMenuItem.Style = miStyle;
        _widgetMenuItem.Click += (_, _) => _onToggleWidget?.Invoke();

        // 8b. Settings
        _settingsMenuItem = new MenuItem
        {
            Header = $"{Strings.T("Settings")}...",
            Icon = CreatePathIcon("M 12 15 C 10.3 15 9 13.7 9 12 C 9 10.3 10.3 9 12 9 C 13.7 9 15 10.3 15 12 C 15 13.7 13.7 15 12 15 Z M 19.4 13 C 19.5 12.7 19.5 12.3 19.5 12 C 19.5 11.7 19.5 11.3 19.4 11 L 21.5 9.4 C 21.7 9.2 21.8 8.9 21.6 8.7 L 19.6 5.2 C 19.5 5 19.2 4.9 19 5 L 16.5 6 C 16 5.6 15.4 5.3 14.8 5.1 L 14.4 2.4 C 14.4 2.2 14.2 2 13.9 2 L 10.1 2 C 9.8 2 9.6 2.2 9.6 2.4 L 9.2 5.1 C 8.6 5.3 8 5.6 7.5 6 L 5 5 C 4.8 4.9 4.5 5 4.4 5.2 L 2.4 8.7 C 2.2 8.9 2.3 9.2 2.5 9.4 L 4.6 11 C 4.5 11.3 4.5 11.7 4.5 12 C 4.5 12.3 4.5 12.7 4.6 13 L 2.5 14.6 C 2.3 14.8 2.2 15.1 2.4 15.3 L 4.4 18.8 C 4.5 19 4.8 19.1 5 19 L 7.5 18 C 8 18.4 8.6 18.7 9.2 18.9 L 9.6 21.6 C 9.6 21.8 9.8 22 10.1 22 L 13.9 22 C 14.2 22 14.4 21.8 14.4 21.6 L 14.8 18.9 C 15.4 18.7 16 18.4 16.5 18 L 19 19 C 19.2 19.1 19.5 19 19.6 18.8 L 21.6 15.3 C 21.8 15.1 21.7 14.8 21.5 14.6 L 19.4 13 Z")
        };
        if (miStyle != null) _settingsMenuItem.Style = miStyle;
        _settingsMenuItem.Click += (_, _) => _onOpenSettings?.Invoke();

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
        _subAboutItem.Click += (_, _) => _onOpenAbout?.Invoke();

        // Submenu: Check for Updates
        _subCheckUpdateItem = new MenuItem
        {
            Header = Strings.T("CheckForUpdates"),
            Icon = CreatePathIcon("M 12 4 V 15 M 7 10 L 12 15 L 17 10 M 5 20 H 19")
        };
        if (miStyle != null) _subCheckUpdateItem.Style = miStyle;
        _subCheckUpdateItem.Click += (_, _) => _onCheckForUpdates?.Invoke();

        _helpMenuItem.Items.Add(_subHelpItem);
        _helpMenuItem.Items.Add(_subFaqItem);
        _helpMenuItem.Items.Add(_subChangelogItem);
        _helpMenuItem.Items.Add(_subWebsiteItem);
        _helpMenuItem.Items.Add(_subDonateItem);
        _helpMenuItem.Items.Add(new Separator { Style = sepStyle });
        _helpMenuItem.Items.Add(_subAboutItem);
        _helpMenuItem.Items.Add(_subCheckUpdateItem);

        _suiteMenuItem = BuildSuiteMenu(miStyle, sepStyle);

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
        _contextMenu.Items.Add(_updateMenuItem);
        _contextMenu.Items.Add(_showMenuItem);
        _contextMenu.Items.Add(_sysInfoMenuItem);
        _contextMenu.Items.Add(_refreshMenuItem);
        _contextMenu.Items.Add(_widgetMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_settingsMenuItem);
        _contextMenu.Items.Add(_helpMenuItem);
        _contextMenu.Items.Add(_suiteMenuItem);
        _contextMenu.Items.Add(new Separator { Style = sepStyle });
        _contextMenu.Items.Add(_exitMenuItem);
    }

    private static MenuItem BuildSuiteMenu(Style? menuItemStyle, Style? separatorStyle)
    {
        var suiteMenu = new MenuItem
        {
            Header = Strings.T("MoreFromCyberGems"),
            Icon = CreatePathIcon("M 6 3 H 18 L 22 9 L 12 21 L 2 9 Z M 2 9 H 22 M 10 3 L 8 9 L 12 21 L 16 9 L 14 3"),
            Tag = "suite",
            Visibility = App.Settings.ShowSuiteRecommendations ? Visibility.Visible : Visibility.Collapsed
        };
        System.Windows.Automation.AutomationProperties.SetName(suiteMenu, Strings.T("MoreFromCyberGems"));
        if (menuItemStyle != null) suiteMenu.Style = menuItemStyle;

        foreach (var app in SuiteCatalog.Others)
        {
            var appMenuItem = new MenuItem
            {
                Header = CreateSuiteMenuHeader(app),
                Icon = CreateSuiteMenuIcon(app),
                Tag = app
            };
            if (menuItemStyle != null) appMenuItem.Style = menuItemStyle;
            appMenuItem.Click += (_, _) => OpenUrl(app.Site);
            suiteMenu.Items.Add(appMenuItem);
        }

        suiteMenu.Items.Add(new Separator { Style = separatorStyle });
        var moreItem = new MenuItem
        {
            Header = Strings.T("SuiteMoreOnline"),
            Icon = CreatePathIcon("M 5 12 H 19 M 13 6 L 19 12 L 13 18"),
            Tag = "suite-more"
        };
        if (menuItemStyle != null) moreItem.Style = menuItemStyle;
        moreItem.Click += (_, _) => OpenUrl("https://cybergems.org/#apps");
        suiteMenu.Items.Add(moreItem);

        return suiteMenu;
    }

    private static StackPanel CreateSuiteMenuHeader(SuiteApp app)
    {
        var title = new TextBlock
        {
            Text = app.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        var description = new TextBlock
        {
            Text = Strings.Current == Lang.Es ? app.PitchEs : app.PitchEn,
            FontSize = 10.5,
            Opacity = 0.78
        };
        description.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

        var header = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(title);
        header.Children.Add(description);
        return header;
    }

    private static Image CreateSuiteMenuIcon(SuiteApp app)
    {
        var image = new Image
        {
            Width = 18,
            Height = 18,
            Stretch = Stretch.Uniform
        };
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Suite/{app.Slug}.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        }
        catch
        {
            // Keep the labeled entry available if an optional suite icon cannot be decoded.
        }

        return image;
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
        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stroke = strokeColor.HasValue ? new SolidColorBrush(strokeColor.Value) : null,
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

        if (!strokeColor.HasValue)
        {
            icon.SetResourceReference(Shape.StrokeProperty, "AccentBrush");
        }

        return icon;
    }

    /// <summary>
    /// Announces (or clears) an available update on the tray surfaces. The tray is the only
    /// surface left when the window is hidden or minimized to the tray, so without this the notice
    /// would never reach a user who runs the app minimized.
    /// </summary>
    public void SetUpdateAvailable(string? versionLabel)
    {
        _updateVersionLabel = string.IsNullOrEmpty(versionLabel) ? null : versionLabel;

        if (_updateMenuItem != null)
        {
            if (_updateVersionLabel is null)
            {
                _updateMenuItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                _updateMenuItem.Header = Strings.T("UpdateAvailable", _updateVersionLabel);
                System.Windows.Automation.AutomationProperties.SetName(
                    _updateMenuItem, Strings.T("UpdateAvailable", _updateVersionLabel));
                _updateMenuItem.Visibility = Visibility.Visible;
            }
        }

        ApplyTooltip();
    }

    /// <summary>
    /// Hover text of the tray icon. The update notice is appended here as well, because hovering
    /// the icon is the one signal that needs no menu to be opened first.
    /// </summary>
    private string BuildTooltip()
    {
        var text = $"CyberManager {UpdateService.GetCurrentVersionLabel()}";
        return _updateVersionLabel is null
            ? text
            : $"{text} — {Strings.T("UpdateAvailable", _updateVersionLabel)}";
    }

    private void ApplyTooltip()
    {
        if (!_isAdded) return;

        // NOTIFYICONDATA.szTip is a fixed 128-character buffer, so the text is clamped before it
        // reaches the marshaller rather than being silently truncated (or rejected) there.
        var tip = BuildTooltip();
        _nid.szTip = tip.Length > 127 ? tip[..127] : tip;
        Shell_NotifyIcon(NIM_MODIFY, ref _nid);
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
        if (_widgetMenuItem != null)
        {
            _widgetMenuItem.Header = Strings.T("FloatingWidget");
            _widgetMenuItem.InputGestureText = App.Settings.WidgetHotkey;
        }
        if (_settingsMenuItem != null) _settingsMenuItem.Header = $"{Strings.T("Settings")}...";
        if (_helpMenuItem != null) _helpMenuItem.Header = Strings.T("Help");
        if (_subHelpItem != null) _subHelpItem.Header = Strings.T("Help");
        if (_subFaqItem != null) _subFaqItem.Header = Strings.T("Faq");
        if (_subChangelogItem != null) _subChangelogItem.Header = Strings.T("Changelog");
        if (_subWebsiteItem != null) _subWebsiteItem.Header = Strings.T("Website");
        if (_subDonateItem != null) _subDonateItem.Header = Strings.T("Donate");
        if (_subAboutItem != null) _subAboutItem.Header = $"{Strings.T("AboutSubtitle")}...";
        if (_subCheckUpdateItem != null) _subCheckUpdateItem.Header = Strings.T("CheckForUpdates");
        // Re-derives the notice from the stored version so a language switch relabels it instead
        // of leaving the previous language behind.
        SetUpdateAvailable(_updateVersionLabel);
        if (_suiteMenuItem != null)
        {
            _suiteMenuItem.Header = Strings.T("MoreFromCyberGems");
            System.Windows.Automation.AutomationProperties.SetName(_suiteMenuItem, Strings.T("MoreFromCyberGems"));
            _suiteMenuItem.Visibility = App.Settings.ShowSuiteRecommendations
                ? Visibility.Visible
                : Visibility.Collapsed;
            foreach (var item in _suiteMenuItem.Items)
            {
                if (item is not MenuItem suiteAppItem) continue;
                if (suiteAppItem.Tag is SuiteApp suiteApp)
                    suiteAppItem.Header = CreateSuiteMenuHeader(suiteApp);
                else if (Equals(suiteAppItem.Tag, "suite-more"))
                    suiteAppItem.Header = Strings.T("SuiteMoreOnline");
            }
        }
        if (_exitMenuItem != null) _exitMenuItem.Header = Strings.T("Exit");
    }

    /// <summary>Keeps the check mark on the floating widget entry in sync with the widget.</summary>
    public void SetWidgetChecked(bool isChecked)
    {
        _widgetChecked = isChecked;
        if (_widgetMenuItem != null) _widgetMenuItem.IsChecked = isChecked;
    }

    public void UpdateTheme()
    {
        if (_contextMenu?.IsOpen == true)
        {
            _contextMenu.IsOpen = false;
        }

        // Recreate the menu so every item, icon, popup and template resolves
        // the complete current palette instead of retaining the previous one.
        BuildContextMenu();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
        {
            _isAdded = AddTrayIcon();
            handled = true;
            return IntPtr.Zero;
        }

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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(
        IntPtr hInst,
        [MarshalAs(UnmanagedType.LPWStr)] string lpszExeFileName,
        int nIconIndex);

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
