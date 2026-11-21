using System;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class PilgrimAnimationTests
{
    private const float Dt = 1f / 60f;
    private const float RunClipSpeed = 5.5f;
    private static readonly ClipSpeeds Speeds = new(RunClipSpeed, Grip: 3.6f, Swim: 2.6f);

    private static PilgrimAnimation Steady(Vector2 velocity, MotionMode mode, float seconds = 1f)
    {
        var animation = new PilgrimAnimation(Speeds);
        animation.Reset();
        for (float t = 0f; t < seconds; t += Dt)
        {
            animation.Update(Dt, velocity, mode);
        }

        return animation;
    }

    [Fact]
    public void StandingShowsTheIdleAndRunningTheRun()
    {
        Assert.True(Steady(Vector2.Zero, MotionMode.Ground).Gait < 0.01f);
        Assert.True(Steady(new Vector2(7f, 0f), MotionMode.Ground).Gait > 0.99f);
        Assert.True(Steady(new Vector2(-7f, 0f), MotionMode.Ground).Gait > 0.99f);
    }

    [Fact]
    public void TheRunKeepsPaceWithTheGround()
    {
        // At the clip's own speed it plays at its own rate; faster, faster, so the feet never slide.
        Assert.Equal(1f, Steady(new Vector2(RunClipSpeed, 0f), MotionMode.Ground).RunRate, 3);
        Assert.Equal(7f / RunClipSpeed, Steady(new Vector2(7f, 0f), MotionMode.Ground).RunRate, 3);
        // Never slow motion while it fades into the idle.
        Assert.True(Steady(new Vector2(0.5f, 0f), MotionMode.Ground).RunRate >= 0.7f);
    }

    [Fact]
    public void TheGaitEasesInsteadOfSnapping()
    {
        var animation = new PilgrimAnimation(Speeds);
        animation.Reset();
        animation.Update(Dt, new Vector2(7f, 0f), MotionMode.Ground);
        Assert.InRange(animation.Gait, 0.05f, 0.5f);
    }

    [Fact]
    public void OffTheGroundTheGaitGoes()
    {
        Assert.True(Steady(new Vector2(7f, 0f), MotionMode.Air).Gait < 0.01f);
        Assert.Equal(PilgrimPose.Air, Steady(new Vector2(7f, 0f), MotionMode.Air).Pose);
    }

    [Fact]
    public void AMomentShowsOverTheMovement()
    {
        var animation = Steady(Vector2.Zero, MotionMode.Ground);
        animation.Moment = PilgrimMoment.Rest;
        animation.Update(Dt, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(PilgrimPose.Rest, animation.Pose);
    }

    [Fact]
    public void ACueLastsOneTickAndAStepDownPlaysNone()
    {
        var animation = Steady(Vector2.Zero, MotionMode.Ground);
        animation.Land(1f);
        animation.Update(Dt, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(PilgrimCue.None, animation.Cue);

        animation.Land(20f);
        animation.Update(Dt, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(PilgrimCue.HardLand, animation.Cue);
        animation.Update(Dt, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(PilgrimCue.None, animation.Cue);
    }

    [Fact]
    public void ABlowOutranksALandingInTheSameTick()
    {
        var animation = Steady(Vector2.Zero, MotionMode.Ground);
        animation.Hurt();
        animation.Land(8f);
        animation.Update(Dt, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(PilgrimCue.Hurt, animation.Cue);
    }

    [Fact]
    public void EveryPoseFallsBackToTheGround()
    {
        foreach (PilgrimPose pose in Enum.GetValues<PilgrimPose>())
        {
            PilgrimPose? p = pose;
            for (int steps = 0; p is { } shown && shown != PilgrimPose.Ground; steps++)
            {
                Assert.True(steps < 8, $"{pose} never falls back to the ground");
                p = PilgrimPoses.Fallback(shown);
            }
        }
    }

    [Fact]
    public void EveryPoseHasItsOwnClipName()
    {
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (PilgrimPose pose in Enum.GetValues<PilgrimPose>())
        {
            Assert.True(names.Add(PilgrimClips.Of(pose)), $"{pose} shares its clip name");
        }

        Assert.DoesNotContain(PilgrimClips.Run, names);
    }

    [Fact]
    public void ARunningJumpLeapsAndAStandingOneTucks()
    {
        Assert.True(Steady(new Vector2(7f, 8f), MotionMode.Air, 0.05f).Leap > 0.99f);
        Assert.True(Steady(new Vector2(0f, 8f), MotionMode.Air, 0.05f).Leap < 0.01f);
    }

    [Fact]
    public void TheFallStartsOverAtTheApex()
    {
        var animation = new PilgrimAnimation(Speeds);
        animation.Reset();
        animation.Update(Dt, new Vector2(0f, 5f), MotionMode.Air);
        Assert.False(animation.FallBegan);
        Assert.Equal(0f, animation.Fall);
        animation.Update(Dt, new Vector2(0f, -0.1f), MotionMode.Air);
        Assert.True(animation.FallBegan);
        animation.Update(Dt, new Vector2(0f, -5f), MotionMode.Air);
        Assert.False(animation.FallBegan);
        Assert.Equal(1f, animation.Fall);
    }

    [Fact]
    public void ALandingShowsOnlyStandingOnTheGround()
    {
        var running = Steady(new Vector2(7f, 0f), MotionMode.Ground);
        running.Land(10f);
        running.Update(Dt, new Vector2(7f, 0f), MotionMode.Ground);
        Assert.Equal(PilgrimCue.None, running.Cue);

        var gripping = Steady(Vector2.Zero, MotionMode.Grip);
        gripping.Land(10f);
        gripping.Update(Dt, Vector2.Zero, MotionMode.Grip);
        Assert.Equal(PilgrimCue.None, gripping.Cue);

        var standing = Steady(Vector2.Zero, MotionMode.Ground);
        standing.Land(10f);
        standing.Update(Dt, Vector2.Zero, MotionMode.Ground);
        Assert.Equal(PilgrimCue.Land, standing.Cue);
    }

    [Fact]
    public void AStrikeDrivesTheWholeBodyStandingAndTheTrunkOverARun()
    {
        var standing = Steady(Vector2.Zero, MotionMode.Ground);
        var running = Steady(new Vector2(7f, 0f), MotionMode.Ground);
        foreach (PilgrimAnimation body in new[] { standing, running })
        {
            for (int i = 0; i < 20; i++)
            {
                body.Strike.Strike(0.3f, 0.14f, 0.5f, StrikeKind.Swing);
                body.Update(Dt, body == running ? new Vector2(7f, 0f) : Vector2.Zero, MotionMode.Ground);
            }
        }

        Assert.True(standing.StrikeWhole > 0.95f);
        Assert.True(running.StrikeWhole < 0.05f);
    }

    [Fact]
    public void TheDownStrikeIsAlwaysWholeBody()
    {
        var animation = Steady(new Vector2(7f, -3f), MotionMode.Air);
        for (int i = 0; i < 20; i++)
        {
            animation.Strike.Strike(0.3f, 0.14f, 0.5f, StrikeKind.Down);
            animation.Update(Dt, new Vector2(7f, -3f), MotionMode.Air);
        }

        Assert.True(animation.StrikeWhole > 0.95f);
    }

    [Fact]
    public void TheCrawlAndTheStrokeKeepPaceAndTheStrokeTreadsWaterStill()
    {
        Assert.Equal(1f, Steady(new Vector2(3.6f, 0f), MotionMode.Grip).PoseRate, 3);
        Assert.Equal(0f, Steady(Vector2.Zero, MotionMode.Grip).PoseRate);
        Assert.Equal(0.45f, Steady(Vector2.Zero, MotionMode.Swim).PoseRate, 3);
        Assert.Equal(1f, Steady(new Vector2(3f, 0f), MotionMode.Dive).PoseRate);
    }

    [Fact]
    public void TheArmAimsAlongTheLineOnlyOnIt()
    {
        Assert.True(Steady(new Vector2(10f, 10f), MotionMode.Zip).ReachWeight > 0.99f);
        Assert.True(Steady(new Vector2(10f, 10f), MotionMode.Air).ReachWeight < 0.01f);
    }

    [Fact]
    public void StandingUpFromARestIsSlowAndADashIsQuick()
    {
        Assert.True(PilgrimPoses.Crossfade(PilgrimPose.Rest, PilgrimPose.Ground) > PilgrimPoses.Crossfade(PilgrimPose.Ground, PilgrimPose.Air));
        Assert.True(PilgrimPoses.Crossfade(PilgrimPose.Ground, PilgrimPose.Dash) < PilgrimPoses.Crossfade(PilgrimPose.Ground, PilgrimPose.Air));
    }
}
