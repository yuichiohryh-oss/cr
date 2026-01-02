using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using WinFormsApp1.Core;
using WinFormsApp1.Infrastructure;

namespace WinFormsApp1;

public partial class Form1 : Form
{
    private readonly PictureBox _pictureBox;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly WindowCapture _capture;
    private IMotionAnalyzer _motionAnalyzer;
    private IElixirEstimator _elixirEstimator;
    private ISuggestionEngine _suggestionEngine;
    private readonly PropertyGrid _propertyGrid;
    private readonly Button _saveButton;
    private readonly Button _reloadButton;
    private readonly Label _settingsPathLabel;

    private Bitmap? _prevFrame;
    private Suggestion _lastSuggestion;
    private ElixirResult _lastElixir;
    private AppSettings _settings;
    private readonly string _settingsPath;

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

        _propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            SelectedObject = _settings
        };

        settingsPanel.Controls.Add(_propertyGrid);
        settingsPanel.Controls.Add(buttonPanel);
        settingsPanel.Controls.Add(_settingsPathLabel);

        Controls.Add(_pictureBox);
        Controls.Add(settingsPanel);

        _capture = new WindowCapture();

        ApplySettings(_settings);

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

    private void ApplySettings(AppSettings settings)
    {
        _motionAnalyzer = new MotionAnalyzer(settings.Motion.ToCore());
        _elixirEstimator = new ElixirEstimator(settings.Elixir.ToCore());
        _suggestionEngine = new SuggestionEngine(settings.Suggestion.ToCore());
        _lastSuggestion = Suggestion.None;
        _lastElixir = new ElixirResult(0f, 0);
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

    private void ReloadSettings()
    {
        _settings = AppSettingsStorage.LoadOrCreate(_settingsPath);
        _propertyGrid.SelectedObject = _settings;
        ApplySettings(_settings);
    }
}
