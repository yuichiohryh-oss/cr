using System;
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
    public static string FormatElapsedStopwatch(long elapsedMs)
    {
        if (elapsedMs < 0)
        {
            elapsedMs = 0;
        }

        TimeSpan span = TimeSpan.FromMilliseconds(elapsedMs);
        long minutes = (long)span.TotalMinutes;
        int seconds = span.Seconds;
        int millis = span.Milliseconds;
        return $"{minutes}:{seconds:00}.{millis:000}";
    }

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
}
