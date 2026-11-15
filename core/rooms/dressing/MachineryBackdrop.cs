using System;
using System.Collections.Generic;
using System.Numerics;
using static TheDeep.DressingLayout;

namespace TheDeep;

/// <summary>A machinery backdrop (<see cref="BackdropStyle.Machinery"/>): the rules over <see cref="MachineryBackdropSpec"/>.</summary>
public static class MachineryBackdrop
{
    /// <summary>The salts of its decisions per gear.</summary>
    private const int GearHeight = 1, GearSize = 2, GearTurn = 4;

    private static readonly MachineryBackdropSpec Spec = MachineryBackdropSpec.Default;

    /// <summary>The pieces of one row of the machinery.</summary>
    public static List<BackdropPlacement> Build(BackdropRow row)
    {
        MachineryBackdropSpec s = Spec;
        var d = BackdropDims.For(row);
        var list = new List<BackdropPlacement>();
        List<float> xs = BackdropLayout.Bays(row);
        float band = BackdropLayout.Band(row);
        for (int k = 0; k < xs.Count; k++)
        {
            // A piston column on every bay line; a great gear on its axle every other bay, at an uneven height.
            list.Add(new BackdropPlacement(BackdropPiece.Piston, new Vector3(xs[k], row.BaseY, 0f), Length: row.TopY - row.BaseY));
            if (k % 2 == 1 || k + 1 >= xs.Count)
            {
                continue;
            }

            float cx = (xs[k] + xs[k + 1]) * 0.5f;
            float cy = row.FloorY + band * (s.GearHeightShare + s.GearHeightSpread * Hash01(row.Seed, GearHeight, k));
            float radius = d.GearRadius * (s.GearSizeShare + s.GearSizeSpread * Hash01(row.Seed, GearSize, k));
            // In front of the pistons, so they pass behind the gear.
            float z = s.GearDepth * row.Scale;
            list.Add(new BackdropPlacement(BackdropPiece.Gear, new Vector3(cx, cy, z), RollZ: Hash01(row.Seed, GearTurn, k) * MathF.PI, Length: radius));
            list.Add(new BackdropPlacement(BackdropPiece.Hub, new Vector3(cx, cy, z - s.HubBack * row.Scale), PitchX: MathF.PI * 0.5f));
        }

        return list;
    }
}
