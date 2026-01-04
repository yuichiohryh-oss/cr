using System;
using System.Drawing;

namespace WinFormsApp1.Core;

public readonly record struct FrameCrop(int Left, int Top, int Right, int Bottom)
{
    public static FrameCrop None => new(0, 0, 0, 0);

    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}

public readonly record struct FrameTrimSettings(
    bool Enabled,
    string Mode,
    int BlackThreshold,
    int BlackSampleStride,
    float BlackMinRatio,
    float MaxTrimRatio,
    int MinContentWidth)
{
    public static FrameTrimSettings Disabled => new(
        false,
        "LeftRight",
        16,
        8,
        0.90f,
        0.20f,
        200);
}

public static class FrameTrimmer
{
    public static bool TryDetectLeftRightCrop(Bitmap bitmap, FrameTrimSettings settings, out FrameCrop crop)
    {
        crop = FrameCrop.None;
        if (!settings.Enabled || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return false;
        }

        if (!string.Equals(settings.Mode, "LeftRight", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int maxTrim = (int)(bitmap.Width * settings.MaxTrimRatio);
        if (maxTrim <= 0)
        {
            return false;
        }

        maxTrim = Math.Min(maxTrim, bitmap.Width / 2);
        int stride = Math.Max(1, settings.BlackSampleStride);

        int left = 0;
        for (int x = 0; x < maxTrim; x++)
        {
            if (IsBlackColumn(bitmap, x, settings.BlackThreshold, stride, settings.BlackMinRatio))
            {
                left++;
            }
            else
            {
                break;
            }
        }

        int right = 0;
        for (int x = bitmap.Width - 1; x >= bitmap.Width - maxTrim; x--)
        {
            if (IsBlackColumn(bitmap, x, settings.BlackThreshold, stride, settings.BlackMinRatio))
            {
                right++;
            }
            else
            {
                break;
            }
        }

        if (left == 0 && right == 0)
        {
            return false;
        }

        int contentWidth = bitmap.Width - left - right;
        if (contentWidth < settings.MinContentWidth)
        {
            return false;
        }

        if (contentWidth <= 0)
        {
            return false;
        }

        crop = new FrameCrop(left, 0, right, 0);
        return !crop.IsEmpty;
    }

    public static Bitmap ApplyCrop(Bitmap bitmap, FrameCrop crop)
    {
        if (crop.IsEmpty)
        {
            return (Bitmap)bitmap.Clone();
        }

        int width = bitmap.Width - crop.Left - crop.Right;
        int height = bitmap.Height - crop.Top - crop.Bottom;
        var rect = new Rectangle(crop.Left, crop.Top, width, height);
        return bitmap.Clone(rect, bitmap.PixelFormat);
    }

    private static bool IsBlackColumn(Bitmap bitmap, int x, int threshold, int stride, float minRatio)
    {
        int blackCount = 0;
        int totalCount = 0;

        for (int y = 0; y < bitmap.Height; y += stride)
        {
            Color pixel = bitmap.GetPixel(x, y);
            if (pixel.R <= threshold && pixel.G <= threshold && pixel.B <= threshold)
            {
                blackCount++;
            }

            totalCount++;
        }

        if (totalCount == 0)
        {
            return false;
        }

        float ratio = blackCount / (float)totalCount;
        return ratio >= minRatio;
    }
}
