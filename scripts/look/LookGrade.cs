using Godot;

namespace TheDeep;

/// <summary>
/// The lookup a look's grade reaches the frame through: a 3D table of every color, through the biome's lift and gain
/// (<see cref="BiomeProfile.Lift"/>, <see cref="BiomeProfile.Gain"/>, as its own 1D lookup does) and then the look's
/// <see cref="ToneMap"/>, set as the environment's color correction. Rebuilt only when what it is built from changes, and
/// disposed by its owner, never left to the finalizer (instructions/godot-csharp.md#no-finalizer).
/// </summary>
public sealed class LookGrade : System.IDisposable
{
    /// <summary>Texels a side: enough for smooth ramps, a few milliseconds to build.</summary>
    private const int Size = 33;

    private ImageTexture3D? _texture;
    private (Color Lift, Color Gain, ToneMap Tone)? _built;

    /// <summary>The lookup for a biome graded from <paramref name="lift"/> to <paramref name="gain"/> under <paramref name="tone"/>.</summary>
    public ImageTexture3D For(Color lift, Color gain, ToneMap tone)
    {
        if (_texture is not null && _built == (lift, gain, tone))
        {
            return _texture;
        }

        _texture?.Dispose();
        _built = (lift, gain, tone);
        System.Numerics.Vector3 low = lift.ToCore();
        System.Numerics.Vector3 span = gain.ToCore() - low;
        var images = new Image[Size];
        var bytes = new byte[Size * Size * 3];
        for (int b = 0; b < Size; b++)
        {
            for (int g = 0; g < Size; g++)
            {
                for (int r = 0; r < Size; r++)
                {
                    // Each texel holds the color at its center, so the engine's linear filter reads the table exactly between them.
                    var color = new System.Numerics.Vector3(r + 0.5f, g + 0.5f, b + 0.5f) / Size;
                    System.Numerics.Vector3 graded = tone.Apply(low + span * color);
                    int at = (g * Size + r) * 3;
                    bytes[at] = Byte(graded.X);
                    bytes[at + 1] = Byte(graded.Y);
                    bytes[at + 2] = Byte(graded.Z);
                }
            }

            images[b] = Image.CreateFromData(Size, Size, false, Image.Format.Rgb8, bytes);
        }

        _texture = new ImageTexture3D();
        using (EngineArrays.Owned<Image> slices = EngineArrays.Own(new Godot.Collections.Array<Image>(images)))
        {
            _texture.Create(Image.Format.Rgb8, Size, Size, Size, false, slices.Value);
        }

        foreach (Image image in images)
        {
            image.Dispose();
        }

        return _texture;
    }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        _built = null;
    }

    private static byte Byte(float channel) => (byte)System.Math.Clamp((int)System.MathF.Round(channel * 255f), 0, 255);
}
