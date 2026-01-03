using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using WinFormsApp1.Core;

namespace WinFormsApp1.Infrastructure;

public sealed class FireballPlacementDetector
{
    public unsafe bool TryDetect(Bitmap frame, SpellDetectionSettings settings, out SpellDetectionResult result)
    {
        result = default;

        var fullRect = new Rectangle(0, 0, frame.Width, frame.Height);
        Rectangle roiRect = ToPixelRect(settings.Roi, frame.Width, frame.Height);
        roiRect = Rectangle.Intersect(fullRect, roiRect);
        if (roiRect.Width <= 0 || roiRect.Height <= 0)
        {
            return false;
        }

        int roiWidth = roiRect.Width;
        int roiHeight = roiRect.Height;
        int roiSize = roiWidth * roiHeight;
        var mask = new byte[roiSize];

        BitmapData data = frame.LockBits(fullRect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            for (int y = 0; y < roiHeight; y++)
            {
                byte* row = (byte*)data.Scan0 + ((y + roiRect.Top) * stride);
                int baseIndex = y * roiWidth;
                for (int x = 0; x < roiWidth; x++)
                {
                    int offset = (x + roiRect.Left) * 3;
                    int b = row[offset];
                    int g = row[offset + 1];
                    int r = row[offset + 2];
                    int gray = (r + g + b) / 3;
                    if (gray >= settings.Fireball.WhiteThreshold)
                    {
                        mask[baseIndex + x] = 1;
                    }
                }
            }
        }
        finally
        {
            frame.UnlockBits(data);
        }

        return TryFindLargestFireball(mask, roiRect, frame.Width, frame.Height, settings.Fireball, out result);
    }

    private static bool TryFindLargestFireball(
        byte[] mask,
        Rectangle roiRect,
        int frameWidth,
        int frameHeight,
        FireballDetectionSettings settings,
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
                if (!FireballFilter.IsFireballCandidate(width, height, area, settings.MinArea, settings.MaxArea, settings.MinAspect, settings.MaxAspect))
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
