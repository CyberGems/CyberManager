using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CyberManager.UI.Services;

/// <summary>
/// Keeps one CyberManager process per session. The first launch owns a named mutex for the
/// lifetime of the process; any later launch fails to take it, wakes the running instance over
/// a registered window message and exits without opening a window, so double-clicking the icon
/// never spawns a second copy with its own collector, tray icon and hotkey set.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    /// <summary>Mutex identity. Local to the session; one desktop user, one app.</summary>
    public const string DefaultMutexName = "CyberManager_SingleInstance";

    private const string ActivationMessageName = "CyberManager_ActivateMainWindow";
    private const int HWND_BROADCAST = 0xffff;

    // The wake-up is broadcast a few times over: the running instance can still be inside
    // OnLoaded (tray, hotkeys, first collection) when the second launch signals, and a single
    // PostMessage fired that early would be lost with no way to notice.
    private const int SignalRetries = 3;
    private const int SignalRetryDelayMs = 250;

    private readonly string _mutexName;
    private Mutex? _owned;
    private HwndSource? _source;
    private uint _activationMessage;
    private bool _disposed;

    /// <summary>Raised on the UI thread when another launch asked this instance to surface.</summary>
    public event Action? ActivationRequested;

    /// <param name="mutexName">Mutex identity; tests pass a unique name so they never touch the real app.</param>
    public SingleInstance(string? mutexName = null)
    {
        _mutexName = string.IsNullOrWhiteSpace(mutexName) ? DefaultMutexName : mutexName;
    }

    /// <summary>
    /// Tries to become the only instance. The winner keeps the mutex (the process is expected
    /// to hold the object for its lifetime) until it exits, at which point the OS releases the
    /// mutex automatically. Losers get <c>false</c> and should signal + exit.
    /// </summary>
    public bool TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _owned = mutex;
        return true;
    }

    /// <summary>Drops the mutex early. Mostly for tests; a running app lets the OS do it on exit.</summary>
    public void Release()
    {
        var owned = _owned;
        _owned = null;
        owned?.Dispose();
    }

    /// <summary>
    /// Asks the running instance to show itself. A registered window message is broadcast
    /// instead of being posted to a specific HWND because a window hidden in the tray does not
    /// reliably expose one; the broadcast reaches every top-level window, including hidden ones.
    /// </summary>
    public void SignalExistingInstance()
    {
        if (_activationMessage == 0)
        {
            _activationMessage = RegisterWindowMessage(ActivationMessageName);
        }

        if (_activationMessage == 0)
        {
            return;
        }

        for (var attempt = 1; attempt <= SignalRetries; attempt++)
        {
            PostMessage((IntPtr)HWND_BROADCAST, _activationMessage, IntPtr.Zero, IntPtr.Zero);
            if (attempt < SignalRetries)
            {
                System.Threading.Thread.Sleep(SignalRetryDelayMs);
            }
        }
    }

    /// <summary>Listens for activation requests on <paramref name="window"/>'s message loop.</summary>
    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            helper.EnsureHandle();
            hwnd = helper.Handle;
        }

        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        _activationMessage = RegisterWindowMessage(ActivationMessageName);
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(HwndHook);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _activationMessage)
        {
            handled = true;
            ActivationRequested?.Invoke();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release();

        if (_source != null)
        {
            try
            {
                _source.RemoveHook(HwndHook);
            }
            catch { }
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
