using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using WinFormsApp1.Core;

namespace WinFormsApp1.Infrastructure;

public sealed class HpBarDetector
{
    private readonly int _step;
    private readonly int _bucketSize;

    public HpBarDetector(int step = 6, int bucketSize = 12)
    {
        _step = Math.Max(1, step);
        _bucketSize = Math.Max(4, bucketSize);
    }

    public unsafe HpBarDetectionResult Detect(Bitmap frame, Roi01? roi = null)
    {
        var enemyBuckets = new HashSet<(int x, int y)>();
        var friendlyBuckets = new HashSet<(int x, int y)>();

        var rect = new Rectangle(0, 0, frame.Width, frame.Height);
        Rectangle scanRect = roi.HasValue ? ToPixelRect(roi.Value, frame.Width, frame.Height) : rect;
        scanRect = Rectangle.Intersect(rect, scanRect);
        if (scanRect.Width <= 0 || scanRect.Height <= 0)
        {
            return HpBarDetectionResult.Empty;
        }

        BitmapData data = frame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            for (int y = scanRect.Top; y < scanRect.Bottom; y += _step)
            {
                byte* row = (byte*)data.Scan0 + (y * stride);
                for (int x = scanRect.Left; x < scanRect.Right; x += _step)
                {
                    int offset = x * 3;
                    int b = row[offset];
                    int g = row[offset + 1];
                    int r = row[offset + 2];

                    Team? team = HpBarColorClassifier.Classify(r, g, b);
                    if (team == Team.Enemy)
                    {
                        enemyBuckets.Add((x / _bucketSize, y / _bucketSize));
                    }
                    else if (team == Team.Friendly)
                    {
                        friendlyBuckets.Add((x / _bucketSize, y / _bucketSize));
                    }
                }
            }
        }
        finally
        {
            frame.UnlockBits(data);
        }

        return new HpBarDetectionResult(
            new EnemyState(BuildUnits(enemyBuckets, frame.Width, frame.Height, _bucketSize, Team.Enemy)),
            new FriendlyState(BuildUnits(friendlyBuckets, frame.Width, frame.Height, _bucketSize, Team.Friendly))
        );
    }

    private static UnitMarker[] BuildUnits(HashSet<(int x, int y)> buckets, int width, int height, int bucketSize, Team team)
    {
        if (buckets.Count == 0)
        {
            return Array.Empty<UnitMarker>();
        }

        var units = new List<UnitMarker>(buckets.Count);
        foreach (var bucket in buckets)
        {
            float x01 = (bucket.x + 0.5f) * bucketSize / width;
            float y01 = (bucket.y + 0.5f) * bucketSize / height;
            Lane lane = x01 < 0.5f ? Lane.Left : Lane.Right;
            units.Add(new UnitMarker(team, x01, y01, lane, 0.5f));
        }

        return units.ToArray();
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
