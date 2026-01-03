using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class HpBarColorClassifierTests
{
    [Fact]
    public void RedPixel_IsEnemy()
    {
        Team? team = HpBarColorClassifier.Classify(r: 200, g: 50, b: 50);
        Assert.Equal(Team.Enemy, team);
    }

    [Fact]
    public void CyanPixel_IsFriendly()
    {
        Team? team = HpBarColorClassifier.Classify(r: 60, g: 170, b: 210);
        Assert.Equal(Team.Friendly, team);
    }

    [Fact]
    public void NeutralPixel_IsUnknown()
    {
        Team? team = HpBarColorClassifier.Classify(r: 120, g: 120, b: 120);
        Assert.Null(team);
    }
}
