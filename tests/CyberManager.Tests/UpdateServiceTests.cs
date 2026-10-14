using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

/// <summary>
/// Covers the release-tag parsing that decides whether a GitHub release counts as a newer version.
/// A tag the parser rejects used to be reported as "up to date", which silently hides a real
/// release, so the shapes GitHub actually produces are pinned down here.
/// </summary>
public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("  v1.2.3  ")]
    public void TryParseReleaseTag_PlainTag_ReturnsNormalizedVersion(string tag)
    {
        Assert.True(UpdateService.TryParseReleaseTag(tag, out var version));
        Assert.Equal(new Version(1, 2, 3, 0), version);
    }

    [Fact]
    public void TryParseReleaseTag_PrereleaseSuffix_IgnoresSuffix()
    {
        Assert.True(UpdateService.TryParseReleaseTag("v1.3.0-rc.1", out var version));
        Assert.Equal(new Version(1, 3, 0, 0), version);
    }

    [Fact]
    public void TryParseReleaseTag_BuildMetadata_IgnoresMetadata()
    {
        Assert.True(UpdateService.TryParseReleaseTag("v1.3.0+build.7", out var version));
        Assert.Equal(new Version(1, 3, 0, 0), version);
    }

    [Fact]
    public void TryParseReleaseTag_TwoPartTag_PadsMissingComponents()
    {
        Assert.True(UpdateService.TryParseReleaseTag("v1.2", out var version));
        Assert.Equal(new Version(1, 2, 0, 0), version);
    }

    [Fact]
    public void TryParseReleaseTag_FourPartTag_KeepsRevision()
    {
        Assert.True(UpdateService.TryParseReleaseTag("v1.2.3.4", out var version));
        Assert.Equal(new Version(1, 2, 3, 4), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("nightly")]
    [InlineData("release-candidate")]
    public void TryParseReleaseTag_WithoutNumericCore_ReturnsFalse(string? tag)
    {
        Assert.False(UpdateService.TryParseReleaseTag(tag, out var version));
        Assert.Null(version);
    }

    [Fact]
    public void TryParseReleaseTag_CurrentThreePartTag_DoesNotLookNewerThanFourPartBuild()
    {
        // The assembly version always has four parts, so a three-part tag describing the same
        // version must not be mistaken for a newer release.
        var current = new Version(1, 2, 3, 0);

        Assert.True(UpdateService.TryParseReleaseTag("v1.2.3", out var latest));
        Assert.False(latest > current);
    }

    [Fact]
    public void TryParseReleaseTag_PrereleaseOfNewerVersion_LooksNewer()
    {
        // Regression guard: Version.TryParse rejects "1.3.0-rc.1" outright, and the check used to
        // turn that into "up to date" against a release that really was newer.
        var current = new Version(1, 2, 3, 0);

        Assert.True(UpdateService.TryParseReleaseTag("v1.3.0-rc.1", out var latest));
        Assert.True(latest > current);
    }

    [Fact]
    public void GetCurrentVersion_IsNormalizedToFourParts()
    {
        var version = UpdateService.GetCurrentVersion();

        Assert.True(version.Major >= 0);
        Assert.True(version.Minor >= 0);
        Assert.True(version.Build >= 0);
        Assert.True(version.Revision >= 0);
    }

    [Fact]
    public void GetCurrentVersionLabel_StartsWithVersionPrefix()
    {
        Assert.StartsWith("v", UpdateService.GetCurrentVersionLabel(), StringComparison.Ordinal);
    }
}
