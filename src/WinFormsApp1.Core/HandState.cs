using System;

namespace WinFormsApp1.Core;

public readonly record struct HandState(string[] Slots, int[] Costs, float[] Confidences)
{
    public static HandState Empty => new(Array.Empty<string>(), Array.Empty<int>(), Array.Empty<float>());

    public static HandState FromSlots(string[] slots)
    {
        return new HandState(slots, Array.Empty<int>(), Array.Empty<float>());
    }

    public string GetSlot(int index)
    {
        if (Slots.Length == 0 || index < 0 || index >= Slots.Length)
        {
            return string.Empty;
        }
        return Slots[index] ?? string.Empty;
    }

    public int GetCost(int index)
    {
        if (Costs.Length == 0 || index < 0 || index >= Costs.Length)
        {
            return -1;
        }
        return Costs[index];
    }

    public float GetConfidence(int index)
    {
        if (Confidences.Length == 0 || index < 0 || index >= Confidences.Length)
        {
            return 0f;
        }
        return Confidences[index];
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
