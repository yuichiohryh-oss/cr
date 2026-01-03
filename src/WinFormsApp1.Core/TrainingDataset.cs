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

public readonly record struct ActionSnapshot(string CardId, Lane Lane, float X01, float Y01);

public readonly record struct TrainingSample(DateTime Timestamp, StateSnapshot State, ActionSnapshot Action);

public readonly record struct TrainingSettings(bool Enabled, string OutputPath, int RecentSpawnSeconds);

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
    private readonly TimeSpan _spawnWindow;
    private readonly TimeSpan _minInterval;
    private HandState _previous = HandState.Empty;
    private DateTime _lastActionTime = DateTime.MinValue;

    public ActionDetector(int spawnWindowMs = 900, int minIntervalMs = 600)
    {
        _spawnWindow = TimeSpan.FromMilliseconds(spawnWindowMs);
        _minInterval = TimeSpan.FromMilliseconds(minIntervalMs);
    }

    public ActionSnapshot? Update(HandState currentHand, IReadOnlyList<SpawnEvent> spawns, DateTime now)
    {
        if (_previous.Slots.Length == 0)
        {
            _previous = currentHand;
            return null;
        }

        if (now - _lastActionTime < _minInterval)
        {
            _previous = currentHand;
            return null;
        }

        if (!TryFindRemovedCard(_previous, currentHand, out string? removedCard, out int removedCost))
        {
            _previous = currentHand;
            return null;
        }

        if (!TryFindRecentFriendlySpawn(spawns, now, out SpawnEvent spawn))
        {
            _previous = currentHand;
            return null;
        }

        _previous = currentHand;
        _lastActionTime = now;

        return new ActionSnapshot(removedCard ?? string.Empty, spawn.Lane, spawn.X01, spawn.Y01);
    }

    private bool TryFindRecentFriendlySpawn(IReadOnlyList<SpawnEvent> spawns, DateTime now, out SpawnEvent result)
    {
        for (int i = spawns.Count - 1; i >= 0; i--)
        {
            SpawnEvent spawn = spawns[i];
            if (spawn.Team != Team.Friendly)
            {
                continue;
            }

            if (now - spawn.Time <= _spawnWindow)
            {
                result = spawn;
                return true;
            }
        }

        result = default;
        return false;
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
}

public sealed class DatasetRecorder
{
    private readonly string _outputPath;
    private readonly JsonSerializerOptions _options;

    public DatasetRecorder(string outputPath)
    {
        _outputPath = outputPath;
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void Append(TrainingSample sample)
    {
        string? dir = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(sample, _options);
        File.AppendAllText(_outputPath, json + Environment.NewLine);
    }
}
