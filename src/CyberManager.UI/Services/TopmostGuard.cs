using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CyberManager.UI.Services;

/// <summary>
/// Keeps a pinned window inside the topmost band for the whole session.
///
/// Windows can silently drop a window out of that band after lock/unlock, full-screen apps,
/// display changes or shell z-order resets, which is why "always on top" stops working after a
/// few hours. The guard follows the window's Topmost property, reasserts the band when the window
/// is shown, and runs a watchdog while it is pinned: one style check every 10 s, and only when
/// WS_EX_TOPMOST is really gone does it call SetWindowPos. A healthy window is never pushed
/// above other topmost windows, so the common case stays a no-op.
/// </summary>
public sealed class TopmostGuard
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly IntPtr HwndTopmost = new IntPtr(-1);

    // GetWindowLongW instead of GetWindowLongPtrW: WS_EX_TOPMOST lives in the low 32 bits and
    // this entry point exists on both x86 and x64.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private readonly Window _window;
    private readonly DispatcherTimer _watchdog;
    private readonly DependencyPropertyDescriptor? _topmostDescriptor;
    private bool _detached;

    public TopmostGuard(Window window)
    {
        _window = window;

        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _watchdog.Tick += (_, _) => Ensure();

        // Following the property instead of requiring callers to notify the guard means a new
        // surface only creates the guard once; any Topmost change starts or stops the watchdog.
        _topmostDescriptor = DependencyPropertyDescriptor.FromProperty(Window.TopmostProperty, typeof(Window));
        _topmostDescriptor?.AddValueChanged(window, OnTopmostChanged);

        window.IsVisibleChanged += OnIsVisibleChanged;
        window.Closed += OnClosed;

        UpdateWatchdog();
    }

    /// <summary>Reasserts the topmost band only when the window actually lost it.</summary>
    public void Ensure()
    {
        if (_detached || !_window.Topmost || !_window.IsVisible) return;

        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if ((GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0) return;

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Releases the window subscriptions. The guard detaches itself when the window closes, so
    /// callers do not need to own its lifetime.
    /// </summary>
    public void Detach()
    {
        if (_detached) return;
        _detached = true;

        _watchdog.Stop();
        _topmostDescriptor?.RemoveValueChanged(_window, OnTopmostChanged);
        _window.IsVisibleChanged -= OnIsVisibleChanged;
        _window.Closed -= OnClosed;
    }

    private void OnTopmostChanged(object? sender, EventArgs e) => UpdateWatchdog();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_window.IsVisible) Ensure();
        UpdateWatchdog();
    }

    private void OnClosed(object? sender, EventArgs e) => Detach();

    private void UpdateWatchdog()
    {
        if (_detached)
        {
            _watchdog.Stop();
            return;
        }

        if (_window.Topmost && _window.IsVisible)
        {
            Ensure();
            if (!_watchdog.IsEnabled) _watchdog.Start();
        }
        else
        {
            _watchdog.Stop();
        }
    }
}
