using System.Diagnostics;
using Godot;

namespace TheDeep;

/// <summary>
/// Bakes every texture set the materials sample (<see cref="SurfaceSet.All"/>, through <see cref="SurfaceBaker"/>) to PNGs in
/// <see cref="Folder"/>, prints "BAKE OK" and quits. Run by hand when a pattern changes, then import, and commit the PNGs:
/// <code>godot --headless --path . -- --bake-surfaces &amp;&amp; godot --headless --import</code>
/// The bake is deterministic (the same pattern writes the same bytes), so a set that did not change shows no diff. Each PNG's
/// import settings are its committed <c>.import</c> file's <c>[params]</c>, kept by a re-bake: VRAM compressed (BPTC), mipmaps,
/// the normal map imported as one. Not part of tools/check.sh: the check imports the committed textures.
/// </summary>
public partial class SurfaceBake : Node
{
    public const string Folder = "res://assets/textures/surfaces";

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
        bool ok = true;
        foreach (SurfaceSet set in SurfaceSet.All)
        {
            var clock = Stopwatch.StartNew();
            SurfaceMaps maps = SurfaceBaker.Bake(set.Pattern, set.Size);
            ok &= Save(maps.Normal, maps.Size, $"{Folder}/{set.Name}_normal.png");
            ok &= Save(maps.Surface, maps.Size, $"{Folder}/{set.Name}_surface.png");
            GD.Print($"[bake] {set.Name}: {maps.Size}x{maps.Size}, {clock.ElapsedMilliseconds} ms");
        }

        if (ok)
        {
            GD.Print("BAKE OK");
        }

        GetTree().Quit(ok ? 0 : 1);
    }

    private static bool Save(byte[] rgb, int size, string path)
    {
        Error error = Image.CreateFromData(size, size, false, Image.Format.Rgb8, rgb).SavePng(path);
        if (error != Error.Ok)
        {
            GD.PushError($"BAKE: could not write {path} ({error})");
            return false;
        }

        return true;
    }
}
