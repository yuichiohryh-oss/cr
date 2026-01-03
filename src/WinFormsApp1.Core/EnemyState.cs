using System;

namespace WinFormsApp1.Core;

public enum Team
{
    Enemy,
    Friendly
}

public enum Lane
{
    Unknown,
    Left,
    Right
}

public readonly record struct UnitMarker(Team Team, float X01, float Y01, Lane Lane, float Confidence);

public readonly record struct EnemyState(UnitMarker[] Units)
{
    public static EnemyState Empty => new(Array.Empty<UnitMarker>());
}

public readonly record struct FriendlyState(UnitMarker[] Units)
{
    public static FriendlyState Empty => new(Array.Empty<UnitMarker>());
}

public readonly record struct HpBarDetectionResult(EnemyState Enemy, FriendlyState Friendly)
{
    public static HpBarDetectionResult Empty => new(EnemyState.Empty, FriendlyState.Empty);
}

public static class HpBarColorClassifier
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
        return r >= 180 && g <= 80 && b <= 80;
    }

    private static bool IsFriendlyBlue(int r, int g, int b)
    {
        return b >= 160 && g >= 120 && r <= 100;
    }
}
