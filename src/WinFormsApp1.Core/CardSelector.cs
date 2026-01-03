using System;
using System.Collections.Generic;

namespace WinFormsApp1.Core;

public sealed class CardSelector
{
    private readonly CardSelectionSettings _settings;
    private readonly Dictionary<string, CardInfo> _cardInfo;
    private readonly HashSet<string> _excludedIds;

    public CardSelector()
        : this(CardSelectionSettings.Default)
    {
    }

    public CardSelector(CardSelectionSettings settings)
    {
        _settings = settings;
        _cardInfo = settings.BuildCardInfoMap();
        _excludedIds = new HashSet<string>(_settings.ExcludedCardIds, StringComparer.OrdinalIgnoreCase);
    }

    public CardSelection? SelectCard(HandState hand, int elixir, MotionResult motion)
    {
        if (hand.Slots.Length == 0)
        {
            return null;
        }

        bool strongThreat = motion.ThreatLeft + motion.ThreatRight >= _settings.StrongThreatThreshold;

        var candidates = new List<Candidate>();
        for (int i = 0; i < hand.Slots.Length; i++)
        {
            string id = hand.GetSlot(i);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            CardInfo info = GetInfoOrFallback(id, hand.GetCost(i));
            if (IsExcluded(id, info))
            {
                continue;
            }

            int cost = GetEffectiveCost(info, hand.GetCost(i));
            if (cost > 0 && cost <= elixir)
            {
                candidates.Add(new Candidate(new CardSelection(i, info.Id), cost, info));
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (strongThreat)
        {
            CardSelection? defensePick = SelectDefensePriority(candidates);
            if (defensePick != null)
            {
                return defensePick;
            }
        }

        return SelectLowestCost(candidates);
    }

    private bool IsExcluded(string id, CardInfo info)
    {
        if (_excludedIds.Contains(id))
        {
            return true;
        }
        if (_settings.ExcludeSpells && info.Roles.HasFlag(CardRole.Spell))
        {
            return true;
        }
        if (_settings.ExcludeBuildings && info.Roles.HasFlag(CardRole.Building))
        {
            return true;
        }
        return false;
    }

    private CardSelection? SelectDefensePriority(List<Candidate> candidates)
    {
        var candidateSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Candidate selection in candidates)
        {
            candidateSet.Add(selection.Selection.CardId);
        }

        foreach (string id in _settings.DefensivePriorityCardIds)
        {
            if (!candidateSet.Contains(id))
            {
                continue;
            }

            if (_cardInfo.TryGetValue(id, out CardInfo info) && info.Roles.HasFlag(CardRole.Defensive))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (string.Equals(candidates[i].Selection.CardId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidates[i].Selection;
                    }
                }
            }
        }

        return null;
    }

    private static CardSelection SelectLowestCost(List<Candidate> candidates)
    {
        Candidate best = candidates[0];

        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].Cost < best.Cost)
            {
                best = candidates[i];
            }
        }

        return best.Selection;
    }

    private static int GetEffectiveCost(CardInfo info, int handCost)
    {
        return handCost > 0 ? handCost : info.Cost;
    }

    private CardInfo GetInfoOrFallback(string id, int fallbackCost)
    {
        if (_cardInfo.TryGetValue(id, out CardInfo info))
        {
            return info;
        }

        return new CardInfo(id, fallbackCost, CardRole.None);
    }

    private readonly record struct Candidate(CardSelection Selection, int Cost, CardInfo Info);
}
