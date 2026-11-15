using Godot;

namespace TheDeep;

/// <summary>The engine's version, as Godot prints it ("4.7.2.stable.mono..."), read once.</summary>
public static class EngineVersion
{
    /// <summary>The version text.</summary>
    public static readonly string Text = Read();

    private static string Read()
    {
        using Godot.Collections.Dictionary info = Engine.GetVersionInfo();
        using Variant key = "string";
        using Variant text = info[key];
        return text.AsString();
    }
}
