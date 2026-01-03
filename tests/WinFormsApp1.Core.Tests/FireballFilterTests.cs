using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class FireballFilterTests
{
    [Fact]
    public void AcceptsNearSquareShapes()
    {
        Assert.True(FireballFilter.IsFireballCandidate(widthPx: 30, heightPx: 28, area: 840, minArea: 60, maxArea: 6000, minAspect: 0.7f, maxAspect: 1.4f));
    }

    [Fact]
    public void RejectsThinShapes()
    {
        Assert.False(FireballFilter.IsFireballCandidate(widthPx: 60, heightPx: 8, area: 480, minArea: 60, maxArea: 6000, minAspect: 0.7f, maxAspect: 1.4f));
    }
}
