using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinFormsApp1.Core;

public sealed class MotionAnalyzer : IMotionAnalyzer
{
    private readonly MotionSettings _settings;

    public MotionAnalyzer(MotionSettings settings)
    {
        _settings = settings;
    }

    public unsafe MotionResult Analyze(Bitmap prev, Bitmap curr)
    {
        if (prev.Width != curr.Width || prev.Height != curr.Height)
        {
            throw new ArgumentException("Frame sizes must match.");
        }

        Rectangle roi = _settings.Roi.ToRectangle(curr.Width, curr.Height);
        if (roi.Width == 0 || roi.Height == 0)
        {
            return new MotionResult(0, 0, false);
        }

        BitmapData prevData = prev.LockBits(roi, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        BitmapData currData = curr.LockBits(roi, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            int step = Math.Max(1, _settings.Step);
            int diffThreshold = _settings.DiffThreshold;
            int stridePrev = prevData.Stride;
            int strideCurr = currData.Stride;

            long left = 0;
            long right = 0;
            int splitX = (int)MathF.Round(roi.Width * _settings.SplitX01);

            for (int y = 0; y < roi.Height; y += step)
            {
                byte* rowPrev = (byte*)prevData.Scan0 + (y * stridePrev);
                byte* rowCurr = (byte*)currData.Scan0 + (y * strideCurr);

                for (int x = 0; x < roi.Width; x += step)
                {
                    int offset = x * 3;
                    int b0 = rowPrev[offset];
                    int g0 = rowPrev[offset + 1];
                    int r0 = rowPrev[offset + 2];

                    int b1 = rowCurr[offset];
                    int g1 = rowCurr[offset + 1];
                    int r1 = rowCurr[offset + 2];

                    int diff = Math.Abs(b1 - b0) + Math.Abs(g1 - g0) + Math.Abs(r1 - r0);
                    if (diff >= diffThreshold)
                    {
                        if (x < splitX)
                        {
                            left++;
                        }
                        else
                        {
                            right++;
                        }
                    }
                }
            }

            long total = left + right;
            bool trigger = total >= _settings.TriggerThreshold;
            return new MotionResult(left, right, trigger);
        }
        finally
        {
            prev.UnlockBits(prevData);
            curr.UnlockBits(currData);
        }
    }
}
