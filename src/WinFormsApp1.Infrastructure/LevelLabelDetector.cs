using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using WinFormsApp1.Core;

namespace WinFormsApp1.Infrastructure;

public sealed class LevelLabelDetector
{
    private readonly int _step;
    private readonly int _bucketSize;
    private readonly int _minBucketHits;

    public LevelLabelDetector(int step = 2, int bucketSize = 6, int minBucketHits = 2)
    {
        _step = Math.Max(1, step);
        _bucketSize = Math.Max(3, bucketSize);
        _minBucketHits = Math.Max(1, minBucketHits);
    }

    public unsafe IReadOnlyList<LevelLabelCandidate> Detect(Bitmap frame, Roi01 roi)
    {
        var rect = new Rectangle(0, 0, frame.Width, frame.Height);
        Rectangle scanRect = ToPixelRect(roi, frame.Width, frame.Height);
        scanRect = Rectangle.Intersect(rect, scanRect);
        if (scanRect.Width <= 0 || scanRect.Height <= 0)
        {
            return Array.Empty<LevelLabelCandidate>();
        }

        int gridWidth = (scanRect.Width + _bucketSize - 1) / _bucketSize;
        int gridHeight = (scanRect.Height + _bucketSize - 1) / _bucketSize;
        int gridSize = gridWidth * gridHeight;
        var enemyCounts = new int[gridSize];
        var friendlyCounts = new int[gridSize];

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

                    Team? team = LevelLabelColorClassifier.Classify(r, g, b);
                    if (team == null)
                    {
                        continue;
                    }

                    int bx = (x - scanRect.Left) / _bucketSize;
                    int by = (y - scanRect.Top) / _bucketSize;
                    int index = (by * gridWidth) + bx;
                    if (team == Team.Enemy)
                    {
                        enemyCounts[index]++;
                    }
                    else
                    {
                        friendlyCounts[index]++;
                    }
                }
            }
        }
        finally
        {
            frame.UnlockBits(data);
        }

        var candidates = new List<LevelLabelCandidate>();
        CollectCandidates(enemyCounts, Team.Enemy, scanRect, frame.Width, frame.Height, gridWidth, gridHeight, candidates);
        CollectCandidates(friendlyCounts, Team.Friendly, scanRect, frame.Width, frame.Height, gridWidth, gridHeight, candidates);
        return candidates;
    }

    private void CollectCandidates(
        int[] counts,
        Team team,
        Rectangle scanRect,
        int frameWidth,
        int frameHeight,
        int gridWidth,
        int gridHeight,
        List<LevelLabelCandidate> output)
    {
        var visited = new bool[counts.Length];
        int samplesPerBucket = Math.Max(1, _bucketSize / _step);
        int maxBucketHits = Math.Max(1, samplesPerBucket * samplesPerBucket);

        for (int by = 0; by < gridHeight; by++)
        {
            for (int bx = 0; bx < gridWidth; bx++)
            {
                int idx = (by * gridWidth) + bx;
                if (visited[idx] || counts[idx] < _minBucketHits)
                {
                    continue;
                }

                int minX = bx;
                int maxX = bx;
                int minY = by;
                int maxY = by;
                int totalHits = 0;
                int bucketCount = 0;

                var stack = new Stack<int>();
                stack.Push(idx);
                visited[idx] = true;

                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    int cy = current / gridWidth;
                    int cx = current - (cy * gridWidth);
                    int count = counts[current];
                    if (count < _minBucketHits)
                    {
                        continue;
                    }

                    bucketCount++;
                    totalHits += count;
                    minX = Math.Min(minX, cx);
                    maxX = Math.Max(maxX, cx);
                    minY = Math.Min(minY, cy);
                    maxY = Math.Max(maxY, cy);

                    TryPush(cx - 1, cy, gridWidth, gridHeight, counts, visited, stack);
                    TryPush(cx + 1, cy, gridWidth, gridHeight, counts, visited, stack);
                    TryPush(cx, cy - 1, gridWidth, gridHeight, counts, visited, stack);
                    TryPush(cx, cy + 1, gridWidth, gridHeight, counts, visited, stack);
                }

                if (bucketCount == 0)
                {
                    continue;
                }

                int widthPx = (maxX - minX + 1) * _bucketSize;
                int heightPx = (maxY - minY + 1) * _bucketSize;
                if (!LevelLabelSizeFilter.IsValidSize(widthPx, heightPx))
                {
                    continue;
                }

                float centerX = scanRect.Left + ((minX + maxX + 1) * 0.5f * _bucketSize);
                float centerY = scanRect.Top + ((minY + maxY + 1) * 0.5f * _bucketSize);
                float x01 = centerX / frameWidth;
                float y01 = centerY / frameHeight;

                float score = totalHits / (float)(bucketCount * maxBucketHits);
                score = Math.Clamp(score, 0f, 1f);

                output.Add(new LevelLabelCandidate(team, x01, y01, score));
            }
        }
    }

    private static void TryPush(
        int x,
        int y,
        int gridWidth,
        int gridHeight,
        int[] counts,
        bool[] visited,
        Stack<int> stack)
    {
        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
        {
            return;
        }

        int idx = (y * gridWidth) + x;
        if (visited[idx] || counts[idx] == 0)
        {
            return;
        }

        visited[idx] = true;
        stack.Push(idx);
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
