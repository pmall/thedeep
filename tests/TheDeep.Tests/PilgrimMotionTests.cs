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
