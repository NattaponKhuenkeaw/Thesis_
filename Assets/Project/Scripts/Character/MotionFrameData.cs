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
    SnapToEnemy,  // Smoothly move towards the nearest or locked target
    DashThroughTarget
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

    [Header("Target Dash")]
    [Min(0f)] public float targetRadius = 8f;
    [Tooltip("Distance beyond the target along the approach direction, in world units.")]
    [Min(0f)] public float passThroughDistance = 1.2f;
    public bool horizontalOnly = true;
    [Tooltip("With horizontal movement, snap a held target onto the player's current height so targets above/below can be cut without launching the player.")]
    public bool alignTargetToDashPlane;
    [Min(0f)] public float minimumTravelDistance = 3f;
    public bool holdTargetAtAnchor;
    [Tooltip("World XY offset from the captured enemy position; X follows the approach side.")]
    public Vector2 targetAnchorOffset;
    public bool faceTargetOnArrival = true;

    [Tooltip("The easing curve used for smooth motion (requires LeanTween).")]
    public LeanTweenType easing = LeanTweenType.linear;

    [Tooltip("Local position delta or absolute target applied at this frame.")]
    public Vector3 position;

    [Tooltip("Local rotation (Euler) delta applied at this frame.")]
    public Vector3 rotation;

    [Tooltip("Local scale delta applied at this frame.")]
    public Vector3 scale = Vector3.one;

    public Vector2 ResolveTargetDashDestination(Vector2 start, Vector2 anchor, int facingDirection)
    {
        Vector2 approach = anchor - start;
        if (horizontalOnly) approach.y = 0f;
        Vector2 direction = approach.sqrMagnitude > 0.0001f ? approach.normalized
            : new Vector2(facingDirection >= 0 ? 1f : -1f, 0f);
        float distance = Mathf.Max(Mathf.Max(0f, minimumTravelDistance), approach.magnitude + Mathf.Max(0f, passThroughDistance));
        return start + direction * distance;
    }

    public Vector2 ResolveTargetDashAnchor(Vector2 start, Vector2 target, int facingDirection)
    {
        Vector2 anchor = target + new Vector2(targetAnchorOffset.x * (facingDirection >= 0 ? 1 : -1), targetAnchorOffset.y);
        if (horizontalOnly && holdTargetAtAnchor && alignTargetToDashPlane) anchor.y = start.y;
        return anchor;
    }
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
