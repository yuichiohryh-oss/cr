namespace WinFormsApp1.Core;

public enum MatchPhase
{
    Early,
    DoubleElixir,
    Overtime,
    Unknown
}

public readonly record struct MatchClockState(MatchPhase Phase, float Confidence)
{
    public static MatchClockState Unknown => new(MatchPhase.Unknown, 0f);
}
