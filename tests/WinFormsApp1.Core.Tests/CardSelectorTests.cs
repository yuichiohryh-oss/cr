using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class CardSelectorTests
{
    [Fact]
    public void LowElixir_PicksCheapestAvailable()
    {
        var selector = new CardSelector();
        var hand = HandState.FromSlots(new[] { "skeletons", "hog", "fireball", "log" });
        var motion = new MotionResult(ThreatLeft: 0, ThreatRight: 0, DefenseTrigger: false);

        CardSelection? selection = selector.SelectCard(hand, elixir: 2, motion);

        Assert.NotNull(selection);
        Assert.Equal(0, selection!.Value.HandIndex);
        Assert.Equal("skeletons", selection.Value.CardId);
    }

    [Fact]
    public void StrongThreat_PrefersDefensivePriority()
    {
        var selector = new CardSelector();
        var hand = HandState.FromSlots(new[] { "skeletons", "musketeer", "hog", "fireball" });
        var motion = new MotionResult(ThreatLeft: 40, ThreatRight: 20, DefenseTrigger: true);

        CardSelection? selection = selector.SelectCard(hand, elixir: 4, motion);

        Assert.NotNull(selection);
        Assert.Equal(1, selection!.Value.HandIndex);
        Assert.Equal("musketeer", selection.Value.CardId);
    }

    [Fact]
    public void NoAffordableCard_ReturnsNull()
    {
        var selector = new CardSelector();
        var hand = HandState.FromSlots(new[] { "hog", "ice_golem", "log", "cannon" });
        var motion = new MotionResult(ThreatLeft: 10, ThreatRight: 5, DefenseTrigger: true);

        CardSelection? selection = selector.SelectCard(hand, elixir: 1, motion);

        Assert.Null(selection);
    }
}
