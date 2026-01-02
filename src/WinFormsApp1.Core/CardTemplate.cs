using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinFormsApp1.Core;

public sealed class CardTemplate
{
    public string Id { get; }
    public int SampleSize { get; }
    public byte[] Samples { get; }

    private CardTemplate(string id, int sampleSize, byte[] samples)
    {
        Id = id;
        SampleSize = sampleSize;
        Samples = samples;
    }

    public static CardTemplate FromBitmap(string id, Bitmap bitmap, int sampleSize)
    {
        if (sampleSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleSize));
        }

        byte[] samples = new byte[sampleSize * sampleSize];
        SampleTemplate(bitmap, sampleSize, samples);
        return new CardTemplate(id, sampleSize, samples);
    }

    private static unsafe void SampleTemplate(Bitmap bitmap, int sampleSize, byte[] samples)
    {
        Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            for (int y = 0; y < sampleSize; y++)
            {
                float v = (y + 0.5f) / sampleSize;
                int py = Math.Clamp((int)MathF.Round(v * rect.Height) - 1, 0, rect.Height - 1);
                byte* row = (byte*)data.Scan0 + (py * data.Stride);

                for (int x = 0; x < sampleSize; x++)
                {
                    float u = (x + 0.5f) / sampleSize;
                    int px = Math.Clamp((int)MathF.Round(u * rect.Width) - 1, 0, rect.Width - 1);
                    int offset = px * 3;
                    byte b = row[offset];
                    byte g = row[offset + 1];
                    byte r = row[offset + 2];
                    byte gray = (byte)((r * 77 + g * 150 + b * 29) >> 8);
                    samples[(y * sampleSize) + x] = gray;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
