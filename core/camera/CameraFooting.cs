namespace TheDeep;

/// <summary>What the pilgrim's body is doing, as the camera's vertical follow sees it (<see cref="CameraFollow"/>).</summary>
public enum CameraFooting
{
    /// <summary>Standing upright on a floor: the camera settles on its height.</summary>
    Ground,

    /// <summary>A jump or a fall: the camera holds the height it left until the body rises out of its band or drops below it.</summary>
    Air,

    /// <summary>Gripping, swimming, zipping on the Harpoon's line: the camera follows the body's height as it goes.</summary>
    Tracked,
}
