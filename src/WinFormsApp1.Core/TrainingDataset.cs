using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinFormsApp1.Core;

public readonly record struct HandCardSnapshot(string CardId, int Cost);

public readonly record struct SpawnSnapshot(Team Team, Lane Lane, float X01, float Y01);

public readonly record struct StateSnapshot(
    MatchPhase Phase,
    int Elixir,
    IReadOnlyList<SpawnSnapshot> EnemySpawns,
    IReadOnlyList<SpawnSnapshot> FriendlySpawns,
    IReadOnlyList<HandCardSnapshot> Hand);

public readonly record struct ActionSnapshot(string CardId, Lane Lane, float? X01, float? Y01);

public readonly record struct TrainingSample(
    DateTime Timestamp,
    StateSnapshot State,
    ActionSnapshot Action,
    string MatchId = "",
    long MatchElapsedMs = 0,
    long FrameIndex = 0);

public readonly record struct TrainingSettings(
    bool Enabled,
    string OutputDir,
    string FileNamePattern,
    int RecentSpawnSeconds,
    int PendingTimeoutMs,
    int ElixirCommitTolerance,
    int UnitCommitMatchWindowMs
);

public readonly record struct PendingAction(
    string CardId,
    int Cost,
    DateTime StartTime,
    int ElixirAtStart
);

public readonly record struct PendingPlacement(
    ActionCommitEvent Commit,
    IActionPlacementResolver Resolver,
    ActionSnapshot BaseAction,
    int FramesLeft
);

public sealed class StateBuilder
{
    private readonly TimeSpan _recentWindow;

    public StateBuilder(int recentSpawnSeconds)
    {
        _recentWindow = TimeSpan.FromSeconds(Math.Max(1, recentSpawnSeconds));
    }

    public StateSnapshot Build(
        MatchClockState clockState,
        ElixirResult elixir,
        IReadOnlyList<SpawnEvent> spawns,
        HandState hand,
        DateTime now)
    {
        var enemy = new List<SpawnSnapshot>();
        var friendly = new List<SpawnSnapshot>();

        for (int i = 0; i < spawns.Count; i++)
        {
            SpawnEvent spawn = spawns[i];
            if (now - spawn.Time > _recentWindow)
            {
                continue;
            }

            var snapshot = new SpawnSnapshot(spawn.Team, spawn.Lane, spawn.X01, spawn.Y01);
            if (spawn.Team == Team.Enemy)
            {
                enemy.Add(snapshot);
            }
            else
            {
                friendly.Add(snapshot);
            }
        }

        var handSnapshots = new List<HandCardSnapshot>();
        bool hasCosts = hand.Costs.Length == hand.Slots.Length;
        for (int i = 0; i < hand.Slots.Length; i++)
        {
            int cost = hasCosts ? hand.Costs[i] : -1;
            handSnapshots.Add(new HandCardSnapshot(hand.Slots[i], cost));
        }

        return new StateSnapshot(clockState.Phase, elixir.ElixirInt, enemy, friendly, handSnapshots);
    }
}

public sealed class ActionDetector
{
    private readonly TimeSpan _pendingTimeout;
    private readonly int _elixirTolerance;
    private readonly IReadOnlyList<IActionPlacementResolver> _resolvers;
    private HandState _previous = HandState.Empty;
    private PendingAction? _pending;
    private PendingPlacement? _pendingPlacement;

    public ActionDetector(int pendingTimeoutMs, int elixirCommitTolerance, IReadOnlyList<IActionPlacementResolver> resolvers)
    {
        _pendingTimeout = TimeSpan.FromMilliseconds(pendingTimeoutMs);
        _elixirTolerance = Math.Max(0, elixirCommitTolerance);
        _resolvers = resolvers;
    }

    public ActionSnapshot? Update(HandState currentHand, ElixirResult elixir, FrameContext context)
    {
        if (_previous.Slots.Length == 0)
        {
            _previous = currentHand;
            return null;
        }

        if (_pendingPlacement.HasValue)
        {
            ActionSnapshot? resolved = TryResolvePendingPlacement(context);
            if (resolved.HasValue)
            {
                _previous = currentHand;
                return resolved.Value;
            }
        }

        if (_pending.HasValue)
        {
            PendingAction pending = _pending.Value;
            if (context.Now - pending.StartTime > _pendingTimeout)
            {
                _pending = null;
            }
            else if (IsCardPresent(currentHand, pending.CardId))
            {
                _pending = null;
            }
            else if (IsElixirCommitted(pending, elixir.ElixirInt))
            {
                var commit = new ActionCommitEvent(pending.CardId, pending.Cost, context.Now);
                _pending = null;
                ActionSnapshot? resolved = ResolveCommit(commit, context);
                _previous = currentHand;
                return resolved;
            }
        }

        if (_pending == null && TryFindRemovedCard(_previous, currentHand, out string? removedCard, out int removedCost))
        {
            if (!string.IsNullOrWhiteSpace(removedCard))
            {
                _pending = new PendingAction(removedCard, removedCost, context.Now, elixir.ElixirInt);
            }
        }

        _previous = currentHand;
        return null;
    }

    private ActionSnapshot? ResolveCommit(ActionCommitEvent commit, FrameContext context)
    {
        IActionPlacementResolver? resolver = FindResolver(commit);
        var baseAction = new ActionSnapshot(commit.CardId, Lane.Unknown, null, null);

        if (resolver == null)
        {
            return baseAction;
        }

        PlacementResult? placement = resolver.Resolve(commit, context);
        if (placement.HasValue)
        {
            return ApplyPlacement(baseAction, placement.Value);
        }

        if (resolver.SearchFrames > 0)
        {
            _pendingPlacement = new PendingPlacement(commit, resolver, baseAction, resolver.SearchFrames);
            return null;
        }

        return baseAction;
    }

    private bool IsElixirCommitted(PendingAction pending, int elixirNow)
    {
        int cost = Math.Max(1, pending.Cost);
        int requiredDrop = Math.Max(1, cost - _elixirTolerance);
        return elixirNow <= pending.ElixirAtStart - requiredDrop;
    }

    private static bool IsCardPresent(HandState hand, string cardId)
    {
        for (int i = 0; i < hand.Slots.Length; i++)
        {
            if (string.Equals(hand.Slots[i], cardId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IActionPlacementResolver? FindResolver(ActionCommitEvent commit)
    {
        for (int i = 0; i < _resolvers.Count; i++)
        {
            IActionPlacementResolver resolver = _resolvers[i];
            if (resolver.CanResolve(commit))
            {
                return resolver;
            }
        }

        return null;
    }

    private static bool TryFindRemovedCard(HandState previous, HandState current, out string? removedCard, out int removedCost)
    {
        removedCard = null;
        removedCost = -1;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < current.Slots.Length; i++)
        {
            string slot = current.Slots[i];
            counts[slot] = counts.TryGetValue(slot, out int count) ? count + 1 : 1;
        }

        bool hasCosts = previous.Costs.Length == previous.Slots.Length;
        for (int i = 0; i < previous.Slots.Length; i++)
        {
            string slot = previous.Slots[i];
            if (counts.TryGetValue(slot, out int count) && count > 0)
            {
                counts[slot] = count - 1;
                continue;
            }

            removedCard = slot;
            removedCost = hasCosts ? previous.Costs[i] : -1;
            return true;
        }

        return false;
    }

    private ActionSnapshot? TryResolvePendingPlacement(FrameContext context)
    {
        if (!_pendingPlacement.HasValue)
        {
            return null;
        }

        PendingPlacement pending = _pendingPlacement.Value;
        PlacementResult? placement = pending.Resolver.Resolve(pending.Commit, context);
        if (placement.HasValue)
        {
            _pendingPlacement = null;
            return ApplyPlacement(pending.BaseAction, placement.Value);
        }

        int remaining = pending.FramesLeft - 1;
        if (remaining <= 0)
        {
            _pendingPlacement = null;
            return pending.BaseAction;
        }

        _pendingPlacement = pending with { FramesLeft = remaining };
        return null;
    }

    private static ActionSnapshot ApplyPlacement(ActionSnapshot baseAction, PlacementResult placement)
    {
        return baseAction with
        {
            X01 = placement.X01,
            Y01 = placement.Y01,
            Lane = placement.Lane
        };
    }
}

public sealed class DatasetRecorder
{
    private readonly JsonSerializerOptions _options;
    private StreamWriter? _writer;

    public DatasetRecorder()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public string? CurrentPath { get; private set; }

    public bool IsOpen => _writer != null;

    public void Open(string outputPath)
    {
        Close();

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _writer = new StreamWriter(outputPath, append: true);
        CurrentPath = outputPath;
    }

    public void Close()
    {
        _writer?.Dispose();
        _writer = null;
        CurrentPath = null;
    }

    public void Append(TrainingSample sample)
    {
        if (_writer == null)
        {
            return;
        }

        string json = JsonSerializer.Serialize(sample, _options);
        _writer.WriteLine(json);
        _writer.Flush();
    }
}
