using System.Windows.Media;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

/// <summary>
/// Guards the heat ramp behind the CPU and RAM badges. It has to scale smoothly with the value —
/// a machine at 99% must read as hot, not just slightly warmer — and it must never blend the
/// metric colour into red, because green or cyan blended towards red passes through olive and grey
/// and over a dark card that is exactly the muddy brown the rows used to show.
/// </summary>
public class UsageHeatTests
{
    private static readonly Color Cpu = Color.FromRgb(0x10, 0xB9, 0x81);

    [Fact]
    public void OpacityGrowsWithUsage()
    {
        Assert.True(UsageHeat.Alpha(5) < UsageHeat.Alpha(40));
        Assert.True(UsageHeat.Alpha(40) < UsageHeat.Alpha(75));
        Assert.True(UsageHeat.Alpha(75) < UsageHeat.Alpha(99));
    }

    [Theory]
    [InlineData(-20)]
    [InlineData(0)]
    public void IdleStaysAlmostTransparent(double percent)
    {
        // A visible pill at rest would compete with the values in the list.
        Assert.True(UsageHeat.Alpha(percent) <= 0.06 + 0.0001);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(140)]
    [InlineData(double.NaN)]
    public void OpacityNeverExceedsTheCeiling(double percent)
    {
        Assert.InRange(UsageHeat.Alpha(percent), 0, 0.58 + 0.0001);
    }

    [Fact]
    public void KeepsTheMetricColourUntilUsageTurnsCritical()
    {
        Assert.Equal(Cpu, UsageHeat.ColorFor(0, Cpu));
        Assert.Equal(Cpu, UsageHeat.ColorFor(79.9, Cpu));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(99)]
    [InlineData(100)]
    public void TurnsRedOnceUsageIsCritical(double percent)
    {
        var colour = UsageHeat.ColorFor(percent, Cpu);

        Assert.True(colour.R > 0xE0, $"expected a red hue, got {colour}");
        Assert.True(colour.G < 0x80 && colour.B < 0x80, $"expected a red hue, got {colour}");
    }

    [Fact]
    public void TintCarriesTheHueAtTheRampedOpacity()
    {
        var tint = UsageHeat.Tint(99, Cpu);

        Assert.True(tint.IsFrozen);
        Assert.Equal((byte)Math.Round(UsageHeat.Alpha(99) * 255), tint.Color.A);
        Assert.Equal(0xEF, tint.Color.R);
    }

    [Fact]
    public void TintStaysTranslucentSoTheThemeBackgroundShowsThrough()
    {
        Assert.True(UsageHeat.Tint(100, Cpu).Color.A < 0xFF);
    }

    [Fact]
    public void SolidIsOpaqueForValuesAndGlyphs()
    {
        var solid = UsageHeat.Solid(10, Cpu);

        Assert.True(solid.IsFrozen);
        Assert.Equal(0xFF, solid.Color.A);
        Assert.Equal(Cpu, solid.Color);
    }

    [Fact]
    public void FromResourceFallsBackWhenTheThemeIsNotLoaded()
    {
        Assert.Equal(Cpu, UsageHeat.FromResource("CpuColor", Cpu));
    }
}
