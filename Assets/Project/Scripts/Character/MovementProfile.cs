using UnityEngine;

/// <summary>
/// Per-character outcomes for the deterministic player motor.
/// Timing is frame-based on the same 60 Hz logic clock used by combat.
/// </summary>
[CreateAssetMenu(fileName = "MovementProfile_", menuName = "Game/Character/Movement Profile")]
public class MovementProfile : ScriptableObject
{
    [Header("Horizontal Movement")]
    [Min(0f)]
    public float moveSpeed = 5f;

    [Header("Jump Arc")]
    [Min(0f)]
    public float jumpSpeed = 22f;

    [Min(0f)]
    public float riseDeceleration = 75.5f;

    [Min(0f)]
    public float jumpFallAcceleration = 95f;

    [Header("Edge / Drop Fall")]
    [Min(0f)]
    public float neutralFallAcceleration = 40f;

    [Min(0f)]
    public float maximumFallSpeed = 28f;

    [Header("Resources")]
    [Min(1)]
    public int maxJumpCount = 2;

    [Min(0)]
    public int maxAirDashCount = 1;

    [Header("60 Hz State Timing")]
    [Min(0)]
    public int coyoteFrames = 7;

    [Min(0)]
    [Tooltip("Zero launches immediately while preserving horizontal control.")]
    public int jumpStartupFrames = 0;

    [Min(0)]
    public int landingRecoveryFrames = 0;
}
