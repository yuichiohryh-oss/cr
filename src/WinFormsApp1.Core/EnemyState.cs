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

public readonly record struct EnemyUnit(float X01, float Y01, Lane Lane, Team Team, float Confidence);

public readonly record struct EnemyState(EnemyUnit[] Units)
{
    public static EnemyState Empty => new(Array.Empty<EnemyUnit>());
}

public readonly record struct FriendlyState(EnemyUnit[] Units)
{
    public static FriendlyState Empty => new(Array.Empty<EnemyUnit>());
}

public readonly record struct HpBarDetectionResult(EnemyState Enemy, FriendlyState Friendly)
{
    public static HpBarDetectionResult Empty => new(EnemyState.Empty, FriendlyState.Empty);
}
