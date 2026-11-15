using Godot;

namespace TheDeep;

/// <summary>The theme items the UI overrides, named once for the process (instructions/godot-csharp.md#no-finalizer).</summary>
public static class ThemeNames
{
    public static readonly StringName Separation = "separation";
    public static readonly StringName Panel = "panel";
    public static readonly StringName Font = "font";
    public static readonly StringName FontSize = "font_size";
    public static readonly StringName FontColor = "font_color";
    public static readonly StringName FontShadowColor = "font_shadow_color";
    public static readonly StringName ShadowOutlineSize = "shadow_outline_size";
    public static readonly StringName ShadowOffsetX = "shadow_offset_x";
    public static readonly StringName ShadowOffsetY = "shadow_offset_y";
    public static readonly StringName LineSpacing = "line_spacing";
}
