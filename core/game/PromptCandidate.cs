namespace TheDeep;

/// <summary>One interactable currently eligible to show its prompt and take the interact button.</summary>
/// <param name="Owner">The interactable claiming the slot.</param>
/// <param name="Priority">Lower priority wins when two interactables overlap.</param>
/// <param name="Distance">Distance to the pilgrim (meters); breaks ties within the same priority.</param>
public readonly record struct PromptCandidate(object Owner, InteractionPriority Priority, float Distance);
