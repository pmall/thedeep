using Godot;

namespace TheDeep;

/// <summary>
/// A species' behavior as data: which brain it runs, with which knobs. Each subclass exports the knobs of
/// one brain (scripts/machines/brains) and creates it; species sharing a behavior with other knobs are
/// still one behavior. A truly new behavior is a new brain plus a small subclass like these.
/// </summary>
[GlobalClass]
public abstract partial class MachineBehavior : Resource
{
    /// <summary>A brain for one machine of <paramref name="species"/>; <paramref name="seed"/> varies it per machine.</summary>
    public abstract MachineBrain CreateBrain(MachineSpecies species, int seed);

    /// <summary>A value in [0, 1) from <paramref name="seed"/>, to vary machines of one species.</summary>
    protected static float Jitter(int seed, int salt) => (float)new System.Random(seed * 7919 + salt).NextDouble();
}
