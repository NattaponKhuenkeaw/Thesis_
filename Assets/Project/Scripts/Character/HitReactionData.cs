using UnityEngine;

/// <summary>
/// Impact tier for damage text scaling.
/// Ultimate = biggest pop, BasicCombo = smallest.
/// </summary>
public enum DamageImpactTier
{
    BasicCombo,
    Skill,
    Ultimate
}

/// <summary>
/// Payload struct that travels from the player's hitbox to the enemy,
/// carrying everything the enemy needs to play its hit reaction.
/// Built by MeleeHitboxDamage from AttackHitFrameData on collision.
/// </summary>
[System.Serializable]
public struct HitReactionData
{
    // =========================================================
    // Damage
    // =========================================================
    public float damage;
    public Vector2 knockbackDirection;
    public float knockbackForce;

    // =========================================================
    // Hitstun (Impact Freeze)
    // =========================================================
    /// <summary>Number of frozen frames on impact (at 60 fps).</summary>
    public int hitstunFrames;

    // =========================================================
    // Knockback Motion
    // =========================================================
    /// <summary>(x,y) velocity applied to the enemy during knockback. X sign is flipped to face away from attacker.</summary>
    public Vector2 reactionVelocity;
    /// <summary>How long the knockback phase lasts (frames). Enemy cannot act during this time.</summary>
    public int knockbackFrames;
    /// <summary>If true the enemy enters Knockdown state (launched airborne, falls).</summary>
    public bool isKnockdown;

    // =========================================================
    // Snap Position
    // =========================================================
    /// <summary>If true, enemy is teleported to attacker position + snapOffset before starting reaction.</summary>
    public bool snapToAttacker;
    /// <summary>Offset from attacker when snapToAttacker is true.</summary>
    public Vector2 snapOffset;

    // =========================================================
    // Visual Feedback
    // =========================================================
    /// <summary>Flash color applied to enemy sprites on impact.</summary>
    public Color flashColor;
    /// <summary>Duration of the sprite flash effect (frames).</summary>
    public int flashFrames;

    // =========================================================
    // Damage Text Impact
    // =========================================================
    /// <summary>Controls how large the damage text pops on hit. Ultimate = biggest.</summary>
    public DamageImpactTier impactTier;

    // =========================================================
    // Factory
    // =========================================================
    /// <summary>Creates a default HitReactionData with sensible fighting-game defaults.</summary>
    public static HitReactionData Default(float damage, Vector2 direction, float knockbackForce)
    {
        return new HitReactionData
        {
            damage             = damage,
            knockbackDirection = direction,
            knockbackForce     = knockbackForce,
            hitstunFrames      = 6,
            reactionVelocity   = new Vector2(knockbackForce * 0.5f, 1f),
            knockbackFrames    = 15,
            isKnockdown        = false,
            snapToAttacker     = false,
            snapOffset         = Vector2.zero,
            flashColor         = Color.red,
            flashFrames        = 6,
            impactTier         = DamageImpactTier.BasicCombo
        };
    }
}
