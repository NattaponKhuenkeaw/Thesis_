using UnityEngine;

// =========================================================
// ActionFrameData — Universal frame-phase timing for ANY action.
// Shared by Attack, Dash, Skill, Ultimate, Hitstun, etc.
// All frame counts are in 60fps logic ticks.
// =========================================================

[System.Serializable]
public class ActionFrameData
{
    [Header("Phase Timing (60 fps)")]
    [Tooltip("Frames before the action becomes active. Direction changes and attack buffering allowed.")]
    public int startupFrames = 10;

    [Tooltip("Frames where the action is active (hitbox, invincibility, cast effect, etc.).")]
    public int activeFrames = 15;

    [Tooltip("Frames after active phase ends. Buffered inputs may be consumed.")]
    public int recoveryFrames = 20;

    [Header("Cancel Window")]
    [Tooltip("Absolute frame where the cancel window opens. All inputs become available. " +
             "Set to 0 or >= TotalFrames to disable cancel window.")]
    public int cancelWindowStartFrame = 35;

    public virtual void Rescale(float ratio)
    {
        startupFrames = Mathf.RoundToInt(startupFrames * ratio);
        activeFrames = Mathf.RoundToInt(activeFrames * ratio);
        recoveryFrames = Mathf.RoundToInt(recoveryFrames * ratio);
        cancelWindowStartFrame = Mathf.RoundToInt(cancelWindowStartFrame * ratio);
    }

    // =========================================================
    // Computed
    // =========================================================

    /// <summary>Total frames for all three phases (Startup + Active + Recovery).</summary>
    public int TotalFrames => startupFrames + activeFrames + recoveryFrames;

    /// <summary>First frame of the active phase (1-indexed, inclusive).</summary>
    public int ActivePhaseStart => startupFrames + 1;

    /// <summary>Last frame of the active phase (1-indexed, inclusive).</summary>
    public int ActivePhaseEnd => startupFrames + activeFrames;

    /// <summary>First frame of the recovery phase (1-indexed, inclusive).</summary>
    public int RecoveryPhaseStart => startupFrames + activeFrames + 1;

    /// <summary>Whether the cancel window is enabled and reachable.</summary>
    public bool HasCancelWindow => cancelWindowStartFrame > 0 && cancelWindowStartFrame <= TotalFrames;

    // =========================================================
    // Phase Query
    // =========================================================

    /// <summary>
    /// Returns the action phase for a given logic frame (1-indexed).
    /// </summary>
    public ActionPhase GetPhase(int frame)
    {
        if (frame <= 0) return ActionPhase.None;
        if (frame > TotalFrames) return ActionPhase.Done;
        if (frame <= startupFrames) return ActionPhase.Startup;
        if (frame <= startupFrames + activeFrames) return ActionPhase.Active;

        // Check cancel window only while the action is still inside recovery.
        if (HasCancelWindow && frame >= cancelWindowStartFrame)
            return ActionPhase.Cancel;

        return ActionPhase.Recovery;
    }
}

/// <summary>
/// Universal action phase enum used by ALL frame-data-driven states.
/// </summary>
public enum ActionPhase
{
    None,       // Not started / idle
    Startup,    // Wind-up: direction lock, attack buffer listen
    Active,     // Active: hitbox on, invincibility, cast effect
    Recovery,   // Cool-down: buffered input consumption window
    Cancel,     // Late recovery: all inputs open, combo continuation
    Done        // Animation complete, transition out
}
