using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinFormsApp1.Core;

public readonly record struct MatchClockSettings(
    Roi01 Roi,
    int WhiteThreshold,
    float MinWhiteRatio,
    float EarlyWhiteRatio
);

public sealed class MatchPhaseEstimator : IMatchPhaseEstimator
{
    private readonly MatchClockSettings _settings;

    public MatchPhaseEstimator(MatchClockSettings settings)
    {
        _settings = settings;
    }

    public unsafe MatchClockState Estimate(Bitmap frame)
    {
        Rectangle roiRect = ToPixelRect(_settings.Roi, frame.Width, frame.Height);
        if (roiRect.Width <= 0 || roiRect.Height <= 0)
        {
            return MatchClockState.Unknown;
        }

        var rect = new Rectangle(0, 0, frame.Width, frame.Height);
        BitmapData data = frame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            int whiteCount = 0;
            int total = 0;

            for (int y = roiRect.Top; y < roiRect.Bottom; y++)
            {
                byte* row = (byte*)data.Scan0 + (y * stride);
                for (int x = roiRect.Left; x < roiRect.Right; x++)
                {
                    int offset = x * 3;
                    int b = row[offset];
                    int g = row[offset + 1];
                    int r = row[offset + 2];
                    int gray = (r + g + b) / 3;
                    if (gray >= _settings.WhiteThreshold)
                    {
                        whiteCount++;
                    }
                    total++;
                }
            }

            if (total <= 0)
            {
                return MatchClockState.Unknown;
            }

            float ratio = whiteCount / (float)total;
            MatchPhase phase = MatchPhaseClassifier.Classify(ratio, _settings.MinWhiteRatio, _settings.EarlyWhiteRatio);
            float confidence = MatchPhaseClassifier.GetConfidence(ratio, _settings.MinWhiteRatio, _settings.EarlyWhiteRatio);
            return new MatchClockState(phase, confidence);
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    private static Rectangle ToPixelRect(Roi01 roi, int width, int height)
    {
        int x = (int)Math.Round(Math.Clamp(roi.X, 0f, 1f) * width);
        int y = (int)Math.Round(Math.Clamp(roi.Y, 0f, 1f) * height);
        int w = (int)Math.Round(Math.Clamp(roi.Width, 0f, 1f) * width);
        int h = (int)Math.Round(Math.Clamp(roi.Height, 0f, 1f) * height);
        return new Rectangle(x, y, w, h);
    }
}

public static class MatchPhaseClassifier
{
    public static MatchPhase Classify(float whiteRatio, float minWhiteRatio, float earlyWhiteRatio)
    {
        if (whiteRatio < minWhiteRatio)
        {
            return MatchPhase.Unknown;
        }

        if (whiteRatio >= earlyWhiteRatio)
        {
            return MatchPhase.Early;
        }

        return MatchPhase.Unknown;
    }

    public static float GetConfidence(float whiteRatio, float minWhiteRatio, float earlyWhiteRatio)
    {
        if (whiteRatio < minWhiteRatio || earlyWhiteRatio <= minWhiteRatio)
        {
            return 0f;
        }

        float t = (whiteRatio - minWhiteRatio) / (earlyWhiteRatio - minWhiteRatio);
        return Math.Clamp(t, 0f, 1f);
    }
}
