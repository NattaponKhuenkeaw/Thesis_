using System.Collections.Generic;
using UnityEngine;

// =========================================================
// HitFrameData — Polymorphic hitbox container for an ActionStep.
// Uses [SerializeReference] so the list can hold different
// concrete hitbox types while staying serializable.
//
// Hierarchy:
//   HitboxBase (abstract)
//   ├── NormalHitbox   — standard melee hitbox
//   ├── GrabHitbox     — grab/throw hitbox
//   └── ProjectileHitbox — spawns a projectile prefab
// =========================================================

// ─── Abstract Base ───────────────────────────────────────

[System.Serializable]
public abstract class HitboxBase
{
    public abstract string HitboxTypeName { get; }
    public virtual Color TimelineTrackColor => debugColor;

    [Header("Timing")]
    [Tooltip("Frame offset relative to the ActionStep's active-phase start.")]
    public int startFrameOffset = 0;

    [Tooltip("How many frames this hitbox stays active.")]
    public int activeFrames = 3;

    [Header("Debug")]
    public Color debugColor = Color.red;
}

// ─── Normal Hitbox ───────────────────────────────────────

[System.Serializable]
public class NormalHitbox : HitboxBase
{
    public override string HitboxTypeName => "Normal";
    public override Color TimelineTrackColor => new Color(1f, 0.93f, 0.56f);

    public NormalHitbox()
    {
        debugColor = TimelineTrackColor;
    }

    // ── Hitbox Geometry ──
    [Header("Hitbox Geometry")]
    public Vector2 offset = new Vector2(0.8f, 0.4f);
    public Vector2 size   = new Vector2(1.0f, 0.8f);

    // ── Damage & Knockback ──
    [Header("Damage & Knockback")]
    public float damageMultiplier = 1f;
    public DamageImpactTier impactTier = DamageImpactTier.BasicCombo;

    // ── Reaction & HitStop ──
    [Header("Reaction & HitStop")]
    public bool useDeterministicReaction = false;
    public EnemyReactionKind reactionKind = EnemyReactionKind.Knockback;
    [Tooltip("Frozen frames on impact (both attacker and victim).")]
    public int impactFreezeFrames = 4;
    [Tooltip("Frames the enemy remains in the core reaction state before recovery opens.")]
    public int reactionFrames = 15;
    [Tooltip("Frames spent recovering after the core reaction state.")]
    public int recoveryFrames = 9;
    [Tooltip("Horizontal speed applied away from the attacker.")]
    public float horizontalSpeed = 3f;
    [Tooltip("Vertical launch speed applied on knockup reactions.")]
    public float launchSpeed = 1f;
    public EnemyLandingBehavior landingBehavior = EnemyLandingBehavior.Knockdown;
    public bool canAirJuggle = true;
    public int maxJuggleCount = 2;
    public float juggleLaunchScale = 0.85f;
    public float juggleStunScale = 0.9f;

    [Header("Legacy Compatibility")]
    [Tooltip("Frozen frames on impact (both attacker and victim).")]
    public int hitstunFrames = 4;

    [Tooltip("Knockback velocity (X away from attacker, Y upward).")]
    public Vector2 reactionVelocity = new Vector2(3f, 1f);

    [Tooltip("Frames the enemy stays in knockback stun.")]
    public int knockbackFrames = 15;

    public bool isKnockdown = false;
    public bool snapToAttacker = false;
    public Vector2 snapOffset = Vector2.zero;

    public Color flashColor = Color.red;
    public int flashFrames = 6;
}

// ─── Grab Hitbox ─────────────────────────────────────────

[System.Serializable]
public class GrabHitbox : HitboxBase
{
    public override string HitboxTypeName => "Grab";
    public override Color TimelineTrackColor => new Color(0.84f, 0.70f, 1f);

    public GrabHitbox()
    {
        debugColor = TimelineTrackColor;
    }

    // ── Hitbox Geometry ──
    [Header("Hitbox Geometry")]
    public Vector2 offset = new Vector2(0.6f, 0.3f);
    public Vector2 size   = new Vector2(0.8f, 0.8f);

    // ── Grab Condition ──
    [Header("Grab Condition")]
    [Tooltip("Maximum range to initiate grab.")]
    public float grabRange = 1.2f;

    [Tooltip("Angle cone (degrees) within which grab is valid.")]
    public float grabAngle = 45f;

    [Tooltip("If true, grab can affect airborne targets.")]
    public bool canGrabAirborne = false;

    [Header("Grab Reaction")]
    public int impactFreezeFrames = 4;
    public int holdFrames = 24;
    public int recoveryFrames = 6;
    public Vector2 snapOffset = new Vector2(0.6f, 0f);
    public Color flashColor = Color.red;
    public int flashFrames = 6;
    public DamageImpactTier impactTier = DamageImpactTier.BasicCombo;

    [Header("Branching (Nested Step)")]
    [Tooltip("If true, the player will instantly transition to a unique AttackStep upon successful grab.")]
    public bool branchOnSuccess = false;
    
    [Tooltip("The sequence of inline StepDatas that execute when the grab connects.")]
    public List<AttackStepData> successSteps = new List<AttackStepData>();
}

// ─── Projectile Hitbox ───────────────────────────────────

/// <summary>
/// Trajectory control for a spawned projectile.
/// </summary>
[System.Serializable]
public class ProjectileTrajectory
{
    public float speed    = 10f;
    public float arc      = 0f;
    public float gravity  = 0f;
}

/// <summary>
/// Data the projectile prefab reads at runtime.
/// </summary>
[System.Serializable]
public class ProjectileStats
{
    public float damage    = 1f;
    public int   hitCount  = 1;
    public float lifespan  = 3f;

    [Header("Reaction")]
    public bool useDeterministicReaction = false;
    public EnemyReactionKind reactionKind = EnemyReactionKind.Knockback;
    public int impactFreezeFrames = 2;
    public int reactionFrames = 10;
    public int recoveryFrames = 6;
    public float horizontalSpeed = 2f;
    public float launchSpeed = 0f;
    public EnemyLandingBehavior landingBehavior = EnemyLandingBehavior.Recover;
    public bool canAirJuggle = true;
    public int maxJuggleCount = 1;
    public float juggleLaunchScale = 0.9f;
    public float juggleStunScale = 0.9f;
    public int hitstunFrames     = 2;
    public Vector2 reactionVelocity = new Vector2(2f, 0f);
    public int knockbackFrames   = 10;
    public Color flashColor = Color.red;
    public int flashFrames = 4;
    public DamageImpactTier impactTier = DamageImpactTier.BasicCombo;
}

[System.Serializable]
public class ProjectileHitbox : HitboxBase
{
    public override string HitboxTypeName => "Projectile";
    public override Color TimelineTrackColor => new Color(0.60f, 0.84f, 1f);

    public ProjectileHitbox()
    {
        debugColor = TimelineTrackColor;
    }

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Vector3 spawnPoint = new Vector3(0.5f, 0.5f, 0f);

    [Header("Trajectory")]
    public ProjectileTrajectory trajectory = new ProjectileTrajectory();

    [Header("Projectile Stats")]
    public ProjectileStats stats = new ProjectileStats();
}

// ─── Container ───────────────────────────────────────────

/// <summary>
/// Top-level container for all hitboxes on an ActionStep.
/// Appears as a gold track on the timeline.
/// </summary>
[System.Serializable]
public class HitFrameData : ActionStepComponent
{
    public override string ComponentName => "Hitbox";
    public override Color TrackColor => new Color(1f, 0.84f, 0f); // gold

    [SerializeReference]
    public List<HitboxBase> hitboxes = new List<HitboxBase>();

    public void Rescale(float ratio)
    {
        foreach (var hb in hitboxes)
        {
            hb.startFrameOffset = Mathf.RoundToInt(hb.startFrameOffset * ratio);
            hb.activeFrames = Mathf.Max(1, Mathf.RoundToInt(hb.activeFrames * ratio));

            if (hb is NormalHitbox normal)
            {
                normal.impactFreezeFrames = Mathf.RoundToInt(normal.impactFreezeFrames * ratio);
                normal.reactionFrames = Mathf.RoundToInt(normal.reactionFrames * ratio);
                normal.recoveryFrames = Mathf.RoundToInt(normal.recoveryFrames * ratio);
                normal.hitstunFrames = Mathf.RoundToInt(normal.hitstunFrames * ratio);
                normal.knockbackFrames = Mathf.RoundToInt(normal.knockbackFrames * ratio);
                normal.flashFrames = Mathf.RoundToInt(normal.flashFrames * ratio);
            }
            else if (hb is GrabHitbox grab)
            {
                grab.impactFreezeFrames = Mathf.RoundToInt(grab.impactFreezeFrames * ratio);
                grab.holdFrames = Mathf.RoundToInt(grab.holdFrames * ratio);
                grab.recoveryFrames = Mathf.RoundToInt(grab.recoveryFrames * ratio);
                grab.flashFrames = Mathf.RoundToInt(grab.flashFrames * ratio);
            }
            else if (hb is ProjectileHitbox proj)
            {
                proj.stats.impactFreezeFrames = Mathf.RoundToInt(proj.stats.impactFreezeFrames * ratio);
                proj.stats.reactionFrames = Mathf.RoundToInt(proj.stats.reactionFrames * ratio);
                proj.stats.recoveryFrames = Mathf.RoundToInt(proj.stats.recoveryFrames * ratio);
                proj.stats.hitstunFrames = Mathf.RoundToInt(proj.stats.hitstunFrames * ratio);
                proj.stats.knockbackFrames = Mathf.RoundToInt(proj.stats.knockbackFrames * ratio);
                proj.stats.flashFrames = Mathf.RoundToInt(proj.stats.flashFrames * ratio);
            }
        }
    }
}
