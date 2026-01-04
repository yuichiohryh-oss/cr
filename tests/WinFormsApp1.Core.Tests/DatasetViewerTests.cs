using System;
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

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }
}
