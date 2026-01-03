using System;
using System.IO;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class DatasetRecorderTests
{
    [Fact]
    public void AppendsJsonlLine()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dataset_{Guid.NewGuid():N}.jsonl");
        try
        {
            var recorder = new DatasetRecorder();
            var state = new StateSnapshot(
                MatchPhase.Early,
                5,
                Array.Empty<SpawnSnapshot>(),
                Array.Empty<SpawnSnapshot>(),
                new[] { new HandCardSnapshot("cannon", 3) });
            var action = new ActionSnapshot("cannon", Lane.Right, 0.6f, 0.8f);
            var sample = new TrainingSample(DateTime.UtcNow, state, action, "match", 1234, 1);

            recorder.Open(path);
            recorder.Append(sample);
            recorder.Close();

            string[] lines = File.ReadAllLines(path);
            Assert.Single(lines);
            Assert.Contains("\"state\"", lines[0], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"action\"", lines[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void DoesNotWriteWhenClosed()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dataset_{Guid.NewGuid():N}.jsonl");
        var recorder = new DatasetRecorder();
        var state = new StateSnapshot(
            MatchPhase.Early,
            5,
            Array.Empty<SpawnSnapshot>(),
            Array.Empty<SpawnSnapshot>(),
            new[] { new HandCardSnapshot("cannon", 3) });
        var action = new ActionSnapshot("cannon", Lane.Right, 0.6f, 0.8f);
        var sample = new TrainingSample(DateTime.UtcNow, state, action, "match", 1234, 1);

        try
        {
            recorder.Append(sample);
            Assert.False(File.Exists(path));
        }
        finally
        {
            recorder.Close();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
