using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using WinFormsApp1.Core;

namespace WinFormsApp1.Infrastructure;

public readonly record struct SpellDetectionResult(float X01, float Y01, Rectangle Bounds, int Area, float Aspect);

public sealed class SpellPlacementDetector
{
    public unsafe bool TryDetectLog(Bitmap prev, Bitmap curr, SpellDetectionSettings settings, out SpellDetectionResult result)
    {
        result = default;
        if (prev.Width != curr.Width || prev.Height != curr.Height)
        {
            return false;
        }

        var fullRect = new Rectangle(0, 0, curr.Width, curr.Height);
        Rectangle roiRect = ToPixelRect(settings.Roi, curr.Width, curr.Height);
        roiRect = Rectangle.Intersect(fullRect, roiRect);
        if (roiRect.Width <= 0 || roiRect.Height <= 0)
        {
            return false;
        }

        int roiWidth = roiRect.Width;
        int roiHeight = roiRect.Height;
        int roiSize = roiWidth * roiHeight;
        var mask = new byte[roiSize];

        BitmapData prevData = prev.LockBits(fullRect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        BitmapData currData = curr.LockBits(fullRect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int prevStride = prevData.Stride;
            int currStride = currData.Stride;
            for (int y = 0; y < roiHeight; y++)
            {
                byte* prevRow = (byte*)prevData.Scan0 + ((y + roiRect.Top) * prevStride);
                byte* currRow = (byte*)currData.Scan0 + ((y + roiRect.Top) * currStride);
                int baseIndex = y * roiWidth;
                for (int x = 0; x < roiWidth; x++)
                {
                    int offset = (x + roiRect.Left) * 3;
                    int pb = prevRow[offset];
                    int pg = prevRow[offset + 1];
                    int pr = prevRow[offset + 2];
                    int cb = currRow[offset];
                    int cg = currRow[offset + 1];
                    int cr = currRow[offset + 2];
                    int diff = (Math.Abs(cr - pr) + Math.Abs(cg - pg) + Math.Abs(cb - pb)) / 3;
                    if (diff >= settings.DiffThreshold)
                    {
                        mask[baseIndex + x] = 1;
                    }
                }
            }
        }
        finally
        {
            prev.UnlockBits(prevData);
            curr.UnlockBits(currData);
        }

        return TryFindLargestLog(mask, roiRect, curr.Width, curr.Height, settings, out result);
    }

    private static bool TryFindLargestLog(
        byte[] mask,
        Rectangle roiRect,
        int frameWidth,
        int frameHeight,
        SpellDetectionSettings settings,
        out SpellDetectionResult result)
    {
        int roiWidth = roiRect.Width;
        int roiHeight = roiRect.Height;
        var visited = new bool[mask.Length];
        int bestArea = 0;
        Rectangle bestBounds = Rectangle.Empty;

        for (int y = 0; y < roiHeight; y++)
        {
            int rowIndex = y * roiWidth;
            for (int x = 0; x < roiWidth; x++)
            {
                int idx = rowIndex + x;
                if (visited[idx] || mask[idx] == 0)
                {
                    continue;
                }

                int minX = x;
                int maxX = x;
                int minY = y;
                int maxY = y;
                int area = 0;

                var stack = new Stack<int>();
                stack.Push(idx);
                visited[idx] = true;

                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    int cy = current / roiWidth;
                    int cx = current - (cy * roiWidth);
                    area++;
                    minX = Math.Min(minX, cx);
                    maxX = Math.Max(maxX, cx);
                    minY = Math.Min(minY, cy);
                    maxY = Math.Max(maxY, cy);

                    TryPush(mask, visited, roiWidth, roiHeight, cx - 1, cy, stack);
                    TryPush(mask, visited, roiWidth, roiHeight, cx + 1, cy, stack);
                    TryPush(mask, visited, roiWidth, roiHeight, cx, cy - 1, stack);
                    TryPush(mask, visited, roiWidth, roiHeight, cx, cy + 1, stack);
                }

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (!SpellLogFilter.IsLogCandidate(width, height, area, settings.MinArea, settings.MaxArea, settings.MinAspect))
                {
                    continue;
                }

                if (area > bestArea)
                {
                    bestArea = area;
                    bestBounds = new Rectangle(minX + roiRect.Left, minY + roiRect.Top, width, height);
                }
            }
        }

        if (bestArea <= 0)
        {
            result = default;
            return false;
        }

        float cx01 = (bestBounds.Left + (bestBounds.Width / 2f)) / frameWidth;
        float cy01 = (bestBounds.Top + (bestBounds.Height / 2f)) / frameHeight;
        float aspect = bestBounds.Width / (float)bestBounds.Height;
        result = new SpellDetectionResult(cx01, cy01, bestBounds, bestArea, aspect);
        return true;
    }

    private static void TryPush(byte[] mask, bool[] visited, int width, int height, int x, int y, Stack<int> stack)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        int idx = (y * width) + x;
        if (visited[idx] || mask[idx] == 0)
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
