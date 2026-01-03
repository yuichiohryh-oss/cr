using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using WinFormsApp1.Core;

namespace WinFormsApp1;

public sealed class AppSettings
{
    public MotionSettingsDto Motion { get; set; } = new();
    public ElixirSettingsDto Elixir { get; set; } = new();
    public SuggestionSettingsDto Suggestion { get; set; } = new();
    public CardSettingsDto Cards { get; set; } = new();
    public CardSelectionSettings CardSelection { get; set; } = CardSelectionSettings.Default;
    public DebugSettingsDto Debug { get; set; } = new();

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Motion = new MotionSettingsDto
            {
                Roi = new RoiSettings { X = 0.0f, Y = 0.46f, Width = 1.0f, Height = 0.44f },
                Step = 6,
                DiffThreshold = 60,
                TriggerThreshold = 90,
                SplitX01 = 0.5f
            },
            Elixir = new ElixirSettingsDto
            {
                Roi = new RoiSettings { X = 0.08f, Y = 0.975f, Width = 0.88f, Height = 0.02f },
                SampleStep = 6,
                PurpleRMin = 120,
                PurpleGMax = 90,
                PurpleBMin = 120,
                PurpleRBMaxDiff = 60,
                SmoothingWindow = 5,
                EmptyBaseline01 = 0.08f,
                FullBaseline01 = 0.79f
            },
            Suggestion = new SuggestionSettingsDto
            {
                NeedElixir = 3,
                RequiredStreak = 2,
                CooldownMs = 700
            },
            Cards = new CardSettingsDto
            {
                TemplateDir = "assets/cards",
                HandRoi = new RoiSettings { X = 0.05f, Y = 0.90f, Width = 0.90f, Height = 0.09f },
                SlotCount = 4,
                SlotInnerPadding01 = 0.08f,
                SampleSize = 24,
                MinScore = 0.70f
            },
            CardSelection = CardSelectionSettings.Default,
            Debug = new DebugSettingsDto
            {
                ShowHpBars = false,
                HpBarRoi = new RoiSettings { X = 0.05f, Y = 0.06f, Width = 0.90f, Height = 0.74f }
            }
        };
    }
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class MotionSettingsDto
{
    public RoiSettings Roi { get; set; } = new();
    public int Step { get; set; }
    public int DiffThreshold { get; set; }
    public long TriggerThreshold { get; set; }
    public float SplitX01 { get; set; }

    public MotionSettings ToCore()
    {
        return new MotionSettings(Roi.ToCore(), Step, DiffThreshold, TriggerThreshold, SplitX01);
    }

    public override string ToString() => "Motion";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class ElixirSettingsDto
{
    public RoiSettings Roi { get; set; } = new();
    public int SampleStep { get; set; }
    public int PurpleRMin { get; set; }
    public int PurpleGMax { get; set; }
    public int PurpleBMin { get; set; }
    public int PurpleRBMaxDiff { get; set; }
    public int SmoothingWindow { get; set; }
    public float EmptyBaseline01 { get; set; }
    public float FullBaseline01 { get; set; }

    public ElixirSettings ToCore()
    {
        return new ElixirSettings(
            Roi.ToCore(),
            SampleStep,
            PurpleRMin,
            PurpleGMax,
            PurpleBMin,
            PurpleRBMaxDiff,
            SmoothingWindow,
            EmptyBaseline01,
            FullBaseline01
        );
    }

    public override string ToString() => "Elixir";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class SuggestionSettingsDto
{
    public int NeedElixir { get; set; }
    public int RequiredStreak { get; set; }
    public int CooldownMs { get; set; }

    public SuggestionSettings ToCore()
    {
        return new SuggestionSettings(NeedElixir, RequiredStreak, TimeSpan.FromMilliseconds(CooldownMs));
    }

    public override string ToString() => "Suggestion";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class CardSettingsDto
{
    public string TemplateDir { get; set; } = string.Empty;
    public RoiSettings HandRoi { get; set; } = new();
    public int SlotCount { get; set; }
    public float SlotInnerPadding01 { get; set; }
    public int SampleSize { get; set; }
    public float MinScore { get; set; }

    public CardRecognitionSettings ToCore()
    {
        return new CardRecognitionSettings(
            HandRoi.ToCore(),
            SlotCount,
            SlotInnerPadding01,
            SampleSize,
            MinScore
        );
    }

    public override string ToString() => "Cards";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class DebugSettingsDto
{
    public bool ShowHpBars { get; set; }
    public RoiSettings HpBarRoi { get; set; } = new();

    public override string ToString() => "Debug";
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class RoiSettings
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public Roi01 ToCore()
    {
        return new Roi01(X, Y, Width, Height);
    }

    public override string ToString() => $"{X:0.###},{Y:0.###},{Width:0.###},{Height:0.###}";
}

public static class AppSettingsStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static AppSettings LoadOrCreate(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch
        {
        }

        AppSettings defaults = AppSettings.CreateDefault();
        Save(path, defaults);
        return defaults;
    }

    public static void Save(string path, AppSettings settings)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(path, json);
    }
}
