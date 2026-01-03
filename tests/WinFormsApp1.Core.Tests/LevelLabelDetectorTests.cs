using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class LevelLabelDetectorTests
{
    [Fact]
    public void ColorClassifier_RedIsEnemy_BlueIsFriendly()
    {
        Assert.Equal(Team.Enemy, LevelLabelColorClassifier.Classify(r: 200, g: 100, b: 80));
        Assert.Equal(Team.Friendly, LevelLabelColorClassifier.Classify(r: 40, g: 120, b: 200));
        Assert.Null(LevelLabelColorClassifier.Classify(r: 120, g: 120, b: 120));
    }

    [Fact]
    public void SizeFilter_RejectsExtremeSizes()
    {
        Assert.False(LevelLabelSizeFilter.IsValidSize(widthPx: 4, heightPx: 4));
        Assert.False(LevelLabelSizeFilter.IsValidSize(widthPx: 80, heightPx: 10));
        Assert.True(LevelLabelSizeFilter.IsValidSize(widthPx: 18, heightPx: 14));
    }
}
