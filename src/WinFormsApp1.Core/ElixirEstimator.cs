using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinFormsApp1.Core;

public sealed class ElixirEstimator : IElixirEstimator
{
    private readonly ElixirSettings _settings;
    private readonly Queue<float> _history;
    private float _historySum;

    public ElixirEstimator(ElixirSettings settings)
    {
        _settings = settings;
        _history = new Queue<float>(Math.Max(1, settings.SmoothingWindow));
        _historySum = 0f;
    }

    public unsafe ElixirResult Estimate(Bitmap frame)
    {
        Rectangle roi = _settings.Roi.ToRectangle(frame.Width, frame.Height);
        if (roi.Width == 0 || roi.Height == 0)
        {
            return new ElixirResult(0f, 0);
        }

        BitmapData data = frame.LockBits(roi, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            int step = Math.Max(1, _settings.SampleStep);
            int samples = 0;
            int hits = 0;

            int y = roi.Height / 2;
            byte* row = (byte*)data.Scan0 + (y * data.Stride);

            for (int x = 0; x < roi.Width; x += step)
            {
                int offset = x * 3;
                int b = row[offset];
                int g = row[offset + 1];
                int r = row[offset + 2];

                if (IsPurple(r, g, b))
                {
                    hits++;
                }
                samples++;
            }

            float filled = samples == 0 ? 0f : (float)hits / samples;
            float normalized = NormalizeFilled(filled);
            float smoothed = ApplySmoothing(normalized);

            int elixir = (int)MathF.Round(smoothed * 10f, MidpointRounding.AwayFromZero);
            elixir = Math.Clamp(elixir, 0, 10);
            return new ElixirResult(smoothed, elixir);
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    private bool IsPurple(int r, int g, int b)
    {
        if (r < _settings.PurpleRMin || b < _settings.PurpleBMin)
        {
            return false;
        }
        if (g > _settings.PurpleGMax)
        {
            return false;
        }
        int rbDiff = Math.Abs(r - b);
        return rbDiff <= _settings.PurpleRBMaxDiff;
    }

    private float ApplySmoothing(float value)
    {
        int window = Math.Max(1, _settings.SmoothingWindow);
        if (window == 1)
        {
            return value;
        }

        _history.Enqueue(value);
        _historySum += value;

        while (_history.Count > window)
        {
            _historySum -= _history.Dequeue();
        }

        return _historySum / _history.Count;
    }

    private float NormalizeFilled(float filled)
    {
        float empty = _settings.EmptyBaseline01;
        float full = _settings.FullBaseline01;
        if (full <= empty + 0.001f)
        {
            return Math.Clamp(filled, 0f, 1f);
        }

        float normalized = (filled - empty) / (full - empty);
        return Math.Clamp(normalized, 0f, 1f);
    }
}
