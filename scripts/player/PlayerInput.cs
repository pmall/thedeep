namespace TheDeep;

/// <summary>Held input for one physics tick. Presses go into the player's input buffers instead.</summary>
public readonly record struct PlayerInput(int MoveX, int MoveY, bool JumpHeld);
