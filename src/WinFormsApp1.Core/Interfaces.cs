using System;
using System.Collections.Generic;
using System.Drawing;

namespace WinFormsApp1.Core;

public interface IMotionAnalyzer
{
    MotionResult Analyze(Bitmap prev, Bitmap curr);
}

public interface IElixirEstimator
{
    ElixirResult Estimate(Bitmap frame);
}

public interface ISuggestionEngine
{
    Suggestion Decide(
        MotionResult motion,
        ElixirResult elixir,
        HandState hand,
        EnemyState enemy,
        IReadOnlyList<SpawnEvent> spawns,
        DateTime now);
}

public interface ICardRecognizer
{
    HandState Recognize(Bitmap frame);
}
