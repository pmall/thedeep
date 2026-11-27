namespace TheDeep;

/// <summary>Order in which overlapping interactables claim the single interact prompt and button press (lower wins).</summary>
public enum InteractionPriority
{
    Shrine = 0,
    Plate = 1,
    LawBreak = 2,
    Lever = 3,
    Conversation = 4,
}
