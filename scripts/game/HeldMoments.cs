using System;
using System.Threading.Tasks;
using Godot;

namespace TheDeep;

/// <summary>
/// The moments that hold the pilgrim (a rest, a relic taken, a law broken, a fall back to a shrine): while one is held the
/// session offers nothing else (<see cref="Busy"/>) and the pilgrim is <see cref="Player.Frozen"/>. Every way a moment ends, an
/// exception or a scene that freed the pilgrim meanwhile included, lets the pilgrim go: the flows end in <see cref="Let"/> or
/// <see cref="Release"/> from a <c>finally</c>, and a pilgrim already freed is left alone.
/// </summary>
public sealed class HeldMoments
{
    private readonly Player _player;

    public HeldMoments(Player player)
    {
        _player = player;
    }

    /// <summary>A moment is held: nothing else is offered to the pilgrim.</summary>
    public bool Busy { get; private set; }

    /// <summary>A moment begins: <see cref="Busy"/> until <see cref="Let"/> or <see cref="Release"/>.</summary>
    public void Begin() => Busy = true;

    /// <summary>
    /// Holds the pilgrim still in <paramref name="moment"/> for as long as <paramref name="during"/> takes, then lets it go whatever
    /// ended it.
    /// </summary>
    public async Task Hold(PilgrimMoment moment, Func<Task> during)
    {
        Begin();
        try
        {
            _player.Velocity = Vector3.Zero;
            _player.Frozen = true;
            _player.Visual.Moment = moment;
            await during();
        }
        finally
        {
            Release();
        }
    }

    /// <summary>Lets the pilgrim go and the session offer again, its pose as it is; a pilgrim already freed (the scene reloading) is left alone.</summary>
    public void Let()
    {
        if (GodotObject.IsInstanceValid(_player))
        {
            _player.Frozen = false;
        }

        Busy = false;
    }

    /// <summary>As <see cref="Let"/>, and the pilgrim leaves the pose of the moment.</summary>
    public void Release()
    {
        if (GodotObject.IsInstanceValid(_player))
        {
            _player.Visual.Moment = PilgrimMoment.None;
        }

        Let();
    }
}
