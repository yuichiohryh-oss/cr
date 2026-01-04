namespace CrDatasetViewer;

public sealed class ViewerRow
{
    public int LineNumber { get; set; }
    public long FrameIndex { get; set; }
    public long MatchElapsedMs { get; set; }
    public string MatchId { get; set; } = string.Empty;
    public string PrevFramePath { get; set; } = string.Empty;
    public string CurrFramePath { get; set; } = string.Empty;
    public string ActionSummary { get; set; } = string.Empty;
    public double? PlotX01 { get; set; }
    public double? PlotY01 { get; set; }
    public string PlotLabel { get; set; } = string.Empty;
    public FrameCrop FrameCrop { get; set; } = FrameCrop.None;
    public bool IsBad { get; set; }
    public string BadReason { get; set; } = string.Empty;
    public string RawLine { get; set; } = string.Empty;

    public static ViewerRow FromRecord(int lineNumber, ViewerRecord record)
    {
        return new ViewerRow
        {
            LineNumber = lineNumber,
            FrameIndex = record.FrameIndex,
            MatchElapsedMs = record.MatchElapsedMs,
            MatchId = record.MatchId,
            PrevFramePath = record.PrevFramePath,
            CurrFramePath = record.CurrFramePath,
            ActionSummary = record.ActionSummary,
            PlotX01 = record.PlotX01,
            PlotY01 = record.PlotY01,
            PlotLabel = record.PlotLabel,
            FrameCrop = record.FrameCrop
        };
    }

    public static ViewerRow FromParseError(int lineNumber, string rawLine, string reason)
    {
        return new ViewerRow
        {
            LineNumber = lineNumber,
            FrameIndex = -1,
            MatchElapsedMs = 0,
            MatchId = string.Empty,
            PrevFramePath = string.Empty,
            CurrFramePath = string.Empty,
            ActionSummary = string.Empty,
            IsBad = true,
            BadReason = reason,
            RawLine = rawLine
        };
    }
}

public readonly record struct ViewerRecord(
    string MatchId,
    long MatchElapsedMs,
    long FrameIndex,
    string PrevFramePath,
    string CurrFramePath,
    string ActionSummary,
    double? PlotX01,
    double? PlotY01,
    string PlotLabel,
    FrameCrop FrameCrop);

public readonly record struct FrameCrop(int Left, int Top, int Right, int Bottom)
{
    public static FrameCrop None => new(0, 0, 0, 0);
}

public readonly record struct BadRowInfo(
    string JsonlFile,
    int LineNumber,
    string Level,
    string Reason,
    string RawLine);
