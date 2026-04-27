using UnityEngine;

/// <summary>
/// Per-phase gravity and momentum rules for airborne combat actions.
/// Drives the fluid "Blazblue-style" air combat feel where each action phase
/// has distinct physics behavior instead of a binary lock/unlock.
///
/// Usage: PlayerAirAttackState evaluates this per-frame and forwards the
/// resolved gravity scale to PlayerMovement.
/// </summary>
[System.Serializable]
public struct AirborneGravityPolicy
{
    // ─── Per-Phase Gravity Scales (0 = locked, 1 = full gravity) ───

    [Tooltip("Fraction of current vertical velocity to retain when entering Startup phase. 0 = instantly stop falling, 1 = keep falling.")]
    [Range(0f, 1f)]
    public float startupVelocityRetain;

    [Tooltip("Gravity scale during Startup phase. Small value lets the player float with a hint of drift.")]
    [Range(0f, 1f)]
    public float startupScale;

    [Tooltip("Gravity scale during Active phase. 0 = fully locked in place, motion data only.")]
    [Range(0f, 1f)]
    public float activeScale;

    [Tooltip("Gravity scale at the start of Recovery. Lerps toward recoveryEndScale over the recovery duration.")]
    [Range(0f, 1f)]
    public float recoveryStartScale;

    [Tooltip("Gravity scale at the end of Recovery. 1.0 = full gravity restored.")]
    [Range(0f, 1f)]
    public float recoveryEndScale;

    // ─── Recovery Curve ───

    [Tooltip("Easing for the recovery gravity lerp. EaseIn = slow start then accelerating, feels like weight returning.")]
    public AirborneRecoveryEasing recoveryEasing;

    // ─── Recovery Exit ───

    [Tooltip("Fraction of maxFallSpeed at which the FSM should exit the airborne attack state during Recovery. " +
             "0.5 = exit when falling at half max speed. Set to 0 to disable early exit.")]
    [Range(0f, 1f)]
    public float velocityExitThreshold;

    // ─── Presets ───

    /// <summary>
    /// Default airborne attack policy.
    /// Retains 30% of falling velocity on startup for a slight "stall".
    /// Startup: 5% gravity (near-frozen float).
    /// Active: 0% (fully locked, motion data only).
    /// Recovery: Lerp from 5% to 100% with ease-in curve.
    /// Exits when falling at 50% of max fall speed.
    /// </summary>
    public static readonly AirborneGravityPolicy Default = new AirborneGravityPolicy
    {
        startupVelocityRetain = 0.3f,
        startupScale       = 0.05f,
        activeScale        = 0.0f,
        recoveryStartScale = 0.05f,
        recoveryEndScale   = 1.0f,
        recoveryEasing     = AirborneRecoveryEasing.EaseIn,
        velocityExitThreshold = 0.5f
    };

    /// <summary>
    /// Fully locked policy (legacy behavior). All phases at 0% gravity.
    /// </summary>
    public static readonly AirborneGravityPolicy FullyLocked = new AirborneGravityPolicy
    {
        startupScale       = 0.0f,
        activeScale        = 0.0f,
        recoveryStartScale = 0.0f,
        recoveryEndScale   = 0.0f,
        recoveryEasing     = AirborneRecoveryEasing.Linear,
        velocityExitThreshold = 0.0f
    };

    // ─── Evaluation ───

    /// <summary>
    /// Resolves the gravity scale for the current phase and progress.
    /// </summary>
    /// <param name="phase">Current action phase.</param>
    /// <param name="recoveryProgress">Normalized progress through Recovery (0..1).</param>
    /// <returns>Gravity scale factor [0..1].</returns>
    public float Evaluate(ActionPhase phase, float recoveryProgress)
    {
        switch (phase)
        {
            case ActionPhase.Startup:
                return startupScale;

            case ActionPhase.Active:
                return activeScale;

            case ActionPhase.Recovery:
                float t = Mathf.Clamp01(recoveryProgress);
                float eased = ApplyEasing(t);
                return Mathf.Lerp(recoveryStartScale, recoveryEndScale, eased);

            case ActionPhase.Cancel:
                return 1.0f; // Full gravity in cancel window

            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Whether the recovery phase should exit early based on vertical velocity.
    /// </summary>
    public bool ShouldExitRecovery(float verticalVelocity, float maxFallSpeed)
    {
        if (velocityExitThreshold <= 0f) return false;
        // Negative velocity = falling. Check if magnitude exceeds threshold.
        float threshold = maxFallSpeed * velocityExitThreshold;
        return verticalVelocity <= -threshold;
    }

    private float ApplyEasing(float t)
    {
        switch (recoveryEasing)
        {
            case AirborneRecoveryEasing.Linear:
                return t;
            case AirborneRecoveryEasing.EaseIn:
                return t * t; // Quadratic ease-in: slow start, fast end
            case AirborneRecoveryEasing.EaseInCubic:
                return t * t * t; // Cubic ease-in: even slower start
            default:
                return t;
        }
    }
}

/// <summary>
/// Easing modes for the airborne recovery gravity ramp.
/// </summary>
public enum AirborneRecoveryEasing
{
    /// <summary>Linear interpolation.</summary>
    Linear,
    /// <summary>Quadratic ease-in: starts slow, accelerates.</summary>
    EaseIn,
    /// <summary>Cubic ease-in: starts very slow, strong acceleration.</summary>
    EaseInCubic
}
