using Godot;

namespace TheDeep;

/// <summary>
/// Engine names and property paths shared across the game, made once for the process
/// (instructions/godot-csharp.md#no-finalizer). A name only one class uses lives in that class, as a <c>static readonly</c>.
/// </summary>
public static class Names
{
    /// <summary>The empty name: the default of a name property not yet set.</summary>
    public static readonly StringName None = "";

    /// <summary>A canvas item's opacity (<c>modulate</c>'s alpha), for tweens.</summary>
    public static readonly NodePath ModulateAlpha = "modulate:a";

    /// <summary>A canvas item's whole <c>modulate</c>, for tweens.</summary>
    public static readonly NodePath Modulate = "modulate";

    /// <summary>A color rect's alpha, for tweens.</summary>
    public static readonly NodePath ColorAlpha = "color:a";

    /// <summary>A light's energy, for tweens.</summary>
    public static readonly NodePath LightEnergy = "light_energy";

    /// <summary>A 3D node's roll in degrees, for tweens.</summary>
    public static readonly NodePath RollDegrees = "rotation_degrees:z";

    /// <summary>A geometry's transparency, for tweens.</summary>
    public static readonly NodePath Transparency = "transparency";
}
