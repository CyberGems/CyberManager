using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CyberManager.UI.Controls;

internal static class HotkeyToolTipContent
{
    internal static FrameworkElement WithShortcut(string label, string shortcut)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var labelText = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        content.Children.Add(labelText);

        var shortcutText = new TextBlock
        {
            Text = shortcut,
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        shortcutText.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

        var badge = new Border
        {
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(5, 1, 5, 1),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = shortcutText
        };
        badge.SetResourceReference(Border.BackgroundProperty, "HeaderBgBrush");
        badge.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        content.Children.Add(badge);
        return content;
    }
}
