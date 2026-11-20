using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class TrayTooltipFormatterTests
{
    [Fact]
    public void FormatUsesTheSuiteSeparatorWithOneSegment()
    {
        Assert.Equal("CyberManager · Alt+Shift+M", TrayTooltipFormatter.Format("Alt+Shift+M"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatWithoutSegmentFallsBackToTheAppName(string? segment)
    {
        Assert.Equal("CyberManager", TrayTooltipFormatter.Format(segment));
    }

    [Fact]
    public void FormatTrimsTheSegmentBeforeFormatting()
    {
        Assert.Equal("CyberManager · Alt+Shift+M", TrayTooltipFormatter.Format("  Alt+Shift+M  "));
    }

    [Fact]
    public void FormatCapsTheTooltipAt63CharactersWithAnEllipsis()
    {
        string tooltip = TrayTooltipFormatter.Format(new string('X', 80));

        Assert.Equal(63, tooltip.Length);
        Assert.EndsWith("…", tooltip);
        Assert.StartsWith("CyberManager · ", tooltip);
    }

    [Fact]
    public void FormatKeepsShortTooltipsUntouched()
    {
        string segment = "Update available 1.2.3";
        string tooltip = TrayTooltipFormatter.Format(segment);

        Assert.Equal($"CyberManager · {segment}", tooltip);
        Assert.DoesNotContain("…", tooltip);
    }
}
