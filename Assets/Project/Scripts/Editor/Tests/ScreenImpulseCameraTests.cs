#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

public class ScreenImpulseCameraTests
{
    [TestCase(false, false, 0.25f, 0.1f)]
    [TestCase(true, false, 0.25f, 0.025f)]
    [TestCase(false, true, 0.25f, 0.1f)]
    [TestCase(true, true, 0.25f, 0.025f)]
    [TestCase(false, false, 0f, 0.1f)]
    [TestCase(true, false, 0f, 0.1f)]
    [TestCase(false, true, 0f, 0f)]
    [TestCase(true, true, 0f, 0f)]
    public void ImpulseClockSeparatesSlowMotionAndHitstop(bool slow, bool hitstop, float scale, float expected)
    {
        Assert.That(GameImpulseRequest.PlaybackDelta(slow, hitstop, 0.1f * scale, 0.1f, scale, false),
            Is.EqualTo(expected).Within(0.0001f));
        Assert.That(GameImpulseRequest.PlaybackDelta(slow, hitstop, 0.1f * scale, 0.1f, scale, true), Is.Zero);
    }

    [Test]
    public void ExistingEventAndDamageTierDefaultsIgnoreBothTimeEffects()
    {
        var request = JsonUtility.FromJson<GameImpulseRequest>("{\"duration\":0.14}");
        Assert.That(request.followSlowMotion, Is.False);
        Assert.That(request.followHitstop, Is.False);
        var action = GameImpulseRequest.CreateActionEventDefault();
        var tier = GameImpulseRequest.CreateImpact(0.4f, 0.1f, 0.1f, 0.025f);
        Assert.That(action.followSlowMotion || action.followHitstop, Is.False);
        Assert.That(tier.followSlowMotion || tier.followHitstop, Is.False);
    }

    [TestCase(CinemachineBrain.UpdateMethods.SmartUpdate)]
    [TestCase(CinemachineBrain.UpdateMethods.FixedUpdate)]
    [TestCase(CinemachineBrain.UpdateMethods.ManualUpdate)]
    public void NativeImpulseSignalsKeepIndependentClocksAndRestoreBrainMode(CinemachineBrain.UpdateMethods update)
    {
        float originalTime = CinemachineCore.CurrentTimeOverride;
        bool originalIgnore = CinemachineImpulseManager.Instance.IgnoreTimeScale;
        CameraManager originalManager = CameraManager.instance;
        var host = new GameObject("Impulse Clock Test");
        GameImpulseController controller = null;
        try
        {
            CameraManager.instance = null;
            CinemachineCore.CurrentTimeOverride = 100f;
            CinemachineImpulseManager.Instance.IgnoreTimeScale = false;
            var source = host.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition.ImpulseDuration = 1f;
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            var brain = host.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = update;
            brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.FixedUpdate;
            controller = host.AddComponent<GameImpulseController>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameImpulseController).GetField("fallbackImpulseSource", fields).SetValue(controller, source);
            typeof(GameImpulseController).GetField("_impulseBrain", fields).SetValue(controller, brain);
            var emit = typeof(GameImpulseController).GetMethod("EmitImpulse", fields);
            var following = (CinemachineImpulseManager.ImpulseEvent)emit.Invoke(controller, new object[] { 1f, true, true });
            var independent = (CinemachineImpulseManager.ImpulseEvent)emit.Invoke(controller, new object[] { 1f, false, false });
            Assert.That(following, Is.Not.Null); Assert.That(independent, Is.Not.Null);
            controller.AdvanceImpulses(0.025f, 0.1f, 0.25f, false);
            Assert.That(100f - following.StartTime, Is.EqualTo(0.025f).Within(0.0001f));
            Assert.That(100f - independent.StartTime, Is.EqualTo(0.1f).Within(0.0001f));
            controller.AdvanceImpulses(0f, 0.1f, 0f, false);
            Assert.That(100f - following.StartTime, Is.EqualTo(0.025f).Within(0.0001f));
            Assert.That(100f - independent.StartTime, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(independent.GetDecayedSignal(source.transform.position, true, out var noise, out _), Is.True);
            Assert.That(noise.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(brain.UpdateMethod, Is.EqualTo(update == CinemachineBrain.UpdateMethods.ManualUpdate
                ? update : CinemachineBrain.UpdateMethods.LateUpdate));
            controller.AdvanceImpulses(0f, 0.1f, 0f, true);
            Assert.That(100f - independent.StartTime, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(brain.UpdateMethod, Is.EqualTo(update));
            Assert.That(brain.BlendUpdateMethod, Is.EqualTo(CinemachineBrain.BrainUpdateMethods.FixedUpdate));
            controller.AdvanceImpulses(0.05f, 0.05f, 1f, false);
            Assert.That(100f - following.StartTime, Is.EqualTo(0.075f).Within(0.0001f));
            Assert.That(100f - independent.StartTime, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(CinemachineImpulseManager.Instance.IgnoreTimeScale, Is.False);
            // Simulate the native pool replacing a signal behind a retained handle.
            float pooledStart = following.StartTime;
            var originalSignal = following.SignalSource;
            following.SignalSource = independent.SignalSource;
            controller.AdvanceImpulses(0.1f, 0.1f, 1f, false);
            Assert.That(following.StartTime, Is.EqualTo(pooledStart));
            following.SignalSource = originalSignal;
            following.Cancel(100f, true);
            controller.AdvanceImpulses(0f, 0f, 0f, false);
            typeof(GameImpulseController).GetMethod("OnDisable", fields).Invoke(controller, null);
            Assert.That(independent.SignalSource, Is.Null);
            Assert.That(brain.UpdateMethod, Is.EqualTo(update));
            Assert.That(brain.BlendUpdateMethod, Is.EqualTo(CinemachineBrain.BrainUpdateMethods.FixedUpdate));
        }
        finally
        {
            if (controller != null)
                typeof(GameImpulseController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            Object.DestroyImmediate(host);
            CameraManager.instance = originalManager;
            CinemachineImpulseManager.Instance.IgnoreTimeScale = originalIgnore;
            CinemachineCore.CurrentTimeOverride = originalTime;
        }
    }

    [UnityTest]
    public IEnumerator ZHitDataHitstopAllowsDamageTierAndEventShakeToRender()
    {
        yield return new EnterPlayMode();
        GameObject host = null, cameraHost = null, target = null;
        try
        {
            host = new GameObject("Hitstop Impulse Services");
            var source = host.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition.ImpulseDuration = 0.4f;
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            var controller = host.AddComponent<GameImpulseController>();
            var freeze = host.AddComponent<GameFreezeManager>();
            cameraHost = new GameObject("Hitstop Impulse Output");
            cameraHost.tag = "MainCamera";
            cameraHost.AddComponent<Camera>().orthographic = true;
            var brain = cameraHost.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var vcamHost = new GameObject("Hitstop Impulse Vcam");
            vcamHost.transform.SetParent(cameraHost.transform);
            vcamHost.transform.position = new Vector3(0f, 0f, -10f);
            var vcam = vcamHost.AddComponent<CinemachineCamera>();
            var listener = vcamHost.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1; listener.Gain = 1f;
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            target = new GameObject("Fixed Update Follow Target");
            var body = target.AddComponent<Rigidbody2D>(); body.gravityScale = 0f;
            body.linearVelocity = Vector2.right;
            vcam.Follow = target.transform;
            vcamHost.AddComponent<CinemachinePositionComposer>();
            var hit = host.AddComponent<HitBoxDamage>();
            hit.cameraShake = host.AddComponent<CameraShakeController>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var hitData = new NormalHitbox { impactTier = DamageImpactTier.Heavy, impactFreezeFrames = 60,
                suppressImpactFeedback = true };
            typeof(HitBoxDamage).GetField("currentNormalHitbox", fields).SetValue(hit, hitData);
            yield return new WaitForSecondsRealtime(0.15f);
            var configuredFeedback = typeof(HitBoxDamage).GetMethod("TriggerConfiguredImpactFeedback", fields);
            var trackedSignals = (IList)typeof(GameImpulseController).GetField("_activeImpulses", fields).GetValue(controller);
            configuredFeedback.Invoke(hit, null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(trackedSignals.Count, Is.Zero);
            hit.ResetActivationState();
            hitData.suppressImpactFeedback = false; hitData.impactFreezeFrames = 0;
            configuredFeedback.Invoke(hit, null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(trackedSignals.Count, Is.GreaterThan(0), "Zero Hitstop frames must retain damage-tier shake.");
            hit.ResetActivationState(); hitData.impactFreezeFrames = 60;
            configuredFeedback.Invoke(hit, null);
            controller.Play(GameImpulseRequest.CreateActionEventDefault());
            Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(0.08f);
            Vector3 first = cameraHost.transform.position;
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(Time.timeScale, Is.Zero);
            CinemachineImpulseManager.Instance.GetImpulseAt(cameraHost.transform.position, false, 1, out var sampledNoise, out _);
            var activeSignals = (IList)typeof(GameImpulseController).GetField("_activeImpulses", fields).GetValue(controller);
            string signalAges = "";
            foreach (object activeSignal in activeSignals)
                signalAges += activeSignal.GetType().GetField("elapsed").GetValue(activeSignal) + ",";
            Assert.That(Mathf.Abs(cameraHost.transform.position.y - first.y), Is.GreaterThan(0.00001f),
                $"first={first}, current={cameraHost.transform.position}, noise={sampledNoise}, vcam={vcam.State.GetCorrectedPosition()}, active={brain.ActiveVirtualCamera?.Name}, mode={brain.UpdateMethod}, signalAges={signalAges}, dt={Time.deltaTime}/{Time.unscaledDeltaTime}");
            Assert.That(brain.UpdateMethod, Is.EqualTo(CinemachineBrain.UpdateMethods.LateUpdate));
            freeze.RestoreTime();
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(brain.UpdateMethod, Is.EqualTo(CinemachineBrain.UpdateMethods.SmartUpdate),
                $"cached={typeof(GameImpulseController).GetField("_previousCameraUpdate", fields).GetValue(controller)}, owned={typeof(GameImpulseController).GetField("_ownsCameraUpdate", fields).GetValue(controller)}");
        }
        finally
        {
            if (host != null) Object.DestroyImmediate(host);
            if (cameraHost != null) Object.DestroyImmediate(cameraHost);
            if (target != null) Object.DestroyImmediate(target);
            Time.timeScale = 1f;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator MenuPauseSuspendsDelayedSlowMotionAndActionFreeze()
    {
        yield return new EnterPlayMode();
        var freeze = GameFreezeManager.Instance;
        GameObject host = null;
        if (freeze == null)
        {
            host = new GameObject("Menu Pause Test Clock");
            freeze = host.AddComponent<GameFreezeManager>();
        }

        float normalStep = Time.fixedDeltaTime;
        int holds = 0;
        try
        {
            freeze.PlaySlowMotion(.25f, .2f, .1f);
            freeze.BeginPauseHold(); holds++;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Time.timeScale, Is.Zero);

            freeze.EndPauseHold(); holds--;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            float deadline = Time.realtimeSinceStartup + 1f;
            while (Time.timeScale == 1f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(.25f));

            freeze.BeginPauseHold(); holds++;
            freeze.BeginPauseHold(); holds++;
            yield return new WaitForSecondsRealtime(.4f);
            freeze.EndPauseHold(); holds--;
            Assert.That(Time.timeScale, Is.Zero);
            freeze.EndPauseHold(); holds--;
            Assert.That(Time.timeScale, Is.EqualTo(.25f));
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(normalStep * .25f).Within(.000001f));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            var scopedFreeze = freeze.BeginScopedFreeze();
            freeze.BeginPauseHold(); holds++;
            Assert.That(GameFreezeManager.GameplayUnscaledDeltaTime, Is.Zero);
            freeze.EndPauseHold(); holds--;
            Assert.That(freeze.OwnsScopedFreeze(scopedFreeze), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(freeze.EndScopedFreeze(scopedFreeze), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            bool freezeCompleted = false;
            freeze.UltimateFreeze(12, () => freezeCompleted = true);
            freeze.BeginPauseHold(); holds++;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(freezeCompleted, Is.False);
            freeze.EndPauseHold(); holds--;
            Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(freezeCompleted, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
        finally
        {
            while (holds-- > 0) freeze.EndPauseHold();
            freeze.RestoreTime();
            if (host != null) Object.DestroyImmediate(host);
        }
        yield return new ExitPlayMode();
    }

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
    [Test]
    public void FlashFadesInHoldsAndFadesOutAtAuthoredBoundaries()
    {
        var settings = new BackgroundOverlayFlashSettings
            { fadeInFrames = 4, holdFrames = 6, fadeOutFrames = 8, peakAlpha = 0.8f };
        Assert.That(settings.DurationFrames, Is.EqualTo(18));
        Assert.That(settings.SampleAlpha(-1), Is.Zero);
        Assert.That(settings.SampleAlpha(0), Is.Zero);
        Assert.That(settings.SampleAlpha(2), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(settings.SampleAlpha(4), Is.EqualTo(0.8f));
        Assert.That(settings.SampleAlpha(9), Is.EqualTo(0.8f));
        Assert.That(settings.SampleAlpha(10), Is.EqualTo(0.8f));
        Assert.That(settings.SampleAlpha(14), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(settings.SampleAlpha(18), Is.Zero);
    }

    [Test]
    public void ExistingFlashKeepsInstantPeakAndEighteenFrameFade()
    {
        var settings = JsonUtility.FromJson<BackgroundOverlayFlashSettings>(
            "{\"fadeOutFrames\":18,\"peakAlpha\":1}");
        Assert.That(settings.fadeInFrames, Is.Zero);
        Assert.That(settings.holdFrames, Is.Zero);
        Assert.That(settings.DurationFrames, Is.EqualTo(18));
        Assert.That(settings.SampleAlpha(0), Is.EqualTo(1f));
        Assert.That(settings.SampleAlpha(9), Is.EqualTo(0.5f));
        Assert.That(settings.SampleAlpha(18), Is.Zero);
    }

    [Test]
    public void ZeroDurationPhasesCutWithoutDividingByZero()
    {
        var settings = new BackgroundOverlayFlashSettings { holdFrames = 4, fadeOutFrames = 0 };
        Assert.That(settings.SampleAlpha(0), Is.EqualTo(1f));
        Assert.That(settings.SampleAlpha(3), Is.EqualTo(1f));
        Assert.That(settings.SampleAlpha(4), Is.Zero);
        settings.fadeInFrames = 4;
        settings.holdFrames = 0;
        Assert.That(settings.SampleAlpha(2), Is.EqualTo(0.5f));
        Assert.That(settings.SampleAlpha(4), Is.Zero);
        settings.fadeInFrames = -1;
        settings.holdFrames = -1;
        settings.fadeOutFrames = -1;
        Assert.That(settings.DurationFrames, Is.Zero);
        Assert.That(settings.SampleAlpha(0), Is.Zero);
    }

    [Test]
    public void TimelineAndRescalingIncludeAllThreePhases()
    {
        var entry = new ActionEventEntry { eventType = ActionEventType.BackgroundOverlayFlash, triggerFrame = 10 };
        entry.backgroundOverlayFlashSettings = new BackgroundOverlayFlashSettings
            { fadeInFrames = 4, holdFrames = 6, fadeOutFrames = 8 };
        var events = new ActionEventFrameData(); events.entries.Add(entry);
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(18));
        events.Rescale(2f);
        Assert.That(entry.triggerFrame, Is.EqualTo(20));
        Assert.That(entry.backgroundOverlayFlashSettings.fadeInFrames, Is.EqualTo(8));
        Assert.That(entry.backgroundOverlayFlashSettings.holdFrames, Is.EqualTo(12));
        Assert.That(entry.backgroundOverlayFlashSettings.fadeOutFrames, Is.EqualTo(16));
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(36));
    }
}
#endif
