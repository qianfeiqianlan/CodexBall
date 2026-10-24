using CodexBall.Core.Utils;

namespace CodexBall.Tests;

public sealed class EdgeHideGeometryTests
{
    [Theory]
    [InlineData(-1, 400, true)]
    [InlineData(901, 400, true)]
    [InlineData(400, -1, true)]
    [InlineData(400, 901, true)]
    [InlineData(-1, -1, true)]
    [InlineData(0, 400, false)]
    [InlineData(900, 400, false)]
    [InlineData(400, 0, false)]
    [InlineData(400, 900, false)]
    [InlineData(400, 400, false)]
    public void DragReleaseOnlyHidesAfterCrossingAnEdge(double x, double y, bool expected)
        => Assert.Equal(expected, EdgeHideGeometry.IsBeyondWorkArea(x, y, 100, 100, 0, 0, 1000, 1000));

    [Fact]
    public void DragReleaseUsesMonitorWorkAreaRatherThanScreenOrigin()
    {
        Assert.False(EdgeHideGeometry.IsBeyondWorkArea(-1920, 40, 240, 340, -1920, 40, 1920, 1000));
        Assert.True(EdgeHideGeometry.IsBeyondWorkArea(-1921, 40, 240, 340, -1920, 40, 1920, 1000));
        Assert.True(EdgeHideGeometry.IsBeyondWorkArea(-1000, 39, 240, 340, -1920, 40, 1920, 1000));
    }

    [Theory]
    [InlineData(10, 400, HideEdge.Left, -94, 400)]
    [InlineData(890, 400, HideEdge.Right, 994, 400)]
    [InlineData(400, 10, HideEdge.Top, 400, -94)]
    [InlineData(400, 890, HideEdge.Bottom, 400, 994)]
    public void HidesTowardNearestEdge(double x, double y, HideEdge edge, double hiddenX, double hiddenY)
    {
        var result = EdgeHideGeometry.Calculate(x, y, 100, 100, 0, 0, 1000, 1000);
        Assert.Equal(edge, result.Edge);
        Assert.Equal(hiddenX, result.HiddenLeft);
        Assert.Equal(hiddenY, result.HiddenTop);
        Assert.Equal(x, result.VisibleLeft);
        Assert.Equal(y, result.VisibleTop);
    }

    [Fact]
    public void ComparesPercentagesInsteadOfRawDistances()
    {
        var result = EdgeHideGeometry.Calculate(90, 40, 100, 10, 0, 0, 1000, 100);
        Assert.Equal(HideEdge.Left, result.Edge); // 9% horizontal beats 40% vertical despite 90 > 40.
    }

    [Fact]
    public void SupportsOffsetMonitorWorkAreasAndClampsOffscreenPositions()
    {
        var result = EdgeHideGeometry.Calculate(-2100, 300, 100, 100, -1920, 40, 1920, 1000);
        Assert.Equal(HideEdge.Left, result.Edge);
        Assert.Equal(-1920, result.VisibleLeft);
        Assert.Equal(-2014, result.HiddenLeft);
    }
}
