using System.Windows.Input;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class GlobalHotkeyServiceTests
{
    [Fact]
    public void TryParseHotkey_ParsesModifierCombination()
    {
        Assert.True(
            GlobalHotkeyService.TryParseHotkey("Ctrl + Alt + M", out var modifiers, out var virtualKey));
        Assert.True(modifiers != 0);
        Assert.Equal((uint)'M', virtualKey);
    }

    [Fact]
    public void TryParseHotkey_ParsesNamedKeys()
    {
        Assert.True(
            GlobalHotkeyService.TryParseHotkey("Ctrl+Shift+F12", out _, out var virtualKey));
        Assert.Equal((uint)0x7B, virtualKey);
    }

    [Fact]
    public void FormatHotkey_UsesStableModifierOrder()
    {
        var hotkey = GlobalHotkeyService.FormatHotkey(
            ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt,
            Key.K);

        Assert.Equal("Ctrl + Alt + Shift + K", hotkey);
    }
}
