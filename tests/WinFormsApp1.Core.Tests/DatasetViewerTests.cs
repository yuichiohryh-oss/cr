using System;
using System.Drawing;
using System.IO;
using CrDatasetViewer;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class DatasetViewerTests
{
    [Fact]
    public void ResolvesFramePathRelativeToMatchDir()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cr_viewer_{Guid.NewGuid():N}");
        try
        {
            string matchDir = Path.Combine(root, "m1");
            Directory.CreateDirectory(Path.Combine(matchDir, "frames"));
            string jsonlPath = Path.Combine(matchDir, "match.jsonl");
            string relative = "frames/a.png";
            string resolved = ViewerHelpers.ResolveFramePath(matchDir, relative);

            string expected = Normalize(Path.Combine(matchDir, "frames", "a.png"));
            Assert.Equal(expected, Normalize(resolved));
            Assert.Equal(expected, Normalize(ViewerHelpers.ResolveFramePath(matchDir, "frames\\a.png")));
            Assert.Equal(matchDir, Path.GetDirectoryName(jsonlPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void NormalizesBackslashPath()
    {
        string normalized = ViewerHelpers.NormalizeRelativePath("frames\\a.png");
        Assert.Equal("frames/a.png", normalized);
    }

    [Fact]
    public void ParsesSnakeAndCamelCaseKeys()
    {
        string json = "{\"match_id\":\"m1\",\"match_elapsed_ms\":120,\"frame_index\":5,\"prev_frame_path\":\"frames/a.png\",\"curr_frame_path\":\"frames/b.png\"}";
        bool ok = ViewerHelpers.TryParseLine(json, out ViewerRecord record, out _);

        Assert.True(ok);
        Assert.Equal("m1", record.MatchId);
        Assert.Equal(120, record.MatchElapsedMs);
        Assert.Equal(5, record.FrameIndex);
        Assert.Equal("frames/a.png", record.PrevFramePath);
        Assert.Equal("frames/b.png", record.CurrFramePath);
    }

    [Fact]
    public void ParsesCamelCaseKeys()
    {
        string json = "{\"matchId\":\"m2\",\"matchElapsedMs\":250,\"frameIndex\":9,\"prevFramePath\":\"frames/p.png\",\"currFramePath\":\"frames/c.png\"}";
        bool ok = ViewerHelpers.TryParseLine(json, out ViewerRecord record, out _);

        Assert.True(ok);
        Assert.Equal("m2", record.MatchId);
        Assert.Equal(250, record.MatchElapsedMs);
        Assert.Equal(9, record.FrameIndex);
        Assert.Equal("frames/p.png", record.PrevFramePath);
        Assert.Equal("frames/c.png", record.CurrFramePath);
    }

    [Fact]
    public void DetectsJsonlFileTarget()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cr_viewer_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            string jsonl = Path.Combine(root, "match.jsonl");
            File.WriteAllText(jsonl, "{}");

            Assert.Equal(OpenTargetKind.JsonlFile, ViewerHelpers.DetectOpenTarget(jsonl));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void DetectsMatchFolderTarget()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cr_viewer_{Guid.NewGuid():N}");
        try
        {
            string matchDir = Path.Combine(root, "m1");
            Directory.CreateDirectory(matchDir);
            Directory.CreateDirectory(Path.Combine(matchDir, "frames"));
            File.WriteAllText(Path.Combine(matchDir, "m1.jsonl"), "{}");

            Assert.Equal(OpenTargetKind.MatchFolder, ViewerHelpers.DetectOpenTarget(matchDir));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void DetectsDatasetRootTarget()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cr_viewer_{Guid.NewGuid():N}");
        try
        {
            string matchDir = Path.Combine(root, "m1");
            Directory.CreateDirectory(matchDir);
            File.WriteAllText(Path.Combine(matchDir, "m1.jsonl"), "{}");

            Assert.Equal(OpenTargetKind.DatasetRoot, ViewerHelpers.DetectOpenTarget(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void DetectsUnknownTarget()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cr_viewer_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            string file = Path.Combine(root, "readme.txt");
            File.WriteAllText(file, "hi");

            Assert.Equal(OpenTargetKind.Unknown, ViewerHelpers.DetectOpenTarget(root));
            Assert.Equal(OpenTargetKind.Unknown, ViewerHelpers.DetectOpenTarget(file));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    [Fact]
    public void TrimsBlackBarsFromLeftAndRight()
    {
        using var source = new Bitmap(100, 20);
        using (var g = Graphics.FromImage(source))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.Black, 0, 0, 10, source.Height);
            g.FillRectangle(Brushes.Black, source.Width - 10, 0, 10, source.Height);
        }

        using var trimmed = ViewerHelpers.CreateDisplayBitmap(source, trimBlackBars: true);
        Assert.Equal(80, trimmed.Width);
        Assert.Equal(source.Height, trimmed.Height);
    }

    [Fact]
    public void DoesNotTrimWhenNoBlackBarsPresent()
    {
        using var source = new Bitmap(100, 20);
        using (var g = Graphics.FromImage(source))
        {
            g.Clear(Color.White);
        }

        using var trimmed = ViewerHelpers.CreateDisplayBitmap(source, trimBlackBars: true);
        Assert.Equal(source.Width, trimmed.Width);
        Assert.Equal(source.Height, trimmed.Height);
    }

}
