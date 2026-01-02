using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class ElixirEstimatorFixtureTests
{
    [Theory]
    [MemberData(nameof(GetFixturePaths))]
    public void FixtureImages_EstimateElixirWithinTolerance(string path, int expected)
    {
        var settings = new ElixirSettings(
            Roi: new Roi01(0.08f, 0.975f, 0.88f, 0.02f),
            SampleStep: 6,
            PurpleRMin: 120,
            PurpleGMax: 90,
            PurpleBMin: 120,
            PurpleRBMaxDiff: 60,
            SmoothingWindow: 1,
            EmptyBaseline01: 0.08f,
            FullBaseline01: 0.79f
        );

        var estimator = new ElixirEstimator(settings);

        using var original = new Bitmap(path);
        using var frame = Ensure24bpp(original);

        ElixirResult result = estimator.Estimate(frame);

        int min = Math.Max(0, expected - 1);
        int max = Math.Min(10, expected + 1);
        Assert.InRange(result.ElixirInt, min, max);
    }

    public static IEnumerable<object[]> GetFixturePaths()
    {
        string root = FindRepoRoot(AppContext.BaseDirectory);
        string dir = Path.Combine(root, "tests", "WinFormsApp1.Core.Tests", "fixtures", "elixir");
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (string file in Directory.GetFiles(dir, "elixir_*.png"))
        {
            int expected = ParseExpected(file);
            yield return new object[] { file, expected };
        }
    }

    private static int ParseExpected(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int underscore = name.LastIndexOf('_');
        if (underscore < 0 || underscore == name.Length - 1)
        {
            throw new InvalidOperationException($"Invalid fixture filename: {name}");
        }
        string value = name[(underscore + 1)..];
        return int.Parse(value);
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

    private static Bitmap Ensure24bpp(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format24bppRgb)
        {
            return (Bitmap)source.Clone();
        }

        return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format24bppRgb);
    }
}
