using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class CardSelectorTests
{
    [Fact]
    public void LowElixir_PicksCheapestAvailable()
    {
        var settings = CreateSettings();
        var selector = new CardSelector(settings);
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
        var settings = CreateSettings();
        var selector = new CardSelector(settings);
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
        var settings = CreateSettings();
        var selector = new CardSelector(settings);
        var hand = HandState.FromSlots(new[] { "hog", "ice_golem", "log", "cannon" });
        var motion = new MotionResult(ThreatLeft: 10, ThreatRight: 5, DefenseTrigger: true);

        CardSelection? selection = selector.SelectCard(hand, elixir: 1, motion);

        Assert.Null(selection);
    }

    [Fact]
    public void ExcludedCard_IsNotSelected()
    {
        var settings = CreateSettings();
        settings.ExcludedCardIds = new[] { "skeletons" };
        var selector = new CardSelector(settings);
        var hand = HandState.FromSlots(new[] { "skeletons", "ice_spirit", "hog", "log" });
        var motion = new MotionResult(ThreatLeft: 0, ThreatRight: 0, DefenseTrigger: false);

        CardSelection? selection = selector.SelectCard(hand, elixir: 2, motion);

        Assert.NotNull(selection);
        Assert.Equal("ice_spirit", selection!.Value.CardId);
    }

    [Fact]
    public void DefensivePriority_RespectsOrder()
    {
        var settings = CreateSettings();
        settings.DefensivePriorityCardIds = new[] { "ice_spirit", "skeletons" };
        var selector = new CardSelector(settings);
        var hand = HandState.FromSlots(new[] { "skeletons", "ice_spirit", "hog", "log" });
        var motion = new MotionResult(ThreatLeft: 30, ThreatRight: 25, DefenseTrigger: true);

        CardSelection? selection = selector.SelectCard(hand, elixir: 2, motion);

        Assert.NotNull(selection);
        Assert.Equal("ice_spirit", selection!.Value.CardId);
    }

    [Fact]
    public void UnknownCardId_UsesHandCost()
    {
        var settings = CreateSettings();
        var selector = new CardSelector(settings);
        var hand = HandState.FromSlotsAndCosts(
            new[] { "unknown_card", "hog", "fireball", "log" },
            new[] { 1, 4, 4, 2 }
        );
        var motion = new MotionResult(ThreatLeft: 0, ThreatRight: 0, DefenseTrigger: false);

        CardSelection? selection = selector.SelectCard(hand, elixir: 2, motion);

        Assert.NotNull(selection);
        Assert.Equal("unknown_card", selection!.Value.CardId);
    }

    [Fact]
    public void UnknownCardId_DoesNotThrow()
    {
        var settings = CreateSettings();
        var selector = new CardSelector(settings);
        var hand = HandState.FromSlots(new[] { "unknown_card", "hog", "fireball", "log" });
        var motion = new MotionResult(ThreatLeft: 0, ThreatRight: 0, DefenseTrigger: false);

        CardSelection? selection = selector.SelectCard(hand, elixir: 4, motion);

        Assert.NotNull(selection);
        Assert.Equal("hog", selection!.Value.CardId);
    }

    private static CardSelectionSettings CreateSettings()
    {
        return new CardSelectionSettings
        {
            Cards = new[]
            {
                new CardDefinition("hog", 4, new[] { "WinCondition" }),
                new CardDefinition("musketeer", 4, new[] { "Defensive" }),
                new CardDefinition("cannon", 3, new[] { "Building", "Defensive" }),
                new CardDefinition("fireball", 4, new[] { "Spell" }),
                new CardDefinition("ice_spirit", 1, new[] { "Defensive", "Cycle" }),
                new CardDefinition("skeletons", 1, new[] { "Defensive", "Cycle" }),
                new CardDefinition("ice_golem", 2, new[] { "Defensive" }),
                new CardDefinition("log", 2, new[] { "Spell" })
            },
            ExcludedCardIds = new string[0],
            DefensivePriorityCardIds = new[] { "musketeer", "ice_golem", "skeletons", "ice_spirit", "cannon" },
            StrongThreatThreshold = 50,
            ExcludeSpells = true,
            ExcludeBuildings = true
        };
    }
}
