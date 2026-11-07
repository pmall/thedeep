using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class PilgrimMotionTests
{
    private const float Dt = 1f / 60f;
    private const float RunSpeed = 7f;

    /// <summary>The pilgrim's rig sizes (resources/parts/pilgrim.tres).</summary>
    private static readonly HumanoidDims Dims = new(0.9f, new Vector2(0f, -0.05f), 0.4f, 0.39f, 0.43f, 0.26f, 0.24f);

    [Fact]
    public void StandingStillKeepsBothAnklesOnTheGround()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 120, Vector2.Zero, MotionMode.Ground);

        foreach ((HumanoidBone thigh, HumanoidBone shin) in Legs)
        {
            Vector2 ankle = Ankle(motion.Pose, thigh, shin);
            Assert.InRange(ankle.Y, Dims.AnkleHeight - 0.01f, Dims.AnkleHeight + 0.01f);
            Assert.InRange(ankle.X, -0.2f, 0.2f);
        }
    }

    [Fact]
    public void RunningAlternatesFootfallsAndKeepsFeetAboveTheGround()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        var falls = new List<int>();
        for (int i = 0; i < 180; i++)
        {
            motion.Update(Dt, new Vector2(RunSpeed, 0f), 1, MotionMode.Ground);
            if (motion.Footfall != 0)
            {
                falls.Add(motion.Footfall);
            }

            foreach ((HumanoidBone thigh, HumanoidBone shin) in Legs)
            {
                Assert.True(Ankle(motion.Pose, thigh, shin).Y > Dims.AnkleHeight - 0.02f, "a foot went through the ground");
            }
        }

        // About 2.6 steps per second at run speed, alternating sides.
        Assert.InRange(falls.Count, 12, 20);
        for (int i = 1; i < falls.Count; i++)
        {
            Assert.NotEqual(falls[i - 1], falls[i]);
        }
    }

    [Fact]
    public void PlantedFootDoesNotSlideAtRunSpeed()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 90, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        // While a foot is on the ground its ankle moves back under the hips as fast as the body moves forward.
        float bodyX = 0f;
        Vector2? previousAnkle = null;
        for (int i = 0; i < 4; i++)
        {
            motion.Update(Dt, new Vector2(RunSpeed, 0f), 1, MotionMode.Ground);
            bodyX += RunSpeed * Dt;
            Vector2 ankle = Ankle(motion.Pose, HumanoidBone.ThighR, HumanoidBone.ShinR);
            if (previousAnkle is { } before && ankle.Y < Dims.AnkleHeight + 0.005f && before.Y < Dims.AnkleHeight + 0.005f)
            {
                Assert.InRange(ankle.X - before.X + RunSpeed * Dt, -0.03f, 0.03f);
            }

            previousAnkle = ankle;
        }
    }

    [Fact]
    public void DashLeansTheChestForwardAndSweepsTheArmsBack()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        Run(motion, 6, new Vector2(28f, 0f), MotionMode.Dash);

        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.5f, $"chest {motion.Pose[HumanoidBone.Chest]}");
        Assert.True(motion.Pose[HumanoidBone.UpperArmR] < -1.4f, $"arm {motion.Pose[HumanoidBone.UpperArmR]}");
    }

    [Fact]
    public void HardLandingCompressesThenRecovers()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        float standing = motion.Pose.HipsOffset.Y;
        motion.Land(20f);
        float lowest = standing;
        for (int i = 0; i < 30; i++)
        {
            motion.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
            lowest = MathF.Min(lowest, motion.Pose.HipsOffset.Y);
        }

        Assert.True(lowest < standing - 0.12f, $"hips only dropped {standing - lowest}");
        Run(motion, 120, Vector2.Zero, MotionMode.Ground);
        Assert.InRange(motion.Pose.HipsOffset.Y, standing - 0.01f, standing + 0.01f);
    }

    [Fact]
    public void LeansIntoAccelerationAndBackWhenBraking()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        float rest = motion.Pose[HumanoidBone.Chest];
        // Speeding up at 70 m/s² for 0.1 s.
        for (int i = 1; i <= 6; i++)
        {
            motion.Update(Dt, new Vector2(70f * Dt * i, 0f), 1, MotionMode.Ground);
        }

        Assert.True(motion.Pose[HumanoidBone.Chest] < rest - 0.15f, "leans forward when accelerating");

        var braking = new PilgrimMotion(Dims, RunSpeed);
        Run(braking, 60, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        float running = braking.Pose[HumanoidBone.Chest];
        for (int i = 1; i <= 6; i++)
        {
            braking.Update(Dt, new Vector2(RunSpeed - 80f * Dt * i, 0f), 1, MotionMode.Ground);
        }

        Assert.True(braking.Pose[HumanoidBone.Chest] > running + 0.15f, "leans back when braking");
    }

    [Fact]
    public void FacingLeftMirrorsNothing()
    {
        // The pose is in the body's own frame: running left facing left looks like running right facing right.
        var right = new PilgrimMotion(Dims, RunSpeed);
        var left = new PilgrimMotion(Dims, RunSpeed);
        Run(right, 50, new Vector2(RunSpeed, 0f), MotionMode.Ground, 1);
        Run(left, 50, new Vector2(-RunSpeed, 0f), MotionMode.Ground, -1);
        foreach (HumanoidBone bone in Enum.GetValues<HumanoidBone>())
        {
            Assert.Equal(right.Pose[bone], left.Pose[bone], 4);
        }
    }

    [Fact]
    public void SwimmingAcrossLeansIntoTheStrokeAndTreadingStaysUpright()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 120, Vector2.Zero, MotionMode.Swim);
        Assert.InRange(motion.Pose[HumanoidBone.Chest], -0.2f, 0.1f);

        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i < 120; i++)
        {
            motion.Update(Dt, new Vector2(3.5f, 0f), 1, MotionMode.Swim);
            lowest = MathF.Min(lowest, motion.Pose[HumanoidBone.UpperArmR]);
            highest = MathF.Max(highest, motion.Pose[HumanoidBone.UpperArmR]);
        }

        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.4f, "swimming across leans forward into the stroke");
        Assert.True(highest - lowest > 1.2f, "the arm paddles from forward to back");
    }

    [Fact]
    public void DivingRaisesTheArmsOverheadSpinsAndSettlesFacingAgain()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 20, new Vector2(0f, -20f), MotionMode.Dive);
        Assert.True(motion.Pose[HumanoidBone.UpperArmR] > 2.6f && motion.Pose[HumanoidBone.UpperArmL] > 2.6f, "both arms overhead");
        Assert.True(motion.Pose[HumanoidBone.FootR] < -1f, "toes pointed down");
        Assert.True(motion.Spin > MathF.PI, "the body spins like a drill");

        Run(motion, 90, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(0f, motion.Spin);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0.6f, 0.8f)]
    [InlineData(0f, 1f)]
    public void OnTheHarpoonsLineTheNearArmReachesAlongIt(float x, float y)
    {
        var motion = new PilgrimMotion(Dims, RunSpeed) { Reach = new Vector2(x, y) };
        Run(motion, 30, Vector2.Zero, MotionMode.Zip);
        float along = MathF.Atan2(x, -y);
        Assert.InRange(motion.Pose[HumanoidBone.UpperArmR], along - 0.05f, along + 0.05f);
        Assert.InRange(motion.Pose[HumanoidBone.ForearmR], along - 0.05f, along + 0.05f);
    }

    [Fact]
    public void AStrikeRaisesTheArmInTheWindUpSweepsItThroughTheBlowAndLetsGo()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, Vector2.Zero, MotionMode.Ground);
        float rest = motion.Pose[HumanoidBone.UpperArmR];

        motion.Strike(0.25f, 0.25f, 0.5f, StrikeKind.Swing);
        motion.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        Assert.True(motion.Pose[HumanoidBone.UpperArmR] > 2.4f, "wound up over the shoulder");

        motion.Strike(0.5f, 0.25f, 0.5f, StrikeKind.Swing);
        motion.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        Assert.True(motion.Pose[HumanoidBone.UpperArmR] < 0.6f, "swept down through the front");
        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.25f, "the body behind the blow");

        motion.EndStrike();
        motion.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        Assert.InRange(motion.Pose[HumanoidBone.UpperArmR], rest - 0.05f, rest + 0.05f);
    }

    [Fact]
    public void AForwardStrikeLeavesTheLegsToTheGaitAndADownStrikeTucksThem()
    {
        var swing = new PilgrimMotion(Dims, RunSpeed);
        var still = new PilgrimMotion(Dims, RunSpeed);
        Run(swing, 30, Vector2.Zero, MotionMode.Ground);
        Run(still, 30, Vector2.Zero, MotionMode.Ground);
        swing.Strike(0.4f, 0.25f, 0.5f, StrikeKind.Swing);
        swing.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        still.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        Assert.Equal(still.Pose[HumanoidBone.ThighR], swing.Pose[HumanoidBone.ThighR], 3);

        var down = new PilgrimMotion(Dims, RunSpeed);
        Run(down, 20, new Vector2(0f, -3f), MotionMode.Air);
        down.Strike(0.5f, 0.25f, 0.5f, StrikeKind.Down);
        down.Update(Dt, new Vector2(0f, -3f), 1, MotionMode.Air);
        Assert.True(down.Pose[HumanoidBone.ThighR] > 0.9f, "the legs tucked up");
        Assert.InRange(down.Pose[HumanoidBone.UpperArmR], -0.4f, 0.4f);
    }

    [Fact]
    public void OnMagGripTheBodyClingsLowWithItsFeetOnTheSurface()
    {
        var walk = new PilgrimMotion(Dims, RunSpeed);
        var grip = new PilgrimMotion(Dims, RunSpeed);
        Run(walk, 90, new Vector2(5.5f, -2f), MotionMode.Ground);
        Run(grip, 90, new Vector2(5.5f, -2f), MotionMode.Grip);
        Assert.True(grip.Pose.HipsOffset.Y < walk.Pose.HipsOffset.Y - 0.08f, "crouched lower");
        Assert.True(grip.Pose[HumanoidBone.Chest] < walk.Pose[HumanoidBone.Chest] - 0.15f, "leaned over the surface");

        // Still planted: no foot goes through the surface, and steps still fall in turn.
        var falls = new List<int>();
        for (int i = 0; i < 120; i++)
        {
            grip.Update(Dt, new Vector2(5.5f, -2f), 1, MotionMode.Grip);
            if (grip.Footfall != 0)
            {
                falls.Add(grip.Footfall);
            }

            foreach ((HumanoidBone thigh, HumanoidBone shin) in Legs)
            {
                Assert.True(Ankle(grip.Pose, thigh, shin).Y > Dims.AnkleHeight - 0.02f, "a foot went through the surface");
            }
        }

        Assert.True(falls.Count >= 6, $"only {falls.Count} steps");

        // Still on the surface, both feet down.
        Run(grip, 120, new Vector2(0f, -2f), MotionMode.Grip);
        foreach ((HumanoidBone thigh, HumanoidBone shin) in Legs)
        {
            Assert.InRange(Ankle(grip.Pose, thigh, shin).Y, Dims.AnkleHeight - 0.01f, Dims.AnkleHeight + 0.01f);
        }
    }

    [Fact]
    public void TheLedgeVaultTucksTheKneesThenRunsOn()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 60, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        motion.Vault();
        Run(motion, 6, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        Assert.True(motion.Pose[HumanoidBone.ThighR] > 1.2f, "the knee tucked up over the edge");
        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.4f, "leaning over the top");
        Run(motion, 30, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        foreach ((HumanoidBone thigh, HumanoidBone shin) in Legs)
        {
            Assert.True(Ankle(motion.Pose, thigh, shin).Y < Dims.AnkleHeight + 0.35f, "back in the run");
        }
    }

    [Fact]
    public void HurtThrowsTheHeadBackThenRecovers()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, Vector2.Zero, MotionMode.Ground);
        float rest = motion.Pose[HumanoidBone.Head];
        motion.Hurt();
        Run(motion, 6, Vector2.Zero, MotionMode.Ground);
        Assert.True(motion.Pose[HumanoidBone.Head] > rest + 0.4f, "thrown back");
        Run(motion, 40, Vector2.Zero, MotionMode.Ground);
        Assert.InRange(motion.Pose[HumanoidBone.Head], rest - 0.05f, rest + 0.05f);
    }

    [Fact]
    public void MomentsHoldTheirPoseAndLetGo()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, Vector2.Zero, MotionMode.Ground);
        Vector2 standing = motion.Pose.HipsOffset;
        float head = motion.Pose[HumanoidBone.Head];

        motion.Moment = PilgrimMoment.Rest;
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        Assert.True(motion.Pose.HipsOffset.Y < standing.Y - 0.25f, "down on one knee");
        Assert.True(motion.Pose[HumanoidBone.Head] < head - 0.4f, "head bowed");
        Assert.True(motion.Pose[HumanoidBone.ThighR] > 1f && motion.Pose[HumanoidBone.ShinL] < -1.2f, "one knee up, the other on the ground");

        motion.Moment = PilgrimMoment.None;
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        Assert.InRange(motion.Pose.HipsOffset.Y, standing.Y - 0.02f, standing.Y + 0.02f);

        motion.Moment = PilgrimMoment.Relic;
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        Assert.True(motion.Pose[HumanoidBone.Head] > head + 0.25f, "head back to receive it");
        Assert.True(motion.Pose[HumanoidBone.UpperArmR] > 0.7f && motion.Pose[HumanoidBone.UpperArmL] > 0.5f, "arms open forward");

        motion.Moment = PilgrimMoment.Death;
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        Assert.True(motion.Pose.HipsOffset.Y < standing.Y - 0.4f, "collapsed onto the knees");
        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.5f, "slumped forward");

        motion.Reset();
        Assert.Equal(PilgrimMoment.None, motion.Moment);
    }

    private static readonly (HumanoidBone Thigh, HumanoidBone Shin)[] Legs =
    {
        (HumanoidBone.ThighR, HumanoidBone.ShinR), (HumanoidBone.ThighL, HumanoidBone.ShinL),
    };

    private static void Run(PilgrimMotion motion, int ticks, Vector2 velocity, MotionMode mode, int facing = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            motion.Update(Dt, velocity, facing, mode);
        }
    }

    private static Vector2 Ankle(HumanoidPose pose, HumanoidBone thigh, HumanoidBone shin)
    {
        var hips = new Vector2(pose.HipsOffset.X, Dims.HipsHeight + pose.HipsOffset.Y);
        Vector2 hip = hips + PlanarIk.Rotate(Dims.HipJoint, pose[HumanoidBone.Hips]);
        Vector2 knee = PlanarIk.End(hip, pose[thigh], Dims.Thigh);
        return PlanarIk.End(knee, pose[shin], Dims.Shin);
    }
}
