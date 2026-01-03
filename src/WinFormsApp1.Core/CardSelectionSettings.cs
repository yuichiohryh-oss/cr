using System;
using System.Collections.Generic;

namespace WinFormsApp1.Core;

public sealed class CardSelectionSettings
{
    public CardDefinition[] Cards { get; set; } = Array.Empty<CardDefinition>();
    public string[] ExcludedCardIds { get; set; } = Array.Empty<string>();
    public string[] DefensivePriorityCardIds { get; set; } = Array.Empty<string>();
    public int StrongThreatThreshold { get; set; } = 50;
    public bool ExcludeSpells { get; set; } = true;
    public bool ExcludeBuildings { get; set; } = true;

    public Dictionary<string, CardInfo> BuildCardInfoMap()
    {
        var map = new Dictionary<string, CardInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (CardDefinition def in Cards)
        {
            if (string.IsNullOrWhiteSpace(def.Id))
            {
                continue;
            }

            CardRole roles = CardRoleParser.Parse(def.Roles);
            map[def.Id] = new CardInfo(def.Id, def.Cost, roles);
        }
        return map;
    }

    public static CardSelectionSettings Default => new()
    {
        Cards = new[]
        {
            new CardDefinition("hog", 4, new[] { "WinCondition" }),
            new CardDefinition("musketeer", 4, new[] { "Defensive" }),
            new CardDefinition("cannon", 3, new[] { "Building", "Defensive" }),
            new CardDefinition("fireball", 4, new[] { "Spell" }),
            new CardDefinition("ice_spirit", 1, new[] { "Defensive", "Cycle" }),
            new CardDefinition("skeletons", 1, new[] { "Defensive", "Cycle" }),
            new CardDefinition("ice_golem", 2, new[] { "Defensive" }),
            new CardDefinition("log", 2, new[] { "Spell" })
        },
        ExcludedCardIds = Array.Empty<string>(),
        DefensivePriorityCardIds = new[] { "musketeer", "ice_golem", "skeletons", "ice_spirit", "cannon" },
        StrongThreatThreshold = 50,
        ExcludeSpells = true,
        ExcludeBuildings = true
    };
}

public sealed record CardDefinition(string Id, int Cost, string[] Roles);

public static class CardRoleParser
{
    public static CardRole Parse(string[]? roles)
    {
        if (roles == null || roles.Length == 0)
        {
            return CardRole.None;
        }

        CardRole result = CardRole.None;
        foreach (string role in roles)
        {
            if (Enum.TryParse<CardRole>(role, ignoreCase: true, out var parsed))
            {
                result |= parsed;
            }
        }
        return result;
    }
}
