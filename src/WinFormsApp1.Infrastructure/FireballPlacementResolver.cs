using System;
using WinFormsApp1.Core;

namespace WinFormsApp1.Infrastructure;

public sealed class FireballPlacementResolver : IActionPlacementResolver
{
    private readonly FireballPlacementDetector _detector;
    private readonly SpellDetectionSettings _settings;

    public FireballPlacementResolver(FireballPlacementDetector detector, SpellDetectionSettings settings)
    {
        _detector = detector;
        _settings = settings;
    }

    public int SearchFrames => _settings.SearchFrames;

    public bool CanResolve(ActionCommitEvent commit)
    {
        return string.Equals(commit.CardId, "fireball", StringComparison.OrdinalIgnoreCase);
    }

    public PlacementResult? Resolve(ActionCommitEvent commit, FrameContext context)
    {
        if (context.Frame == null)
        {
            return null;
        }

        if (!_detector.TryDetect(context.Frame, _settings, out SpellDetectionResult marker))
        {
            return null;
        }

        Lane lane = marker.X01 < 0.45f ? Lane.Left : (marker.X01 > 0.55f ? Lane.Right : Lane.Center);
        return new PlacementResult(marker.X01, marker.Y01, lane, 0.6f);
    }
}
