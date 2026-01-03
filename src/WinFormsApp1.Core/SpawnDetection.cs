using System;
using System.Collections.Generic;

namespace WinFormsApp1.Core;

public readonly record struct LevelLabelCandidate(Team Team, float X01, float Y01, float Score);

public readonly record struct SpawnEvent(
    Team Team,
    Lane Lane,
    float X01,
    float Y01,
    DateTime Time,
    float Confidence
);

public static class LevelLabelColorClassifier
{
    public static Team? Classify(int r, int g, int b)
    {
        if (IsEnemyRed(r, g, b))
        {
            return Team.Enemy;
        }

        if (IsFriendlyBlue(r, g, b))
        {
            return Team.Friendly;
        }

        return null;
    }

    private static bool IsEnemyRed(int r, int g, int b)
    {
        return r > g + 40 && r > b + 40;
    }

    private static bool IsFriendlyBlue(int r, int g, int b)
    {
        return b > r + 30 && b > g + 10;
    }
}

public static class LevelLabelSizeFilter
{
    public static bool IsValidSize(int widthPx, int heightPx)
    {
        if (widthPx <= 0 || heightPx <= 0)
        {
            return false;
        }

        int minSize = 6;
        int maxSize = 64;
        if (widthPx < minSize || heightPx < minSize)
        {
            return false;
        }

        if (widthPx > maxSize || heightPx > maxSize)
        {
            return false;
        }

        float aspect = widthPx / (float)heightPx;
        return aspect >= 0.5f && aspect <= 2.5f;
    }
}

public sealed class SpawnEventDetector
{
    private readonly float _confirmDistance01;
    private readonly float _repeatDistance01;
    private readonly TimeSpan _repeatCooldown;
    private readonly TimeSpan _keepWindow;
    private List<LevelLabelCandidate> _previous = new();
    private readonly List<SpawnEvent> _recent = new();

    public SpawnEventDetector(
        float confirmDistance01 = 0.03f,
        float repeatDistance01 = 0.03f,
        int repeatCooldownMs = 800,
        int keepWindowMs = 2000)
    {
        _confirmDistance01 = confirmDistance01;
        _repeatDistance01 = repeatDistance01;
        _repeatCooldown = TimeSpan.FromMilliseconds(repeatCooldownMs);
        _keepWindow = TimeSpan.FromMilliseconds(keepWindowMs);
    }

    public IReadOnlyList<SpawnEvent> Update(IReadOnlyList<LevelLabelCandidate> current, DateTime now)
    {
        var newEvents = new List<SpawnEvent>();
        if (_previous.Count > 0 && current.Count > 0)
        {
            foreach (LevelLabelCandidate candidate in current)
            {
                if (!HasNearbyMatch(_previous, candidate, _confirmDistance01))
                {
                    continue;
                }

                if (IsNearRecent(candidate, now))
                {
                    continue;
                }

                Lane lane = GetLane(candidate.X01);
                var spawn = new SpawnEvent(candidate.Team, lane, candidate.X01, candidate.Y01, now, candidate.Score);
                newEvents.Add(spawn);
                _recent.Add(spawn);
            }
        }

        PruneRecent(now);
        _previous = new List<LevelLabelCandidate>(current);

        return _recent.ToArray();
    }

    private bool HasNearbyMatch(List<LevelLabelCandidate> list, LevelLabelCandidate candidate, float distance01)
    {
        foreach (LevelLabelCandidate prev in list)
        {
            if (prev.Team != candidate.Team)
            {
                continue;
            }

            if (Distance01(prev, candidate) <= distance01)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNearRecent(LevelLabelCandidate candidate, DateTime now)
    {
        foreach (SpawnEvent existing in _recent)
        {
            if (existing.Team != candidate.Team)
            {
                continue;
            }

            if (now - existing.Time > _repeatCooldown)
            {
                continue;
            }

            float dx = existing.X01 - candidate.X01;
            float dy = existing.Y01 - candidate.Y01;
            if ((dx * dx) + (dy * dy) <= _repeatDistance01 * _repeatDistance01)
            {
                return true;
            }
        }

        return false;
    }

    private void PruneRecent(DateTime now)
    {
        for (int i = _recent.Count - 1; i >= 0; i--)
        {
            if (now - _recent[i].Time > _keepWindow)
            {
                _recent.RemoveAt(i);
            }
        }
    }

    private static float Distance01(LevelLabelCandidate a, LevelLabelCandidate b)
    {
        float dx = a.X01 - b.X01;
        float dy = a.Y01 - b.Y01;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static Lane GetLane(float x01)
    {
        if (x01 < 0.45f)
        {
            return Lane.Left;
        }

        if (x01 > 0.55f)
        {
            return Lane.Right;
        }

        return Lane.Center;
    }
}
