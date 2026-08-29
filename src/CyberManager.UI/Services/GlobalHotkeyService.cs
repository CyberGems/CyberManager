using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CyberManager.UI.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _isRegistered;

    public event Action? HotkeyPressed;

    public bool Register(Window window, string hotkeyString = "Ctrl+Alt+M")
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle;
            if (_hwnd == IntPtr.Zero)
            {
                helper.EnsureHandle();
                _hwnd = helper.Handle;
            }

            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(HwndHook);

            var (modifiers, vk) = ParseHotkey(hotkeyString);
            _isRegistered = RegisterHotKey(_hwnd, HotkeyId, modifiers | MOD_NOREPEAT, vk);
            return _isRegistered;
        }
        catch
        {
            return false;
        }
    }

    public void Unregister()
    {
        if (_isRegistered && _hwnd != IntPtr.Zero)
        {
            try
            {
                UnregisterHotKey(_hwnd, HotkeyId);
            }
            catch { }
            _isRegistered = false;
        }

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

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    private static (uint modifiers, uint vk) ParseHotkey(string str)
    {
        uint mod = 0;
        uint vk = 0x4D; // Default: 'M'

        if (string.IsNullOrWhiteSpace(str))
        {
            return (MOD_CONTROL | MOD_ALT, 0x4D);
        }

        var parts = str.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var trimmed = p.Trim();
            if (trimmed.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("control", StringComparison.OrdinalIgnoreCase))
                mod |= MOD_CONTROL;
            else if (trimmed.Equals("alt", StringComparison.OrdinalIgnoreCase))
                mod |= MOD_ALT;
            else if (trimmed.Equals("shift", StringComparison.OrdinalIgnoreCase))
                mod |= MOD_SHIFT;
            else if (trimmed.Equals("win", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("windows", StringComparison.OrdinalIgnoreCase))
                mod |= MOD_WIN;
            else if (trimmed.Length == 1 && char.IsLetterOrDigit(trimmed[0]))
            {
                vk = (uint)char.ToUpperInvariant(trimmed[0]);
            }
            else if (trimmed.Equals("esc", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("escape", StringComparison.OrdinalIgnoreCase))
            {
                vk = 0x1B; // VK_ESCAPE
            }
            else if (trimmed.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(trimmed.AsSpan(1), out var fNum) && fNum >= 1 && fNum <= 12)
            {
                vk = (uint)(0x70 + (fNum - 1)); // VK_F1 to VK_F12
            }
        }

        if (mod == 0) mod = MOD_CONTROL | MOD_ALT;
        return (mod, vk);
    }

    public void Dispose()
    {
        Unregister();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
