using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using Xunit;

namespace TheDeep.Tests;

/// <summary>The tests' tuning fixtures (<see cref="TestTuning"/>) held to the files they claim to copy: player_tuning.tres and parts/pilgrim.tres.</summary>
public class TuningFixtureTests
{
    private static Dictionary<string, float> Tuning()
    {
        // The main [resource] block: `Name = number` lines after the last [resource] header.
        string text = File.ReadAllText(RepoPaths.Of("resources/player_tuning.tres"));
        string main = text[text.LastIndexOf("[resource]", System.StringComparison.Ordinal)..];
        var values = new Dictionary<string, float>();
        foreach (Match m in Regex.Matches(main, @"^(\w+) = (-?[\d.]+)\s*$", RegexOptions.Multiline))
        {
            values[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        return values;
    }

    [Fact]
    public void TheMoveFixtureIsTheLiveTuning()
    {
        Dictionary<string, float> t = CommaDecimals.Under(Tuning);
        MoveLimits m = TestTuning.Move;
        Assert.Equal(
            new[] { t["RunSpeed"], t["AirAcceleration"], t["AirDeceleration"], t["AirDrag"], t["AirCarrySpeed"], t["JumpHeight"], t["JumpTimeToApex"], t["JumpCutFactor"],
                t["FallGravityMultiplier"], t["ApexHangThreshold"], t["ApexGravityMultiplier"], t["MaxFallSpeed"], t["DashDistance"], t["DashDuration"],
                t["DashExitSpeedFactor"], t["AirDashes"], 2f * t["BodyRadius"], t["BodyHeight"] },
            new[] { m.RunSpeed, m.AirAcceleration, m.AirDeceleration, m.AirDrag, m.AirCarrySpeed, m.JumpHeight, m.JumpTimeToApex, m.JumpCutFactor,
                m.FallGravityMultiplier, m.ApexHangThreshold, m.ApexGravityMultiplier, m.MaxFallSpeed, m.DashDistance, m.DashDuration,
                m.DashExitSpeedFactor, m.AirDashes, m.Width, m.Height });
    }

    [Fact]
    public void TheHarpoonGripAndWaterFixturesAreTheLiveTuning()
    {
        Dictionary<string, float> t = Tuning();
        HarpoonLimits h = TestTuning.Harpoon;
        Assert.Equal(
            new[] { t["HarpoonRange"], t["HarpoonMinDistance"], t["HarpoonZipSpeed"], t["HarpoonZipAcceleration"], t["HarpoonExitSpeedFactor"], t["HarpoonExitLift"] },
            new[] { h.Range, h.MinDistance, h.ZipSpeed, h.ZipAcceleration, h.ExitSpeedFactor, h.ExitLift });
        GripLimits g = TestTuning.Grip;
        Assert.Equal(
            new[] { t["GripLeapAwaySpeed"], t["GripLeapUpFactor"], t["GripRegrabDelay"], t["GripDropSpeed"], t["GripLetGoSpeed"] },
            new[] { g.LeapAwaySpeed, g.LeapUpFactor, g.RegrabDelay, g.DropSpeed, g.LetGoSpeed });
        WaterLimits w = TestTuning.Water;
        Assert.Equal(
            new[] { t["SwimSpeed"], t["SwimAcceleration"], t["SwimDeceleration"], t["FloatDepth"], t["FloatStiffness"], t["RiseSpeed"], t["SinkSpeed"],
                t["WaterAcceleration"], t["SplashKeep"], t["WaterEnterDepth"], t["WaterJumpWindow"] },
            new[] { w.SwimSpeed, w.SwimAcceleration, w.SwimDeceleration, w.FloatDepth, w.FloatStiffness, w.RiseSpeed, w.SinkSpeed,
                w.WaterAcceleration, w.SplashKeep, w.EnterDepth, w.JumpWindow });
        Assert.Equal(TestTuning.ClimbRise * JumpMath.TakeOffSpeed(t["JumpHeight"], t["JumpTimeToApex"]), TestTuning.Planner.Rise);
    }

    [Fact]
    public void TheControlFixtureIsTheLiveTuning()
    {
        Dictionary<string, float> t = Tuning();
        ControlLimits c = TestTuning.Control;
        Assert.Equal(
            new[] { t["RunSpeed"], t["GroundAcceleration"], t["GroundDeceleration"], t["AirAcceleration"], t["AirDeceleration"], t["AirDrag"], t["AirCarrySpeed"],
                JumpMath.Gravity(t["JumpHeight"], t["JumpTimeToApex"]), t["ApexHangThreshold"], t["ApexGravityMultiplier"], t["FallGravityMultiplier"],
                t["MaxFallSpeed"], t["CoyoteTime"], t["JumpBufferTime"], t["DashBufferTime"], t["DashCooldown"], t["HarpoonBufferTime"], t["GripRegrabDelay"],
                t["AirDashes"], t["HurtKnockback"], t["HurtLift"], t["HurtStun"], t["StickThreshold"] },
            new[] { c.RunSpeed, c.GroundAcceleration, c.GroundDeceleration, c.AirAcceleration, c.AirDeceleration, c.AirDrag, c.AirCarrySpeed,
                c.Gravity, c.ApexHangThreshold, c.ApexGravityMultiplier, c.FallGravityMultiplier,
                c.MaxFallSpeed, c.CoyoteTime, c.JumpBufferTime, c.DashBufferTime, c.DashCooldown, c.HarpoonBufferTime, c.GripRegrabDelay,
                c.AirDashes, c.HurtKnockback, c.HurtLift, c.HurtStun, c.StickThreshold });
    }

    [Fact]
    public void TheRigFixtureIsThePilgrimsParts()
    {
        // Joint offsets from the sub-resources: `Name = &"X"` then `Offset = Vector3(x, y, z)`.
        string text = File.ReadAllText(RepoPaths.Of("resources/parts/pilgrim.tres"));
        var rest = new Dictionary<string, Vector3>();
        foreach (Match m in Regex.Matches(text, @"Name = &""(\w+)""\r?\nParent = [^\r\n]*\r?\nOffset = Vector3\(([^)]*)\)"))
        {
            string[] p = m.Groups[2].Value.Split(',');
            rest[m.Groups[1].Value] = new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }

        HumanoidDims d = TestTuning.Dims;
        Assert.Equal(
            new[] { rest["Hips"].Y, rest["ThighR"].X, rest["ThighR"].Y, rest["ThighR"].Z, rest["ShinR"].Length(), rest["FootR"].Length(),
                rest["Spine"].Y + rest["Chest"].Y + rest["UpperArmR"].Y, rest["ForearmR"].Length(), rest["HandR"].Length() },
            new[] { d.HipsHeight, d.HipJoint.X, d.HipJoint.Y, d.HipSpread, d.Thigh, d.Shin, d.ShoulderHeight, d.UpperArm, d.Forearm },
            new FloatComparer());

        static float F(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);
    }

    private sealed class FloatComparer : IEqualityComparer<float>
    {
        public bool Equals(float a, float b) => System.MathF.Abs(a - b) < 1e-5f;

        public int GetHashCode(float f) => 0;
    }
}
