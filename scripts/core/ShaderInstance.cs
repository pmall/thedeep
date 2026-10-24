using Godot;

namespace TheDeep;

/// <summary>
/// Per-instance shader uniform names (`instance uniform` in the shaders), set with
/// <see cref="GeometryInstance3D.SetInstanceShaderParameter"/>. Use these instead of string literals.
/// </summary>
public static class ShaderInstance
{
    /// <summary>Size of the box a mesh draws, in meters (assets/shaders/lib/box.gdshaderinc): edge wear, bevels, panels.</summary>
    public static readonly StringName BoxSize = "box_size";
}
