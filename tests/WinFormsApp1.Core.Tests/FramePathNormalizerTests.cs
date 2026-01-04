using System;
using System.IO;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class FramePathNormalizerTests
{
    [Fact]
    public void NormalizesFullPathToFramesRelative()
    {
        string matchDir = Path.Combine(Path.GetTempPath(), $"match_{Guid.NewGuid():N}");
        string fullPath = Path.Combine(matchDir, "frames", "sample_prev.png");

        string normalized = FramePathNormalizer.NormalizeToFramesRelative(matchDir, fullPath, "frames");

        Assert.Equal("frames/sample_prev.png", normalized);
    }

    [Fact]
    public void NormalizesDotSegmentsToFramesRelative()
    {
        string matchDir = Path.Combine(Path.GetTempPath(), $"match_{Guid.NewGuid():N}");
        string input = "././frames/sample_curr.png";

        string normalized = FramePathNormalizer.NormalizeToFramesRelative(matchDir, input, "frames");

        Assert.Equal("frames/sample_curr.png", normalized);
    }

    [Fact]
    public void NormalizesParentFramesToFramesRelative()
    {
        string matchDir = Path.Combine(Path.GetTempPath(), $"match_{Guid.NewGuid():N}");
        string input = "../frames/sample_curr.png";

        string normalized = FramePathNormalizer.NormalizeToFramesRelative(matchDir, input, "frames");

        Assert.Equal("frames/sample_curr.png", normalized);
    }
}
