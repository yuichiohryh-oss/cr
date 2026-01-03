using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class MatchPhaseClassifierTests
{
    [Fact]
    public void BelowMinRatio_IsUnknown()
    {
        MatchPhase phase = MatchPhaseClassifier.Classify(whiteRatio: 0.005f, minWhiteRatio: 0.01f, earlyWhiteRatio: 0.05f);
        Assert.Equal(MatchPhase.Unknown, phase);
    }

    [Fact]
    public void AboveEarlyRatio_IsEarly()
    {
        MatchPhase phase = MatchPhaseClassifier.Classify(whiteRatio: 0.06f, minWhiteRatio: 0.01f, earlyWhiteRatio: 0.05f);
        Assert.Equal(MatchPhase.Early, phase);
    }
}
