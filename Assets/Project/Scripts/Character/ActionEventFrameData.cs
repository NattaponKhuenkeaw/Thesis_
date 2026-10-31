using System;
using System.Collections.Generic;
using CombatCore;
using UnityEngine;

public enum ActionEventType
{
    UltimateOverlaySequence,
    TeleportToNearestEnemy,
    ScreenImpulse,
    AfterImage,
    SetFacingDirection,
    BackgroundOverlayFlash,
    [InspectorName("Action Camera Motion (Legacy)")] ActionCameraMotion,
    ProceduralSlashField,
    [InspectorName("Camera Animation Control")] CameraAnimationControl
}

public enum ActionFacingMode
{
    Opposite,
    Left,
    Right
}

[Serializable]
public class FacingDirectionEventSettings
{
    public ActionFacingMode mode = ActionFacingMode.Opposite;

    public int ResolveDirection(int currentDirection)
    {
        switch (mode)
        {
            case ActionFacingMode.Left:
                return -1;

            case ActionFacingMode.Right:
                return 1;

            default:
                return currentDirection >= 0 ? -1 : 1;
        }
    }
}

public enum UltimateFreezeTimelineMode
{
    PauseNodeDuringFreeze,
    AdvanceNodeUnscaledDuringFreeze
}

[Serializable]
public class UltimateOverlaySequenceSettings
{
    [Tooltip("How fast the background snaps to white.")]
    public float fadeInDuration = 0.03f;

    [Tooltip("How long the white frame is held.")]
    public float holdDuration = 0.15f;

    [Tooltip("How fast the white shifts into the dark overlay.")]
    public float darkenDuration = 0.12f;

    [Tooltip("How long the dark screen is held before fading back out.")]
    public float blackHoldDuration = 0.5f;

    [Tooltip("How fast the dark overlay fades back to transparent.")]
    public float fadeOutDuration = 0.25f;

    public Color flashColor = Color.white;
    public Color darkColor = new Color(0.05f, 0.02f, 0.1f, 1f);

    public float GetTotalDurationSeconds()
    {
        return Mathf.Max(0f, fadeInDuration)
            + Mathf.Max(0f, holdDuration)
            + Mathf.Max(0f, darkenDuration)
            + Mathf.Max(0f, blackHoldDuration)
            + Mathf.Max(0f, fadeOutDuration);
    }
}

[Serializable]
public class ActionEventEntry
{
    public string label = "Ultimate Sequence";
    public ActionEventType eventType = ActionEventType.UltimateOverlaySequence;

    [Tooltip("Absolute frame on the ActionStep timeline where this event fires.")]
    public int triggerFrame;

    public Color trackColor = new Color(1f, 0.72f, 0.2f, 1f);

    [Header("Overlay Sequence")]
    public UltimateOverlaySequenceSettings overlaySettings = new UltimateOverlaySequenceSettings();

    [Header("Background Impact Flash")]
    public BackgroundOverlayFlashSettings backgroundOverlayFlashSettings = new BackgroundOverlayFlashSettings();

    [Header("Freeze Behavior")]
    public bool freezeWorld = true;
    [Tooltip("If greater than 0, overrides the freeze duration. Otherwise, the freeze lasts for the duration of the event sequence.")]
    public float freezeDurationOverride = 0f;
    public UltimateFreezeTimelineMode timelineMode = UltimateFreezeTimelineMode.PauseNodeDuringFreeze;

    [Header("Activation Payload")]
    public Project.Combat.VFXType activationVFXType = Project.Combat.VFXType.None;
    public GameObject activationVFXPrefab;
    public Vector3 activationVFXOffset;
    public AudioClip activationVoice;
    public AudioClip activationSFX;
    [Tooltip("Payload SFX gain, multiplied by the Ultimate action's Activation SFX Volume when applicable.")]
    [Range(0f, 1f)] public float activationSFXVolume = 1f;
    public float voiceDelay = 0.1f;

    [Header("Teleport")]
    public TeleportEventSettings teleportSettings = new TeleportEventSettings();

    [Header("Screen Impulse")]
    public ScreenImpulseEventSettings screenImpulseSettings = new ScreenImpulseEventSettings();

    [Header("After Image")]
    public AfterImageEventSettings afterImageSettings = new AfterImageEventSettings();

    [Header("Facing Direction")]
    public FacingDirectionEventSettings facingDirectionSettings = new FacingDirectionEventSettings();

    [Header("Action Camera Motion")]
    public ActionCameraMotionSettings cameraMotionSettings = new ActionCameraMotionSettings();
    public CameraAnimationFrameData cameraAnimation;

    [Header("Procedural Slash Field")]
    public ProceduralSlashFieldSettings slashFieldSettings;

    [Header("Ultimate Zoom Settings")]
    [Tooltip("If true, the camera will instantly zoom in on the player during the sequence.")]
    public bool enableUltimateZoom = false;
    [Tooltip("The orthographic size target for the ultimate zoom.")]
    public float ultimateZoomSize = 5f;

    public int GetTimelineDurationFrames(int actionTotalFrames = -1)
    {
        if (eventType == ActionEventType.CameraAnimationControl)
            return cameraAnimation?.DurationFrames ?? 1;
        if (eventType == ActionEventType.ProceduralSlashField && slashFieldSettings != null)
            return slashFieldSettings.endFrame;
        if (eventType == ActionEventType.ActionCameraMotion && cameraMotionSettings != null)
            return cameraMotionSettings.DurationFrames;

        if (eventType == ActionEventType.UltimateOverlaySequence && overlaySettings != null)
        {
            return Mathf.Max(1, Mathf.CeilToInt(overlaySettings.GetTotalDurationSeconds() * FrameDataConstants.LOGIC_FPS));
        }

        if (eventType == ActionEventType.TeleportToNearestEnemy && teleportSettings != null)
        {
            return Mathf.Max(1, Mathf.CeilToInt(teleportSettings.GetTotalDurationSeconds() * FrameDataConstants.LOGIC_FPS));
        }

        if (eventType == ActionEventType.ScreenImpulse && screenImpulseSettings != null)
        {
            int impulseFrames = Mathf.CeilToInt((screenImpulseSettings.impulse?.GetTotalDurationSeconds() ?? 0f) * FrameDataConstants.LOGIC_FPS);
            return Mathf.Max(1, Mathf.Max(impulseFrames, screenImpulseSettings.cameraControl?.DurationFrames ?? 0));
        }

        if (eventType == ActionEventType.AfterImage && afterImageSettings != null)
        {
            int endFrame = afterImageSettings.endMode == AfterImageEndMode.UntilActionEnd && actionTotalFrames > 0
                ? actionTotalFrames
                : afterImageSettings.endFrame;
            return Mathf.Max(1, endFrame - triggerFrame + 1);
        }

        if (eventType == ActionEventType.BackgroundOverlayFlash && backgroundOverlayFlashSettings != null)
        {
            return Mathf.Max(1, backgroundOverlayFlashSettings.DurationFrames);
        }

        return 1;
    }
}

[Serializable]
public class BackgroundOverlayFlashSettings
{
    [Tooltip("Logic frames to fade from transparent to peak opacity. Zero gives an instant flash.")]
    [Min(0)] public int fadeInFrames;

    [Tooltip("Logic frames to hold the flash at peak opacity before fading out.")]
    [Min(0)] public int holdFrames;

    [Tooltip("Logic frames to fade back to transparent. Zero gives an instant cut.")]
    [Min(0)]
    public int fadeOutFrames = 18;

    [Tooltip("Color used by the fullscreen impact flash. White is the standard impact flash.")]
    public Color flashColor = Color.white;

    [Tooltip("Peak opacity reached at the end of the flash fade-in.")]
    [Range(0f, 1f)]
    public float peakAlpha = 1f;

    public int DurationFrames => Mathf.Max(0, fadeInFrames) + Mathf.Max(0, holdFrames) + Mathf.Max(0, fadeOutFrames);

    public float SampleAlpha(float frame)
    {
        if (frame < 0f || frame >= DurationFrames) return 0f;
        int fadeIn = Mathf.Max(0, fadeInFrames);
        int holdEnd = fadeIn + Mathf.Max(0, holdFrames);
        float peak = Mathf.Clamp01(peakAlpha);
        if (frame < fadeIn) return peak * frame / fadeIn;
        if (frame < holdEnd) return peak;
        return peak * (1f - (frame - holdEnd) / Mathf.Max(0, fadeOutFrames));
    }

    public void Rescale(float ratio)
    {
        fadeInFrames = Mathf.Max(0, Mathf.RoundToInt(fadeInFrames * ratio));
        holdFrames = Mathf.Max(0, Mathf.RoundToInt(holdFrames * ratio));
        fadeOutFrames = Mathf.Max(0, Mathf.RoundToInt(fadeOutFrames * ratio));
    }
}

[Serializable]
public class TeleportEventSettings
{
    [Tooltip("Search radius used to find the nearest enemy. Works like projectile tracking radius.")]
    public float targetRadius = 8f;

    [Tooltip("Horizontal spacing kept between the player and the target after teleporting.")]
    public float teleportOffsetX = 1.2f;

    [Tooltip("If enabled, the camera cuts to the player for the duration of this event.")]
    public bool snapCameraToPlayer = true;

    [Tooltip("How long the camera stays snapped to the player before returning to normal behavior.")]
    public float cameraSnapDuration = 0.12f;

    [Tooltip("Play a small fullscreen flicker to soften the teleport cut.")]
    public bool playScreenFlick = true;

    public UltimateOverlaySequenceSettings screenFlickSettings = CreateDefaultScreenFlickSettings();

    [Tooltip("Small impulse strength used to hide the teleport cut.")]
    public float cameraImpulseStrength = 0.22f;

    public float GetTotalDurationSeconds()
    {
        float flickDuration = playScreenFlick && screenFlickSettings != null
            ? screenFlickSettings.GetTotalDurationSeconds()
            : 0f;
        return Mathf.Max(Mathf.Max(0f, cameraSnapDuration), flickDuration);
    }

    private static UltimateOverlaySequenceSettings CreateDefaultScreenFlickSettings()
    {
        return new UltimateOverlaySequenceSettings
        {
            fadeInDuration = 0.01f,
            holdDuration = 0.015f,
            darkenDuration = 0.015f,
            blackHoldDuration = 0.04f,
            fadeOutDuration = 0.06f,
            flashColor = new Color(1f, 1f, 1f, 1f),
            darkColor = new Color(0f, 0f, 0f, 0.85f)
        };
    }
}

[Serializable]
public class ScreenImpulseEventSettings
{
    public GameImpulseRequest impulse = GameImpulseRequest.CreateActionEventDefault();
    public ScreenImpulseCameraSettings cameraControl = new ScreenImpulseCameraSettings();
}

[Serializable]
public class ScreenImpulseCameraSettings
{
    public bool enabled;
    [Tooltip("Below 1 zooms in; above 1 zooms out. Room bounds may limit the result.")]
    [Min(0.01f)] public float zoomMultiplier = 1f;
    [Tooltip("Hold the rendered world position captured when the event fires. Does not lock the player.")]
    public bool jumpCutLock;
    [Range(-1f, 1f)] public float horizontalViewportOffset;
    [Min(0)] public int transitionInFrames;
    [Min(0)] public int holdFrames;
    [Min(0)] public int returnFrames;

    public int DurationFrames => enabled
        ? Mathf.Max(0, transitionInFrames) + Mathf.Max(0, holdFrames) + Mathf.Max(0, returnFrames) + 1
        : 0;

    public float SampleWeight(float frame)
    {
        if (!enabled || frame < 0f) return 0f;
        int transition = Mathf.Max(0, transitionInFrames);
        int holdEnd = transition + Mathf.Max(0, holdFrames);
        int restore = Mathf.Max(0, returnFrames);
        if (transition > 0 && frame < transition)
            return Mathf.SmoothStep(0f, 1f, frame / transition);
        if (frame <= holdEnd) return 1f;
        if (restore == 0 || frame >= holdEnd + restore) return 0f;
        return 1f - Mathf.SmoothStep(0f, 1f, (frame - holdEnd) / restore);
    }

    public void Rescale(float ratio)
    {
        transitionInFrames = Mathf.Max(0, Mathf.RoundToInt(transitionInFrames * ratio));
        holdFrames = Mathf.Max(0, Mathf.RoundToInt(holdFrames * ratio));
        returnFrames = Mathf.Max(0, Mathf.RoundToInt(returnFrames * ratio));
    }
}

public enum AfterImageEndMode
{
    UntilActionEnd,
    EndAtFrame
}

[Serializable]
public class AfterImageEventSettings
{
    [Tooltip("How often to spawn a dash-like after image while this event is active.")]
    public float spawnIntervalSeconds = 0.05f;

    [Tooltip("Whether the after image runs to the end of the step or stops on a specific frame.")]
    public AfterImageEndMode endMode = AfterImageEndMode.UntilActionEnd;

    [Tooltip("Absolute frame where the after image effect stops when End Mode is set to EndAtFrame.")]
    public int endFrame = 1;
}

[Serializable]
public class ActionEventFrameData : ActionStepComponent
{
    public override string ComponentName => "Events";
    public override Color TrackColor => new Color(1f, 0.72f, 0.2f, 1f);

    public List<ActionEventEntry> entries = new List<ActionEventEntry>();

    public void Rescale(float ratio)
    {
        if (entries == null)
        {
            return;
        }

        foreach (ActionEventEntry entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            entry.triggerFrame = Mathf.RoundToInt(entry.triggerFrame * ratio);
            if (entry.eventType == ActionEventType.BackgroundOverlayFlash
                || (entry.eventType == ActionEventType.ProceduralSlashField && entry.slashFieldSettings?.fullScreenSlash == true))
                entry.backgroundOverlayFlashSettings?.Rescale(ratio);
            if (entry.eventType == ActionEventType.CameraAnimationControl)
                entry.cameraAnimation?.Rescale(ratio);
            if (entry.eventType == ActionEventType.ScreenImpulse)
                entry.screenImpulseSettings?.cameraControl?.Rescale(ratio);
            if (entry.eventType == ActionEventType.ActionCameraMotion)
                entry.cameraMotionSettings?.Rescale(ratio);
            if (entry.eventType == ActionEventType.ProceduralSlashField)
                entry.slashFieldSettings?.Rescale(ratio);
        }
    }
}
