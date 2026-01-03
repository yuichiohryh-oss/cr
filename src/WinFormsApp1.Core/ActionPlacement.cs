using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinFormsApp1.Core;

public readonly record struct ActionCommitEvent(string CardId, int Cost, DateTime Time);

public readonly record struct PlacementResult(float X01, float Y01, Lane Lane, float Confidence);

public readonly record struct FrameContext(
    DateTime Now,
    IReadOnlyList<SpawnEvent> Spawns,
    Bitmap? PrevFrame,
    Bitmap? Frame,
    SpellDetectionSettings SpellSettings
);

public interface IActionPlacementResolver
{
    int SearchFrames { get; }
    bool CanResolve(ActionCommitEvent commit);
    PlacementResult? Resolve(ActionCommitEvent commit, FrameContext context);
}

public sealed class UnitPlacementResolver : IActionPlacementResolver
{
    private readonly TimeSpan _matchWindow;

    public UnitPlacementResolver(int matchWindowMs)
    {
        _matchWindow = TimeSpan.FromMilliseconds(Math.Max(0, matchWindowMs));
    }

    public int SearchFrames => 0;

    public bool CanResolve(ActionCommitEvent commit)
    {
        return !IsSpellCard(commit.CardId);
    }

    public PlacementResult? Resolve(ActionCommitEvent commit, FrameContext context)
    {
        SpawnEvent? best = null;
        for (int i = context.Spawns.Count - 1; i >= 0; i--)
        {
            SpawnEvent spawn = context.Spawns[i];
            if (spawn.Team != Team.Friendly)
            {
                continue;
            }

            TimeSpan delta = commit.Time - spawn.Time;
            if (delta < TimeSpan.Zero || delta > _matchWindow)
            {
                continue;
            }

            best = spawn;
            break;
        }

        if (best.HasValue)
        {
            SpawnEvent spawn = best.Value;
            return new PlacementResult(spawn.X01, spawn.Y01, spawn.Lane, spawn.Confidence);
        }

        return null;
    }

    private static bool IsSpellCard(string cardId)
    {
        return string.Equals(cardId, "log", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cardId, "fireball", StringComparison.OrdinalIgnoreCase);
    }
}
