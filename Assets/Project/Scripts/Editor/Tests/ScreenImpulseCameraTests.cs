#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

public class ScreenImpulseCameraTests
{
    [Test]
    public void CameraEnvelopeCutsHoldsAndReturns()
    {
        var settings = new ScreenImpulseCameraSettings { enabled = true, holdFrames = 4, returnFrames = 4 };
        Assert.That(settings.SampleWeight(-1), Is.Zero);
        Assert.That(settings.SampleWeight(0), Is.EqualTo(1f));
        Assert.That(settings.SampleWeight(4), Is.EqualTo(1f));
        Assert.That(settings.SampleWeight(6), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(settings.SampleWeight(8), Is.Zero);
        settings.returnFrames = 0;
        Assert.That(settings.SampleWeight(5), Is.Zero);
    }

    [Test]
    public void SingleZoomHoldsPeakForOneThousandFramesThenReturnsToNormal()
    {
        var settings = new ScreenImpulseCameraSettings
            { enabled = true, zoomMultiplier = 0.8f, transitionInFrames = 10, holdFrames = 1000, returnFrames = 100 };
        Assert.That(settings.SampleWeight(5), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(settings.SampleWeight(10), Is.EqualTo(1f));
        Assert.That(settings.SampleWeight(1000), Is.EqualTo(1f));
        Assert.That(settings.SampleWeight(1010), Is.EqualTo(1f));
        Assert.That(settings.SampleWeight(1060), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(settings.SampleWeight(1110), Is.Zero);
    }

    [Test]
    public void LegacyKeysConvertToPeakZoomInsteadOfHoldingFinalNormalKey()
    {
        var settings = new ActionCameraMotionSettings { holdFrames = 1000, returnFrames = 100 };
        settings.keys.Add(new ActionCameraMotionSettings.Key { frame = 10, sizeMultiplier = 0.8f });
        settings.keys.Add(new ActionCameraMotionSettings.Key { frame = 20, sizeMultiplier = 1f });
        settings.zoomControl = settings.CreateSingleZoomSettings();
        Assert.That(settings.zoomControl.zoomMultiplier, Is.EqualTo(0.8f));
        Assert.That(settings.zoomControl.transitionInFrames, Is.EqualTo(10));
        Assert.That(settings.zoomControl.holdFrames, Is.EqualTo(1000));
        Assert.That(settings.DurationFrames, Is.EqualTo(1111));
    }

    [Test]
    public void TimelineIncludesCameraEnvelopeAndRescalesItsFrames()
    {
        var entry = new ActionEventEntry { eventType = ActionEventType.ScreenImpulse };
        entry.screenImpulseSettings.impulse = null;
        var camera = entry.screenImpulseSettings.cameraControl;
        camera.enabled = true;
        camera.transitionInFrames = 4;
        camera.holdFrames = 6;
        camera.returnFrames = 4;
        Assert.That(camera.SampleWeight(2), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(15));
        var data = new ActionEventFrameData();
        data.entries.Add(entry);
        data.Rescale(2f);
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(29));
        camera.enabled = false;
        Assert.That(camera.SampleWeight(8), Is.Zero);
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(1));
    }

    [Test]
    public void ZoomOutIsLimitedByRoomAndLockKeepsDepth()
    {
        CameraState state = OrthographicState();
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(40f, 20f, 1f));
        float maximumSize = Mathf.Min(bounds.extents.y, bounds.extents.x / state.Lens.Aspect);
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state, bounds, 20f, 0f,
            true, new Vector3(100f, 100f, 100f), 1f), Is.True);
        Assert.That(state.Lens.OrthographicSize, Is.EqualTo(maximumSize).Within(0.001f));
        Assert.That(state.GetCorrectedPosition().z, Is.EqualTo(-10f));
        Assert.That(state.GetCorrectedPosition().x + maximumSize * state.Lens.Aspect,
            Is.LessThanOrEqualTo(bounds.max.x + 0.001f));
        Assert.That(state.GetCorrectedPosition().y + maximumSize,
            Is.LessThanOrEqualTo(bounds.max.y + 0.001f));
    }

    [Test]
    public void LockReturnsTowardCurrentBaseRatherThanCapturedBase()
    {
        CameraState state = OrthographicState();
        state.RawPosition = new Vector3(6f, 0f, -10f);
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state,
            new Bounds(Vector3.zero, new Vector3(100f, 100f, 1f)), 1f, 0f,
            true, Vector3.zero, 0.5f), Is.True);
        Assert.That(state.GetCorrectedPosition().x, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void LegacyCameraEnvelopeStillRejectsZoomOut()
    {
        CameraState state = OrthographicState();
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state,
            new Bounds(Vector3.zero, new Vector3(100f, 100f, 1f)), 2f, 0f), Is.True);
        Assert.That(state.Lens.OrthographicSize, Is.EqualTo(3f));
    }

    [Test]
    public void MotionWaitsForFirstRelativeKeyThenHoldsAndReturns()
    {
        var settings = new ActionCameraMotionSettings { holdFrames = 4, returnFrames = 6 };
        settings.keys.Add(new ActionCameraMotionSettings.Key
            { frame = 10, sizeMultiplier = 0.9f, horizontalViewportOffset = 0.5f });
        settings.Sample(0, out float size, out float offset);
        Assert.That(size, Is.EqualTo(1f));
        Assert.That(offset, Is.Zero);
        settings.Sample(9, out size, out offset);
        Assert.That(size, Is.EqualTo(1f));
        settings.Sample(10, out size, out offset);
        Assert.That(size, Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(offset, Is.EqualTo(0.5f).Within(0.0001f));
        settings.Sample(14, out size, out offset);
        Assert.That(size, Is.EqualTo(0.9f).Within(0.0001f));
        settings.Sample(17, out size, out offset);
        Assert.That(size, Is.EqualTo(0.95f).Within(0.0001f));
        Assert.That(offset, Is.EqualTo(0.25f).Within(0.0001f));
        settings.Sample(20, out size, out offset);
        Assert.That(size, Is.EqualTo(1f));
        Assert.That(offset, Is.Zero);
        Assert.That(settings.DurationFrames, Is.EqualTo(21));
        settings.Rescale(2f);
        Assert.That(settings.DurationFrames, Is.EqualTo(41));
    }

    [Test]
    public void HorizontalOffsetMovesByScreenWidthAndStopsAtRoomEdge()
    {
        CameraState state = OrthographicState();
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 1f));
        float screenWidth = state.Lens.OrthographicSize * state.Lens.Aspect * 2f;
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state, bounds, 1f, 0.5f), Is.True);
        Assert.That(state.GetCorrectedPosition().x, Is.EqualTo(screenWidth * 0.5f).Within(0.001f));
        state = OrthographicState();
        state.RawPosition = new Vector3(bounds.max.x, 0f, -10f);
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state, bounds, 1f, 1e8f), Is.True);
        Assert.That(state.GetCorrectedPosition().x + screenWidth * 0.5f,
            Is.LessThanOrEqualTo(bounds.max.x + 0.001f));
    }

    private static CameraState OrthographicState()
    {
        CameraState state = CameraState.Default;
        state.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        state.Lens.OrthographicSize = 3f;
        state.RawPosition = new Vector3(0f, 0f, -10f);
        return state;
    }
}
#endif
