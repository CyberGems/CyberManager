using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

public class ProcessMetadataResolverTests
{
    [Fact]
    public void SelectFriendlyName_PrefersFileDescription()
    {
        var result = ProcessMetadataResolver.SelectFriendlyName(
            "Desktop Window Manager",
            "Microsoft Windows",
            "dwm.exe");

        Assert.Equal("Desktop Window Manager", result);
    }

    [Fact]
    public void SelectFriendlyName_FallsBackToProductThenImageName()
    {
        Assert.Equal(
            "Windows Host",
            ProcessMetadataResolver.SelectFriendlyName("", "Windows Host", "svchost.exe"));
        Assert.Equal(
            "svchost.exe",
            ProcessMetadataResolver.SelectFriendlyName(null, " ", "svchost.exe"));
    }
}
