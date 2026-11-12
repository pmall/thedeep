namespace TheDeep;

/// <summary>
/// A piece the pilgrim walks into or through by design (a pickup's plinth, a false wall): its meshes stand in the play plane with no
/// solid, and the room check's open-mesh rule leaves them (<see cref="GroundRules.OpenSpans"/>).
/// </summary>
public interface IWalkedThrough
{
}
