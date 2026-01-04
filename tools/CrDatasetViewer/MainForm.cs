using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace CrDatasetViewer;

public sealed class MainForm : Form
{
    private readonly ListBox _jsonlList;
    private readonly DataGridView _grid;
    private readonly PictureBox _prevBox;
    private readonly PictureBox _currBox;
    private readonly Label _prevLabel;
    private readonly Label _currLabel;
    private readonly Label _helpLabel;
    private readonly Label _statusLabel;
    private readonly Button _openRootButton;
    private readonly Button _openMatchDirButton;
    private readonly Button _openJsonlButton;
    private readonly Button _reloadButton;
    private readonly CheckBox _trimBlackBarsCheck;

    private string? _datasetRoot;
    private string? _currentJsonl;
    private BindingList<ViewerRow> _rows = new();
    private List<int> _badRowIndexes = new();
    private Dictionary<int, BadRowInfo> _badRowsByLine = new();
    private Dictionary<string, BadRowInfo> _badRowsByMatchLine = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, BadRowInfo> _badRowsByMatchFrame = new(StringComparer.OrdinalIgnoreCase);
    private Image? _prevImage;
    private Image? _currImage;

    public MainForm()
    {
        Text = "CrDatasetViewer";
        Width = 1400;
        Height = 900;
        KeyPreview = true;
        AllowDrop = true;

        DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] items || items.Length == 0)
            {
                return;
            }

            OpenDroppedPath(items[0]);
        };

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 520
        };

        var leftSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 200
        };

        _jsonlList = new ListBox { Dock = DockStyle.Fill };
        _jsonlList.SelectedIndexChanged += (_, _) =>
        {
            if (_jsonlList.SelectedItem is JsonlEntry entry)
            {
                LoadJsonl(entry.FullPath);
            }
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ShowCellToolTips = true
        };
        _grid.SelectionChanged += (_, _) => UpdateImagesFromSelection();
        _grid.CellFormatting += (_, e) => FormatElapsedCell(e);

        _grid.Columns.Add(CreateTextColumn("LineNumber", "Line"));
        _grid.Columns.Add(CreateTextColumn("FrameIndex", "Frame"));
        _grid.Columns.Add(CreateTextColumn("MatchElapsedMs", "Elapsed"));
        _grid.Columns.Add(CreateTextColumn("MatchId", "Match"));
        _grid.Columns.Add(CreateTextColumn("ActionSummary", "Action"));
        _grid.Columns.Add(CreateTextColumn("PrevFramePath", "Prev"));
        _grid.Columns.Add(CreateTextColumn("CurrFramePath", "Curr"));
        _grid.Columns.Add(CreateTextColumn("IsBad", "Bad"));
        _grid.Columns.Add(CreateTextColumn("BadReason", "Reason"));

        leftSplit.Panel1.Controls.Add(_jsonlList);
        leftSplit.Panel2.Controls.Add(_grid);

        var controlPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6)
        };

        _openRootButton = new Button { Text = "Open Dataset Root..." };
        _openMatchDirButton = new Button { Text = "Open Match Folder..." };
        _openJsonlButton = new Button { Text = "Open JSONL File..." };
        _reloadButton = new Button { Text = "Reload" };
        _trimBlackBarsCheck = new CheckBox { Text = "Trim black bars", AutoSize = true, Checked = true };
        _helpLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Padding = new Padding(6, 6, 6, 0),
            Text = "Tip: Open a Match Folder (dataset/<matchId>) for the fastest review."
        };
        _statusLabel = new Label { AutoSize = true, Padding = new Padding(4, 6, 4, 0), Text = "Opened: (none)" };

        _openRootButton.Click += (_, _) => SelectDatasetRoot();
        _openMatchDirButton.Click += (_, _) => SelectMatchDir();
        _openJsonlButton.Click += (_, _) => SelectJsonl();
        _reloadButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_currentJsonl))
            {
                LoadJsonl(_currentJsonl);
            }
        };
        _trimBlackBarsCheck.CheckedChanged += (_, _) => UpdateImagesFromSelection();

        controlPanel.Controls.Add(_openRootButton);
        controlPanel.Controls.Add(_openMatchDirButton);
        controlPanel.Controls.Add(_openJsonlButton);
        controlPanel.Controls.Add(_reloadButton);
        controlPanel.Controls.Add(_trimBlackBarsCheck);
        controlPanel.Controls.Add(_helpLabel);
        controlPanel.Controls.Add(_statusLabel);

        var leftPanel = new Panel { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(leftSplit);
        leftPanel.Controls.Add(controlPanel);

        mainSplit.Panel1.Controls.Add(leftPanel);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        rightPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _prevLabel = new Label { Text = "Prev", Dock = DockStyle.Fill, AutoSize = true };
        _currLabel = new Label { Text = "Curr", Dock = DockStyle.Fill, AutoSize = true };
        _prevBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
        _currBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };

        rightPanel.Controls.Add(_prevLabel, 0, 0);
        rightPanel.Controls.Add(_currLabel, 1, 0);
        rightPanel.Controls.Add(_prevBox, 0, 1);
        rightPanel.Controls.Add(_currBox, 1, 1);

        mainSplit.Panel2.Controls.Add(rightPanel);

        Controls.Add(mainSplit);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.N)
        {
            JumpBad(1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.P)
        {
            JumpBad(-1);
            e.Handled = true;
        }
        else
        {
            base.OnKeyDown(e);
        }
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string propertyName, string header)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = header,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
        };
    }

    private void SelectDatasetRoot()
    {
        using var dialog = new FolderBrowserDialog();
        string? initial = GetExistingDirectory(ViewerSettings.Default.LastDatasetRoot);
        if (!string.IsNullOrWhiteSpace(initial))
        {
            dialog.SelectedPath = initial;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenDatasetRoot(dialog.SelectedPath);
        }
    }

    private void SelectMatchDir()
    {
        using var dialog = new FolderBrowserDialog();
        string? initial = GetExistingDirectory(ViewerSettings.Default.LastMatchFolder)
            ?? GetExistingDirectory(ViewerSettings.Default.LastDatasetRoot);
        if (!string.IsNullOrWhiteSpace(initial))
        {
            dialog.SelectedPath = initial;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenMatchFolder(dialog.SelectedPath);
        }
    }

    private void SelectJsonl()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSONL (*.jsonl)|*.jsonl|All files (*.*)|*.*"
        };
        string? initialDir = GetInitialJsonlDirectory();
        if (!string.IsNullOrWhiteSpace(initialDir))
        {
            dialog.InitialDirectory = initialDir;
        }
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenJsonlFile(dialog.FileName);
        }
    }

    private void LoadJsonlListFromRoot(string root)
    {
        _jsonlList.Items.Clear();
        foreach (string jsonl in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
        {
            string display = Path.GetRelativePath(root, jsonl);
            _jsonlList.Items.Add(new JsonlEntry(jsonl, display));
        }

        if (_jsonlList.Items.Count > 0)
        {
            _jsonlList.SelectedIndex = 0;
        }
    }

    private void LoadJsonl(string path)
    {
        _currentJsonl = path;
        _rows = new BindingList<ViewerRow>();
        LoadBadRows(path);
        _badRowIndexes = new List<int>();

        int lineNumber = 0;
        foreach (string line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!ViewerHelpers.TryParseLine(line, out ViewerRecord record, out string? reason))
            {
                var row = ViewerRow.FromParseError(lineNumber, line, reason ?? "parse_error");
                MarkBadRow(row, lineNumber);
                _rows.Add(row);
                continue;
            }

            var dataRow = ViewerRow.FromRecord(lineNumber, record);
            MarkBadRow(dataRow, lineNumber);
            _rows.Add(dataRow);
        }

        _grid.DataSource = _rows;
        _grid.ClearSelection();
        ApplyBadRowStyle();
        UpdateImagesFromSelection();
    }

    private void MarkBadRow(ViewerRow row, int lineNumber)
    {
        if (_badRowsByLine.TryGetValue(lineNumber, out BadRowInfo info))
        {
            row.IsBad = true;
            row.BadReason = info.Reason;
            return;
        }

        if (string.IsNullOrWhiteSpace(row.MatchId))
        {
            return;
        }

        string lineKey = BuildKey(row.MatchId, lineNumber);
        if (_badRowsByMatchLine.TryGetValue(lineKey, out info))
        {
            row.IsBad = true;
            row.BadReason = info.Reason;
            return;
        }

        if (row.FrameIndex >= 0)
        {
            string frameKey = BuildKey(row.MatchId, row.FrameIndex);
            if (_badRowsByMatchFrame.TryGetValue(frameKey, out info))
            {
                row.IsBad = true;
                row.BadReason = info.Reason;
            }
        }
    }

    private void ApplyBadRowStyle()
    {
        _badRowIndexes.Clear();
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].IsBad)
            {
                _badRowIndexes.Add(i);
                if (i < _grid.Rows.Count)
                {
                    _grid.Rows[i].DefaultCellStyle.BackColor = Color.MistyRose;
                }
            }
        }
    }

    private void LoadBadRows(string jsonlPath)
    {
        _badRowsByLine = new Dictionary<int, BadRowInfo>();
        _badRowsByMatchLine = new Dictionary<string, BadRowInfo>(StringComparer.OrdinalIgnoreCase);
        _badRowsByMatchFrame = new Dictionary<string, BadRowInfo>(StringComparer.OrdinalIgnoreCase);

        string? matchDir = Path.GetDirectoryName(jsonlPath);
        string? datasetRoot = _datasetRoot ?? (matchDir != null ? Directory.GetParent(matchDir)?.FullName : null);
        if (datasetRoot == null)
        {
            return;
        }

        string badRowsPath = Path.Combine(datasetRoot, "_inspect", "bad_rows.jsonl");
        if (!File.Exists(badRowsPath))
        {
            return;
        }

        string jsonlFull = NormalizePathForCompare(jsonlPath, datasetRoot);
        foreach (string line in File.ReadLines(badRowsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!ViewerHelpers.TryParseBadRow(line, out BadRowInfo info))
            {
                continue;
            }

            bool sameJsonl = IsSameJsonl(info.JsonlFile, jsonlFull, datasetRoot);
            if (sameJsonl && !_badRowsByLine.ContainsKey(info.LineNumber))
            {
                _badRowsByLine[info.LineNumber] = info;
            }

            if (ViewerHelpers.TryParseLine(info.RawLine, out ViewerRecord record, out _))
            {
                if (!string.IsNullOrWhiteSpace(record.MatchId))
                {
                    if (info.LineNumber > 0)
                    {
                        string lineKey = BuildKey(record.MatchId, info.LineNumber);
                        if (!_badRowsByMatchLine.ContainsKey(lineKey))
                        {
                            _badRowsByMatchLine[lineKey] = info;
                        }
                    }

                    if (record.FrameIndex >= 0)
                    {
                        string frameKey = BuildKey(record.MatchId, record.FrameIndex);
                        if (!_badRowsByMatchFrame.ContainsKey(frameKey))
                        {
                            _badRowsByMatchFrame[frameKey] = info;
                        }
                    }
                }
            }
        }
    }

    private static bool IsSameJsonl(string jsonlFromBad, string currentJsonlFull, string datasetRoot)
    {
        string candidate = NormalizePathForCompare(jsonlFromBad, datasetRoot);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return string.Equals(candidate, currentJsonlFull, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForCompare(string path, string datasetRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string candidate = path;
        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.Combine(datasetRoot, candidate);
        }

        candidate = Path.GetFullPath(candidate);
        return candidate.Replace('\\', '/');
    }

    private static string BuildKey(string matchId, long number)
    {
        return $"{matchId}|{number}";
    }

    private void UpdateImagesFromSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ViewerRow row)
        {
            ClearImages();
            return;
        }

        LoadFrame(row, row.PrevFramePath, _prevBox, _prevLabel);
        LoadFrame(row, row.CurrFramePath, _currBox, _currLabel);
    }

    private void LoadFrame(ViewerRow row, string? relativePath, PictureBox target, Label label)
    {
        if (string.IsNullOrWhiteSpace(_currentJsonl))
        {
            label.Text = "No JSONL";
            SetImage(target, null);
            return;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            label.Text = "No path";
            SetImage(target, null);
            return;
        }

        string matchDir = Path.GetDirectoryName(_currentJsonl) ?? string.Empty;
        string resolved = ViewerHelpers.ResolveFramePath(matchDir, relativePath);
        label.Text = ViewerHelpers.NormalizeRelativePath(relativePath);

        if (!File.Exists(resolved))
        {
            label.Text = $"Missing: {ViewerHelpers.NormalizeRelativePath(relativePath)}";
            SetImage(target, null);
            return;
        }

        try
        {
            using var img = Image.FromFile(resolved);
            using var baseBmp = new Bitmap(img);
            bool trimBlackBars = _trimBlackBarsCheck.Checked;
            var output = ViewerHelpers.CreateDisplayBitmap(baseBmp, trimBlackBars);

            using var g = Graphics.FromImage(output);
            string overlay = BuildOverlayText(row);
            DrawOverlay(g, overlay);

            SetImage(target, output);
        }
        catch (Exception ex)
        {
            label.Text = $"Load failed: {ex.Message}";
            SetImage(target, null);
        }
    }

    private void SetImage(PictureBox target, Image? image)
    {
        if (target == _prevBox)
        {
            _prevImage?.Dispose();
            _prevImage = image;
        }
        else
        {
            _currImage?.Dispose();
            _currImage = image;
        }

        target.Image = image;
    }

    private void ClearImages()
    {
        _prevLabel.Text = "Prev";
        _currLabel.Text = "Curr";
        SetImage(_prevBox, null);
        SetImage(_currBox, null);
    }

    private static string BuildOverlayText(ViewerRow row)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(row.MatchId))
        {
            sb.Append($"match {row.MatchId} ");
        }

        if (row.MatchElapsedMs > 0)
        {
            sb.Append($"elapsed {row.MatchElapsedMs}ms ");
        }

        if (row.FrameIndex >= 0)
        {
            sb.Append($"frame {row.FrameIndex}");
        }

        if (!string.IsNullOrWhiteSpace(row.ActionSummary))
        {
            sb.AppendLine();
            sb.Append(row.ActionSummary);
        }

        if (!string.IsNullOrWhiteSpace(row.BadReason))
        {
            sb.AppendLine();
            sb.Append($"BAD: {row.BadReason}");
        }

        return sb.ToString();
    }

    private static void DrawOverlay(Graphics g, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        SizeF size = g.MeasureString(text, font);
        var rect = new RectangleF(6, 6, size.Width + 10, size.Height + 8);
        using var bg = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
        using var fg = new SolidBrush(Color.White);
        g.FillRectangle(bg, rect);
        g.DrawString(text, font, fg, new PointF(10, 10));
    }

    private void JumpBad(int direction)
    {
        if (_badRowIndexes.Count == 0 || _grid.CurrentRow == null)
        {
            return;
        }

        int current = _grid.CurrentRow.Index;
        int next = -1;

        if (direction > 0)
        {
            foreach (int index in _badRowIndexes)
            {
                if (index > current)
                {
                    next = index;
                    break;
                }
            }
        }
        else
        {
            for (int i = _badRowIndexes.Count - 1; i >= 0; i--)
            {
                int index = _badRowIndexes[i];
                if (index < current)
                {
                    next = index;
                    break;
                }
            }
        }

        if (next == -1)
        {
            next = direction > 0 ? _badRowIndexes[0] : _badRowIndexes[^1];
        }

        if (next >= 0 && next < _grid.Rows.Count)
        {
            _grid.ClearSelection();
            _grid.Rows[next].Selected = true;
            _grid.CurrentCell = _grid.Rows[next].Cells[0];
        }
    }

    private sealed record JsonlEntry(string FullPath, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private void OpenDroppedPath(string path)
    {
        OpenTargetKind kind = ViewerHelpers.DetectOpenTarget(path);
        switch (kind)
        {
            case OpenTargetKind.JsonlFile:
                OpenJsonlFile(path);
                break;
            case OpenTargetKind.MatchFolder:
                OpenMatchFolder(path);
                break;
            case OpenTargetKind.DatasetRoot:
                OpenDatasetRoot(path);
                break;
            default:
                ShowDropError(path);
                break;
        }
    }

    private void OpenDatasetRoot(string path)
    {
        _datasetRoot = path;
        LoadJsonlListFromRoot(path);
        UpdateStatus(OpenTargetKind.DatasetRoot, path);

        ViewerSettings.Default.LastDatasetRoot = path;
        ViewerSettings.Default.Save();
    }

    private void OpenMatchFolder(string path)
    {
        string? jsonl = Directory.EnumerateFiles(path, "*.jsonl", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(jsonl))
        {
            MessageBox.Show(this, "No JSONL found in the selected match folder.\nPick dataset/<matchId> that contains a *.jsonl file.", "Open Match Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _datasetRoot = Directory.GetParent(path)?.FullName;
        _jsonlList.Items.Clear();
        _jsonlList.Items.Add(new JsonlEntry(jsonl, Path.GetFileName(jsonl)));
        _jsonlList.SelectedIndex = 0;
        UpdateStatus(OpenTargetKind.MatchFolder, path);

        ViewerSettings.Default.LastMatchFolder = path;
        ViewerSettings.Default.LastDatasetRoot = _datasetRoot ?? ViewerSettings.Default.LastDatasetRoot;
        ViewerSettings.Default.LastJsonlFile = jsonl;
        ViewerSettings.Default.Save();
    }

    private void OpenJsonlFile(string path)
    {
        _datasetRoot = Directory.GetParent(path)?.Parent?.FullName;
        _jsonlList.Items.Clear();
        _jsonlList.Items.Add(new JsonlEntry(path, Path.GetFileName(path)));
        _jsonlList.SelectedIndex = 0;
        UpdateStatus(OpenTargetKind.JsonlFile, path);

        ViewerSettings.Default.LastMatchFolder = Directory.GetParent(path)?.FullName ?? ViewerSettings.Default.LastMatchFolder;
        ViewerSettings.Default.LastJsonlFile = path;
        ViewerSettings.Default.LastDatasetRoot = _datasetRoot ?? ViewerSettings.Default.LastDatasetRoot;
        ViewerSettings.Default.Save();
    }

    private void UpdateStatus(OpenTargetKind kind, string path)
    {
        string label = kind switch
        {
            OpenTargetKind.DatasetRoot => "Dataset Root",
            OpenTargetKind.MatchFolder => "Match Folder",
            OpenTargetKind.JsonlFile => "JSONL File",
            _ => "Unknown"
        };
        _statusLabel.Text = $"Opened: {label} — {path}";
    }

    private static string? GetExistingDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? path : null;
    }

    private static string? GetInitialJsonlDirectory()
    {
        string lastJsonl = ViewerSettings.Default.LastJsonlFile;
        if (!string.IsNullOrWhiteSpace(lastJsonl) && File.Exists(lastJsonl))
        {
            return Path.GetDirectoryName(lastJsonl);
        }

        string? match = GetExistingDirectory(ViewerSettings.Default.LastMatchFolder);
        if (!string.IsNullOrWhiteSpace(match))
        {
            return match;
        }

        return GetExistingDirectory(ViewerSettings.Default.LastDatasetRoot);
    }

    private void ShowDropError(string path)
    {
        MessageBox.Show(
            this,
            $"Unsupported drop target:\n{path}\n\nDrop one of:\n- dataset root folder (containing <matchId> subfolders)\n- match folder (dataset/<matchId> with a *.jsonl file)\n- match_*.jsonl file",
            "Drag & Drop",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void FormatElapsedCell(DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        if (_grid.Columns[e.ColumnIndex].DataPropertyName != "MatchElapsedMs")
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].DataBoundItem is not ViewerRow row)
        {
            return;
        }

        e.Value = ViewerHelpers.FormatElapsed(row.MatchElapsedMs);
        e.FormattingApplied = true;
        _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = $"{row.MatchElapsedMs}ms";
    }
}
