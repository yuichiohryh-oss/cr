using System;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class ActionDetectorTests
{
    [Fact]
    public void DetectsSingleActionFromHandChangeAndFriendlySpawn()
    {
        var detector = new ActionDetector(spawnWindowMs: 1000, minIntervalMs: 0);

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, Array.Empty<SpawnEvent>(), t0);

        var spawn = new SpawnEvent(Team.Friendly, Lane.Right, 0.64f, 0.78f, t0.AddMilliseconds(50), 0.8f);
        ActionSnapshot? action = detector.Update(current, new[] { spawn }, t0.AddMilliseconds(100));

        Assert.True(action.HasValue);
        Assert.Equal("cannon", action.Value.CardId);
        Assert.Equal(Lane.Right, action.Value.Lane);
    }

    [Fact]
    public void DoesNotDuplicateActionWhenHandStable()
    {
        var detector = new ActionDetector(spawnWindowMs: 1000, minIntervalMs: 500);

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, Array.Empty<SpawnEvent>(), t0);

        var spawn = new SpawnEvent(Team.Friendly, Lane.Left, 0.35f, 0.72f, t0.AddMilliseconds(50), 0.8f);
        detector.Update(current, new[] { spawn }, t0.AddMilliseconds(100));

        ActionSnapshot? second = detector.Update(current, new[] { spawn }, t0.AddMilliseconds(200));
        Assert.False(second.HasValue);
    }
}
