#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;

public class CameraAnimationSequenceTests
{
    private static readonly CameraAnimationPose Baseline = new CameraAnimationPose(new Vector2(2f, 3f), 10f);

    private static CameraAnimationSegment Segment(CameraAnimationTask task, int start, int end,
        CameraAnimationChannels channels = CameraAnimationChannels.Both) => new CameraAnimationSegment
        { task = task, startFrame = start, endFrame = end, channels = channels, easing = CameraAnimationEasing.Linear };

    [Test]
    public void EventRelativeFramesWaitThenZoomHoldAndReturnForFullDuration()
    {
        var data = new CameraAnimationFrameData();
        var zoom = Segment(CameraAnimationTask.Zoom, 10, 20); zoom.zoomMultiplier = 0.5f;
        data.segments.Add(zoom);
        data.segments.Add(Segment(CameraAnimationTask.Hold, 20, 1020, CameraAnimationChannels.Zoom));
        data.segments.Add(Segment(CameraAnimationTask.Return, 1020, 1120, CameraAnimationChannels.Zoom));
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        Assert.That(sampler.Sample(-1, Baseline).Size, Is.EqualTo(10f));
        Assert.That(sampler.Sample(9, Baseline).Size, Is.EqualTo(10f));
        Assert.That(sampler.Sample(15, Baseline).Size, Is.EqualTo(7.5f));
        Assert.That(sampler.Sample(20, Baseline).Size, Is.EqualTo(5f));
        Assert.That(sampler.Sample(1000, Baseline).Size, Is.EqualTo(5f));
        Assert.That(sampler.Sample(1070, Baseline).Size, Is.EqualTo(7.5f));
        Assert.That(sampler.Sample(1120, Baseline).Size, Is.EqualTo(10f));
        var entry = new ActionEventEntry { eventType = ActionEventType.CameraAnimationControl, triggerFrame = 30, cameraAnimation = data };
        Assert.That(sampler.Sample(45 - entry.triggerFrame, Baseline).Size, Is.EqualTo(7.5f));
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(1121));
    }

    [Test]
    public void MoveAndZoomCanRunTogetherAndRemainFixedWhileNormalCameraMoves()
    {
        var data = new CameraAnimationFrameData();
        var move = Segment(CameraAnimationTask.Move, 0, 10); move.position = new Vector2(4f, 2f);
        var zoom = Segment(CameraAnimationTask.Zoom, 0, 10); zoom.zoomMultiplier = 0.5f;
        data.segments.Add(move); data.segments.Add(zoom);
        data.segments.Add(Segment(CameraAnimationTask.Hold, 10, 100, CameraAnimationChannels.Both));
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        var half = sampler.Sample(5, Baseline);
        Assert.That(half.Position, Is.EqualTo(new Vector2(4f, 4f)));
        Assert.That(half.Size, Is.EqualTo(7.5f));
        var held = sampler.Sample(50, new CameraAnimationPose(new Vector2(100f, 80f), 20f));
        Assert.That(held.Position, Is.EqualTo(new Vector2(6f, 5f)));
        Assert.That(held.Size, Is.EqualTo(5f));
    }

    [Test]
    public void WorldPointCutsAndReturnUseLiveNormalComposition()
    {
        var data = new CameraAnimationFrameData();
        var move = Segment(CameraAnimationTask.Move, 5, 5); move.positionSpace = CameraAnimationPositionSpace.WorldPoint;
        move.position = new Vector2(20f, 30f);
        data.segments.Add(move);
        data.segments.Add(Segment(CameraAnimationTask.Return, 10, 20, CameraAnimationChannels.Position));
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        Assert.That(sampler.Sample(4, Baseline).Position, Is.EqualTo(Baseline.Position));
        Assert.That(sampler.Sample(5, Baseline).Position, Is.EqualTo(move.position));
        var normal = new CameraAnimationPose(new Vector2(40f, 50f), 12f);
        Assert.That(sampler.Sample(15, normal).Position, Is.EqualTo(new Vector2(30f, 40f)));
        Assert.That(sampler.Sample(20, normal).Position, Is.EqualTo(normal.Position));
        Assert.That(sampler.Sample(20, normal).Size, Is.EqualTo(normal.Size));
    }

    [Test]
    public void OutOfOrderSegmentsAndSamplingAreDeterministicAndSnapshotDoesNotMutateSource()
    {
        var data = new CameraAnimationFrameData();
        data.segments.Add(Segment(CameraAnimationTask.Return, 20, 30, CameraAnimationChannels.Zoom));
        var zoom = Segment(CameraAnimationTask.Zoom, 0, 10); zoom.zoomMultiplier = 0.8f;
        data.segments.Add(zoom);
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        float expected = sampler.Sample(25, Baseline).Size;
        sampler.Sample(0, Baseline); sampler.Sample(100, Baseline);
        Assert.That(sampler.Sample(25, Baseline).Size, Is.EqualTo(expected));
        Assert.That(data.segments[0].task, Is.EqualTo(CameraAnimationTask.Return));
        zoom.zoomMultiplier = 0.2f;
        Assert.That(sampler.Sample(10, Baseline).Size, Is.EqualTo(8f));
    }

    [Test]
    public void OverlapAndDuplicateCutsRejectButBoundaryCutsAreStable()
    {
        var data = new CameraAnimationFrameData();
        data.segments.Add(Segment(CameraAnimationTask.Zoom, 0, 10));
        data.segments.Add(Segment(CameraAnimationTask.Hold, 5, 15, CameraAnimationChannels.Zoom));
        Assert.That(data.GetValidationError(), Does.Contain("overlap"));
        Assert.Throws<ArgumentException>(() => new CameraAnimationSampler(data, Baseline, 2f));
        data.segments.Clear();
        var cut = Segment(CameraAnimationTask.Zoom, 0, 0); cut.zoomMultiplier = 0.5f;
        var transition = Segment(CameraAnimationTask.Zoom, 0, 10); transition.zoomMultiplier = 0.8f;
        data.segments.Add(transition); data.segments.Add(cut);
        Assert.That(data.GetValidationError(), Is.Null);
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        Assert.That(sampler.Sample(0, Baseline).Size, Is.EqualTo(5f));
        Assert.That(sampler.Sample(5, Baseline).Size, Is.EqualTo(6.5f));
        data.segments.Add(cut.Copy());
        Assert.That(data.GetValidationError(), Does.Contain("overlap"));
    }

    [Test]
    public void GapRetainsReachedPoseAndFacingOffsetUsesCapturedBaseline()
    {
        var data = new CameraAnimationFrameData();
        var move = Segment(CameraAnimationTask.Move, 0, 0); move.position = new Vector2(3f, 2f); move.facingRelativeX = true;
        data.segments.Add(move);
        data.segments.Add(Segment(CameraAnimationTask.Return, 100, 110, CameraAnimationChannels.Position));
        var sampler = new CameraAnimationSampler(data, Baseline, 2f, -1);
        Assert.That(sampler.Sample(50, Baseline).Position, Is.EqualTo(new Vector2(-1f, 5f)));
    }

    [Test]
    public void ZoomOutAndFrameRescaleKeepTargetsAndTiming()
    {
        var data = new CameraAnimationFrameData();
        var zoom = Segment(CameraAnimationTask.Zoom, 4, 10); zoom.zoomMultiplier = 2f;
        data.segments.Add(zoom);
        data.Rescale(2f);
        Assert.That(zoom.startFrame, Is.EqualTo(8)); Assert.That(zoom.endFrame, Is.EqualTo(20));
        Assert.That(data.DurationFrames, Is.EqualTo(21));
        Assert.That(new CameraAnimationSampler(data, Baseline, 2f).Sample(20, Baseline).Size, Is.EqualTo(20f));
    }

    [Test]
    public void InvalidRangesAndNonfiniteTargetsProduceValidationErrors()
    {
        var data = new CameraAnimationFrameData();
        var segment = Segment(CameraAnimationTask.Move, 10, 5); data.segments.Add(segment);
        Assert.That(data.GetValidationError(), Is.Not.Null);
        segment.endFrame = 10; segment.position = new Vector2(float.NaN, 0f);
        Assert.That(data.GetValidationError(), Is.Not.Null);
        segment.position = Vector2.zero; segment.zoomMultiplier = float.PositiveInfinity;
        Assert.That(data.GetValidationError(), Is.Not.Null);
    }

    [Test]
    public void EnvelopeConversionPreservesPeakHoldReturnAndLockWithoutMutatingInput()
    {
        var envelope = new ScreenImpulseCameraSettings { enabled = true, zoomMultiplier = 0.8f,
            transitionInFrames = 10, holdFrames = 1000, returnFrames = 100, jumpCutLock = true };
        var data = CameraAnimationConversion.FromEnvelope(envelope);
        Assert.That(data.GetValidationError(), Is.Null);
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        var movedNormal = new CameraAnimationPose(new Vector2(100f, 200f), 10f);
        Assert.That(sampler.Sample(1000, movedNormal).Size, Is.EqualTo(8f));
        Assert.That(sampler.Sample(1000, movedNormal).Position, Is.EqualTo(Baseline.Position));
        Assert.That(sampler.Sample(1060, movedNormal).Size, Is.EqualTo(9f));
        Assert.That(sampler.Sample(1110, movedNormal).Position, Is.EqualTo(movedNormal.Position));
        Assert.That(envelope.enabled, Is.True); Assert.That(envelope.holdFrames, Is.EqualTo(1000));
    }

    [Test]
    public void LegacyConversionPreservesEveryKeyAndRejectsAmbiguousDuplicateFrames()
    {
        var legacy = new ActionCameraMotionSettings { holdFrames = 20, returnFrames = 10 };
        legacy.keys.Add(new ActionCameraMotionSettings.Key { frame = 10, sizeMultiplier = 0.9f, horizontalViewportOffset = 0.1f });
        legacy.keys.Add(new ActionCameraMotionSettings.Key { frame = 0 });
        legacy.keys.Add(new ActionCameraMotionSettings.Key { frame = 20, sizeMultiplier = 0.8f, horizontalViewportOffset = 0.2f });
        var data = CameraAnimationConversion.FromLegacyMotion(legacy);
        Assert.That(data.GetValidationError(), Is.Null);
        var sampler = new CameraAnimationSampler(data, Baseline, 2f);
        Assert.That(sampler.Sample(10, Baseline).Size, Is.EqualTo(9f));
        Assert.That(sampler.Sample(20, Baseline).Size, Is.EqualTo(8f));
        Assert.That(sampler.Sample(40, Baseline).Size, Is.EqualTo(8f));
        Assert.That(sampler.Sample(50, Baseline).Size, Is.EqualTo(10f));
        Assert.That(legacy.keys[0].frame, Is.EqualTo(10));
        Assert.That(data.segments.Count, Is.EqualTo(8));
        legacy.keys.Add(new ActionCameraMotionSettings.Key { frame = 10 });
        Assert.Throws<ArgumentException>(() => CameraAnimationConversion.FromLegacyMotion(legacy));
    }

    [Test]
    public void DisabledEnvelopeCannotBecomeAnActiveCameraSequence()
    {
        Assert.Throws<ArgumentException>(() => CameraAnimationConversion.FromEnvelope(new ScreenImpulseCameraSettings()));
        Assert.That((int)ActionEventType.ActionCameraMotion, Is.EqualTo(6));
        Assert.That((int)ActionEventType.ProceduralSlashField, Is.EqualTo(7));
        Assert.That((int)ActionEventType.CameraAnimationControl, Is.EqualTo(8));
    }

    [Test]
    public void ExplicitImpulseConversionSplitsCameraAndPreservesImpulsePayload()
    {
        var source = new ActionEventEntry { eventType = ActionEventType.ScreenImpulse, triggerFrame = 12, label = "Impact" };
        source.screenImpulseSettings.cameraControl.enabled = true;
        source.screenImpulseSettings.cameraControl.zoomMultiplier = 0.75f;
        var impulse = source.screenImpulseSettings.impulse;
        var owner = new ActionEventFrameData(); owner.entries.Add(source);
        var preview = CameraAnimationConversion.Preview(source);
        Assert.That(owner.entries.Count, Is.EqualTo(1));
        Assert.That(source.screenImpulseSettings.cameraControl.enabled, Is.True);
        var converted = CameraAnimationConversion.Apply(owner, source, preview);
        Assert.That(owner.entries.Count, Is.EqualTo(2));
        Assert.That(owner.entries[1], Is.SameAs(converted));
        Assert.That(converted.eventType, Is.EqualTo(ActionEventType.CameraAnimationControl));
        Assert.That(converted.triggerFrame, Is.EqualTo(12));
        Assert.That(source.eventType, Is.EqualTo(ActionEventType.ScreenImpulse));
        Assert.That(source.screenImpulseSettings.impulse, Is.SameAs(impulse));
        Assert.That(source.screenImpulseSettings.cameraControl.enabled, Is.False);
    }

    [Test]
    public void ExplicitLegacyConversionRetainsOriginalKeysAndRejectsInvalidDataBeforeMutation()
    {
        var source = new ActionEventEntry { eventType = ActionEventType.ActionCameraMotion };
        source.cameraMotionSettings.keys.Add(new ActionCameraMotionSettings.Key { sizeMultiplier = 0.9f });
        var original = source.cameraMotionSettings;
        var owner = new ActionEventFrameData(); owner.entries.Add(source);
        Assert.Throws<ArgumentException>(() => CameraAnimationConversion.Apply(owner, source, new CameraAnimationFrameData()));
        Assert.That(source.eventType, Is.EqualTo(ActionEventType.ActionCameraMotion));
        Assert.That(source.cameraAnimation, Is.Null);
        var preview = CameraAnimationConversion.Preview(source);
        CameraAnimationConversion.Apply(owner, source, preview);
        Assert.That(source.cameraMotionSettings, Is.SameAs(original));
        Assert.That(source.cameraMotionSettings.keys.Count, Is.EqualTo(1));
        Assert.That(source.cameraAnimation, Is.SameAs(preview));
        Assert.That(owner.entries.Count, Is.EqualTo(1));
    }
}
#endif
