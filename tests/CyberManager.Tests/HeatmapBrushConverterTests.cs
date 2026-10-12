using System.Globalization;
using System.Windows.Media;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

/// <summary>
/// Guards the CPU and memory heat ramps used by the process list. The ramp used to be a
/// hard-coded amber painted at low opacity, which composites to brown over the dark card, so
/// these tests pin it to the theme accent and keep the low buckets cool.
/// </summary>
public class HeatmapBrushConverterTests
{
    private const long Megabyte = 1024L * 1024L;

    [Fact]
    public void MemoryRamp_IsTransparentBelowFirstThreshold()
    {
        Assert.Equal(Colors.Transparent, MemoryBucket(100).Color);
    }

    [Fact]
    public void MemoryRamp_ClimbsThroughEveryBucket()
    {
        var buckets = new[]
        {
            MemoryBucket(500),
            MemoryBucket(1000),
            MemoryBucket(2000),
            MemoryBucket(5000)
        };

        for (var i = 1; i < buckets.Length; i++)
        {
            Assert.True(
                buckets[i].Color.A > buckets[i - 1].Color.A,
                $"Bucket {i} must be stronger than bucket {i - 1}.");
        }
    }

    [Fact]
    public void MemoryRamp_KeepsTheTopBucketRed()
    {
        var top = MemoryBucket(5000).Color;

        Assert.True(top.R > top.G && top.R > top.B, "The top bucket must stay red.");
    }

    [Fact]
    public void Ramp_KeepsLowBucketsCool()
    {
        // Amber at low opacity over the navy card is what produced the muddy brown look, so the
        // accent-driven buckets must never lean warm (red channel above the green one).
        var buckets = new[] { MemoryBucket(500), MemoryBucket(1000), MemoryBucket(2000) };

        for (var i = 0; i < buckets.Length; i++)
        {
            Assert.True(
                buckets[i].Color.R <= buckets[i].Color.G,
                $"Bucket {i + 1} leans warm and would read as brown over the dark card.");
        }
    }

    [Fact]
    public void CpuAndMemoryRamps_ShareTheSameBuckets()
    {
        Assert.Equal(Colors.Transparent, CpuBucket(0.5).Color);
        Assert.Equal(MemoryBucket(1000).Color, CpuBucket(10.0).Color);
        Assert.Equal(MemoryBucket(5000).Color, CpuBucket(50.0).Color);
    }

    private static SolidColorBrush MemoryBucket(long megabytes) =>
        (SolidColorBrush)new MemoryHeatmapBrushConverter().Convert(
            megabytes * Megabyte, typeof(Brush), null!, CultureInfo.InvariantCulture);

    private static SolidColorBrush CpuBucket(double percent) =>
        (SolidColorBrush)new CpuHeatmapBrushConverter().Convert(
            percent, typeof(Brush), null!, CultureInfo.InvariantCulture);
}
