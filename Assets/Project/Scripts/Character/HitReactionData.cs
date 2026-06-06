using UnityEngine;

/// <summary>
/// Impact tier for damage text scaling.
/// Ultimate = biggest pop, BasicCombo = smallest.
/// </summary>
public enum DamageImpactTier
{
    Low,
    Medium,
    Heavy,
    Ultimate
}

public enum DamageType
{
    Physical,
    Fire,
    Water,
    Ice,
    Electro,
    Wind,
    Rock,
    Nature
}

public enum DamageRoutine
{
    Normal,
    Elemental,
    Additional
}

public enum EnemyReactionKind
{
    Flinch,
    Knockback,
    Knockup,
    Grab
}

public enum EnemyReactionState
{
    None,
    ImpactFreeze,
    Knockback,
    Knockup,
    Knockfall,
    Knockdown,
    Grabbed,
    Recovering
}

public enum EnemyLandingBehavior
{
    Recover,
    Knockdown
}

/// <summary>
/// Payload struct that travels from the player's hitbox to the enemy,
/// carrying everything the enemy needs to play its hit reaction.
/// Built by HitBoxDamage from HitboxBase on collision.
/// </summary>
[System.Serializable]
public struct HitReactionData
{
    // =========================================================
    // Damage
    // =========================================================
    public float damage;
    public bool isCriticalHit;
    public bool damageIsFinal;
    public Vector2 knockbackDirection;

    // =========================================================
    // Deterministic Reaction Payload
    // =========================================================
    public EnemyReactionKind reactionKind;
    public ActionFrameData reactionTiming;
    public float horizontalSpeed;
    public float launchSpeed;
    public EnemyLandingBehavior landingBehavior;
    public bool canAirJuggle;
    public int maxJuggleCount;
    public float juggleLaunchScale;
    public float juggleStunScale;
    public int knockupFrames;
    public int knockfallFrames;
    public bool useSnapToAnchor;
    public bool hasSnapWorldPosition;
    public Vector2 snapWorldPosition;
    public bool forceFaceAttacker;

    // =========================================================
    // Grab Binding
    // =========================================================
    public Transform attackerTransform;
    public Transform grabSocket;
    public Vector2 grabOffset;
    public int reactionOwnerId;

    // =========================================================
    // Legacy Compatibility
    // =========================================================
    /// <summary>Number of frozen frames on impact (at 60 fps).</summary>
    public int hitstunFrames;
    /// <summary>Legacy velocity payload kept for additive compatibility.</summary>
    public Vector2 reactionVelocity;
    /// <summary>Legacy motion duration kept for additive compatibility.</summary>
    public int knockbackFrames;
    /// <summary>Legacy knockdown flag kept for additive compatibility.</summary>
    public bool isKnockdown;
    /// <summary>Legacy snap flag kept for additive compatibility.</summary>
    public bool snapToAttacker;
    /// <summary>Legacy snap offset kept for additive compatibility.</summary>
    public Vector2 snapOffset;

    // =========================================================
    // Visual Feedback
    // =========================================================
    public Color flashColor;
    public int flashFrames;

    public bool breaksGrab;

    // =========================================================
    // Damage Text Impact
    // =========================================================
    public DamageImpactTier impactTier;
    public DamageType damageType;
    public DamageRoutine damageRoutine;

    public int ImpactFreezeFrames => reactionTiming != null ? Mathf.Max(0, reactionTiming.startupFrames) : Mathf.Max(0, hitstunFrames);
    public int ActiveReactionFrames => reactionTiming != null ? Mathf.Max(0, reactionTiming.activeFrames) : Mathf.Max(0, knockbackFrames);
    public int RecoveryFrames => reactionTiming != null ? Mathf.Max(0, reactionTiming.recoveryFrames) : 0;
    public int TotalReactionFrames => ImpactFreezeFrames + ActiveReactionFrames + knockfallFrames + RecoveryFrames;
    public float HorizontalSpeed => horizontalSpeed > 0f ? horizontalSpeed : Mathf.Abs(reactionVelocity.x);
    public float LaunchSpeed => launchSpeed > 0f ? launchSpeed : Mathf.Max(0f, reactionVelocity.y);

    public static ActionFrameData CreateTiming(int impactFreezeFrames, int reactionFrames, int recoveryFrames)
    {
        return new ActionFrameData
        {
            startupFrames = Mathf.Max(0, impactFreezeFrames),
            activeFrames = Mathf.Max(0, reactionFrames),
            recoveryFrames = Mathf.Max(0, recoveryFrames),
            cancelWindowStartFrame = 0
        };
    }

    public static EnemyReactionKind InferLegacyReactionKind(Vector2 legacyVelocity, bool legacyKnockdown)
    {
        if (legacyKnockdown || legacyVelocity.y > 0.01f)
        {
            return EnemyReactionKind.Knockup;
        }

        if (Mathf.Abs(legacyVelocity.x) > 0.01f)
        {
            return EnemyReactionKind.Knockback;
        }

        return EnemyReactionKind.Flinch;
    }

    public static EnemyLandingBehavior InferLandingBehavior(Vector2 legacyVelocity, bool legacyKnockdown)
    {
        if (legacyKnockdown || legacyVelocity.y > 0.01f)
        {
            return EnemyLandingBehavior.Knockdown;
        }

        return EnemyLandingBehavior.Recover;
    }

    public static bool UsesHorizontalSpeed(EnemyReactionKind reactionKind)
    {
        return reactionKind == EnemyReactionKind.Knockback
            || reactionKind == EnemyReactionKind.Knockup;
    }

    public static bool UsesLaunchSpeed(EnemyReactionKind reactionKind)
    {
        return reactionKind == EnemyReactionKind.Knockup;
    }

    public static bool UsesLandingBehavior(EnemyReactionKind reactionKind)
    {
        return reactionKind == EnemyReactionKind.Knockup;
    }

    public static bool UsesAirJuggleSettings(EnemyReactionKind reactionKind)
    {
        return reactionKind == EnemyReactionKind.Knockup;
    }

    public static HitReactionData Default(float damage, Vector2 direction, float knockbackStrength)
    {
        float horizontal = Mathf.Max(0f, knockbackStrength * 0.5f);

        return new HitReactionData
        {
            damage = damage,
            isCriticalHit = false,
            damageIsFinal = false,
            knockbackDirection = direction,
            reactionKind = horizontal > 0f ? EnemyReactionKind.Knockback : EnemyReactionKind.Flinch,
            reactionTiming = CreateTiming(6, 15, 9),
            horizontalSpeed = horizontal,
            launchSpeed = 0f,
            landingBehavior = EnemyLandingBehavior.Recover,
            canAirJuggle = true,
            maxJuggleCount = 2,
            juggleLaunchScale = 0.85f,
            juggleStunScale = 0.9f,
            hasSnapWorldPosition = false,
            snapWorldPosition = Vector2.zero,
            forceFaceAttacker = false,
            attackerTransform = null,
            grabSocket = null,
            grabOffset = Vector2.zero,
            reactionOwnerId = 0,
            hitstunFrames = 6,
            reactionVelocity = new Vector2(horizontal, 0f),
            knockbackFrames = 15,
            isKnockdown = false,
            snapToAttacker = false,
            snapOffset = Vector2.zero,
            flashColor = Color.red,
            flashFrames = 6,
            impactTier = DamageImpactTier.Low,
            damageType = DamageType.Physical,
            damageRoutine = DamageRoutine.Normal
        };
    }
}
