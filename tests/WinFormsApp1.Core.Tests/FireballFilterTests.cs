using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class FireballFilterTests
{
    [Fact]
    public void AcceptsNearSquareShapes()
    {
        bool ok = FireballFilter.IsFireballCandidate(
            widthPx: 30,
            heightPx: 28,
            area: 840,
            minArea: 60,
            maxArea: 6000,
            minAspect: 0.7f,
            maxAspect: 1.4f);

        Assert.True(ok);
    }

    [Fact]
    public void RejectsThinShapes()
    {
        bool ok = FireballFilter.IsFireballCandidate(
            widthPx: 60,
            heightPx: 8,
            area: 480,
            minArea: 60,
            maxArea: 6000,
            minAspect: 0.7f,
            maxAspect: 1.4f);

        Assert.False(ok);
    }
}
