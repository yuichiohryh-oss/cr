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

    public unsafe HpBarDetectionResult Detect(Bitmap frame)
    {
        var enemyBuckets = new HashSet<(int x, int y)>();
        var friendlyBuckets = new HashSet<(int x, int y)>();

        var rect = new Rectangle(0, 0, frame.Width, frame.Height);
        BitmapData data = frame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            for (int y = 0; y < rect.Height; y += _step)
            {
                byte* row = (byte*)data.Scan0 + (y * stride);
                for (int x = 0; x < rect.Width; x += _step)
                {
                    int offset = x * 3;
                    int b = row[offset];
                    int g = row[offset + 1];
                    int r = row[offset + 2];

                    if (IsRedBar(r, g, b))
                    {
                        enemyBuckets.Add((x / _bucketSize, y / _bucketSize));
                    }
                    else if (IsGreenBar(r, g, b))
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

    private static EnemyUnit[] BuildUnits(HashSet<(int x, int y)> buckets, int width, int height, int bucketSize, Team team)
    {
        if (buckets.Count == 0)
        {
            return Array.Empty<EnemyUnit>();
        }

        var units = new List<EnemyUnit>(buckets.Count);
        foreach (var bucket in buckets)
        {
            float x01 = (bucket.x + 0.5f) * bucketSize / width;
            float y01 = (bucket.y + 0.5f) * bucketSize / height;
            Lane lane = x01 < 0.5f ? Lane.Left : Lane.Right;
            units.Add(new EnemyUnit(x01, y01, lane, team, 0.5f));
        }

        return units.ToArray();
    }

    private static bool IsRedBar(int r, int g, int b)
    {
        return r >= 180 && g <= 80 && b <= 80;
    }

    private static bool IsGreenBar(int r, int g, int b)
    {
        return g >= 180 && r <= 80 && b <= 80;
    }
}
