using System;
using System.Drawing;

namespace WinFormsApp1.Core;

public readonly record struct MotionResult(long ThreatLeft, long ThreatRight, bool DefenseTrigger);

public readonly record struct ElixirResult(float Filled01, int ElixirInt);

public readonly record struct Suggestion(bool HasSuggestion, float X01, float Y01, string Text)
{
    public static Suggestion None => new(false, 0f, 0f, string.Empty);
}

public readonly record struct Point01(float X, float Y);

public readonly record struct Roi01(float X, float Y, float Width, float Height)
{
    public Rectangle ToRectangle(int width, int height)
    {
        int x = (int)MathF.Round(X * width);
        int y = (int)MathF.Round(Y * height);
        int w = (int)MathF.Round(Width * width);
        int h = (int)MathF.Round(Height * height);

        x = Math.Clamp(x, 0, Math.Max(0, width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, height - 1));
        int right = Math.Clamp(x + w, 0, width);
        int bottom = Math.Clamp(y + h, 0, height);

        int finalW = Math.Max(0, right - x);
        int finalH = Math.Max(0, bottom - y);
        return new Rectangle(x, y, finalW, finalH);
    }
}

public readonly record struct MotionSettings(
    Roi01 Roi,
    int Step,
    int DiffThreshold,
    long TriggerThreshold,
    float SplitX01
);

public readonly record struct ElixirSettings(
    Roi01 Roi,
    int SampleStep,
    int PurpleRMin,
    int PurpleGMax,
    int PurpleBMin,
    int PurpleRBMaxDiff,
    int SmoothingWindow
);

public readonly record struct SuggestionSettings(
    int NeedElixir,
    int RequiredStreak,
    TimeSpan Cooldown
);

public static class SuggestionPoints
{
    public static readonly Point01 LeftDef = new(0.26f, 0.74f);
    public static readonly Point01 RightDef = new(0.74f, 0.74f);
    public static readonly Point01 Kite = new(0.50f, 0.68f);
}
