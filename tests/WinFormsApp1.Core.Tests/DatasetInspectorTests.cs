using System;
using System.IO;
using CrDatasetInspector;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class DatasetInspectorTests
{
    [Fact]
    public void ReportsSequenceErrorsAndWarnings()
    {
        string root = CreateTempDir();
        try
        {
            string jsonlPath = CreateJsonl(root, new[]
            {
                CreateLine("m1", 100, 1, "frames/a.png", "frames/b.png"),
                CreateLine("m1", 100, 1, "frames/a.png", "frames/b.png"),
                CreateLine("m1", 90, 0, "frames/a.png", "frames/b.png")
            });

            var options = new InspectorOptions(root, "*.jsonl", Path.Combine(root, "_inspect"), false, false);
            var runner = new InspectorRunner(options);
            InspectorResult result = runner.Run();

            Assert.Contains(result.Issues, issue => issue.Reason == "frame_index_same");
            Assert.Contains(result.Issues, issue => issue.Reason == "frame_index_decrease");
            Assert.Contains(result.Issues, issue => issue.JsonlFile == jsonlPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReportsMatchIdChange()
    {
        string root = CreateTempDir();
        try
        {
            CreateJsonl(root, new[]
            {
                CreateLine("m1", 100, 1, "frames/a.png", "frames/b.png"),
                CreateLine("m2", 120, 2, "frames/a.png", "frames/b.png")
            });

            var options = new InspectorOptions(root, "*.jsonl", Path.Combine(root, "_inspect"), false, false);
            var runner = new InspectorRunner(options);
            InspectorResult result = runner.Run();

            Assert.Contains(result.Issues, issue => issue.Reason == "match_id_changed");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AcceptsFramesPathWithBackslash()
    {
        string root = CreateTempDir();
        try
        {
            CreateJsonl(root, new[]
            {
                CreateLine("m1", 100, 1, "frames\\a.png", "frames\\b.png")
            });

            var options = new InspectorOptions(root, "*.jsonl", Path.Combine(root, "_inspect"), false, false);
            var runner = new InspectorRunner(options);
            InspectorResult result = runner.Run();

            Assert.Equal(0, result.ErrorCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void WritesBadRowsJsonlWithLineNumberAndRawLine()
    {
        string root = CreateTempDir();
        try
        {
            string jsonlPath = Path.Combine(root, "broken.jsonl");
            File.WriteAllText(jsonlPath, "not json");

            string outDir = Path.Combine(root, "_inspect");
            int code = Program.Main(new[] { root, "--out", outDir });
            Assert.Equal(1, code);

            string badRows = Path.Combine(outDir, "bad_rows.jsonl");
            string[] lines = File.ReadAllLines(badRows);
            Assert.Single(lines);
            Assert.Contains("\"line_number\"", lines[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"raw_line\"", lines[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not json", lines[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDir()
    {
        string root = Path.Combine(Path.GetTempPath(), $"cr_inspector_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateJsonl(string root, string[] lines)
    {
        string jsonlPath = Path.Combine(root, "match.jsonl");
        File.WriteAllLines(jsonlPath, lines);

        string framesDir = Path.Combine(root, "frames");
        Directory.CreateDirectory(framesDir);
        File.WriteAllBytes(Path.Combine(framesDir, "a.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(framesDir, "b.png"), new byte[] { 1 });

        return jsonlPath;
    }

    private static string CreateLine(string matchId, long elapsedMs, long frameIndex, string prevPath, string currPath)
    {
        string prev = Escape(prevPath);
        string curr = Escape(currPath);
        return $"{{\"matchId\":\"{matchId}\",\"matchElapsedMs\":{elapsedMs},\"frameIndex\":{frameIndex},\"prevFramePath\":\"{prev}\",\"currFramePath\":\"{curr}\"}}";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
