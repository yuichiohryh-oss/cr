using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using WinFormsApp1.Core;
using WinFormsApp1.Infrastructure;

namespace WinFormsApp1;

public partial class Form1 : Form
{
    private readonly PictureBox _pictureBox;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly WindowCapture _capture;
    private readonly IMotionAnalyzer _motionAnalyzer;
    private readonly IElixirEstimator _elixirEstimator;
    private readonly ISuggestionEngine _suggestionEngine;

    private Bitmap? _prevFrame;
    private Suggestion _lastSuggestion;
    private ElixirResult _lastElixir;

    public Form1()
    {
        InitializeComponent();

        Text = "Clash Royale Advisor";
        ClientSize = new Size(960, 540);

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };
        Controls.Add(_pictureBox);
        _pictureBox.Paint += PictureBox_Paint;

        _capture = new WindowCapture();

        var motionSettings = new MotionSettings(
            Roi: new Roi01(0.0f, 0.46f, 1.0f, 0.44f),
            Step: 6,
            DiffThreshold: 60,
            TriggerThreshold: 90,
            SplitX01: 0.5f
        );

        var elixirSettings = new ElixirSettings(
            Roi: new Roi01(0.20f, 0.86f, 0.60f, 0.08f),
            SampleStep: 6,
            PurpleRMin: 120,
            PurpleGMax: 90,
            PurpleBMin: 120,
            PurpleRBMaxDiff: 60,
            SmoothingWindow: 5
        );

        var suggestionSettings = new SuggestionSettings(
            NeedElixir: 3,
            RequiredStreak: 2,
            Cooldown: TimeSpan.FromMilliseconds(700)
        );

        _motionAnalyzer = new MotionAnalyzer(motionSettings);
        _elixirEstimator = new ElixirEstimator(elixirSettings);
        _suggestionEngine = new SuggestionEngine(suggestionSettings);

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
        base.OnFormClosing(e);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        Bitmap? frame = _capture.CaptureClient("ClashRoyale");
        if (frame == null)
        {
            return;
        }

        MotionResult motion = _prevFrame != null
            ? _motionAnalyzer.Analyze(_prevFrame, frame)
            : new MotionResult(0, 0, false);

        ElixirResult elixir = _elixirEstimator.Estimate(frame);
        Suggestion suggestion = _suggestionEngine.Decide(motion, elixir, DateTime.Now);

        _lastSuggestion = suggestion;
        _lastElixir = elixir;

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
        using var font = new Font("Segoe UI", 12f, FontStyle.Bold);
        using var debugFont = new Font("Segoe UI", 10f, FontStyle.Regular);

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
}
