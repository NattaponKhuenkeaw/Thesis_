using System.Collections.Generic;
using UnityEngine;

// =========================================================
// AudioFrameData — Per-frame Audio control for an ActionStep.
// Designers can play AudioClips at specific frames and
// control their Volume / Pitch / Channel over time.
// =========================================================

public enum AudioChannel
{
    Voice,
    ActionSFX,
    HitSFX,
    Ultimate
}

/// <summary>
/// Describes one Audio play event on the timeline.
/// </summary>
[System.Serializable]
public class AudioEntry
{
    public string label = "SFX";
    public AudioClip clip;

    [Tooltip("Absolute frame on the ActionStep timeline where this audio plays.")]
    public int triggerFrame;

    [Tooltip("Duration in frames the Audio is active (for timeline visualisation).")]
    public int duration = 4;

    public AudioChannel channel = AudioChannel.ActionSFX;

    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitchVariation = 1f;
    [Tooltip("Continuously slow playback and lower pitch with gameplay slow motion. Trigger timing remains action-frame based.")]
    public bool slowMotionAware;

    public Color trackColor = new Color(0.2f, 0.8f, 0.4f); // green
}

/// <summary>
/// Container for all Audio events on an ActionStep.
/// Appears as green tracks on the timeline (one row per entry).
/// </summary>
[System.Serializable]
public class AudioFrameData : ActionStepComponent
{
    public override string ComponentName => "Audio";
    public override Color TrackColor => new Color(0.2f, 0.8f, 0.4f); // green

    public List<AudioEntry> entries = new List<AudioEntry>();

    public void Rescale(float ratio)
    {
        foreach (var entry in entries)
        {
            entry.triggerFrame = Mathf.RoundToInt(entry.triggerFrame * ratio);
            entry.duration = Mathf.Max(1, Mathf.RoundToInt(entry.duration * ratio));
        }
    }
}
