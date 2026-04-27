using UnityEngine;

/// <summary>
/// Data-driven movement tuning profile. Assign to CharacterObject to give each
/// character unique movement feel (Fast / Medium / Slow archetypes).
/// PlayerMovement reads from this at runtime — no field copying needed.
/// </summary>
[CreateAssetMenu(fileName = "MovementProfile_", menuName = "Game/Character/Movement Profile")]
public class MovementProfile : ScriptableObject
{
    // =========================================================
    // Movement Stats
    // =========================================================
    [Header("Movement Stats")]
    [Tooltip("Base horizontal movement speed on ground.")]
    public float moveSpeed = 5f;
    [Tooltip("Upward impulse force applied when jumping.")]
    public float jumpForce = 10f;
    [Tooltip("Total number of jumps allowed (1 = single jump, 2 = double jump).")]
    public int maxJumpCount = 1;
    [Tooltip("Total number of air dashes allowed before landing.")]
    public int maxAirDashCount = 1;
    [Tooltip("How quickly the character reaches max speed on ground.")]
    public float groundAcceleration = 80f;
    [Tooltip("How quickly the character stops on ground when no input is given.")]
    public float groundDeceleration = 90f;
    [Tooltip("How quickly the character reaches max speed in the air.")]
    public float airAcceleration = 55f;
    [Tooltip("How quickly the character stops in the air when no input is given.")]
    public float airDeceleration = 40f;
    [Tooltip("Horizontal speed multiplier applied when airborne.")]
    public float airSpeedMultiplier = 1f;

    // =========================================================
    // Fall Settings
    // =========================================================
    [Header("Fall Settings")]
    [Tooltip("Gravity multiplier applied when holding 'Down' while falling.")]
    public float fastFallMultiplier = 2.2f;
    [Tooltip("Gravity multiplier applied while moving upwards.")]
    public float riseGravityMultiplier = 1f;
    [Tooltip("Gravity multiplier applied at the peak (apex) of a jump.")]
    public float apexGravityMultiplier = 2.4f;
    [Tooltip("Vertical velocity range around zero that counts as the jump apex.")]
    public float apexVelocityThreshold = 1.6f;
    [Tooltip("How long gravity is disabled or reduced after an attack before falling starts.")]
    public float postAttackGravityGraceDuration = 0.2f;

    // =========================================================
    // Jump Feel
    // =========================================================
    [Header("Jump Feel")]
    [Tooltip("Time window after leaving a platform where a jump input is still accepted.")]
    public float coyoteTime = 0.08f;
    [Tooltip("Time window before hitting the ground where a jump input is buffered.")]
    public float jumpBufferTime = 0.12f;
    [Tooltip("Multiplier applied to vertical velocity if the jump button is released early.")]
    public float jumpCutMultiplier = 0.5f;
    [Tooltip("Standard gravity multiplier applied while falling.")]
    public float fallGravityMultiplier = 1.6f;
    [Tooltip("Gravity multiplier applied for short/low jumps.")]
    public float lowJumpGravityMultiplier = 2.2f;
    [Tooltip("Terminal velocity (max speed) when falling.")]
    public float maxFallSpeed = 18f;

    // =========================================================
    // Feel
    // =========================================================
    [Header("Feel")]
    [Tooltip("Delayed startup before the character actually leaves the ground after a jump input.")]
    public float jumpStartupTime = 0.05f;
    [Tooltip("Duration of movement lock/animation when landing from a height.")]
    public float landingLagTime = 0.1f;
    [Tooltip("Duration of the drop-down (one-way platform) phase.")]
    public float dropDownDuration = 0.2f;

    // =========================================================
    // Action Context
    // =========================================================
    [Header("Action Context")]
    [Tooltip("Max distance to the ground to still be considered 'grounded' for combat action selection.")]
    public float groundedActionProbeDistance = 0.35f;
    [Tooltip("Vertical velocity threshold; above this, the character is always considered 'airborne'.")]
    public float airborneActionVelocityThreshold = 0.05f;

    // =========================================================
    // Attack Lock-On
    // =========================================================
    [Header("Attack Lock-On")]
    [Tooltip("If true, the character will automatically snap their facing direction toward a target when starting an attack.")]
    public bool enableAttackFacingLockOn = true;
    [Tooltip("Horizontal input threshold to manually override and cancel an attack facing lock-on.")]
    public float attackFacingCancelInputThreshold = 0.2f;
}
