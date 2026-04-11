using UnityEngine;

[System.Serializable]
public class AttackHitFrameData
{
    // =========================================================
    // Timing
    // =========================================================
    [Header("Timing")]
    [Tooltip("Frame offset relative to the Step's active phase start")]
    public int startFrameOffset = 0;
    [Tooltip("How many frames this specific hitbox stays active")]
    public int activeFrames = 3;

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

    // =========================================================
    // Enemy Reaction
    // =========================================================
    [Header("Enemy Reaction")]
    public bool useDeterministicReaction = false;
    public EnemyReactionKind reactionKind = EnemyReactionKind.Knockback;
    public int impactFreezeFrames = 4;
    public int reactionFrames = 15;
    public int recoveryFrames = 9;
    public float horizontalSpeed = 3f;
    public float launchSpeed = 1f;
    public EnemyLandingBehavior landingBehavior = EnemyLandingBehavior.Knockdown;
    public bool canAirJuggle = true;
    public int maxJuggleCount = 2;
    public float juggleLaunchScale = 0.85f;
    public float juggleStunScale = 0.9f;

    [Header("Legacy Enemy Reaction")]
    [Tooltip("Number of frozen frames on impact (both attacker and victim).")]
    public int hitstunFrames = 4;

    [Tooltip("Knockback force as (horizontal magnitude, vertical magnitude). " +
             "X is always applied AWAY from attacker, so set X as a positive value for push-away strength. " +
             "Y is launch height (positive = upward).")]
    public Vector2 reactionVelocity = new Vector2(3f, 1f);
    [Tooltip("How long the enemy stays in knockback stun (frames). Enemy cannot act during this.")]
    public int knockbackFrames = 15;
    [Tooltip("If true, enemy enters Knockdown state (launched airborne, waits for landing).")]
    public bool isKnockdown = false;

    [Tooltip("If true, snaps enemy to attacker position + snapOffset on impact.")]
    public bool snapToAttacker = false;
    [Tooltip("Position offset from attacker when snap is enabled. X is auto-flipped based on direction.")]
    public Vector2 snapOffset = Vector2.zero;

    [Tooltip("Flash color applied to enemy sprite on impact.")]
    public Color flashColor = Color.red;
    [Tooltip("Duration of the sprite flash effect (frames).")]
    public int flashFrames = 6;

    // =========================================================
    // Damage Text Impact
    // =========================================================
    [Header("Damage Text Impact")]
    [Tooltip("Controls how much the damage text pops. Ultimate = biggest, BasicCombo = smallest.")]
    public DamageImpactTier impactTier = DamageImpactTier.BasicCombo;

    // =========================================================
    // Debug
    // =========================================================
    [Header("Debug")]
    public Color debugColor = Color.red;
}
