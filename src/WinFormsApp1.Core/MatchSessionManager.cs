using System;
using System.Diagnostics;

namespace WinFormsApp1.Core;

public sealed class MatchSessionManager
{
    private readonly Stopwatch _stopwatch = new Stopwatch();

    public string CurrentMatchId { get; private set; } = string.Empty;
    public DateTime StartTimeLocal { get; private set; }
    public bool IsRunning => _stopwatch.IsRunning;
    public long ElapsedMs => _stopwatch.ElapsedMilliseconds;
    public long FrameIndex { get; private set; }

    public void StartNewMatch()
    {
        CurrentMatchId = Guid.NewGuid().ToString("N");
        StartTimeLocal = DateTime.Now;
        FrameIndex = 0;
        _stopwatch.Reset();
        _stopwatch.Start();
    }

    public void EndMatch()
    {
        _stopwatch.Stop();
    }

    public long NextFrame()
    {
        if (!_stopwatch.IsRunning)
        {
            return FrameIndex;
        }

        FrameIndex++;
        return FrameIndex;
    }
}

public static class MatchFileNameFormatter
{
    public static string BuildFileName(string pattern, DateTime startTimeLocal, string matchId)
    {
        string safePattern = string.IsNullOrWhiteSpace(pattern)
            ? "match_{yyyyMMdd_HHmmss}_{matchId}.jsonl"
            : pattern;

        string timeToken = startTimeLocal.ToString("yyyyMMdd_HHmmss");
        return safePattern
            .Replace("{yyyyMMdd_HHmmss}", timeToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{matchId}", matchId, StringComparison.OrdinalIgnoreCase);
    }
}
