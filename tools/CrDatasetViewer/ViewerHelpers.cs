using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CrDatasetViewer;

public enum OpenTargetKind
{
    Unknown = 0,
    DatasetRoot,
    MatchFolder,
    JsonlFile
}

public static class ViewerHelpers
{
    private const int BlackThreshold = 16;
    private const int MinTrimWidth = 4;

    public static OpenTargetKind DetectOpenTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return OpenTargetKind.Unknown;
        }

        if (File.Exists(path))
        {
            return Path.GetExtension(path).Equals(".jsonl", StringComparison.OrdinalIgnoreCase)
                ? OpenTargetKind.JsonlFile
                : OpenTargetKind.Unknown;
        }

        if (!Directory.Exists(path))
        {
            return OpenTargetKind.Unknown;
        }

        if (Directory.EnumerateFiles(path, "*.jsonl", SearchOption.TopDirectoryOnly).Any())
        {
            return OpenTargetKind.MatchFolder;
        }

        foreach (string dir in Directory.EnumerateDirectories(path))
        {
            if (Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly).Any())
            {
                return OpenTargetKind.DatasetRoot;
            }
        }

        return OpenTargetKind.Unknown;
    }

    public static string NormalizeRelativePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
    }

    public static string ResolveFramePath(string matchDir, string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        return Path.Combine(matchDir, normalized);
    }

    public static Bitmap CreateDisplayBitmap(Bitmap source, bool trimBlackBars)
    {
        if (!trimBlackBars)
        {
            return new Bitmap(source);
        }

        int leftTrim = CountLeadingBlackColumns(source, fromLeft: true);
        int rightTrim = CountLeadingBlackColumns(source, fromLeft: false);

        if (leftTrim == 0 && rightTrim == 0)
        {
            return new Bitmap(source);
        }

        int width = source.Width - leftTrim - rightTrim;
        if (width < 1)
        {
            return new Bitmap(source);
        }

        var rect = new Rectangle(leftTrim, 0, width, source.Height);
        return source.Clone(rect, source.PixelFormat);
    }

    public static bool TryParseLine(string line, out ViewerRecord record, out string? reason)
    {
        record = default;
        reason = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            TryGetStringAny(root, out string matchId, "matchId", "match_id");
            if (!TryGetLongAny(root, out long matchElapsed, "matchElapsedMs", "match_elapsed_ms"))
            {
                reason = "missing_match_elapsed_ms";
                return false;
            }

            if (!TryGetLongAny(root, out long frameIndex, "frameIndex", "frame_index"))
            {
                reason = "missing_frame_index";
                return false;
            }

            TryGetStringAny(root, out string prevPath, "prevFramePath", "prev_frame_path");
            TryGetStringAny(root, out string currPath, "currFramePath", "curr_frame_path");

            string actionSummary = BuildActionSummary(root);
            record = new ViewerRecord(matchId, matchElapsed, frameIndex, prevPath, currPath, actionSummary);
            return true;
        }
        catch (JsonException ex)
        {
            reason = $"json_parse_error:{ex.Message}";
            return false;
        }
    }

    public static bool TryParseBadRow(string line, out BadRowInfo info)
    {
        info = default;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            TryGetStringAny(root, out string jsonlFile, "jsonl_file", "jsonlFile");
            TryGetStringAny(root, out string level, "level");
            TryGetStringAny(root, out string reason, "reason");
            TryGetStringAny(root, out string rawLine, "raw_line", "rawLine");
            TryGetLongAny(root, out long lineNumber, "line_number", "lineNumber");

            info = new BadRowInfo(jsonlFile, (int)lineNumber, level, reason, rawLine);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildActionSummary(JsonElement root)
    {
        if (!root.TryGetProperty("action", out JsonElement action) || action.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        TryGetStringAny(action, out string cardId, "cardId", "card_id");
        string lane = GetStringOrNumber(action, "lane");
        double? x01 = GetDoubleAny(action, "x01", "x");
        double? y01 = GetDoubleAny(action, "y01", "y");

        var parts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(cardId))
        {
            parts.Add(cardId);
        }

        if (!string.IsNullOrWhiteSpace(lane))
        {
            parts.Add($"lane:{lane}");
        }

        if (x01.HasValue && y01.HasValue)
        {
            parts.Add($"pos:{x01:0.###},{y01:0.###}");
        }

        return string.Join(" ", parts);
    }

    private static bool TryGetStringAny(JsonElement root, out string value, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? string.Empty;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetLongAny(JsonElement root, out long value, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (root.TryGetProperty(name, out JsonElement element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value))
                {
                    return true;
                }

                if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private static string GetStringOrNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement element))
        {
            return string.Empty;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetRawText();
        }

        return string.Empty;
    }

    private static double? GetDoubleAny(JsonElement root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (!root.TryGetProperty(name, out JsonElement element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double value))
            {
                return value;
            }

            if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int CountLeadingBlackColumns(Bitmap source, bool fromLeft)
    {
        int width = source.Width;
        int height = source.Height;
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        int count = 0;
        int start = fromLeft ? 0 : width - 1;
        int step = fromLeft ? 1 : -1;

        for (int x = start; x >= 0 && x < width; x += step)
        {
            if (!IsMostlyBlackColumn(source, x))
            {
                break;
            }

            count++;
        }

        return count >= MinTrimWidth ? count : 0;
    }

    private static bool IsMostlyBlackColumn(Bitmap source, int x)
    {
        int height = source.Height;
        int sampleCount = Math.Clamp(height / 8, 3, 24);
        int step = Math.Max(1, height / sampleCount);

        for (int y = 0; y < height; y += step)
        {
            Color color = source.GetPixel(x, y);
            if (!IsNearlyBlack(color))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNearlyBlack(Color color)
    {
        return color.R <= BlackThreshold && color.G <= BlackThreshold && color.B <= BlackThreshold;
    }
}
