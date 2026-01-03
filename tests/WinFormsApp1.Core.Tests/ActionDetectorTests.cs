using System;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class ActionDetectorTests
{
    [Fact]
    public void DetectsSingleActionFromHandChangeAndFriendlySpawn()
    {
        var detector = new ActionDetector(spawnWindowMs: 1000, pendingTimeoutMs: 1500, elixirCommitTolerance: 1);

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), Array.Empty<SpawnEvent>(), t0);

        var spawn = new SpawnEvent(Team.Friendly, Lane.Right, 0.64f, 0.78f, t0.AddMilliseconds(50), 0.8f);
        ActionCommit? pending = detector.Update(current, new ElixirResult(0.6f, 6), new[] { spawn }, t0.AddMilliseconds(100));
        Assert.False(pending.HasValue);

        ActionCommit? action = detector.Update(current, new ElixirResult(0.4f, 3), new[] { spawn }, t0.AddMilliseconds(200));
        Assert.True(action.HasValue);
        Assert.Equal("cannon", action.Value.Action.CardId);
        Assert.Equal(Lane.Right, action.Value.Action.Lane);

        ActionCommit? duplicate = detector.Update(current, new ElixirResult(0.4f, 3), new[] { spawn }, t0.AddMilliseconds(300));
        Assert.False(duplicate.HasValue);
    }

    [Fact]
    public void DoesNotDuplicateActionWhenHandStable()
    {
        var detector = new ActionDetector(spawnWindowMs: 1000, pendingTimeoutMs: 1500, elixirCommitTolerance: 1);

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), Array.Empty<SpawnEvent>(), t0);

        var spawn = new SpawnEvent(Team.Friendly, Lane.Left, 0.35f, 0.72f, t0.AddMilliseconds(50), 0.8f);
        detector.Update(current, new ElixirResult(0.4f, 3), new[] { spawn }, t0.AddMilliseconds(100));

        ActionCommit? second = detector.Update(current, new ElixirResult(0.4f, 3), new[] { spawn }, t0.AddMilliseconds(200));
        Assert.False(second.HasValue);
    }

    [Fact]
    public void PendingWithoutElixirDropDoesNotCommit()
    {
        var detector = new ActionDetector(spawnWindowMs: 1000, pendingTimeoutMs: 1500, elixirCommitTolerance: 1);

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), Array.Empty<SpawnEvent>(), t0);

        ActionCommit? action = detector.Update(current, new ElixirResult(0.6f, 6), Array.Empty<SpawnEvent>(), t0.AddMilliseconds(200));
        Assert.False(action.HasValue);
    }

    [Fact]
    public void PendingTimesOutWithoutCommit()
    {
        var detector = new ActionDetector(spawnWindowMs: 1000, pendingTimeoutMs: 500, elixirCommitTolerance: 1);

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), Array.Empty<SpawnEvent>(), t0);

        detector.Update(current, new ElixirResult(0.6f, 6), Array.Empty<SpawnEvent>(), t0.AddMilliseconds(100));

        ActionCommit? action = detector.Update(current, new ElixirResult(0.2f, 2), Array.Empty<SpawnEvent>(), t0.AddMilliseconds(700));
        Assert.False(action.HasValue);
    }
}
