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

    public static bool TryMapNormalizedPointToImagePoint(
        double x01,
        double y01,
        int imageWidth,
        int imageHeight,
        FrameCrop crop,
        out float imageX,
        out float imageY)
    {
        imageX = 0f;
        imageY = 0f;

        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return false;
        }

        double w0 = imageWidth + crop.Left + crop.Right;
        double h0 = imageHeight + crop.Top + crop.Bottom;
        if (w0 <= 0 || h0 <= 0)
        {
            return false;
        }

        double px0 = x01 * w0;
        double py0 = y01 * h0;
        double px1 = px0 - crop.Left;
        double py1 = py0 - crop.Top;

        imageX = (float)px1;
        imageY = (float)py1;
        return true;
    }

    public static bool TryMapImagePointToControlPoint(
        int imageWidth,
        int imageHeight,
        int controlWidth,
        int controlHeight,
        float imageX,
        float imageY,
        out float controlX,
        out float controlY)
    {
        controlX = 0f;
        controlY = 0f;

        if (imageWidth <= 0 || imageHeight <= 0 || controlWidth <= 0 || controlHeight <= 0)
        {
            return false;
        }

        float imageAspect = imageWidth / (float)imageHeight;
        float controlAspect = controlWidth / (float)controlHeight;

        float displayWidth;
        float displayHeight;
        float offsetX;
        float offsetY;

        if (controlAspect > imageAspect)
        {
            displayHeight = controlHeight;
            displayWidth = displayHeight * imageAspect;
            offsetX = (controlWidth - displayWidth) / 2f;
            offsetY = 0f;
        }
        else
        {
            displayWidth = controlWidth;
            displayHeight = displayWidth / imageAspect;
            offsetX = 0f;
            offsetY = (controlHeight - displayHeight) / 2f;
        }

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        controlX = offsetX + (imageX * scaleX);
        controlY = offsetY + (imageY * scaleY);
        return true;
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
            FrameCrop crop = TryGetFrameCrop(root, out FrameCrop parsedCrop) ? parsedCrop : FrameCrop.None;
            double? plotX01 = null;
            double? plotY01 = null;
            string plotLabel = string.Empty;

            if (TryGetActionPlot(root, out double actionX01, out double actionY01, out string actionLabel))
            {
                plotX01 = actionX01;
                plotY01 = actionY01;
                plotLabel = actionLabel;
            }
            else if (TryGetSpawnPlot(root, out double spawnX01, out double spawnY01, out string spawnLabel))
            {
                plotX01 = spawnX01;
                plotY01 = spawnY01;
                plotLabel = spawnLabel;
            }

            record = new ViewerRecord(
                matchId,
                matchElapsed,
                frameIndex,
                prevPath,
                currPath,
                actionSummary,
                plotX01,
                plotY01,
                plotLabel,
                crop);
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

    private static bool TryGetActionPlot(JsonElement root, out double x01, out double y01, out string label)
    {
        x01 = 0d;
        y01 = 0d;
        label = string.Empty;

        if (!root.TryGetProperty("action", out JsonElement action) || action.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        double? x = GetDoubleAny(action, "x01", "x");
        double? y = GetDoubleAny(action, "y01", "y");
        if (!x.HasValue || !y.HasValue)
        {
            return false;
        }

        string lane = GetStringOrNumber(action, "lane");
        label = string.IsNullOrWhiteSpace(lane) ? "action" : $"action lane={lane}";
        x01 = x.Value;
        y01 = y.Value;
        return true;
    }

    private static bool TryGetSpawnPlot(JsonElement root, out double x01, out double y01, out string label)
    {
        x01 = 0d;
        y01 = 0d;
        label = string.Empty;

        if (!root.TryGetProperty("state", out JsonElement state) || state.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (TryGetSpawnPlotFromList(state, "friendlySpawns", "friendly", out x01, out y01, out label))
        {
            return true;
        }

        if (TryGetSpawnPlotFromList(state, "enemySpawns", "enemy", out x01, out y01, out label))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetSpawnPlotFromList(
        JsonElement state,
        string propertyName,
        string labelPrefix,
        out double x01,
        out double y01,
        out string label)
    {
        x01 = 0d;
        y01 = 0d;
        label = string.Empty;

        if (!state.TryGetProperty(propertyName, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        for (int i = list.GetArrayLength() - 1; i >= 0; i--)
        {
            JsonElement spawn = list[i];
            if (spawn.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            double? x = GetDoubleAny(spawn, "x01", "x");
            double? y = GetDoubleAny(spawn, "y01", "y");
            if (!x.HasValue || !y.HasValue)
            {
                continue;
            }

            string lane = GetStringOrNumber(spawn, "lane");
            label = string.IsNullOrWhiteSpace(lane)
                ? $"{labelPrefix} spawn"
                : $"{labelPrefix} lane={lane}";
            x01 = x.Value;
            y01 = y.Value;
            return true;
        }

        return false;
    }

    private static bool TryGetFrameCrop(JsonElement root, out FrameCrop crop)
    {
        crop = FrameCrop.None;

        if (!TryGetPropertyAny(root, out JsonElement cropElement, "frame_crop", "frameCrop"))
        {
            return false;
        }

        if (cropElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        bool hasAny = false;
        int left = 0;
        int top = 0;
        int right = 0;
        int bottom = 0;

        if (TryGetIntAny(cropElement, out int value, "left"))
        {
            left = value;
            hasAny = true;
        }

        if (TryGetIntAny(cropElement, out value, "top"))
        {
            top = value;
            hasAny = true;
        }

        if (TryGetIntAny(cropElement, out value, "right"))
        {
            right = value;
            hasAny = true;
        }

        if (TryGetIntAny(cropElement, out value, "bottom"))
        {
            bottom = value;
            hasAny = true;
        }

        if (!hasAny)
        {
            return false;
        }

        crop = new FrameCrop(left, top, right, bottom);
        return true;
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

    private static bool TryGetPropertyAny(JsonElement root, out JsonElement value, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (root.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetIntAny(JsonElement root, out int value, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (root.TryGetProperty(name, out JsonElement element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
                {
                    return true;
                }

                if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double doubleValue))
                {
                    value = (int)Math.Round(doubleValue);
                    return true;
                }

                if (element.ValueKind == JsonValueKind.String
                    && int.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
        }

        value = 0;
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
