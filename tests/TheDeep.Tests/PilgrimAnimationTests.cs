using System;
using System.Numerics;
using Xunit;

namespace TheDeep.Tests;

public class PilgrimAnimationTests
{
    private const float Dt = 1f / 60f;
    private const float RunClipSpeed = 5.5f;

    private static PilgrimAnimation Steady(Vector2 velocity, MotionMode mode, float seconds = 1f)
    {
        var animation = new PilgrimAnimation(RunClipSpeed);
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
        var animation = new PilgrimAnimation(RunClipSpeed);
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
}
