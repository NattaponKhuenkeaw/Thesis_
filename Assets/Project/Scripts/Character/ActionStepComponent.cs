using UnityEngine;

// =========================================================
// ActionStepComponent — Base class for optional sub-components
// that can be attached to an AttackStepData (or DashData).
// Each component drives a timeline track in the editor.
// =========================================================

/// <summary>
/// Abstract base for modular sub-components on an ActionStep.
/// Concrete types: MotionFrameData, VFXFrameData, HitFrameData.
/// </summary>
[System.Serializable]
public abstract class ActionStepComponent
{
    /// <summary>Display name shown in the editor card header and timeline lane label.</summary>
    public abstract string ComponentName { get; }

    /// <summary>Accent colour for the timeline track row.</summary>
    public abstract Color TrackColor { get; }
}
