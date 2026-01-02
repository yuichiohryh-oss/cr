using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinFormsApp1.Core;

public sealed class CardRecognizer : ICardRecognizer
{
    private readonly CardRecognitionSettings _settings;
    private readonly IReadOnlyList<CardTemplate> _templates;

    public CardRecognizer(CardRecognitionSettings settings, IReadOnlyList<CardTemplate> templates)
    {
        _settings = settings;
        _templates = templates;
    }

    public unsafe HandState Recognize(Bitmap frame)
    {
        if (_templates.Count == 0)
        {
            return HandState.Empty;
        }

        Rectangle roi = _settings.HandRoi.ToRectangle(frame.Width, frame.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return HandState.Empty;
        }

        int slotCount = Math.Max(1, _settings.SlotCount);
        string[] slots = new string[slotCount];

        BitmapData data = frame.LockBits(roi, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int slotWidth = roi.Width / slotCount;
            int slotHeight = roi.Height;
            int pad = (int)MathF.Round(MathF.Min(slotWidth, slotHeight) * _settings.SlotInnerPadding01);

            int sampleSize = Math.Max(4, _settings.SampleSize);
            byte[] sampleBuffer = new byte[sampleSize * sampleSize];

            for (int i = 0; i < slotCount; i++)
            {
                int slotX = i * slotWidth;
                var slotRect = new Rectangle(slotX + pad, pad, Math.Max(1, slotWidth - (pad * 2)), Math.Max(1, slotHeight - (pad * 2)));

                SampleSlot(data, slotRect, sampleSize, sampleBuffer);

                float bestScore = float.NegativeInfinity;
                string bestId = string.Empty;
                foreach (CardTemplate template in _templates)
                {
                    if (template.SampleSize != sampleSize)
                    {
                        continue;
                    }

                    float score = ComputeScore(sampleBuffer, template.Samples);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestId = template.Id;
                    }
                }

                if (bestScore >= _settings.MinScore)
                {
                    slots[i] = bestId;
                }
                else
                {
                    slots[i] = string.Empty;
                }
            }
        }
        finally
        {
            frame.UnlockBits(data);
        }

        return new HandState(slots);
    }

    private static unsafe void SampleSlot(BitmapData data, Rectangle slot, int sampleSize, byte[] buffer)
    {
        int stride = data.Stride;
        for (int y = 0; y < sampleSize; y++)
        {
            float v = (y + 0.5f) / sampleSize;
            int py = slot.Top + Math.Clamp((int)MathF.Round(v * slot.Height) - 1, 0, slot.Height - 1);
            byte* row = (byte*)data.Scan0 + (py * stride);

            for (int x = 0; x < sampleSize; x++)
            {
                float u = (x + 0.5f) / sampleSize;
                int px = slot.Left + Math.Clamp((int)MathF.Round(u * slot.Width) - 1, 0, slot.Width - 1);
                int offset = px * 3;
                byte b = row[offset];
                byte g = row[offset + 1];
                byte r = row[offset + 2];
                byte gray = (byte)((r * 77 + g * 150 + b * 29) >> 8);
                buffer[(y * sampleSize) + x] = gray;
            }
        }
    }

    private static float ComputeScore(byte[] slotSamples, byte[] templateSamples)
    {
        int length = slotSamples.Length;
        int sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += Math.Abs(slotSamples[i] - templateSamples[i]);
        }

        float avgDiff = (float)sum / length;
        float score = 1f - (avgDiff / 255f);
        return score;
    }
}
