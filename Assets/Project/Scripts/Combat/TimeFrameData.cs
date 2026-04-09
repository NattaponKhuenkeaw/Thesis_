using System.Collections.Generic;
using UnityEngine;

// =========================================================
// TimeFrameData — VFX/Enemy timeline data hierarchy.
// Stores VFX trigger events relative to an AnimationClip's
// frame timeline. Cascades FPS changes to all children.
// =========================================================

/// <summary>
/// Precise per-frame motion keyframe for a VFX object.
/// All values are deltas applied each frame (not absolute positions).
/// </summary>
[System.Serializable]
public class FrameMotionControl
{
    public int    frame;
    public Vector2 positionDelta;
    public float  rotationDelta;
    public float  scaleDelta;
}

/// <summary>
/// Describes how a single VFX object moves across a range of frames.
/// Relative to the parent VFXChildNode's triggerFrame.
/// </summary>
[System.Serializable]
public class VFXFrameMotion
{
    public int   startFrame;          // frame offset from triggerFrame
    public int   duration;            // how many frames the motion lasts
    public Vector3 localOffset;       // spawn position offset from character
    public List<FrameMotionControl> motionKeyframes = new List<FrameMotionControl>();
}

/// <summary>
/// Represents a single VFX or Enemy prefab event on the timeline.
/// One node = one attack step's VFX trigger point.
/// </summary>
[System.Serializable]
public class VFXChildNode
{
    public string     label         = "VFX Event";
    public GameObject vfxPrefab;      // drag-dropped VFX prefab
    public GameObject enemyPrefab;    // drag-dropped Enemy prefab (for hit-reaction preview)
    public int        triggerFrame;   // absolute frame on the master timeline
    public Color      trackColor     = new Color(0.4f, 0.8f, 1f, 1f);

    public List<VFXFrameMotion> frameMotions = new List<VFXFrameMotion>();
}

/// <summary>
/// Root container tied to one AttackStepData's animation clip.
/// If the clip FPS or length changes, call RecalculateOnFPSChange to cascade.
/// </summary>
[System.Serializable]
public class TimeFrameData
{
    // ── Link to action ──
    public string stepName;           // mirrors AttackStepData.stepName for display

    // ── VFX Events ──
    public List<VFXChildNode> vfxNodes = new List<VFXChildNode>();

    // ── FPS Cascade ──
    /// <summary>
    /// Rescales all child trigger frames when the master clip's FPS changes.
    /// Call this whenever you detect clip.frameRate has changed.
    /// </summary>
    public void RecalculateOnFPSChange(float oldFPS, float newFPS)
    {
        if (oldFPS <= 0f || newFPS <= 0f || Mathf.Approximately(oldFPS, newFPS)) return;
        float ratio = newFPS / oldFPS;

        foreach (var node in vfxNodes)
        {
            node.triggerFrame = Mathf.RoundToInt(node.triggerFrame * ratio);

            foreach (var fm in node.frameMotions)
            {
                fm.startFrame = Mathf.RoundToInt(fm.startFrame * ratio);
                fm.duration   = Mathf.Max(1, Mathf.RoundToInt(fm.duration * ratio));

                foreach (var mk in fm.motionKeyframes)
                    mk.frame = Mathf.RoundToInt(mk.frame * ratio);
            }
        }
    }
}
