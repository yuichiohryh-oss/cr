using System;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class ActionDetectorTests
{
    [Fact]
    public void DetectsSingleActionFromHandChangeAndFriendlySpawn()
    {
        var resolver = new UnitPlacementResolver(matchWindowMs: 700);
        var detector = new ActionDetector(pendingTimeoutMs: 1500, elixirCommitTolerance: 1, new[] { resolver });

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), CreateContext(t0, Array.Empty<SpawnEvent>()));

        var spawn = new SpawnEvent(Team.Friendly, Lane.Right, 0.64f, 0.78f, t0.AddMilliseconds(50), 0.8f);
        ActionSnapshot? pending = detector.Update(current, new ElixirResult(0.6f, 6), CreateContext(t0.AddMilliseconds(100), new[] { spawn }));
        Assert.False(pending.HasValue);

        ActionSnapshot? action = detector.Update(current, new ElixirResult(0.4f, 3), CreateContext(t0.AddMilliseconds(200), new[] { spawn }));
        Assert.True(action.HasValue);
        Assert.Equal("cannon", action.Value.CardId);
        Assert.Equal(Lane.Right, action.Value.Lane);

        ActionSnapshot? duplicate = detector.Update(current, new ElixirResult(0.4f, 3), CreateContext(t0.AddMilliseconds(300), new[] { spawn }));
        Assert.False(duplicate.HasValue);
    }

    [Fact]
    public void PendingWithoutElixirDropDoesNotCommit()
    {
        var resolver = new UnitPlacementResolver(matchWindowMs: 700);
        var detector = new ActionDetector(pendingTimeoutMs: 1500, elixirCommitTolerance: 1, new[] { resolver });

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), CreateContext(t0, Array.Empty<SpawnEvent>()));

        ActionSnapshot? action = detector.Update(current, new ElixirResult(0.6f, 6), CreateContext(t0.AddMilliseconds(200), Array.Empty<SpawnEvent>()));
        Assert.False(action.HasValue);
    }

    [Fact]
    public void PendingTimesOutWithoutCommit()
    {
        var resolver = new UnitPlacementResolver(matchWindowMs: 700);
        var detector = new ActionDetector(pendingTimeoutMs: 500, elixirCommitTolerance: 1, new[] { resolver });

        var previous = HandState.FromSlotsAndCosts(
            new[] { "cannon", "skeletons", "musketeer", "fireball" },
            new[] { 3, 1, 4, 4 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "fireball", "log" },
            new[] { 1, 4, 4, 2 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), CreateContext(t0, Array.Empty<SpawnEvent>()));

        detector.Update(current, new ElixirResult(0.6f, 6), CreateContext(t0.AddMilliseconds(100), Array.Empty<SpawnEvent>()));

        ActionSnapshot? action = detector.Update(current, new ElixirResult(0.2f, 2), CreateContext(t0.AddMilliseconds(700), Array.Empty<SpawnEvent>()));
        Assert.False(action.HasValue);
    }

    [Fact]
    public void FireballCommitProducesActionAfterResolverTimeout()
    {
        var resolver = new FakeResolver("fireball", searchFrames: 1);
        var detector = new ActionDetector(pendingTimeoutMs: 1500, elixirCommitTolerance: 1, new[] { resolver });

        var previous = HandState.FromSlotsAndCosts(
            new[] { "fireball", "skeletons", "musketeer", "log" },
            new[] { 4, 1, 4, 2 });
        var current = HandState.FromSlotsAndCosts(
            new[] { "skeletons", "musketeer", "log", "cannon" },
            new[] { 1, 4, 2, 3 });

        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        detector.Update(previous, new ElixirResult(0.6f, 6), CreateContext(t0, Array.Empty<SpawnEvent>()));

        detector.Update(current, new ElixirResult(0.6f, 6), CreateContext(t0.AddMilliseconds(100), Array.Empty<SpawnEvent>()));
        ActionSnapshot? commit = detector.Update(current, new ElixirResult(0.1f, 1), CreateContext(t0.AddMilliseconds(200), Array.Empty<SpawnEvent>()));

        Assert.False(commit.HasValue);

        ActionSnapshot? resolved = detector.Update(current, new ElixirResult(0.1f, 1), CreateContext(t0.AddMilliseconds(300), Array.Empty<SpawnEvent>()));
        Assert.True(resolved.HasValue);
        Assert.Equal("fireball", resolved.Value.CardId);
        Assert.Null(resolved.Value.X01);
        Assert.Null(resolved.Value.Y01);
    }

    [Fact]
    public void UnitResolverMatchesRecentSpawn()
    {
        var resolver = new UnitPlacementResolver(matchWindowMs: 700);
        var commit = new ActionCommitEvent("cannon", 3, new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc));
        var spawn = new SpawnEvent(Team.Friendly, Lane.Left, 0.22f, 0.73f, commit.Time.AddMilliseconds(-200), 0.8f);
        var context = CreateContext(commit.Time, new[] { spawn });

        PlacementResult? placement = resolver.Resolve(commit, context);

        Assert.True(placement.HasValue);
        Assert.Equal(Lane.Left, placement.Value.Lane);
    }

    private static FrameContext CreateContext(DateTime now, IReadOnlyList<SpawnEvent> spawns)
    {
        var spells = new SpellDetectionSettings(
            true,
            new Roi01(0f, 0f, 1f, 1f),
            25,
            40,
            3000,
            4f,
            1,
            new FireballDetectionSettings(220, 60, 6000, 0.7f, 1.4f));
        return new FrameContext(now, spawns, null, null, spells);
    }

    private sealed class FakeResolver : IActionPlacementResolver
    {
        private readonly string _cardId;
        public FakeResolver(string cardId, int searchFrames)
        {
            _cardId = cardId;
            SearchFrames = searchFrames;
        }

        public int SearchFrames { get; }

        public bool CanResolve(ActionCommitEvent commit)
        {
            return string.Equals(commit.CardId, _cardId, StringComparison.OrdinalIgnoreCase);
        }

        public PlacementResult? Resolve(ActionCommitEvent commit, FrameContext context)
        {
            return null;
        }
    }
}
