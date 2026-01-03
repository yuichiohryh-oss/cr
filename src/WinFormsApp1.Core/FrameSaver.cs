using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace WinFormsApp1.Core;

public sealed class FrameSaver
{
    public (string PrevPath, string CurrPath)? SaveFrames(
        Bitmap prev,
        Bitmap curr,
        string baseDir,
        string framesDirName,
        string imageFormat,
        int jpegQuality,
        int maxWidth,
        long matchElapsedMs,
        long frameIndex)
    {
        if (prev == null || curr == null)
        {
            return null;
        }

        string dirName = string.IsNullOrWhiteSpace(framesDirName) ? "frames" : framesDirName;
        string framesDir = Path.Combine(baseDir, dirName);
        Directory.CreateDirectory(framesDir);

        string extension = NormalizeExtension(imageFormat);
        string prefix = $"{matchElapsedMs:00000000}_{frameIndex:00000}";
        string prevName = $"{prefix}_prev.{extension}";
        string currName = $"{prefix}_curr.{extension}";

        string prevPath = Path.Combine(framesDir, prevName);
        string currPath = Path.Combine(framesDir, currName);

        using (Bitmap prevToSave = ResizeIfNeeded(prev, maxWidth))
        using (Bitmap currToSave = ResizeIfNeeded(curr, maxWidth))
        {
            SaveImage(prevToSave, prevPath, extension, jpegQuality);
            SaveImage(currToSave, currPath, extension, jpegQuality);
        }

        string relPrev = Path.Combine(dirName, prevName).Replace('\\', '/');
        string relCurr = Path.Combine(dirName, currName).Replace('\\', '/');
        return (relPrev, relCurr);
    }

    private static string NormalizeExtension(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "png";
        }

        string value = format.Trim().TrimStart('.').ToLowerInvariant();
        return value == "jpg" ? "jpeg" : value;
    }

    private static Bitmap ResizeIfNeeded(Bitmap source, int maxWidth)
    {
        if (maxWidth <= 0 || source.Width <= maxWidth)
        {
            return (Bitmap)source.Clone();
        }

        float scale = maxWidth / (float)source.Width;
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = new Bitmap(maxWidth, height, source.PixelFormat);
        using (Graphics g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            g.DrawImage(source, 0, 0, maxWidth, height);
        }

        return resized;
    }

    private static void SaveImage(Bitmap bitmap, string path, string extension, int jpegQuality)
    {
        if (extension == "jpeg")
        {
            ImageCodecInfo? codec = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.MimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase));
            if (codec == null)
            {
                bitmap.Save(path, ImageFormat.Jpeg);
                return;
            }

            using var parameters = new EncoderParameters(1);
            long quality = Math.Clamp(jpegQuality, 0, 100);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bitmap.Save(path, codec, parameters);
            return;
        }

        bitmap.Save(path, ImageFormat.Png);
    }
}
