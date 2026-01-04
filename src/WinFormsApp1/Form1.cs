using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using WinFormsApp1.Core;
using WinFormsApp1.Infrastructure;

namespace WinFormsApp1;

public partial class Form1 : Form
{
    private enum ActiveRoi
    {
        Motion,
        Elixir,
        Hand
    }

    private readonly PictureBox _pictureBox;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly WindowCapture _capture;
    private readonly WindowEnumerator _windowEnumerator;
    private readonly HpBarDetector _hpBarDetector;
    private readonly LevelLabelDetector _levelLabelDetector;
    private readonly SpawnEventDetector _spawnEventDetector;
    private readonly SpellPlacementDetector _spellPlacementDetector;
    private readonly FireballPlacementDetector _fireballPlacementDetector;
    private StateBuilder _stateBuilder;
    private ActionDetector _actionDetector;
    private readonly DatasetRecorder _datasetRecorder;
    private readonly FrameSaver _frameSaver;
    private IMotionAnalyzer _motionAnalyzer;
    private IElixirEstimator _elixirEstimator;
    private IMatchPhaseEstimator _matchPhaseEstimator;
    private ISuggestionEngine _suggestionEngine;
    private ICardRecognizer? _cardRecognizer;
    private readonly PropertyGrid _propertyGrid;
    private readonly Button _saveButton;
    private readonly Button _reloadButton;
    private readonly Label _settingsPathLabel;
    private readonly Label _cardsStatusLabel;
    private readonly Label _selectedWindowLabel;
    private readonly Label _matchStatusLabel;
    private readonly Label _matchFileLabel;
    private readonly RadioButton _motionRoiRadio;
    private readonly RadioButton _elixirRoiRadio;
    private readonly RadioButton _handRoiRadio;
    private readonly ComboBox _windowComboBox;
    private readonly Button _refreshWindowsButton;
    private readonly Button _selectWindowButton;
    private readonly Button _startMatchButton;
    private readonly Button _endMatchButton;

    private Bitmap? _prevFrame;
    private Suggestion _lastSuggestion;
    private ElixirResult _lastElixir;
    private HandState _lastHand;
    private HpBarDetectionResult _lastHpBars;
    private IReadOnlyList<SpawnEvent> _lastSpawns;
    private MatchClockState _lastClock;
    private TrainingSettings _trainingSettings;
    private SpellDetectionSettings _spellSettings;
    private PointF? _lastLogPoint;
    private PointF? _lastFireballPoint;
    private WindowInfo? _selectedWindow;
    private readonly MatchSessionManager _matchSession;
    private string? _currentMatchPath;
    private ActionSnapshot? _lastRecordedAction;
    private string _lastRecordedMatchId = string.Empty;
    private long _lastRecordedElapsedMs;
    private long _lastRecordedFrameIndex;
    private string? _lastRecordedPrevPath;
    private string? _lastRecordedCurrPath;
    private AppSettings _settings;
    private readonly string _settingsPath;
    private ActiveRoi _activeRoi;
    private bool _dragging;
    private PointF _dragStart;
    private RectangleF _dragRect;

    public Form1()
    {
        InitializeComponent();

        Text = "Clash Royale Advisor";
        ClientSize = new Size(960, 540);

        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _settings = AppSettingsStorage.LoadOrCreate(_settingsPath);

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };
        _pictureBox.Paint += PictureBox_Paint;
        _pictureBox.MouseDown += PictureBox_MouseDown;
        _pictureBox.MouseMove += PictureBox_MouseMove;
        _pictureBox.MouseUp += PictureBox_MouseUp;

        var settingsPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 320
        };

        _settingsPathLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = $"Settings: {Path.GetFileName(_settingsPath)}",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _cardsStatusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Cards: not loaded",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _selectedWindowLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = "Selected: (auto)",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _matchStatusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Match: STOPPED",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _matchFileLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = "Match file: (none)",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        var windowGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Height = 140,
            Text = "Capture Window"
        };

        _windowComboBox = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 28
        };
        _windowComboBox.DoubleClick += (_, _) => SelectWindowFromList();

        var windowButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 4, 6, 4)
        };
        _refreshWindowsButton = new Button { Text = "Refresh", AutoSize = true };
        _selectWindowButton = new Button { Text = "Select", AutoSize = true };
        _refreshWindowsButton.Click += (_, _) => RefreshWindowList();
        _selectWindowButton.Click += (_, _) => SelectWindowFromList();
        windowButtons.Controls.Add(_refreshWindowsButton);
        windowButtons.Controls.Add(_selectWindowButton);

        windowGroup.Controls.Add(windowButtons);
        windowGroup.Controls.Add(_windowComboBox);

        var matchPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 6, 6)
        };

        _startMatchButton = new Button { Text = "Start Match", AutoSize = true };
        _endMatchButton = new Button { Text = "End Match", AutoSize = true, Enabled = false };
        _startMatchButton.Click += (_, _) => StartMatch();
        _endMatchButton.Click += (_, _) => EndMatch();
        matchPanel.Controls.Add(_startMatchButton);
        matchPanel.Controls.Add(_endMatchButton);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 6, 6)
        };

        _saveButton = new Button { Text = "Save && Apply", AutoSize = true };
        _reloadButton = new Button { Text = "Reload", AutoSize = true };
        _saveButton.Click += (_, _) => SaveAndApplySettings();
        _reloadButton.Click += (_, _) => ReloadSettings();
        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_reloadButton);

        var roiGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Height = 70,
            Text = "Drag ROI"
        };

        _motionRoiRadio = new RadioButton
        {
            Text = "Motion ROI",
            Location = new Point(10, 20),
            AutoSize = true
        };
        _elixirRoiRadio = new RadioButton
        {
            Text = "Elixir ROI",
            Location = new Point(120, 20),
            AutoSize = true
        };
        _handRoiRadio = new RadioButton
        {
            Text = "Hand ROI",
            Location = new Point(10, 42),
            AutoSize = true
        };
        _motionRoiRadio.CheckedChanged += (_, _) => { if (_motionRoiRadio.Checked) _activeRoi = ActiveRoi.Motion; };
        _elixirRoiRadio.CheckedChanged += (_, _) => { if (_elixirRoiRadio.Checked) _activeRoi = ActiveRoi.Elixir; };
        _handRoiRadio.CheckedChanged += (_, _) => { if (_handRoiRadio.Checked) _activeRoi = ActiveRoi.Hand; };

        roiGroup.Controls.Add(_motionRoiRadio);
        roiGroup.Controls.Add(_elixirRoiRadio);
        roiGroup.Controls.Add(_handRoiRadio);

        _propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            SelectedObject = _settings
        };

        settingsPanel.Controls.Add(_propertyGrid);
        settingsPanel.Controls.Add(roiGroup);
        settingsPanel.Controls.Add(windowGroup);
        settingsPanel.Controls.Add(buttonPanel);
        settingsPanel.Controls.Add(_selectedWindowLabel);
        settingsPanel.Controls.Add(matchPanel);
        settingsPanel.Controls.Add(_matchFileLabel);
        settingsPanel.Controls.Add(_matchStatusLabel);
        settingsPanel.Controls.Add(_cardsStatusLabel);
        settingsPanel.Controls.Add(_settingsPathLabel);

        Controls.Add(_pictureBox);
        Controls.Add(settingsPanel);

        _capture = new WindowCapture();
        _windowEnumerator = new WindowEnumerator();
        _hpBarDetector = new HpBarDetector();
        _levelLabelDetector = new LevelLabelDetector();
        _spawnEventDetector = new SpawnEventDetector();
        _spellPlacementDetector = new SpellPlacementDetector();
        _fireballPlacementDetector = new FireballPlacementDetector();
        _frameSaver = new FrameSaver();
        _lastHpBars = HpBarDetectionResult.Empty;
        _lastSpawns = Array.Empty<SpawnEvent>();
        _lastClock = MatchClockState.Unknown;
        _matchSession = new MatchSessionManager();
        _trainingSettings = new TrainingSettings(
            false,
            "dataset",
            "match_{yyyyMMdd_HHmmss}_{matchId}.jsonl",
            4,
            1500,
            1,
            700,
            true,
            "frames",
            "png",
            90,
            0,
            true,
            "LeftRight",
            16,
            8,
            0.90f,
            0.20f,
            200);
        _spellSettings = new SpellDetectionSettings(
            true,
            new Roi01(0f, 0f, 1f, 1f),
            25,
            40,
            3000,
            4f,
            6,
            new FireballDetectionSettings(220, 60, 6000, 0.7f, 1.4f));

        _activeRoi = ActiveRoi.Hand;
        _handRoiRadio.Checked = true;
        ApplySettings(_settings);
        RefreshWindowList();
        _datasetRecorder = new DatasetRecorder();
        RefreshWindowList();

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 100
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _timer.Stop();
        _prevFrame?.Dispose();
        _pictureBox.Image?.Dispose();
        _datasetRecorder.Close();
        base.OnFormClosing(e);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        Bitmap? frame;
        if (_selectedWindow != null)
        {
            frame = _capture.CaptureClient(_selectedWindow.Hwnd);
        }
        else
        {
            frame = _capture.CaptureClient("ClashRoyale");
        }
        if (frame == null)
        {
            return;
        }

        MotionResult motion = _prevFrame != null
            ? _motionAnalyzer.Analyze(_prevFrame, frame)
            : new MotionResult(0, 0, false);

        DateTime now = DateTime.Now;
        long frameIndex = _matchSession.IsRunning ? _matchSession.NextFrame() : 0;
        ElixirResult elixir = _elixirEstimator.Estimate(frame);
        MatchClockState clockState = _matchPhaseEstimator.Estimate(frame);
        HandState hand = _cardRecognizer != null ? _cardRecognizer.Recognize(frame) : HandState.Empty;
        HpBarDetectionResult hpBars = _settings.Debug.ShowHpBars
            ? _hpBarDetector.Detect(frame, _settings.Debug.HpBarRoi.ToCore())
            : HpBarDetectionResult.Empty;
        IReadOnlyList<LevelLabelCandidate> labels = _levelLabelDetector.Detect(frame, _settings.Debug.LevelLabelRoi.ToCore());
        IReadOnlyList<SpawnEvent> spawns = _spawnEventDetector.Update(labels, now);
        Suggestion suggestion = _suggestionEngine.Decide(motion, elixir, hand, hpBars.Enemy, spawns, clockState, now);

        if (_trainingSettings.Enabled && _matchSession.IsRunning && _datasetRecorder.IsOpen)
        {
            StateSnapshot state = _stateBuilder.Build(clockState, elixir, spawns, hand, now);
            var context = new FrameContext(now, spawns, _prevFrame, frame, _spellSettings);
            ActionSnapshot? action = _actionDetector.Update(hand, elixir, context);
            if (action.HasValue)
            {
                TrainingSample sample = new TrainingSample(
                    DateTime.UtcNow,
                    state,
                    action.Value,
                    _matchSession.CurrentMatchId,
                    _matchSession.ElapsedMs,
                    frameIndex);
                if (_trainingSettings.SaveFramesOnRecord && _currentMatchPath != null && _prevFrame != null)
                {
                    string matchDir = Path.GetDirectoryName(_currentMatchPath) ?? AppContext.BaseDirectory;
                    using Bitmap prevClone = (Bitmap)_prevFrame.Clone();
                    using Bitmap currClone = (Bitmap)frame.Clone();
                    FrameTrimSettings trimSettings = _trainingSettings.ToTrimSettings();
                    var saved = _frameSaver.SaveFramesWithCrop(
                        prevClone,
                        currClone,
                        matchDir,
                        _trainingSettings.FramesDirName,
                        _trainingSettings.FrameImageFormat,
                        _trainingSettings.FrameJpegQuality,
                        _trainingSettings.MaxSavedFrameWidth,
                        _matchSession.ElapsedMs,
                        frameIndex,
                        trimSettings);
                    if (saved.HasValue)
                    {
                        string prevRel = FramePathNormalizer.NormalizeToFramesRelative(
                            matchDir,
                            saved.Value.PrevPath,
                            _trainingSettings.FramesDirName);
                        string currRel = FramePathNormalizer.NormalizeToFramesRelative(
                            matchDir,
                            saved.Value.CurrPath,
                            _trainingSettings.FramesDirName);
                        sample = sample with
                        {
                            PrevFramePath = prevRel,
                            CurrFramePath = currRel,
                            FrameCrop = saved.Value.FrameCrop
                        };
                    }
                }
                AppendSampleSafe(sample);
                _lastRecordedAction = sample.Action;
                _lastRecordedMatchId = sample.MatchId;
                _lastRecordedElapsedMs = sample.MatchElapsedMs;
                _lastRecordedFrameIndex = sample.FrameIndex;
                _lastRecordedPrevPath = string.IsNullOrWhiteSpace(sample.PrevFramePath) ? null : sample.PrevFramePath;
                _lastRecordedCurrPath = string.IsNullOrWhiteSpace(sample.CurrFramePath) ? null : sample.CurrFramePath;
                if (action.Value.X01.HasValue && action.Value.Y01.HasValue)
                {
                    if (string.Equals(action.Value.CardId, "log", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastLogPoint = new PointF(action.Value.X01.Value, action.Value.Y01.Value);
                    }
                    else if (string.Equals(action.Value.CardId, "fireball", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastFireballPoint = new PointF(action.Value.X01.Value, action.Value.Y01.Value);
                    }
                }
            }
        }

        _lastSuggestion = suggestion;
        _lastElixir = elixir;
        _lastHand = hand;
        _lastHpBars = hpBars;
        _lastSpawns = spawns;
        _lastClock = clockState;

        Image? oldImage = _pictureBox.Image;
        _pictureBox.Image = frame;
        oldImage?.Dispose();

        Bitmap? oldPrev = _prevFrame;
        _prevFrame = frame.Clone(new Rectangle(0, 0, frame.Width, frame.Height), PixelFormat.Format24bppRgb);
        oldPrev?.Dispose();

        _pictureBox.Invalidate();
    }

    private void PictureBox_Paint(object? sender, PaintEventArgs e)
    {
        if (_pictureBox.Image == null)
        {
            return;
        }

        RectangleF displayRect = GetImageDisplayRect(_pictureBox);
        if (displayRect.Width <= 0 || displayRect.Height <= 0)
        {
            return;
        }

        using var dotBrush = new SolidBrush(Color.Red);
        using var textBrush = new SolidBrush(Color.Yellow);
        using var debugBrush = new SolidBrush(Color.Lime);
        using var motionPen = new Pen(Color.Cyan, 2f);
        using var elixirPen = new Pen(Color.Orange, 2f);
        using var handPen = new Pen(Color.Magenta, 2f);
        using var dragPen = new Pen(Color.White, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using var font = new Font("Segoe UI", 12f, FontStyle.Bold);
        using var debugFont = new Font("Segoe UI", 10f, FontStyle.Regular);

        DrawRoi(e.Graphics, displayRect, _settings.Motion.Roi, motionPen);
        DrawRoi(e.Graphics, displayRect, _settings.Elixir.Roi, elixirPen);
        DrawRoi(e.Graphics, displayRect, _settings.Cards.HandRoi, handPen);
        DrawHandSlots(e.Graphics, displayRect, _settings.Cards.HandRoi, _settings.Cards.SlotCount, _lastSuggestion.SelectedHandIndex);
        if (_settings.Debug.ShowHpBars)
        {
            DrawHpBars(e.Graphics, displayRect, _lastHpBars);
        }

        if (_settings.Debug.ShowLevelLabels)
        {
            DrawSpawnEvents(e.Graphics, displayRect, _lastSpawns);
        }
        if (_settings.Debug.ShowSpellMarkers)
        {
            if (_lastLogPoint.HasValue)
            {
                DrawSpellMarker(e.Graphics, displayRect, _lastLogPoint.Value, Color.Gold);
            }

            if (_lastFireballPoint.HasValue)
            {
                DrawSpellMarker(e.Graphics, displayRect, _lastFireballPoint.Value, Color.White);
            }
        }

        if (_dragging && _dragRect.Width > 1f && _dragRect.Height > 1f)
        {
            e.Graphics.DrawRectangle(dragPen, _dragRect.X, _dragRect.Y, _dragRect.Width, _dragRect.Height);
        }

        if (_lastSuggestion.HasSuggestion)
        {
            float x = displayRect.Left + (_lastSuggestion.X01 * displayRect.Width);
            float y = displayRect.Top + (_lastSuggestion.Y01 * displayRect.Height);
            float radius = 10f;

            e.Graphics.FillEllipse(dotBrush, x - radius, y - radius, radius * 2f, radius * 2f);
            e.Graphics.DrawString(_lastSuggestion.Text, font, textBrush, x + 12f, y - 12f);
        }

        string elixirText = $"Elixir: {_lastElixir.ElixirInt}/10";
        e.Graphics.DrawString(elixirText, debugFont, debugBrush, displayRect.Left + 8f, displayRect.Top + 8f);

        string handText = _lastHand.Slots.Length == 0 ? "Hand: (unknown)" : $"Hand: {string.Join(", ", _lastHand.Slots)}";
        e.Graphics.DrawString(handText, debugFont, debugBrush, displayRect.Left + 8f, displayRect.Top + 28f);
        if (_settings.Debug.ShowClockPhase)
        {
            string clockText = $"Phase: {_lastClock.Phase} ({_lastClock.Confidence:0.00})";
            e.Graphics.DrawString(clockText, debugFont, debugBrush, displayRect.Left + 8f, displayRect.Top + 48f);
        }

        if (_settings.Debug.ShowStopwatch)
        {
            string status = _matchSession.IsRunning ? "RUNNING" : "STOPPED";
            string idText = string.IsNullOrWhiteSpace(_matchSession.CurrentMatchId)
                ? "-"
                : _matchSession.CurrentMatchId[..Math.Min(8, _matchSession.CurrentMatchId.Length)];
            string timeText = TimeSpan.FromMilliseconds(_matchSession.ElapsedMs).ToString(@"mm\:ss\.fff");
            string fileText = string.IsNullOrWhiteSpace(_currentMatchPath)
                ? "(none)"
                : Path.GetFileName(_currentMatchPath);
            float y = displayRect.Top + 68f;
            e.Graphics.DrawString($"Match: {status}", debugFont, debugBrush, displayRect.Left + 8f, y);
            y += 18f;
            e.Graphics.DrawString($"MatchId: {idText}", debugFont, debugBrush, displayRect.Left + 8f, y);
            y += 18f;
            e.Graphics.DrawString($"File: {fileText}", debugFont, debugBrush, displayRect.Left + 8f, y);
            y += 18f;
            e.Graphics.DrawString($"Stopwatch: {timeText}", debugFont, debugBrush, displayRect.Left + 8f, y);
            y += 18f;
            e.Graphics.DrawString($"Frame: {_matchSession.FrameIndex}", debugFont, debugBrush, displayRect.Left + 8f, y);
        }

        if (_settings.Debug.ShowLastAction && _lastRecordedAction.HasValue)
        {
            var action = _lastRecordedAction.Value;
            string card = action.CardId;
            string type = GetActionType(action.CardId);
            string lane = action.Lane.ToString();
            string pos = action.X01.HasValue && action.Y01.HasValue
                ? $"{action.X01:0.00},{action.Y01:0.00}"
                : "n/a";
            string idText = string.IsNullOrWhiteSpace(_lastRecordedMatchId)
                ? "-"
                : _lastRecordedMatchId[..Math.Min(8, _lastRecordedMatchId.Length)];
            float y = displayRect.Top + 158f;
            e.Graphics.DrawString($"LastAction: {card} type={type} lane={lane} pos={pos}", debugFont, debugBrush, displayRect.Left + 8f, y);
            y += 18f;
            e.Graphics.DrawString($"MatchId: {idText} t={_lastRecordedElapsedMs}ms frame={_lastRecordedFrameIndex}", debugFont, debugBrush, displayRect.Left + 8f, y);
            y += 18f;
            if (_lastRecordedPrevPath != null || _lastRecordedCurrPath != null)
            {
                string prevText = _lastRecordedPrevPath ?? "-";
                string currText = _lastRecordedCurrPath ?? "-";
                e.Graphics.DrawString($"Prev: {prevText}", debugFont, debugBrush, displayRect.Left + 8f, y);
                y += 18f;
                e.Graphics.DrawString($"Curr: {currText}", debugFont, debugBrush, displayRect.Left + 8f, y);
            }
        }
    }

    private static RectangleF GetImageDisplayRect(PictureBox pictureBox)
    {
        if (pictureBox.Image == null)
        {
            return RectangleF.Empty;
        }

        float imageWidth = pictureBox.Image.Width;
        float imageHeight = pictureBox.Image.Height;
        float boxWidth = pictureBox.ClientSize.Width;
        float boxHeight = pictureBox.ClientSize.Height;

        if (boxWidth <= 0 || boxHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return RectangleF.Empty;
        }

        float imageAspect = imageWidth / imageHeight;
        float boxAspect = boxWidth / boxHeight;

        if (boxAspect > imageAspect)
        {
            float height = boxHeight;
            float width = height * imageAspect;
            float x = (boxWidth - width) / 2f;
            return new RectangleF(x, 0f, width, height);
        }
        else
        {
            float width = boxWidth;
            float height = width / imageAspect;
            float y = (boxHeight - height) / 2f;
            return new RectangleF(0f, y, width, height);
        }
    }

    private void PictureBox_MouseDown(object? sender, MouseEventArgs e)
    {
        RectangleF displayRect = GetImageDisplayRect(_pictureBox);
        if (displayRect.Contains(e.Location))
        {
            _dragging = true;
            _dragStart = e.Location;
            _dragRect = new RectangleF(e.Location.X, e.Location.Y, 0f, 0f);
        }
    }

    private void PictureBox_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        RectangleF displayRect = GetImageDisplayRect(_pictureBox);
        float x0 = Math.Clamp(Math.Min(_dragStart.X, e.Location.X), displayRect.Left, displayRect.Right);
        float y0 = Math.Clamp(Math.Min(_dragStart.Y, e.Location.Y), displayRect.Top, displayRect.Bottom);
        float x1 = Math.Clamp(Math.Max(_dragStart.X, e.Location.X), displayRect.Left, displayRect.Right);
        float y1 = Math.Clamp(Math.Max(_dragStart.Y, e.Location.Y), displayRect.Top, displayRect.Bottom);

        _dragRect = new RectangleF(x0, y0, x1 - x0, y1 - y0);
        _pictureBox.Invalidate();
    }

    private void PictureBox_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        RectangleF displayRect = GetImageDisplayRect(_pictureBox);
        if (_dragRect.Width < 4f || _dragRect.Height < 4f || displayRect.Width <= 0f || displayRect.Height <= 0f)
        {
            _pictureBox.Invalidate();
            return;
        }

        RoiSettings roi = new RoiSettings
        {
            X = (_dragRect.Left - displayRect.Left) / displayRect.Width,
            Y = (_dragRect.Top - displayRect.Top) / displayRect.Height,
            Width = _dragRect.Width / displayRect.Width,
            Height = _dragRect.Height / displayRect.Height
        };

        if (_activeRoi == ActiveRoi.Motion)
        {
            _settings.Motion.Roi = roi;
        }
        else if (_activeRoi == ActiveRoi.Elixir)
        {
            _settings.Elixir.Roi = roi;
        }
        else
        {
            _settings.Cards.HandRoi = roi;
        }

        _propertyGrid.Refresh();
        ApplySettings(_settings);
        _pictureBox.Invalidate();
    }

    private static void DrawRoi(Graphics g, RectangleF displayRect, RoiSettings roi, Pen pen)
    {
        float x = displayRect.Left + (roi.X * displayRect.Width);
        float y = displayRect.Top + (roi.Y * displayRect.Height);
        float w = roi.Width * displayRect.Width;
        float h = roi.Height * displayRect.Height;
        g.DrawRectangle(pen, x, y, w, h);
    }

    private static void DrawHandSlots(Graphics g, RectangleF displayRect, RoiSettings roi, int slotCount, int? selectedIndex)
    {
        if (slotCount <= 0)
        {
            return;
        }

        float handX = displayRect.Left + (roi.X * displayRect.Width);
        float handY = displayRect.Top + (roi.Y * displayRect.Height);
        float handW = roi.Width * displayRect.Width;
        float handH = roi.Height * displayRect.Height;

        float slotW = handW / slotCount;
        using var slotPen = new Pen(Color.FromArgb(160, Color.White), 1f);
        using var selectedPen = new Pen(Color.Yellow, 2f);

        for (int i = 0; i < slotCount; i++)
        {
            float x = handX + (i * slotW);
            var rect = new RectangleF(x, handY, slotW, handH);
            g.DrawRectangle(slotPen, rect.X, rect.Y, rect.Width, rect.Height);

            if (selectedIndex.HasValue && selectedIndex.Value == i)
            {
                g.DrawRectangle(selectedPen, rect.X + 1f, rect.Y + 1f, rect.Width - 2f, rect.Height - 2f);
            }
        }
    }

    private static void DrawHpBars(Graphics g, RectangleF displayRect, HpBarDetectionResult bars)
    {
        const float barWidth = 14f;
        const float barHeight = 4f;
        using var enemyPen = new Pen(Color.Red, 2f);
        using var friendlyPen = new Pen(Color.Cyan, 2f);

        foreach (var unit in bars.Enemy.Units)
        {
            float x = displayRect.Left + (unit.X01 * displayRect.Width);
            float y = displayRect.Top + (unit.Y01 * displayRect.Height);
            g.DrawRectangle(enemyPen, x - (barWidth / 2f), y - (barHeight / 2f), barWidth, barHeight);
        }

        foreach (var unit in bars.Friendly.Units)
        {
            float x = displayRect.Left + (unit.X01 * displayRect.Width);
            float y = displayRect.Top + (unit.Y01 * displayRect.Height);
            g.DrawRectangle(friendlyPen, x - (barWidth / 2f), y - (barHeight / 2f), barWidth, barHeight);
        }
    }

    private static void DrawSpawnEvents(Graphics g, RectangleF displayRect, IReadOnlyList<SpawnEvent> spawns)
    {
        const float size = 8f;
        using var enemyPen = new Pen(Color.Red, 2f);
        using var friendlyPen = new Pen(Color.Cyan, 2f);

        foreach (SpawnEvent spawn in spawns)
        {
            float x = displayRect.Left + (spawn.X01 * displayRect.Width);
            float y = displayRect.Top + (spawn.Y01 * displayRect.Height);
            Pen pen = spawn.Team == Team.Enemy ? enemyPen : friendlyPen;
            g.DrawRectangle(pen, x - (size / 2f), y - (size / 2f), size, size);
        }
    }

    private static void DrawSpellMarker(Graphics g, RectangleF displayRect, PointF marker, Color color)
    {
        const float size = 10f;
        using var pen = new Pen(color, 2f);
        float x = displayRect.Left + (marker.X * displayRect.Width);
        float y = displayRect.Top + (marker.Y * displayRect.Height);
        g.DrawEllipse(pen, x - (size / 2f), y - (size / 2f), size, size);
    }

    private void ApplySettings(AppSettings settings)
    {
        _motionAnalyzer = new MotionAnalyzer(settings.Motion.ToCore());
        _elixirEstimator = new ElixirEstimator(settings.Elixir.ToCore());
        _matchPhaseEstimator = new MatchPhaseEstimator(settings.Clock.ToCore());
        _suggestionEngine = new SuggestionEngine(settings.Suggestion.ToCore(), new CardSelector(settings.CardSelection));
        _cardRecognizer = TryCreateCardRecognizer(settings.Cards);
        _trainingSettings = settings.Training.ToCore();
        _spellSettings = settings.Spells.ToCore();
        _stateBuilder = new StateBuilder(_trainingSettings.RecentSpawnSeconds);
        var resolvers = new List<IActionPlacementResolver>
        {
            new LogPlacementResolver(_spellPlacementDetector, _spellSettings),
            new FireballPlacementResolver(_fireballPlacementDetector, _spellSettings),
            new UnitPlacementResolver(_trainingSettings.UnitCommitMatchWindowMs)
        };
        _actionDetector = new ActionDetector(
            _trainingSettings.PendingTimeoutMs,
            _trainingSettings.ElixirCommitTolerance,
            resolvers);
        _lastSuggestion = Suggestion.None;
        _lastElixir = new ElixirResult(0f, 0);
        _lastHand = HandState.Empty;
        _lastHpBars = HpBarDetectionResult.Empty;
        _lastSpawns = Array.Empty<SpawnEvent>();
        _lastClock = MatchClockState.Unknown;
        _lastLogPoint = null;
        _lastFireballPoint = null;
        _lastRecordedAction = null;
        _lastRecordedMatchId = string.Empty;
        _lastRecordedElapsedMs = 0;
        _lastRecordedFrameIndex = 0;
        _lastRecordedPrevPath = null;
        _lastRecordedCurrPath = null;
        UpdateMatchUi();
    }

    private void SaveAndApplySettings()
    {
        if (_propertyGrid.SelectedObject is AppSettings settings)
        {
            _settings = settings;
        }

        AppSettingsStorage.Save(_settingsPath, _settings);
        ApplySettings(_settings);
    }

    private void RefreshWindowList()
    {
        var windows = _windowEnumerator.GetOpenWindows();
        _windowComboBox.BeginUpdate();
        try
        {
            _windowComboBox.Items.Clear();
            foreach (WindowInfo window in windows)
            {
                _windowComboBox.Items.Add(window);
            }
        }
        finally
        {
            _windowComboBox.EndUpdate();
        }

        if (_windowComboBox.Items.Count > 0)
        {
            _windowComboBox.SelectedIndex = 0;
        }
    }

    private void SelectWindowFromList()
    {
        if (_windowComboBox.SelectedItem is WindowInfo window)
        {
            _selectedWindow = window;
            _selectedWindowLabel.Text = $"Selected: {window.Title} ({window.ProcessName})";
        }
    }

    private void ReloadSettings()
    {
        _settings = AppSettingsStorage.LoadOrCreate(_settingsPath);
        _propertyGrid.SelectedObject = _settings;
        ApplySettings(_settings);
    }

    private ICardRecognizer? TryCreateCardRecognizer(CardSettingsDto settings)
    {
        string? dir = ResolveTemplateDir(settings.TemplateDir);
        if (dir == null)
        {
            _cardsStatusLabel.Text = "Cards: template dir not found";
            return null;
        }

        var templates = new List<CardTemplate>();
        foreach (string file in Directory.GetFiles(dir, "*.png"))
        {
            string id = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            using var bmp = new Bitmap(file);
            using var bmp24 = bmp.PixelFormat == PixelFormat.Format24bppRgb
                ? (Bitmap)bmp.Clone()
                : bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb);
            templates.Add(CardTemplate.FromBitmap(id, bmp24, settings.SampleSize));
        }

        if (templates.Count == 0)
        {
            _cardsStatusLabel.Text = "Cards: templates not found";
            return null;
        }

        _cardsStatusLabel.Text = $"Cards: {templates.Count} templates";
        return new CardRecognizer(settings.ToCore(), templates);
    }

    private static string? ResolveTemplateDir(string templateDir)
    {
        if (string.IsNullOrWhiteSpace(templateDir))
        {
            return null;
        }

        if (Path.IsPathRooted(templateDir) && Directory.Exists(templateDir))
        {
            return templateDir;
        }

        string baseDir = AppContext.BaseDirectory;
        string candidate = Path.GetFullPath(Path.Combine(baseDir, templateDir));
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        string? repoRoot = FindRepoRoot(baseDir);
        if (repoRoot != null)
        {
            candidate = Path.GetFullPath(Path.Combine(repoRoot, templateDir));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WinFormsApp1.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return null;
    }

    private void AppendSampleSafe(TrainingSample sample)
    {
        if (!_datasetRecorder.IsOpen)
        {
            return;
        }

        try
        {
            _datasetRecorder.Append(sample);
        }
        catch
        {
        }
    }

    private void StartMatch()
    {
        _matchSession.StartNewMatch();
        if (_trainingSettings.Enabled)
        {
            _currentMatchPath = CreateMatchOutputPath(_trainingSettings, _matchSession);
            _datasetRecorder.Open(_currentMatchPath);
        }
        else
        {
            _currentMatchPath = null;
        }
        UpdateMatchUi();
    }

    private void EndMatch()
    {
        if (!_matchSession.IsRunning)
        {
            return;
        }

        _matchSession.EndMatch();
        _datasetRecorder.Close();
        UpdateMatchUi();
    }

    private void UpdateMatchUi()
    {
        _matchStatusLabel.Text = _matchSession.IsRunning ? "Match: RUNNING" : "Match: STOPPED";
        _matchFileLabel.Text = string.IsNullOrWhiteSpace(_currentMatchPath)
            ? "Match file: (none)"
            : $"Match file: {Path.GetFileName(_currentMatchPath)}";
        _endMatchButton.Enabled = _matchSession.IsRunning;
    }

    private static string CreateMatchOutputPath(TrainingSettings settings, MatchSessionManager session)
    {
        string baseDir = string.IsNullOrWhiteSpace(settings.OutputDir)
            ? Path.Combine(AppContext.BaseDirectory, "dataset")
            : Path.IsPathRooted(settings.OutputDir)
                ? settings.OutputDir
                : Path.Combine(AppContext.BaseDirectory, settings.OutputDir);

        string matchDir = Path.Combine(baseDir, session.CurrentMatchId);
        Directory.CreateDirectory(matchDir);
        string fileName = MatchFileNameFormatter.BuildFileName(
            settings.FileNamePattern,
            session.StartTimeLocal,
            session.CurrentMatchId);
        return Path.Combine(matchDir, fileName);
    }

    private static string GetActionType(string cardId)
    {
        if (string.Equals(cardId, "log", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cardId, "fireball", StringComparison.OrdinalIgnoreCase))
        {
            return "Spell";
        }

        return "Unit";
    }

}
