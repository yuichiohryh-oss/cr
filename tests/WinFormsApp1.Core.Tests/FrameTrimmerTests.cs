using System.Drawing;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class FrameTrimmerTests
{
    [Fact]
    public void TrimsLeftRightBlackBars()
    {
        using var bmp = new Bitmap(100, 20);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.FillRectangle(Brushes.White, 10, 0, 80, 20);
        }

        var settings = new FrameTrimSettings(
            true,
            "LeftRight",
            16,
            1,
            0.90f,
            0.20f,
            1);

        bool trimmed = FrameTrimmer.TryDetectLeftRightCrop(bmp, settings, out FrameCrop crop);

        Assert.True(trimmed);
        Assert.Equal(10, crop.Left);
        Assert.Equal(10, crop.Right);
        using var cropped = FrameTrimmer.ApplyCrop(bmp, crop);
        Assert.Equal(80, cropped.Width);
    }

    [Fact]
    public void DoesNotTrimWhenNoBlackBars()
    {
        using var bmp = new Bitmap(100, 20);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
        }

        var settings = new FrameTrimSettings(
            true,
            "LeftRight",
            16,
            1,
            0.90f,
            0.20f,
            1);

        bool trimmed = FrameTrimmer.TryDetectLeftRightCrop(bmp, settings, out FrameCrop crop);

        Assert.False(trimmed);
        Assert.True(crop.IsEmpty);
    }

    [Fact]
    public void RespectsMaxTrimRatio()
    {
        using var bmp = new Bitmap(100, 20);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.FillRectangle(Brushes.White, 30, 0, 70, 20);
        }

        var settings = new FrameTrimSettings(
            true,
            "LeftRight",
            16,
            1,
            0.90f,
            0.20f,
            1);

        bool trimmed = FrameTrimmer.TryDetectLeftRightCrop(bmp, settings, out FrameCrop crop);

        Assert.True(trimmed);
        Assert.Equal(20, crop.Left);
        Assert.Equal(0, crop.Right);
    }

    [Fact]
    public void SkipsTrimWhenContentWidthTooSmall()
    {
        using var bmp = new Bitmap(100, 20);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.FillRectangle(Brushes.White, 10, 0, 80, 20);
        }

        var settings = new FrameTrimSettings(
            true,
            "LeftRight",
            16,
            1,
            0.90f,
            0.20f,
            90);

        bool trimmed = FrameTrimmer.TryDetectLeftRightCrop(bmp, settings, out FrameCrop crop);

        Assert.False(trimmed);
        Assert.True(crop.IsEmpty);
    }
}
