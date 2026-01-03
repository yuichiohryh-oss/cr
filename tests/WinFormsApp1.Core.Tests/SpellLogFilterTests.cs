using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class SpellLogFilterTests
{
    [Fact]
    public void RejectsNonLogShapes()
    {
        Assert.False(SpellLogFilter.IsLogCandidate(widthPx: 8, heightPx: 8, area: 64, minArea: 40, maxArea: 3000, minAspect: 4f));
        Assert.False(SpellLogFilter.IsLogCandidate(widthPx: 16, heightPx: 7, area: 112, minArea: 40, maxArea: 3000, minAspect: 4f));
    }

    [Fact]
    public void AcceptsWideBars()
    {
        Assert.True(SpellLogFilter.IsLogCandidate(widthPx: 48, heightPx: 6, area: 288, minArea: 40, maxArea: 3000, minAspect: 4f));
    }
}
