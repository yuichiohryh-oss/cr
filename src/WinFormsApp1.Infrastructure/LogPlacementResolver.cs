using System;
using WinFormsApp1.Core;

namespace WinFormsApp1.Infrastructure;

public sealed class LogPlacementResolver : IActionPlacementResolver
{
    private readonly SpellPlacementDetector _detector;
    private readonly SpellDetectionSettings _settings;

    public LogPlacementResolver(SpellPlacementDetector detector, SpellDetectionSettings settings)
    {
        _detector = detector;
        _settings = settings;
    }

    public int SearchFrames => _settings.SearchFrames;

    public bool CanResolve(ActionCommitEvent commit)
    {
        return string.Equals(commit.CardId, "log", StringComparison.OrdinalIgnoreCase);
    }

    public PlacementResult? Resolve(ActionCommitEvent commit, FrameContext context)
    {
        if (context.PrevFrame == null || context.Frame == null)
        {
            return null;
        }

        if (!_detector.TryDetectLog(context.PrevFrame, context.Frame, _settings, out SpellDetectionResult marker))
        {
            return null;
        }

        Lane lane = marker.X01 < 0.45f ? Lane.Left : (marker.X01 > 0.55f ? Lane.Right : Lane.Center);
        return new PlacementResult(marker.X01, marker.Y01, lane, 0.6f);
    }
}
