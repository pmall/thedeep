using Godot;

namespace TheDeep;

/// <summary>
/// A line spoken to the pilgrim when it comes near, once per visit (a priest-machine's command at the forbidden door): an area
/// on the play plane, centered on the node; <see cref="Session"/> has the banner speak it when the pilgrim's feet enter it, in its
/// spoken voice (<see cref="Voice.Spoken"/>: the speaker named over the words, no title), so it never reads as a name announced.
/// Text only: it opens and closes nothing.
/// </summary>
public partial class Proclamation : Marker3D, IAnnounces
{
    /// <summary>The area on the play plane (width, height), centered on the node.</summary>
    [Export] public Vector2 Size { get; set; } = new(6f, 4f);

    /// <summary>Who speaks (a line naming the speaker, its caption over the words) and what it says.</summary>
    [Export] public StringName SpeakerLine { get; set; } = "";
    [Export] public StringName TextLine { get; set; } = "";

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { SpeakerLine, TextLine };

    /// <summary>What the banner says as it speaks.</summary>
    public Announcement Said => Announcement.Spoken(SpeakerLine, TextLine);

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<Announcement> Announcements => new[] { Said };

    /// <summary>Whether <paramref name="point"/> is inside the area on the play plane.</summary>
    public bool Contains(Vector3 point) =>
        Mathf.Abs(point.X - GlobalPosition.X) <= Size.X * 0.5f && Mathf.Abs(point.Y - GlobalPosition.Y) <= Size.Y * 0.5f;
}
