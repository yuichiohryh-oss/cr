using System;
using System.IO;

namespace WinFormsApp1.Core;

public static class FramePathNormalizer
{
    public static string NormalizeToFramesRelative(string matchDir, string framePath, string? framesDirName = null)
    {
        if (string.IsNullOrWhiteSpace(matchDir) || string.IsNullOrWhiteSpace(framePath))
        {
            return string.Empty;
        }

        string dirName = string.IsNullOrWhiteSpace(framesDirName) ? "frames" : framesDirName;
        string fullPath = Path.IsPathRooted(framePath)
            ? Path.GetFullPath(framePath)
            : Path.GetFullPath(Path.Combine(matchDir, framePath));

        string relative = Path.GetRelativePath(matchDir, fullPath);
        string normalized = relative.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized == ".." || normalized.StartsWith("../", StringComparison.Ordinal))
        {
            return Path.Combine(dirName, Path.GetFileName(fullPath)).Replace('\\', '/');
        }

        if (normalized.StartsWith(dirName + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        string marker = "/" + dirName + "/";
        int index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return normalized[(index + 1)..];
        }

        return Path.Combine(dirName, Path.GetFileName(fullPath)).Replace('\\', '/');
    }
}
