namespace CyberManager.UI.Controls;

internal static class TitleBarHotkeys
{
    internal const string About = "F1";
    internal const string Processes = "Ctrl+1";
    internal const string Performance = "Ctrl+2";
    internal const string Settings = "Ctrl+3";
    internal const string Update = "Ctrl+Shift+U";
    internal const string ToggleCompact = "Ctrl+Shift+M";
    internal const string More = "Alt+M";
    internal const string TogglePin = "Ctrl+Shift+P";
    internal const string Minimize = "Ctrl+Alt+Down";
    internal const string Maximize = "Ctrl+Alt+Up";
    internal const string Close = "Alt+F4";

    internal static string WithShortcut(string label, string shortcut) =>
        $"{label} ({shortcut})";
}
