using System.Windows;
using System.Windows.Media;
using TrainingLog.Controls;
using Xunit;

namespace TrainingLog.Tests;

public class RoundedClipTests
{
    [Fact]
    public void BuildGeometryMakesRoundedRectangleOfGivenSize()
    {
        var geometry = RoundedClip.BuildGeometry(new Size(204, 28), 4);

        var rect = Assert.IsType<RectangleGeometry>(geometry);
        Assert.Equal(new Rect(0, 0, 204, 28), rect.Rect);
        Assert.Equal(4, rect.RadiusX);
        Assert.Equal(4, rect.RadiusY);
    }

    [Fact]
    public void BuildGeometryCarriesZeroRadius()
    {
        var geometry = RoundedClip.BuildGeometry(new Size(16, 8), 0);

        var rect = Assert.IsType<RectangleGeometry>(geometry);
        Assert.Equal(0, rect.RadiusX);
        Assert.Equal(0, rect.RadiusY);
    }
}
