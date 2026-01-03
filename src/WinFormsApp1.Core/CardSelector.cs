using System;
using System.Collections.Generic;

namespace WinFormsApp1.Core;

public sealed class CardSelector
{
    private readonly CardSelectorSettings _settings;
    private readonly Dictionary<string, CardInfo> _cardInfo;

    public CardSelector()
        : this(CardSelectorSettings.Default)
    {
    }

    public CardSelector(CardSelectorSettings settings)
    {
        _settings = settings;
        _cardInfo = BuildCardInfo();
    }

    public CardSelection? SelectCard(HandState hand, int elixir, MotionResult motion)
    {
        if (hand.Slots.Length == 0)
        {
            return null;
        }

        bool strongThreat = motion.ThreatLeft + motion.ThreatRight >= _settings.StrongThreatThreshold;

        var candidates = new List<CardSelection>();
        for (int i = 0; i < hand.Slots.Length; i++)
        {
            string id = hand.GetSlot(i);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!_cardInfo.TryGetValue(id, out CardInfo info))
            {
                continue;
            }

            if (IsExcluded(info))
            {
                continue;
            }

            if (info.Cost <= elixir)
            {
                candidates.Add(new CardSelection(i, info.Id));
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

    private bool IsExcluded(CardInfo info)
    {
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

    private CardSelection? SelectDefensePriority(List<CardSelection> candidates)
    {
        var candidateSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CardSelection selection in candidates)
        {
            candidateSet.Add(selection.CardId);
        }

        foreach (string id in _settings.DefensivePriority)
        {
            if (!candidateSet.Contains(id))
            {
                continue;
            }

            if (_cardInfo.TryGetValue(id, out CardInfo info) && info.Roles.HasFlag(CardRole.Defensive))
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (string.Equals(candidates[i].CardId, id, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidates[i];
                    }
                }
            }
        }

        return null;
    }

    private CardSelection SelectLowestCost(List<CardSelection> candidates)
    {
        CardSelection best = candidates[0];
        int bestCost = GetCost(best.CardId);

        for (int i = 1; i < candidates.Count; i++)
        {
            int cost = GetCost(candidates[i].CardId);
            if (cost < bestCost)
            {
                best = candidates[i];
                bestCost = cost;
            }
        }

        return best;
    }

    private int GetCost(string id)
    {
        return _cardInfo.TryGetValue(id, out CardInfo info) ? info.Cost : int.MaxValue;
    }

    private static Dictionary<string, CardInfo> BuildCardInfo()
    {
        return new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["hog"] = new("hog", 4, CardRole.WinCondition),
            ["musketeer"] = new("musketeer", 4, CardRole.Defensive),
            ["cannon"] = new("cannon", 3, CardRole.Building | CardRole.Defensive),
            ["fireball"] = new("fireball", 4, CardRole.Spell),
            ["ice_spirit"] = new("ice_spirit", 1, CardRole.Defensive | CardRole.Cycle),
            ["skeletons"] = new("skeletons", 1, CardRole.Defensive | CardRole.Cycle),
            ["ice_golem"] = new("ice_golem", 2, CardRole.Defensive),
            ["log"] = new("log", 2, CardRole.Spell)
        };
    }
}
