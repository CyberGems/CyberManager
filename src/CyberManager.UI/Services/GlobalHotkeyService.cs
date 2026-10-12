using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CyberManager.UI.Services;

/// <summary>
/// Registers system-wide hotkeys on a single window. Several hotkeys can be registered at once;
/// each one is identified by an id so re-registering one does not disturb the others.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    /// <summary>Hotkey that shows and hides the main window.</summary>
    public const int MainHotkeyId = 9001;

    /// <summary>Hotkey that shows and hides the floating metrics widget.</summary>
    public const int WidgetHotkeyId = 9002;

    private const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;
    private static readonly char[] HotkeySeparators = ['+', ' '];

    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly HashSet<int> _registeredIds = new();

    /// <summary>Raised with the id of the hotkey that was pressed.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>Registers the main-window hotkey. Kept for the common single-hotkey case.</summary>
    public bool Register(Window window, string hotkeyString = "Alt+Shift+M") =>
        Register(window, MainHotkeyId, hotkeyString);

    /// <summary>
    /// Registers (or replaces) the hotkey identified by <paramref name="id"/>. An empty or blank
    /// string clears that hotkey and is reported as success, which is how the settings UI
    /// represents "no shortcut".
    /// </summary>
    public bool Register(Window window, int id, string hotkeyString)
    {
        try
        {
            if (!EnsureHook(window)) return false;

            Unregister(id);

            if (string.IsNullOrWhiteSpace(hotkeyString))
            {
                return true;
            }

            if (!TryParseHotkey(hotkeyString, out var modifiers, out var vk))
            {
                return false;
            }

            if (!RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, vk))
            {
                return false;
            }

            _registeredIds.Add(id);
            return true;
        }
        catch
        {
            Unregister(id);
            return false;
        }
    }

    /// <summary>True when the hotkey with the given id currently owns a system registration.</summary>
    public bool IsRegistered(int id) => _registeredIds.Contains(id);

    public void Unregister(int id)
    {
        if (!_registeredIds.Remove(id) || _hwnd == IntPtr.Zero) return;

        try
        {
            UnregisterHotKey(_hwnd, id);
        }
        catch { }
    }

    /// <summary>Releases every hotkey and detaches the window hook.</summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredIds.ToArray())
        {
            Unregister(id);
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

        _hwnd = IntPtr.Zero;
    }

    /// <summary>Releases every hotkey and detaches the window hook.</summary>
    public void Unregister() => UnregisterAll();

    private bool EnsureHook(Window window)
    {
        if (_source != null && _hwnd != IntPtr.Zero) return true;

        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        if (_hwnd == IntPtr.Zero)
        {
            helper.EnsureHandle();
            _hwnd = helper.Handle;
        }

        if (_hwnd == IntPtr.Zero) return false;

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(HwndHook);
        return _source != null;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_registeredIds.Contains(id))
            {
                handled = true;
                HotkeyPressed?.Invoke(id);
            }
        }
        return IntPtr.Zero;
    }

    public static bool TryParseHotkey(string? str, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        var hasKey = false;
        var parts = str.Split(HotkeySeparators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var trimmed = p.Trim();
            if (trimmed.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("control", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_CONTROL;
            else if (trimmed.Equals("alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_ALT;
            else if (trimmed.Equals("shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_SHIFT;
            else if (trimmed.Equals("win", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_WIN;
            else if (!hasKey && TryParseKey(trimmed, out vk))
            {
                hasKey = true;
            }
            else
            {
                return false;
            }
        }

        return modifiers != 0 && hasKey;
    }

    public static ModifierKeys GetModifier(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LWin or Key.RWin => ModifierKeys.Windows,
        _ => ModifierKeys.None
    };

    public static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (key != Key.None) parts.Add(FormatKey(key));
        return string.Join(" + ", parts);
    }

    private static bool TryParseKey(string token, out uint vk)
    {
        vk = 0;
        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            vk = (uint)char.ToUpperInvariant(token[0]);
            return true;
        }

        if (token.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token.AsSpan(1), out var fNum) &&
            fNum >= 1 && fNum <= 24)
        {
            vk = (uint)(0x70 + (fNum - 1));
            return true;
        }

        var normalized = token.ToUpperInvariant() switch
        {
            "ESC" or "ESCAPE" => "Escape",
            "RETURN" => "Enter",
            "BACKSPACE" => "Back",
            "DEL" => "Delete",
            "PGUP" => "PageUp",
            "PGDN" => "PageDown",
            _ => token
        };

        if (!Enum.TryParse<Key>(normalized, ignoreCase: true, out var key) || key == Key.None)
        {
            return false;
        }

        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    private static string FormatKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(CultureInfo.InvariantCulture),
        >= Key.NumPad0 and <= Key.NumPad9 => $"NumPad{(int)key - (int)Key.NumPad0}",
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.Escape => "Esc",
        Key.Enter => "Enter",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Space => "Space",
        Key.Tab => "Tab",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        _ => key.ToString()
    };

    public void Dispose()
    {
        UnregisterAll();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
