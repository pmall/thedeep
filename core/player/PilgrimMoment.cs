namespace TheDeep;

/// <summary>A held moment the pilgrim's animation plays over whatever the body is doing (<see cref="PilgrimAnimation.Moment"/>).</summary>
public enum PilgrimMoment
{
    None,

    /// <summary>Resting at a shrine: down on one knee, head bowed.</summary>
    Rest,

    /// <summary>Taking a relic: chest lifted, head back, arms open to receive it.</summary>
    Relic,

    /// <summary>Dying: thrown down on the face.</summary>
    Death,
}
