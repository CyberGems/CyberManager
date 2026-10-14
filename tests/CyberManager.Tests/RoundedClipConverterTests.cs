using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CyberManager.UI.Services;
using Xunit;

namespace CyberManager.Tests;

/// <summary>
/// Guards the rounded clip applied to the process grid. WPF's ClipToBounds only clips to the
/// rectangular bounds and ignores CornerRadius, so the grid needs an explicit rounded geometry or
/// its background, rows and header band square off the card's rounded corners.
/// </summary>
public class RoundedClipConverterTests
{
    [Fact]
    public void BuildsAGeometryMatchingTheElementAndRadius()
    {
        var geometry = Clip(1000, 600, "9");

        Assert.Equal(new Rect(0, 0, 1000, 600), geometry.Rect);
        Assert.Equal(9, geometry.RadiusX);
        Assert.Equal(9, geometry.RadiusY);
    }

    [Fact]
    public void ClampsTheRadiusToHalfTheShorterSide()
    {
        // A radius larger than half the element would produce a self-intersecting geometry.
        var geometry = Clip(10, 4, "9");

        Assert.Equal(2, geometry.RadiusX);
        Assert.Equal(2, geometry.RadiusY);
    }

    [Fact]
    public void FallsBackToSquareCornersWhenNoRadiusIsGiven()
    {
        var geometry = Clip(100, 100, null);

        Assert.Equal(0, geometry.RadiusX);
        Assert.Equal(0, geometry.RadiusY);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(0, 0)]
    public void ReturnsDoNothingBeforeLayout(double width, double height)
    {
        var result = new RoundedClipConverter().Convert(
            new object[] { width, height }, typeof(Geometry), "9", CultureInfo.InvariantCulture);

        Assert.Equal(Binding.DoNothing, result);
    }

    [Fact]
    public void ReturnsDoNothingForMissingValues()
    {
        var result = new RoundedClipConverter().Convert(
            new object[] { 100.0 }, typeof(Geometry), "9", CultureInfo.InvariantCulture);

        Assert.Equal(Binding.DoNothing, result);
    }

    private static RectangleGeometry Clip(double width, double height, string? radius) =>
        (RectangleGeometry)new RoundedClipConverter().Convert(
            new object[] { width, height }, typeof(Geometry), radius!, CultureInfo.InvariantCulture);
}
