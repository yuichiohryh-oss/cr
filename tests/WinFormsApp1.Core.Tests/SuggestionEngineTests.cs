using System;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class SuggestionEngineTests
{
    [Fact]
    public void ElixirNotEnough_NoSuggestion()
    {
        var settings = new SuggestionSettings(NeedElixir: 3, RequiredStreak: 2, Cooldown: TimeSpan.FromMilliseconds(700));
        var engine = new SuggestionEngine(settings);

        var motion = new MotionResult(ThreatLeft: 10, ThreatRight: 5, DefenseTrigger: true);
        var elixir = new ElixirResult(Filled01: 0.2f, ElixirInt: 2);

        var t0 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var s1 = engine.Decide(motion, elixir, t0);
        var s2 = engine.Decide(motion, elixir, t0.AddMilliseconds(100));

        Assert.False(s1.HasSuggestion);
        Assert.False(s2.HasSuggestion);
    }

    [Fact]
    public void RightThreat_PicksRightDefense()
    {
        var settings = new SuggestionSettings(NeedElixir: 3, RequiredStreak: 2, Cooldown: TimeSpan.FromMilliseconds(700));
        var engine = new SuggestionEngine(settings);

        var motion = new MotionResult(ThreatLeft: 5, ThreatRight: 20, DefenseTrigger: true);
        var elixir = new ElixirResult(Filled01: 0.5f, ElixirInt: 5);

        var t0 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        engine.Decide(motion, elixir, t0);
        var suggestion = engine.Decide(motion, elixir, t0.AddMilliseconds(200));

        Assert.True(suggestion.HasSuggestion);
        Assert.InRange(suggestion.X01, SuggestionPoints.RightDef.X - 0.001f, SuggestionPoints.RightDef.X + 0.001f);
        Assert.InRange(suggestion.Y01, SuggestionPoints.RightDef.Y - 0.001f, SuggestionPoints.RightDef.Y + 0.001f);
    }

    [Fact]
    public void CooldownBlocksRapidRepeat()
    {
        var settings = new SuggestionSettings(NeedElixir: 3, RequiredStreak: 2, Cooldown: TimeSpan.FromMilliseconds(700));
        var engine = new SuggestionEngine(settings);

        var motion = new MotionResult(ThreatLeft: 12, ThreatRight: 3, DefenseTrigger: true);
        var elixir = new ElixirResult(Filled01: 0.6f, ElixirInt: 6);

        var t0 = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        engine.Decide(motion, elixir, t0);
        var first = engine.Decide(motion, elixir, t0.AddMilliseconds(200));
        var second = engine.Decide(motion, elixir, t0.AddMilliseconds(400));

        Assert.True(first.HasSuggestion);
        Assert.False(second.HasSuggestion);
    }
}
