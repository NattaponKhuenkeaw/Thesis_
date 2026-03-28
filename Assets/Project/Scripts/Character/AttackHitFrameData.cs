using UnityEngine;

[System.Serializable]
public class AttackHitFrameData
{
    // =========================================================
    // Timing
    // =========================================================
    [Header("Timing")]
    public float hitTime = 0.1f;
    public float activeDuration = 0.05f;
    public float cameraShakeStrength = 0f;

    // =========================================================
    // Hitbox
    // =========================================================
    [Header("Hitbox")]
    public Vector2 offset = new Vector2(0.8f, 0.4f);
    public Vector2 size = new Vector2(1.0f, 0.8f);

    // =========================================================
    // Damage / Knockback
    // =========================================================
    [Header("Damage / Knockback")]
    public float damageMultiplier = 1f;
    [Tooltip("Legacy knockback impulse force. Only used as fallback when no EnemyHitReactionHandler exists.")]
    public float knockbackForce = 4f;

    // =========================================================
    // Enemy Reaction
    // =========================================================
    [Header("Enemy Reaction")]
    [Tooltip("Number of frozen frames on impact (at 60fps: 6 frames ≈ 0.1s freeze). Only used if impactFreezeTime is 0.")]
    public int hitstunFrames = 4;
    [Tooltip("Direct time-based impact freeze (seconds). If > 0 this overrides hitstunFrames.")]
    public float impactFreezeTime = 0f;

    [Tooltip("Knockback force as (horizontal magnitude, vertical magnitude). " +
             "X is always applied AWAY from attacker, so set X as a positive value for push-away strength. " +
             "Y is launch height (positive = upward).")]
    public Vector2 reactionVelocity = new Vector2(3f, 1f);
    [Tooltip("How long the enemy stays in knockback stun (seconds). Enemy cannot act during this.")]
    public float knockbackDuration = 0.25f;
    [Tooltip("If true, enemy enters Knockdown state (launched airborne, waits for landing).")]
    public bool isKnockdown = false;

    [Tooltip("If true, snaps enemy to attacker position + snapOffset on impact.")]
    public bool snapToAttacker = false;
    [Tooltip("Position offset from attacker when snap is enabled. X is auto-flipped based on direction.")]
    public Vector2 snapOffset = Vector2.zero;

    [Tooltip("Flash color applied to enemy sprite on impact.")]
    public Color flashColor = Color.red;
    [Tooltip("Duration of the sprite flash effect (seconds).")]
    public float flashDuration = 0.1f;

    // =========================================================
    // Debug
    // =========================================================
    [Header("Debug")]
    public Color debugColor = Color.red;
}