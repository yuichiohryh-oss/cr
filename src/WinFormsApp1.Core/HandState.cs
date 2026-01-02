using System;

namespace WinFormsApp1.Core;

public readonly record struct HandState(string[] Slots)
{
    public static HandState Empty => new(Array.Empty<string>());

    public string GetSlot(int index)
    {
        if (Slots.Length == 0 || index < 0 || index >= Slots.Length)
        {
            return string.Empty;
        }
        return Slots[index] ?? string.Empty;
    }

    public bool Contains(string cardId)
    {
        if (Slots.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < Slots.Length; i++)
        {
            if (string.Equals(Slots[i], cardId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
