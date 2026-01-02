using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class CardRecognizerTests
{
    [Fact]
    public void Recognize_SyntheticHandMatchesTemplates()
    {
        string root = FindRepoRoot(AppContext.BaseDirectory);
        string dir = Path.Combine(root, "tests", "WinFormsApp1.Core.Tests", "fixtures", "cards");

        var templateIds = new[] { "hog", "cannon", "fireball", "log" };
        var templates = new List<CardTemplate>();
        foreach (string id in templateIds)
        {
            string path = Path.Combine(dir, id + ".png");
            using var bmp = new Bitmap(path);
            using var bmp24 = bmp.PixelFormat == PixelFormat.Format24bppRgb
                ? (Bitmap)bmp.Clone()
                : bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb);
            templates.Add(CardTemplate.FromBitmap(id, bmp24, 24));
        }

        using var frame = new Bitmap(400, 100, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(frame))
        {
            g.Clear(Color.Black);
            for (int i = 0; i < templateIds.Length; i++)
            {
                using var cardBmp = new Bitmap(Path.Combine(dir, templateIds[i] + ".png"));
                var dest = new Rectangle(i * 100, 0, 100, 100);
                g.DrawImage(cardBmp, dest);
            }
        }

        var settings = new CardRecognitionSettings(
            HandRoi: new Roi01(0f, 0f, 1f, 1f),
            SlotCount: 4,
            SlotInnerPadding01: 0f,
            SampleSize: 24,
            MinScore: 0.65f
        );

        var recognizer = new CardRecognizer(settings, templates);
        HandState hand = recognizer.Recognize(frame);

        Assert.Equal(templateIds.Length, hand.Slots.Length);
        Assert.Equal("hog", hand.GetSlot(0));
        Assert.Equal("cannon", hand.GetSlot(1));
        Assert.Equal("fireball", hand.GetSlot(2));
        Assert.Equal("log", hand.GetSlot(3));
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WinFormsApp1.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
