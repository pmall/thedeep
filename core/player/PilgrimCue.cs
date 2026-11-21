namespace TheDeep;

/// <summary>A short move the pilgrim's body plays once over its pose (<see cref="PilgrimAnimation.Cue"/>).</summary>
public enum PilgrimCue
{
    None,
    /// <summary>Touching down from a short fall.</summary>
    Land,
    /// <summary>Touching down from a long fall: the knees take it.</summary>
    HardLand,
    /// <summary>A blow taken.</summary>
    Hurt,
    /// <summary>The ledge assist lifting the body onto a top.</summary>
    Vault,
}
