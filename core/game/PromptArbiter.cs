using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// Arbitrates the single interact prompt and button press across overlapping interactables (shrines, scripture plates, law breaks,
/// levers, conversations), pure. Each interactable kind registers its nearest eligible owner in its <see cref="InteractionPriority"/>
/// slot; the lowest-priority active slot owns the prompt (<see cref="IsActive"/>) and may consume the tick's interact press at most
/// once (<see cref="TryUse"/>), preventing prompt overlap or double-firing on a single press.
/// </summary>
public sealed class PromptArbiter
{
    private readonly Dictionary<InteractionPriority, PromptCandidate> _slots = new();
    private ulong? _usedTick;

    /// <summary>The active interactable that currently owns the interact prompt, or null when none is eligible.</summary>
    public PromptCandidate? Winner { get; private set; }

    /// <summary>Whether any interactable currently owns the prompt.</summary>
    public bool Any => Winner is not null;

    /// <summary>
    /// Updates <paramref name="slot"/> to <paramref name="owner"/> (at <paramref name="distance"/> meters), or clears the slot when
    /// <paramref name="owner"/> is null, and recomputes <see cref="Winner"/>.
    /// </summary>
    public void Set(InteractionPriority slot, object? owner, float distance = 0f)
    {
        if (owner is null)
        {
            _slots.Remove(slot);
        }
        else
        {
            _slots[slot] = new PromptCandidate(owner, slot, distance);
        }

        Recompute();
    }

    /// <summary>Whether <paramref name="owner"/> currently owns the interact prompt.</summary>
    public bool IsActive(object owner) => Winner is { } winner && ReferenceEquals(winner.Owner, owner);

    /// <summary>
    /// Claims the interact press on physics tick <paramref name="tick"/> for <paramref name="owner"/> if it owns the prompt and no
    /// other interactable has already consumed that tick's press.
    /// </summary>
    public bool TryUse(object owner, ulong tick)
    {
        if (!IsActive(owner) || _usedTick == tick)
        {
            return false;
        }

        _usedTick = tick;
        return true;
    }

    /// <summary>Clears every active candidate and any consumed press (when changing rooms).</summary>
    public void Clear()
    {
        _slots.Clear();
        Winner = null;
        _usedTick = null;
    }

    private void Recompute()
    {
        PromptCandidate? best = null;
        foreach (PromptCandidate candidate in _slots.Values)
        {
            if (best is not { } current
                || candidate.Priority < current.Priority
                || (candidate.Priority == current.Priority && candidate.Distance < current.Distance))
            {
                best = candidate;
            }
        }

        Winner = best;
    }
}
