using System;

namespace WinFormsApp1.Core;

public sealed class SuggestionEngine : ISuggestionEngine
{
    private readonly SuggestionSettings _settings;
    private readonly CardSelector _cardSelector;
    private int _streak;
    private DateTime _lastSuggest;

    public SuggestionEngine(SuggestionSettings settings, CardSelector cardSelector)
    {
        _settings = settings;
        _cardSelector = cardSelector;
        _streak = 0;
        _lastSuggest = DateTime.MinValue;
    }

    public Suggestion Decide(MotionResult motion, ElixirResult elixir, HandState hand, DateTime now)
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

        CardSelection? selection = _cardSelector.SelectCard(hand, elixir.ElixirInt, motion);
        if (selection == null)
        {
            return Suggestion.None;
        }

        _lastSuggest = now;
        _streak = 0;

        return BuildSuggestion(motion, selection.Value);
    }

    private static Suggestion BuildSuggestion(MotionResult motion, CardSelection selection)
    {
        string label;
        float x;
        float y;

        if (motion.ThreatLeft == motion.ThreatRight)
        {
            label = "KITE";
            x = SuggestionPoints.Kite.X;
            y = SuggestionPoints.Kite.Y;
        }
        else if (motion.ThreatLeft > motion.ThreatRight)
        {
            label = "DEF L";
            x = SuggestionPoints.LeftDef.X;
            y = SuggestionPoints.LeftDef.Y;
        }
        else
        {
            label = "DEF R";
            x = SuggestionPoints.RightDef.X;
            y = SuggestionPoints.RightDef.Y;
        }

        string text = $"{label}: {selection.CardId}";
        return new Suggestion(true, x, y, text, selection.HandIndex, selection.CardId);
    }
}
