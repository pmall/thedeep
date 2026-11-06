namespace TheDeep;

/// <summary>A held moment the pilgrim's animation plays over whatever the body is doing (<see cref="PilgrimMotion.Moment"/>).</summary>
public enum PilgrimMoment
{
    None,

    /// <summary>Resting at a shrine: down on one knee, head bowed.</summary>
    Rest,

    /// <summary>Taking a relic: chest lifted, head back, arms open to receive it.</summary>
    Relic,

    /// <summary>Dying: collapsed onto the knees, slumped.</summary>
    Death,
}
