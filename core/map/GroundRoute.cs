using System.Globalization;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A ground route to play (<see cref="GroundRoutes"/>): the planner's <see cref="Leap"/>, where the real pilgrim starts
/// (<see cref="Start"/>: a run-up behind a running take-off, the take-off itself for a standing one) and the floor it must land on.
/// </summary>
public sealed record GroundRoute(Leap Leap, Vector2 Start, Ledge Landing)
{
    /// <summary>The route in words, for a problem line.</summary>
    public string What
    {
        get
        {
            GroundMove m = Leap.Move;
            string move = m.JumpTicks switch
            {
                0 => "walking off",
                < 0 => "a full jump",
                _ => $"a jump cut after {m.JumpTicks} ticks",
            };
            string way = m.Dir switch { < 0 => " west", > 0 => " east", _ => " straight up" };
            string dash = m.DashTick >= 0 ? $", an air dash at tick {m.DashTick}" : "";
            Vector2 end = Leap.Path[^1];
            return string.Create(CultureInfo.InvariantCulture,
                $"{move}{way}{dash} from ({Leap.TakeOff.X:0.##}, {Leap.TakeOff.Y:0.##}) onto ({end.X:0.##}, {end.Y:0.##})");
        }
    }
}
