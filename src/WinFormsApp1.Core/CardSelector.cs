using System;
using System.Collections.Generic;

namespace WinFormsApp1.Core;

public sealed class CardSelector
{
    private readonly IReadOnlyList<string> _defensePriority;

    public CardSelector()
    {
        _defensePriority = new List<string>
        {
            "cannon",
            "skeletons",
            "ice_spirit",
            "ice_golem",
            "musketeer",
            "log",
            "fireball",
            "hog"
        };
    }

    public string SelectDefenseCard(HandState hand)
    {
        foreach (string card in _defensePriority)
        {
            if (hand.Contains(card))
            {
                return card;
            }
        }

        return string.Empty;
    }
}
