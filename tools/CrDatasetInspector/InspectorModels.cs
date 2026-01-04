using System.Collections.Generic;

namespace CrDatasetInspector;

public sealed record InspectorOptions(
    string DatasetRoot,
    string JsonlGlob,
    string OutDir,
    bool Strict,
    bool VerifyImageLoad);

public sealed record InspectorIssue(
    string JsonlFile,
    int LineNumber,
    string Level,
    string Reason,
    string RawLine);

public sealed record MatchReport(
    string JsonlFile,
    string MatchId,
    int TotalRows,
    int ErrorCount,
    int WarningCount,
    double FrameIndexAvgDelta,
    long FrameIndexMaxGap,
    double ElapsedMsAvgDelta,
    long ElapsedMsMaxGap);

public sealed class InspectorResult
{
    public List<MatchReport> Matches { get; } = new();
    public List<InspectorIssue> Issues { get; } = new();

    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}
