using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CrDatasetInspector;

public static class Program
{
    public static int Main(string[] args)
    {
        if (!TryParseArgs(args, out InspectorOptions options, out string? error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        var runner = new InspectorRunner(options);
        InspectorResult result = runner.Run();

        Directory.CreateDirectory(options.OutDir);
        WriteReports(result, options.OutDir);
        WriteConsoleSummary(result);

        if (result.ErrorCount > 0)
        {
            return 1;
        }

        if (options.Strict && result.WarningCount > 0)
        {
            return 1;
        }

        return 0;
    }

    private static bool TryParseArgs(string[] args, out InspectorOptions options, out string? error)
    {
        options = new InspectorOptions(string.Empty, "*.jsonl", string.Empty, false, false);
        error = null;

        if (args.Length == 0 || Array.Exists(args, arg => arg is "--help" or "-h"))
        {
            error = "datasetRoot is required.";
            return false;
        }

        string datasetRoot = string.Empty;
        string jsonlGlob = "*.jsonl";
        string? outDir = null;
        bool strict = false;
        bool verifyImageLoad = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--jsonl-glob")
            {
                if (!TryGetValue(args, ref i, out jsonlGlob, out error))
                {
                    return false;
                }
            }
            else if (arg == "--out")
            {
                if (!TryGetValue(args, ref i, out string outValue, out error))
                {
                    return false;
                }

                outDir = outValue;
            }
            else if (arg == "--strict")
            {
                strict = true;
            }
            else if (arg == "--verify-image-load")
            {
                verifyImageLoad = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown option: {arg}";
                return false;
            }
            else if (string.IsNullOrWhiteSpace(datasetRoot))
            {
                datasetRoot = arg;
            }
            else
            {
                error = $"Unexpected argument: {arg}";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(datasetRoot))
        {
            error = "datasetRoot is required.";
            return false;
        }

        string fullRoot = Path.GetFullPath(datasetRoot);
        string outputDir = outDir ?? Path.Combine(fullRoot, "_inspect");
        options = new InspectorOptions(fullRoot, jsonlGlob, outputDir, strict, verifyImageLoad);
        return true;
    }

    private static bool TryGetValue(string[] args, ref int index, out string value, out string? error)
    {
        error = null;
        value = string.Empty;
        if (index + 1 >= args.Length)
        {
            error = $"Missing value for {args[index]}";
            return false;
        }

        index++;
        value = args[index];
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"Missing value for {args[index - 1]}";
            return false;
        }

        return true;
    }

    private static void WriteReports(InspectorResult result, string outDir)
    {
        WriteSummary(result, Path.Combine(outDir, "summary.md"));
        WriteReportJson(result, Path.Combine(outDir, "report.json"));
        WriteBadRows(result, Path.Combine(outDir, "bad_rows.jsonl"));
    }

    private static void WriteSummary(InspectorResult result, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("# Dataset Inspector Summary");
        writer.WriteLine();
        writer.WriteLine($"Matches: {result.Matches.Count}");
        writer.WriteLine($"Errors: {result.ErrorCount}");
        writer.WriteLine($"Warnings: {result.WarningCount}");
        writer.WriteLine();

        if (result.Matches.Count > 0)
        {
            writer.WriteLine("## Matches");
            foreach (MatchReport match in result.Matches)
            {
                writer.WriteLine($"- {Path.GetFileName(match.JsonlFile)} rows={match.TotalRows} errors={match.ErrorCount} warnings={match.WarningCount} frame_avg={match.FrameIndexAvgDelta:0.##} frame_max={match.FrameIndexMaxGap} elapsed_avg={match.ElapsedMsAvgDelta:0.##} elapsed_max={match.ElapsedMsMaxGap}");
            }
        }

        var errorFiles = result.Matches.Where(m => m.ErrorCount > 0).Select(m => Path.GetFileName(m.JsonlFile)).Distinct().ToArray();
        if (errorFiles.Length > 0)
        {
            writer.WriteLine();
            writer.WriteLine("## Files With Errors");
            foreach (string file in errorFiles)
            {
                writer.WriteLine($"- {file}");
            }
        }
    }

    private static void WriteReportJson(InspectorResult result, string path)
    {
        var payload = new
        {
            errorCount = result.ErrorCount,
            warningCount = result.WarningCount,
            matches = result.Matches,
            issues = result.Issues
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void WriteBadRows(InspectorResult result, string path)
    {
        using var writer = new StreamWriter(path);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        foreach (InspectorIssue issue in result.Issues)
        {
            var row = new
            {
                jsonl_file = issue.JsonlFile,
                line_number = issue.LineNumber,
                level = issue.Level,
                reason = issue.Reason,
                raw_line = issue.RawLine
            };

            writer.WriteLine(JsonSerializer.Serialize(row, options));
        }
    }

    private static void WriteConsoleSummary(InspectorResult result)
    {
        Console.WriteLine($"Matches: {result.Matches.Count}");
        Console.WriteLine($"Errors: {result.ErrorCount}  Warnings: {result.WarningCount}");

        var errorFiles = result.Matches.Where(m => m.ErrorCount > 0).Select(m => Path.GetFileName(m.JsonlFile)).Distinct().ToArray();
        if (errorFiles.Length > 0)
        {
            Console.WriteLine("Files with errors:");
            foreach (string file in errorFiles)
            {
                Console.WriteLine($"- {file}");
            }
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/CrDatasetInspector -- <datasetRoot> [--jsonl-glob \"*.jsonl\"] [--out <outDir>] [--strict] [--verify-image-load]");
    }
}
