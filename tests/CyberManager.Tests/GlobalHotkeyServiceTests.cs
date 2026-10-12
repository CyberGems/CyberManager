using System.Windows.Input;
using CyberManager.Common.Settings;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class GlobalHotkeyServiceTests
{
    [Fact]
    public void HotkeyIds_AreDistinct()
    {
        Assert.NotEqual(GlobalHotkeyService.MainHotkeyId, GlobalHotkeyService.WidgetHotkeyId);
    }

    [Fact]
    public void TryParseHotkey_ParsesDefaultWidgetShortcut()
    {
        Assert.True(
            GlobalHotkeyService.TryParseHotkey(new AppSettings().WidgetHotkey, out var modifiers, out var virtualKey));
        Assert.True(modifiers != 0);
        Assert.Equal((uint)'W', virtualKey);
    }

    [Fact]
    public void TryParseHotkey_RejectsModifiersWithoutKey()
    {
        Assert.False(GlobalHotkeyService.TryParseHotkey("Ctrl+Alt", out _, out _));
    }

    [Fact]
    public void TryParseHotkey_RejectsEmptyInput()
    {
        Assert.False(GlobalHotkeyService.TryParseHotkey("", out _, out _));
        Assert.False(GlobalHotkeyService.TryParseHotkey(null, out _, out _));
    }
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
