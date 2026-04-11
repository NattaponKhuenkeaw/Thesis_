using System.Collections.Generic;
using UnityEngine;

// =========================================================
// MotionFrameData — Per-frame motion control for an ActionStep.
// Replaces the old ForwardMotion toggle with full designer
// control over Position / Rotation / Scale at specific frames.
// =========================================================

public enum MotionTaskType
{
    Relative,    // Incremental displacement (cumulative)
    Absolute,    // Snap to a specific relative anchor position
    SnapToEnemy  // Smoothly move towards the nearest or locked target
}

/// <summary>
/// A single motion keyframe: transform snapshot at a specific frame.
/// </summary>
[System.Serializable]
public class MotionKeyframe
{
    [Tooltip("Frame index where the motion begins.")]
    public int startFrame;

    [Tooltip("Frame index where the motion ends. If equal to StartFrame, it occurs instantly.")]
    public int endFrame;

    [Tooltip("How the motion is applied.")]
    public MotionTaskType taskType = MotionTaskType.Relative;

    [Tooltip("The easing curve used for smooth motion (requires LeanTween).")]
    public LeanTweenType easing = LeanTweenType.linear;

    [Tooltip("Local position delta or absolute target applied at this frame.")]
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
    public override Color TrackColor => Color.white;

    public List<MotionKeyframe> keyframes = new List<MotionKeyframe>();

    public void Rescale(float ratio)
    {
        foreach (var kf in keyframes)
        {
            kf.startFrame = Mathf.RoundToInt(kf.startFrame * ratio);
            kf.endFrame = Mathf.RoundToInt(kf.endFrame * ratio);
        }
    }
}
