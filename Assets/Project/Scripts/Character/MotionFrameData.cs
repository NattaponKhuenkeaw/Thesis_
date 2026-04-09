using System.Collections.Generic;
using UnityEngine;

// =========================================================
// MotionFrameData — Per-frame motion control for an ActionStep.
// Replaces the old ForwardMotion toggle with full designer
// control over Position / Rotation / Scale at specific frames.
// =========================================================

/// <summary>
/// A single motion keyframe: transform snapshot at a specific frame.
/// </summary>
[System.Serializable]
public class MotionKeyframe
{
    [Tooltip("Frame index within the ActionStep's total timeline.")]
    public int frame;

    [Tooltip("Local position delta applied at this frame.")]
    public Vector3 position;

    [Tooltip("Local rotation (Euler) delta applied at this frame.")]
    public Vector3 rotation;

    [Tooltip("Local scale delta applied at this frame.")]
    public Vector3 scale = Vector3.one;
}

/// <summary>
/// Gives designers per-frame control over the character's
/// motion during an ActionStep (attack/dash/skill).
/// Appears as a green track on the timeline.
/// </summary>
[System.Serializable]
public class MotionFrameData : ActionStepComponent
{
    public override string ComponentName => "Motion";
    public override Color TrackColor => new Color(0.18f, 0.72f, 0.38f); // green

    public List<MotionKeyframe> keyframes = new List<MotionKeyframe>();
}
