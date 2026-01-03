namespace WinFormsApp1.Core;

public readonly record struct SpellDetectionSettings(
    bool Enabled,
    Roi01 Roi,
    int DiffThreshold,
    int MinArea,
    int MaxArea,
    float MinAspect,
    int SearchFrames
);

public static class SpellLogFilter
{
    public static bool IsLogCandidate(int widthPx, int heightPx, int area, int minArea, int maxArea, float minAspect)
    {
        if (widthPx <= 0 || heightPx <= 0)
        {
            return false;
        }

        if (area < minArea || area > maxArea)
        {
            return false;
        }

        int minSide = Math.Min(widthPx, heightPx);
        int maxSide = Math.Max(widthPx, heightPx);
        float aspect = maxSide / (float)minSide;

        if (minSide > 6)
        {
            return false;
        }

        if (maxSide < 20)
        {
            return false;
        }

        return aspect >= minAspect;
    }
}
