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
    private static readonly HumanoidDims Dims = new(0.9f, new Vector2(0f, -0.05f), 0.085f, 0.4f, 0.39f, 0.43f, 0.26f, 0.24f);

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
    public void TheRunningBodyBobsAndTurnsAgainstItselfWhileTheHeadStaysSteady()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 90, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        float lowHips = float.MaxValue, highHips = float.MinValue, minChest = float.MaxValue, maxChest = float.MinValue;
        float minHead = float.MaxValue, maxHead = float.MinValue, maxHipYaw = 0f;
        for (int i = 0; i < 60; i++)
        {
            motion.Update(Dt, new Vector2(RunSpeed, 0f), 1, MotionMode.Ground);
            HumanoidPose pose = motion.Pose;
            lowHips = MathF.Min(lowHips, pose.HipsOffset.Y);
            highHips = MathF.Max(highHips, pose.HipsOffset.Y);
            minChest = MathF.Min(minChest, pose[HumanoidBone.Chest]);
            maxChest = MathF.Max(maxChest, pose[HumanoidBone.Chest]);
            minHead = MathF.Min(minHead, pose[HumanoidBone.Head]);
            maxHead = MathF.Max(maxHead, pose[HumanoidBone.Head]);
            float hipYaw = pose.Turn(HumanoidBone.Hips).Yaw, chestYaw = pose.Turn(HumanoidBone.Chest).Yaw;
            maxHipYaw = MathF.Max(maxHipYaw, MathF.Abs(hipYaw));
            Assert.True(hipYaw * chestYaw <= 0f, $"the chest turns with the hips ({hipYaw}, {chestYaw})");
            Assert.True(pose[HumanoidBone.Chest] < -0.2f, $"the chest leans forward ({pose[HumanoidBone.Chest]})");
        }

        Assert.True(highHips - lowHips > 0.04f, $"the hips only bob {highHips - lowHips}");
        Assert.True(maxHipYaw > 0.15f, $"the hips only turn {maxHipYaw}");
        Assert.True(maxHead - minHead < 0.5f * (maxChest - minChest), "the head is as unsteady as the chest");

        Run(motion, 120, Vector2.Zero, MotionMode.Ground);
        Assert.InRange(motion.Pose.Turn(HumanoidBone.Hips).Yaw, -0.01f, 0.01f);
        Assert.InRange(motion.Pose.Turn(HumanoidBone.Chest).Yaw, -0.01f, 0.01f);
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

    // The tuning's strike timing as shares of the whole strike: the wind-up over by WindUp, the hit window by Active.
    private const float WindUp = 0.143f;
    private const float Active = 0.5f;

    [Fact]
    public void AStrikeCoilsTheBladeBehindCutsThroughTheFrontHoldsThenLetsGo()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, Vector2.Zero, MotionMode.Ground);
        float rest = motion.Pose[HumanoidBone.ForearmR];

        Strike(motion, WindUp, StrikeKind.Swing);
        Assert.True(motion.Pose[HumanoidBone.ForearmR] > 3.6f, "the blade cocked back over the shoulder");
        Assert.True(motion.Pose.Turn(HumanoidBone.Chest).Yaw < -0.4f, "the chest turned open, the near shoulder back");

        Strike(motion, Active, StrikeKind.Swing);
        Assert.True(motion.Pose[HumanoidBone.ForearmR] < 1f, "swept down through the front");
        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.3f, "the body behind the blow");
        Assert.True(motion.Pose.Turn(HumanoidBone.Chest).Yaw > 0.4f, "the near shoulder driven through");

        motion.EndStrike();
        motion.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        Assert.True(motion.Pose[HumanoidBone.ForearmR] < 1.2f, "the end pose held a moment");
        Run(motion, 40, Vector2.Zero, MotionMode.Ground);
        Assert.InRange(motion.Pose[HumanoidBone.ForearmR], rest - 0.05f, rest + 0.05f);
    }

    [Fact]
    public void TheHipsLeadTheCutAndTheArmComesLast()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, Vector2.Zero, MotionMode.Ground);
        Strike(motion, WindUp, StrikeKind.Swing);
        float hipsFrom = motion.Pose.Turn(HumanoidBone.Hips).Yaw, armFrom = motion.Pose[HumanoidBone.ForearmR];
        Strike(motion, Active, StrikeKind.Swing);
        float hipsTo = motion.Pose.Turn(HumanoidBone.Hips).Yaw, armTo = motion.Pose[HumanoidBone.ForearmR];

        var early = new PilgrimMotion(Dims, RunSpeed);
        Run(early, 30, Vector2.Zero, MotionMode.Ground);
        Strike(early, WindUp + (Active - WindUp) * 0.2f, StrikeKind.Swing);
        float hips = (early.Pose.Turn(HumanoidBone.Hips).Yaw - hipsFrom) / (hipsTo - hipsFrom);
        float arm = (early.Pose[HumanoidBone.ForearmR] - armFrom) / (armTo - armFrom);
        Assert.True(hips > arm + 0.15f, $"the hips ahead of the arm early in the cut ({hips:0.00} against {arm:0.00})");
    }

    [Fact]
    public void EachHitOfTheChainStartsWhereTheLastEnded()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 30, Vector2.Zero, MotionMode.Ground);
        foreach (StrikeKind kind in new[] { StrikeKind.Swing, StrikeKind.Second, StrikeKind.Finisher })
        {
            float before = motion.Pose[HumanoidBone.ForearmR];
            Strike(motion, 0f, kind);
            Assert.InRange(motion.Pose[HumanoidBone.ForearmR], before - 0.05f, before + 0.05f);
            for (float p = 0.05f; p <= 1f; p += 0.05f)
            {
                Strike(motion, p, kind);
            }

            motion.EndStrike();
            Run(motion, 3, Vector2.Zero, MotionMode.Ground); // the cooldown between two strikes of a chain
        }

        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.5f, "the finisher held low, the whole body in it");
    }

    [Fact]
    public void StandingTheLegsStepIntoTheStrikeRunningTheyKeepTheGaitAndADownStrikeDrawsThemUp()
    {
        var standing = new PilgrimMotion(Dims, RunSpeed);
        Run(standing, 30, Vector2.Zero, MotionMode.Ground);
        float idle = Ankle(standing.Pose, HumanoidBone.ThighR, HumanoidBone.ShinR).X;
        Strike(standing, Active, StrikeKind.Swing);
        Assert.True(Ankle(standing.Pose, HumanoidBone.ThighR, HumanoidBone.ShinR).X > idle + 0.15f, "the near foot stepped ahead");

        var swing = new PilgrimMotion(Dims, RunSpeed);
        var still = new PilgrimMotion(Dims, RunSpeed);
        Run(swing, 60, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        Run(still, 60, new Vector2(RunSpeed, 0f), MotionMode.Ground);
        swing.Strike(0.4f, WindUp, Active, StrikeKind.Swing);
        swing.Update(Dt, new Vector2(RunSpeed, 0f), 1, MotionMode.Ground);
        still.Update(Dt, new Vector2(RunSpeed, 0f), 1, MotionMode.Ground);
        Assert.Equal(still.Pose[HumanoidBone.ThighR], swing.Pose[HumanoidBone.ThighR], 1);

        var down = new PilgrimMotion(Dims, RunSpeed);
        Run(down, 20, new Vector2(0f, -3f), MotionMode.Air);
        down.Strike(0.5f, WindUp, Active, StrikeKind.Down);
        down.Update(Dt, new Vector2(0f, -3f), 1, MotionMode.Air);
        Assert.True(down.Pose[HumanoidBone.ThighR] > 1.4f, "the knees drawn up");
        Assert.InRange(down.Pose[HumanoidBone.ForearmR], -1f, 0.3f); // swept down under the body
        for (int i = 0; i < 30; i++)
        {
            down.Strike(0.5f + i * 0.015f, WindUp, Active, StrikeKind.Down);
            down.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
        }

        Assert.InRange(Ankle(down.Pose, HumanoidBone.ThighR, HumanoidBone.ShinR).Y, Dims.AnkleHeight - 0.03f, Dims.AnkleHeight + 0.03f); // landed
    }

    [Fact]
    public void TheBladeSpringsOutForAStrikeAndFoldsAwayAfterItsTrailOnlyThroughTheCut()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 10, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(0f, motion.Blade);
        Strike(motion, 0.05f, StrikeKind.Swing);
        Assert.Equal(0f, motion.Edge);
        Strike(motion, WindUp, StrikeKind.Swing);
        Assert.True(motion.Blade > 0.8f, "out by the coil");
        Strike(motion, WindUp + 0.1f, StrikeKind.Swing);
        Assert.True(motion.Edge > 0.9f, "the cut leaves a trail");
        Strike(motion, Active, StrikeKind.Swing);
        Assert.Equal(0f, motion.Edge); // none laid as it settles back into the hold
        motion.EndStrike();
        Run(motion, 60, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(0f, motion.Blade);
    }

    private static void Strike(PilgrimMotion motion, float progress, StrikeKind kind)
    {
        motion.Strike(progress, WindUp, Active, kind);
        motion.Update(Dt, Vector2.Zero, 1, MotionMode.Ground);
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

    [Theory]
    [InlineData(true, false, MotionMode.Dash)]
    [InlineData(false, false, MotionMode.AirDash)]
    [InlineData(true, true, MotionMode.Bore)]
    [InlineData(false, true, MotionMode.Bore)]
    public void DashModeFollowsWhereItBeganAndTheRock(bool grounded, bool boring, MotionMode expected) =>
        Assert.Equal(expected, MotionModes.Dash(grounded, boring));

    [Theory]
    [InlineData(true, true, 0f, MotionMode.Ground)]
    [InlineData(true, false, -4f, MotionMode.Sink)]
    [InlineData(false, false, 3.5f, MotionMode.Rise)]
    [InlineData(false, false, 0.5f, MotionMode.Swim)]
    [InlineData(false, false, -1f, MotionMode.Swim)]
    public void WaterModeSinksOnTheTanksAndRisesWhenBlown(bool sunk, bool onFloor, float velocityY, MotionMode expected) =>
        Assert.Equal(expected, MotionModes.Water(sunk, onFloor, velocityY, 4f));

    [Fact]
    public void HarpoonThrowsUntilTheLineBites()
    {
        Assert.Equal(MotionMode.Throw, MotionModes.Harpoon(bitten: false));
        Assert.Equal(MotionMode.Zip, MotionModes.Harpoon(bitten: true));
    }

    [Fact]
    public void AirDashTucksThenFliesFlatWithLegsTogether()
    {
        var ground = new PilgrimMotion(Dims, RunSpeed);
        Run(ground, 6, new Vector2(20f, 0f), MotionMode.Dash);

        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 20, new Vector2(0f, 3f), MotionMode.Air);
        Run(motion, 2, new Vector2(20f, 0f), MotionMode.AirDash);
        Assert.True(motion.Pose[HumanoidBone.ThighR] > 0.6f, "the knees snap up first");

        Run(motion, 4, new Vector2(20f, 0f), MotionMode.AirDash);
        HumanoidPose pose = motion.Pose;
        Assert.True(pose[HumanoidBone.Chest] < ground.Pose[HumanoidBone.Chest] - 0.25f, "laid flatter than the ground lunge");
        Assert.True(pose[HumanoidBone.ThighR] < 0f && pose[HumanoidBone.ThighL] < 0f, "both legs behind");
        Assert.True(MathF.Abs(pose[HumanoidBone.ThighR] - pose[HumanoidBone.ThighL]) < 0.2f, "legs together, not split");
        Assert.True(MathF.Abs(ground.Pose[HumanoidBone.ThighR] - ground.Pose[HumanoidBone.ThighL]) > 0.8f, "the ground dash splits them");
    }

    [Fact]
    public void BoreDrawsTheArmsInThenSpearsThemAheadTogether()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 20, Vector2.Zero, MotionMode.Ground);
        Run(motion, 1, new Vector2(20f, 0f), MotionMode.Bore);
        Run(motion, 1, new Vector2(20f, 0f), MotionMode.Bore);
        Assert.True(motion.Pose[HumanoidBone.ForearmR] - motion.Pose[HumanoidBone.UpperArmR] > 1f, "elbows folded, fists drawn in");

        Run(motion, 6, new Vector2(20f, 0f), MotionMode.Bore);
        HumanoidPose pose = motion.Pose;
        Assert.InRange(pose[HumanoidBone.UpperArmR], 1.5f, 2.2f);
        Assert.True(MathF.Abs(pose[HumanoidBone.UpperArmR] - pose[HumanoidBone.UpperArmL]) < 0.15f, "both arms together");
        Assert.True(MathF.Abs(pose[HumanoidBone.ForearmR] - pose[HumanoidBone.UpperArmR]) < 0.1f, "arms straight, a spear");
        Assert.True(pose[HumanoidBone.Chest] < -1.1f, "laid flat behind them");
        Assert.True(pose[HumanoidBone.ShinR] < -0.9f && pose[HumanoidBone.ShinL] < -0.9f, "knees tucked");
    }

    [Fact]
    public void ThrowCocksTheArmThenCastsItAlongTheLine()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed) { Reach = new Vector2(1f, 0.2f) };
        float along = MathF.Atan2(1f, -0.2f);
        Run(motion, 20, Vector2.Zero, MotionMode.Ground);
        Run(motion, 3, Vector2.Zero, MotionMode.Throw);
        Assert.True(motion.Pose[HumanoidBone.UpperArmR] > along + 0.3f, "elbow raised high");
        Assert.True(motion.Pose[HumanoidBone.ForearmR] > 3f, "hand dropped behind the head");
        Assert.True(motion.Pose[HumanoidBone.Chest] > 0.05f, "leaning back into the wind-up");

        Run(motion, 4, Vector2.Zero, MotionMode.Throw);
        Assert.InRange(motion.Pose[HumanoidBone.UpperArmR], along - 0.3f, along + 0.35f);
        Assert.True(motion.Pose[HumanoidBone.Chest] < -0.1f, "the body thrown after the arm");

        Run(motion, 20, Vector2.Zero, MotionMode.Throw);
        Assert.InRange(motion.Pose[HumanoidBone.UpperArmR], along - 0.05f, along + 0.05f);
    }

    [Fact]
    public void SinkGoesFeetFirstAndRiseSweepsTheArmsDown()
    {
        var motion = new PilgrimMotion(Dims, RunSpeed);
        Run(motion, 60, Vector2.Zero, MotionMode.Swim);
        Run(motion, 5, new Vector2(0f, -2f), MotionMode.Sink);
        Assert.True(motion.Pose[HumanoidBone.ThighR] > 0.5f, "knees drawn up as the tanks fill");

        Run(motion, 60, new Vector2(0f, -4.5f), MotionMode.Sink);
        HumanoidPose pose = motion.Pose;
        Assert.True(MathF.Abs(pose[HumanoidBone.ThighR]) < 0.2f && MathF.Abs(pose[HumanoidBone.ThighL]) < 0.2f, "legs straight down");
        Assert.True(MathF.Abs(pose[HumanoidBone.ShinR] - pose[HumanoidBone.ThighR]) < 0.15f, "knees straight");
        Assert.True(MathF.Abs(pose[HumanoidBone.UpperArmR]) > 1.8f && MathF.Abs(pose[HumanoidBone.UpperArmL]) > 1.8f, "arms lifted");
        Assert.True(pose[HumanoidBone.Head] < -0.15f, "looking down");

        Run(motion, 4, new Vector2(0f, 3f), MotionMode.Rise);
        Assert.True(MathF.Abs(motion.Pose[HumanoidBone.UpperArmR]) > 1.5f, "arms still high, gathering");
        Run(motion, 40, new Vector2(0f, 4f), MotionMode.Rise);
        pose = motion.Pose;
        Assert.True(MathF.Abs(pose[HumanoidBone.UpperArmR]) < 0.4f && MathF.Abs(pose[HumanoidBone.UpperArmL]) < 0.4f, "arms swept to the sides");
        Assert.True(pose[HumanoidBone.Head] > 0.2f, "looking up");
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
        Vector2 hip = pose.HipJoint(Dims, thigh == HumanoidBone.ThighR ? 1 : -1);
        Vector2 knee = PlanarIk.End(hip, pose[thigh], Dims.Thigh);
        return PlanarIk.End(knee, pose[shin], Dims.Shin);
    }
}
