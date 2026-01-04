using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace CrDatasetInspector;

public sealed class InspectorRunner
{
    private readonly InspectorOptions _options;

    public InspectorRunner(InspectorOptions options)
    {
        _options = options;
    }

    public InspectorResult Run()
    {
        var result = new InspectorResult();
        foreach (string jsonl in EnumerateJsonlFiles())
        {
            AnalyzeFile(jsonl, result);
        }

        return result;
    }

    private IEnumerable<string> EnumerateJsonlFiles()
    {
        if (!Directory.Exists(_options.DatasetRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(_options.DatasetRoot, _options.JsonlGlob, SearchOption.AllDirectories);
    }

    private void AnalyzeFile(string jsonlFile, InspectorResult result)
    {
        string? jsonlDir = Path.GetDirectoryName(jsonlFile);
        if (jsonlDir == null)
        {
            return;
        }

        string? matchId = null;
        int totalRows = 0;
        int fileErrors = 0;
        int fileWarnings = 0;
        long? lastFrame = null;
        long? lastElapsed = null;
        long frameDeltaSum = 0;
        int frameDeltaCount = 0;
        long frameMaxGap = 0;
        long elapsedDeltaSum = 0;
        int elapsedDeltaCount = 0;
        long elapsedMaxGap = 0;

        int lineNumber = 0;
        foreach (string line in File.ReadLines(jsonlFile))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalRows++;
            if (!TryParseLine(line, out var parsed, out string? reason))
            {
                AddIssue(result, jsonlFile, lineNumber, "error", reason ?? "parse_error", line, ref fileErrors, ref fileWarnings);
                continue;
            }

            if (string.IsNullOrWhiteSpace(parsed.MatchId))
            {
                AddIssue(result, jsonlFile, lineNumber, "error", "missing_match_id", line, ref fileErrors, ref fileWarnings);
            }
            else if (matchId == null)
            {
                matchId = parsed.MatchId;
            }
            else if (!string.Equals(matchId, parsed.MatchId, StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(result, jsonlFile, lineNumber, "error", "match_id_changed", line, ref fileErrors, ref fileWarnings);
            }

            CheckSequence(result, jsonlFile, lineNumber, line, parsed.FrameIndex, parsed.MatchElapsedMs, ref lastFrame, ref lastElapsed, ref frameDeltaSum, ref frameDeltaCount, ref frameMaxGap, ref elapsedDeltaSum, ref elapsedDeltaCount, ref elapsedMaxGap, ref fileErrors, ref fileWarnings);
            CheckFramePath(result, jsonlFile, lineNumber, line, jsonlDir, parsed.PrevFramePath, "prev_frame_path", ref fileErrors, ref fileWarnings);
            CheckFramePath(result, jsonlFile, lineNumber, line, jsonlDir, parsed.CurrFramePath, "curr_frame_path", ref fileErrors, ref fileWarnings);

            if (!string.IsNullOrWhiteSpace(parsed.PrevFramePath) && string.Equals(parsed.PrevFramePath, parsed.CurrFramePath, StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(result, jsonlFile, lineNumber, "warn", "prev_curr_same_path", line, ref fileErrors, ref fileWarnings);
            }
        }

        if (totalRows == 0)
        {
            AddIssue(result, jsonlFile, 0, "error", "empty_jsonl", string.Empty, ref fileErrors, ref fileWarnings);
        }

        result.Matches.Add(new MatchReport(
            jsonlFile,
            matchId ?? string.Empty,
            totalRows,
            fileErrors,
            fileWarnings,
            frameDeltaCount > 0 ? frameDeltaSum / (double)frameDeltaCount : 0,
            frameMaxGap,
            elapsedDeltaCount > 0 ? elapsedDeltaSum / (double)elapsedDeltaCount : 0,
            elapsedMaxGap));

        result.ErrorCount += fileErrors;
        result.WarningCount += fileWarnings;
    }

    private static void AddIssue(
        InspectorResult result,
        string jsonlFile,
        int lineNumber,
        string level,
        string reason,
        string rawLine,
        ref int fileErrors,
        ref int fileWarnings)
    {
        result.Issues.Add(new InspectorIssue(jsonlFile, lineNumber, level, reason, rawLine));
        if (level == "error")
        {
            fileErrors++;
        }
        else
        {
            fileWarnings++;
        }
    }

    private static void CheckSequence(
        InspectorResult result,
        string jsonlFile,
        int lineNumber,
        string rawLine,
        long frameIndex,
        long elapsedMs,
        ref long? lastFrame,
        ref long? lastElapsed,
        ref long frameDeltaSum,
        ref int frameDeltaCount,
        ref long frameMaxGap,
        ref long elapsedDeltaSum,
        ref int elapsedDeltaCount,
        ref long elapsedMaxGap,
        ref int fileErrors,
        ref int fileWarnings)
    {
        if (lastFrame.HasValue)
        {
            long delta = frameIndex - lastFrame.Value;
            if (delta < 0)
            {
                AddIssue(result, jsonlFile, lineNumber, "error", "frame_index_decrease", rawLine, ref fileErrors, ref fileWarnings);
            }
            else if (delta == 0)
            {
                AddIssue(result, jsonlFile, lineNumber, "warn", "frame_index_same", rawLine, ref fileErrors, ref fileWarnings);
            }
            else
            {
                frameDeltaSum += delta;
                frameDeltaCount++;
                frameMaxGap = Math.Max(frameMaxGap, delta);
            }
        }

        if (lastElapsed.HasValue)
        {
            long delta = elapsedMs - lastElapsed.Value;
            if (delta < 0)
            {
                AddIssue(result, jsonlFile, lineNumber, "error", "elapsed_ms_decrease", rawLine, ref fileErrors, ref fileWarnings);
            }
            else if (delta == 0)
            {
                AddIssue(result, jsonlFile, lineNumber, "warn", "elapsed_ms_same", rawLine, ref fileErrors, ref fileWarnings);
            }
            else
            {
                elapsedDeltaSum += delta;
                elapsedDeltaCount++;
                elapsedMaxGap = Math.Max(elapsedMaxGap, delta);
            }
        }

        lastFrame = frameIndex;
        lastElapsed = elapsedMs;
    }

    private void CheckFramePath(
        InspectorResult result,
        string jsonlFile,
        int lineNumber,
        string rawLine,
        string jsonlDir,
        string? path,
        string fieldName,
        ref int fileErrors,
        ref int fileWarnings)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddIssue(result, jsonlFile, lineNumber, "error", $"{fieldName}_missing", rawLine, ref fileErrors, ref fileWarnings);
            return;
        }

        string normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalized))
        {
            AddIssue(result, jsonlFile, lineNumber, "error", $"{fieldName}_absolute", rawLine, ref fileErrors, ref fileWarnings);
            return;
        }

        if (!normalized.StartsWith("frames/", StringComparison.OrdinalIgnoreCase))
        {
            AddIssue(result, jsonlFile, lineNumber, "error", $"{fieldName}_not_frames", rawLine, ref fileErrors, ref fileWarnings);
            return;
        }

        string fullPath = Path.Combine(jsonlDir, normalized);
        if (!File.Exists(fullPath))
        {
            AddIssue(result, jsonlFile, lineNumber, "error", $"{fieldName}_missing_file", rawLine, ref fileErrors, ref fileWarnings);
            return;
        }

        if (_options.VerifyImageLoad)
        {
            try
            {
                using var image = Image.FromFile(fullPath);
            }
            catch
            {
                AddIssue(result, jsonlFile, lineNumber, "error", $"{fieldName}_load_failed", rawLine, ref fileErrors, ref fileWarnings);
            }
        }
    }

    private static bool TryParseLine(string line, out ParsedLine parsed, out string? reason)
    {
        parsed = default;
        reason = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            if (!TryGetString(root, "matchId", out string matchId))
            {
                matchId = string.Empty;
            }

            if (!TryGetLong(root, "matchElapsedMs", out long matchElapsed))
            {
                reason = "missing_match_elapsed_ms";
                return false;
            }

            if (!TryGetLong(root, "frameIndex", out long frameIndex))
            {
                reason = "missing_frame_index";
                return false;
            }

            TryGetString(root, "prevFramePath", out string prevPath);
            TryGetString(root, "currFramePath", out string currPath);

            parsed = new ParsedLine(matchId, matchElapsed, frameIndex, prevPath, currPath);
            return true;
        }
        catch (JsonException ex)
        {
            reason = $"json_parse_error:{ex.Message}";
            return false;
        }
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        if (root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetLong(JsonElement root, string name, out long value)
    {
        if (root.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private readonly record struct ParsedLine(
        string MatchId,
        long MatchElapsedMs,
        long FrameIndex,
        string PrevFramePath,
        string CurrFramePath);
}
