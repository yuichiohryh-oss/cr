using System;

namespace WinFormsApp1.Core;

public sealed class SuggestionEngine : ISuggestionEngine
{
    private readonly SuggestionSettings _settings;
    private int _streak;
    private DateTime _lastSuggest;

    public SuggestionEngine(SuggestionSettings settings)
    {
        _settings = settings;
        _streak = 0;
        _lastSuggest = DateTime.MinValue;
    }

    public Suggestion Decide(MotionResult motion, ElixirResult elixir, DateTime now)
    {
        bool canTrigger = motion.DefenseTrigger && elixir.ElixirInt >= _settings.NeedElixir;
        if (canTrigger)
        {
            _streak++;
        }
        else
        {
            _streak = 0;
            return Suggestion.None;
        }

        if (_streak < _settings.RequiredStreak)
        {
            return Suggestion.None;
        }

        if (now - _lastSuggest < _settings.Cooldown)
        {
            return Suggestion.None;
        }

        _lastSuggest = now;

        return BuildSuggestion(motion);
    }

    private static Suggestion BuildSuggestion(MotionResult motion)
    {
        if (motion.ThreatLeft == motion.ThreatRight)
        {
            return new Suggestion(true, SuggestionPoints.Kite.X, SuggestionPoints.Kite.Y, "KITE");
        }

        if (motion.ThreatLeft > motion.ThreatRight)
        {
            return new Suggestion(true, SuggestionPoints.LeftDef.X, SuggestionPoints.LeftDef.Y, "DEF L");
        }

        return new Suggestion(true, SuggestionPoints.RightDef.X, SuggestionPoints.RightDef.Y, "DEF R");
    }
}
