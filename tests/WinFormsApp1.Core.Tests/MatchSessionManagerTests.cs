using System;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class MatchSessionManagerTests
{
    [Fact]
    public void StartNewMatchResetsState()
    {
        var manager = new MatchSessionManager();
        manager.StartNewMatch();

        Assert.True(manager.IsRunning);
        Assert.False(string.IsNullOrWhiteSpace(manager.CurrentMatchId));
        Assert.Equal(0, manager.FrameIndex);
        Assert.InRange(manager.ElapsedMs, 0, 50);
    }

    [Fact]
    public void NextFrameIncrementsWhenRunning()
    {
        var manager = new MatchSessionManager();
        manager.StartNewMatch();

        long first = manager.NextFrame();
        long second = manager.NextFrame();

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public void BuildFileNameFormatsTokens()
    {
        var start = new DateTime(2026, 1, 3, 12, 34, 56);
        string name = MatchFileNameFormatter.BuildFileName("match_{yyyyMMdd_HHmmss}_{matchId}.jsonl", start, "abc");

        Assert.Equal("match_20260103_123456_abc.jsonl", name);
    }
}
