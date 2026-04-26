using System;
using System.Collections.Generic;
using CombatCore;
using UnityEngine;

public enum ActionEventType
{
    UltimateOverlaySequence
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

    [Header("Freeze Behavior")]
    public bool freezeWorld = true;
    public UltimateFreezeTimelineMode timelineMode = UltimateFreezeTimelineMode.PauseNodeDuringFreeze;

    [Header("Activation Payload")]
    public Project.Combat.VFXType activationVFXType = Project.Combat.VFXType.None;
    public GameObject activationVFXPrefab;
    public Vector3 activationVFXOffset;
    public AudioClip activationVoice;
    public AudioClip activationSFX;
    public float voiceDelay = 0.1f;

    public int GetTimelineDurationFrames()
    {
        if (eventType == ActionEventType.UltimateOverlaySequence && overlaySettings != null)
        {
            return Mathf.Max(1, Mathf.CeilToInt(overlaySettings.GetTotalDurationSeconds() * FrameDataConstants.LOGIC_FPS));
        }

        return 1;
    }
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
        }
    }
}
