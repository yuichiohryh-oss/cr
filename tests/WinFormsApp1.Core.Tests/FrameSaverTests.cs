using System;
using System.Drawing;
using System.IO;
using WinFormsApp1.Core;
using Xunit;

namespace WinFormsApp1.Core.Tests;

public sealed class FrameSaverTests
{
    [Fact]
    public void SavesPrevAndCurrFrames()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), $"frames_{Guid.NewGuid():N}");
        Directory.CreateDirectory(baseDir);

        try
        {
            using var prev = new Bitmap(2, 2);
            using var curr = new Bitmap(2, 2);
            var saver = new FrameSaver();

            var result = saver.SaveFrames(
                prev,
                curr,
                baseDir,
                "frames",
                "png",
                90,
                0,
                matchElapsedMs: 123,
                frameIndex: 7);

            Assert.NotNull(result);
            string prevPath = Path.Combine(baseDir, result!.Value.PrevPath.Replace('/', Path.DirectorySeparatorChar));
            string currPath = Path.Combine(baseDir, result.Value.CurrPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(prevPath));
            Assert.True(File.Exists(currPath));
        }
        finally
        {
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
    }
}
