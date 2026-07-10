using System.Collections.Generic;
using UnityEngine;

// =========================================================
// VFXFrameData — Per-frame VFX control for an ActionStep.
// Designers can spawn VFX prefabs at specific frames and
// control their Position / Rotation / Scale over time.
// =========================================================

/// <summary>
/// A single transform keyframe for a VFX instance.
/// </summary>
[System.Serializable]
public class VFXTransformKey
{
    [Tooltip("Frame index relative to the VFX entry's trigger frame.")]
    public int frame;

    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
}

/// <summary>
/// Describes one VFX spawn event on the timeline.
/// </summary>
[System.Serializable]
public class VFXEntry
{
    public string label = "VFX";
    public Project.Combat.VFXType vfxType = Project.Combat.VFXType.None;
    public AnimationClip vfxClip;
    public GameObject vfxPrefab;

    [Tooltip("Absolute frame on the ActionStep timeline where this VFX spawns.")]
    public int triggerFrame;

    [Tooltip("Visual duration on timeline (in frames). Also controls how long the VFX gizmo remains visible during editor preview.")]
    public int duration = 4;

    public Color trackColor = new Color(0.55f, 0.30f, 1f); // violet

    public List<VFXTransformKey> transformKeys = new List<VFXTransformKey>();
    
    [Tooltip("If true, the VFX will restart its animation when the parent ActionStep loops its Active Phase.")]
    public bool loopDuringActive = false;

    [Tooltip("If true, adjusts sorting layer and order to match the Ultimate Flash Overlay (behind players/enemies, in front of background).")]
    public bool adjustLayer = false;

    [Tooltip("If true, scales the VFX to fit the screen size, similar to the Ultimate Flash Overlay.")]
    public bool fitScreenSize = false;
}

/// <summary>
/// Container for all VFX events on an ActionStep.
/// Appears as violet tracks on the timeline (one row per entry).
/// </summary>
[System.Serializable]
public class VFXFrameData : ActionStepComponent
{
    public override string ComponentName => "VFX";
    public override Color TrackColor => new Color(0.55f, 0.30f, 1f); // violet

    public List<VFXEntry> entries = new List<VFXEntry>();

    public void Rescale(float ratio)
    {
        foreach (var entry in entries)
        {
            entry.triggerFrame = Mathf.RoundToInt(entry.triggerFrame * ratio);
            entry.duration = Mathf.Max(1, Mathf.RoundToInt(entry.duration * ratio));
            foreach (var key in entry.transformKeys)
            {
                key.frame = Mathf.RoundToInt(key.frame * ratio);
            }
        }
    }
}
