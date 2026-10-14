using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CyberManager.UI.Controls;

/// <summary>
/// Body of the CPU and RAM tooltips. The title-bar pills and the full window's metric cards show
/// the same card — metric name, the value in its heat colour, the detail lines and an optional
/// hint — so both windows describe the hardware identically.
/// </summary>
public partial class MetricToolTipContent : UserControl
{
    public MetricToolTipContent() => InitializeComponent();

    /// <summary>
    /// Builds the tooltip wrapper, so both windows get the same size and padding. The content is
    /// created once and then updated in place: rebuilding the tooltip on every sample would close
    /// the popup under the pointer instead of letting it tick along while the user reads it.
    /// </summary>
    public static ToolTip Create(out MetricToolTipContent content)
    {
        content = new MetricToolTipContent();
        return new ToolTip
        {
            Content = content,
            Padding = new Thickness(14, 10, 14, 10)
        };
    }

    public void SetContent(string title, string value, Brush valueBrush, string detail, string? hint = null)
    {
        TitleText.Text = title;
        ValueText.Text = value;
        ValueText.Foreground = valueBrush;
        DetailText.Text = detail;
        HintText.Text = hint ?? string.Empty;
        HintText.Visibility = string.IsNullOrWhiteSpace(hint) ? Visibility.Collapsed : Visibility.Visible;
    }
}
