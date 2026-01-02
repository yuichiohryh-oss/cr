using System;
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
    Suggestion Decide(MotionResult motion, ElixirResult elixir, DateTime now);
}
