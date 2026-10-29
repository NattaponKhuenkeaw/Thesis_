#if UNITY_INCLUDE_TESTS
using CombatCore;
using NUnit.Framework;
using UnityEngine;

public class AnimationSlowMotionTests
{
    [Test]
    public void IndependentCameraReachesPeakInRealTimeWhileFollowingCameraRemainsInTransition()
    {
        var data = new CameraAnimationFrameData();
        data.segments.Add(new CameraAnimationSegment { task = CameraAnimationTask.Zoom, endFrame = 60,
            zoomMultiplier = 0.5f, easing = CameraAnimationEasing.Linear });
        var baseline = new CameraAnimationPose(Vector2.zero, 10f);
        var following = new CameraAnimationSampler(data, baseline, 1f);
        data.followSlowMotion = false;
        var independent = new CameraAnimationSampler(data, baseline, 1f);
        float followingFrame = 0f, independentFrame = 0f;
        for (int i = 0; i < 60; i++)
        {
            followingFrame += AnimationTimePolicy.Delta(following.FollowSlowMotion, 0.25f / 60f, 1f / 60f, false) * FrameDataConstants.LOGIC_FPS;
            independentFrame += AnimationTimePolicy.Delta(independent.FollowSlowMotion, 0.25f / 60f, 1f / 60f, false) * FrameDataConstants.LOGIC_FPS;
        }
        Assert.That(following.Sample(followingFrame, baseline).Size, Is.EqualTo(8.75f).Within(0.001f));
        Assert.That(independent.Sample(independentFrame, baseline).Size, Is.EqualTo(5f).Within(0.001f));
        Assert.That(following.FollowSlowMotion, Is.True, "Per-run settings must not change when source data is edited.");
    }

    [Test]
    public void IndependentClockStillStopsDuringPauseOrHitstop()
    {
        float elapsed = 0f;
        for (int i = 0; i < 120; i++) elapsed += AnimationTimePolicy.Delta(false, 0f, 1f / 60f, true);
        Assert.That(elapsed, Is.Zero);
        elapsed += AnimationTimePolicy.Delta(false, 0.25f, 1f, false);
        Assert.That(elapsed, Is.EqualTo(1f));
        Assert.That(AnimationTimePolicy.Delta(true, 0.25f, 1f, false), Is.EqualTo(0.25f));
    }

    [Test]
    public void MidSequenceSlowMotionChangesDoNotChangeIndependentProgress()
    {
        float following = 0f, independent = 0f;
        foreach (float factor in new[] { 1f, 0.25f, 0.1f, 1f })
        {
            following += AnimationTimePolicy.Delta(true, factor, 1f, false);
            independent += AnimationTimePolicy.Delta(false, factor, 1f, false);
        }
        Assert.That(following, Is.EqualTo(2.35f).Within(0.0001f));
        Assert.That(independent, Is.EqualTo(4f));
    }

    [Test]
    public void SoundUsesAuthoredPitchForEachScaleChangeWithoutCompounding()
    {
        const float basePitch = 1.2f;
        Assert.That(AnimationTimePolicy.AudioPitch(basePitch, 0.25f, true), Is.EqualTo(0.3f).Within(0.0001f));
        Assert.That(AnimationTimePolicy.AudioPitch(basePitch, 0.5f, true), Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(AnimationTimePolicy.AudioPitch(basePitch, 1f, true), Is.EqualTo(basePitch));
        Assert.That(AnimationTimePolicy.AudioPitch(basePitch, 0.25f, false), Is.EqualTo(basePitch));
        Assert.That(AnimationTimePolicy.AudioPitch(basePitch, 0f, true), Is.GreaterThan(0f), "A full freeze uses Pause instead of zero pitch.");
    }

    [Test]
    public void UntouchedTrackDefaultsPreserveCurrentBehavior()
    {
        Assert.That(new CameraAnimationFrameData().followSlowMotion, Is.True);
        Assert.That(new MotionFrameData().followSlowMotion, Is.True);
        Assert.That(new AudioEntry().slowMotionAware, Is.False);
    }
}
#endif
