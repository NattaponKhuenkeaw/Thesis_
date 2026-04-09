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

    // ── Hitbox Geometry ──
    [Header("Hitbox Geometry")]
    public Vector2 offset = new Vector2(0.8f, 0.4f);
    public Vector2 size   = new Vector2(1.0f, 0.8f);

    // ── Damage & Knockback ──
    [Header("Damage & Knockback")]
    public float damageMultiplier = 1f;
    public float knockbackForce   = 4f;
    public float cameraShakeStrength = 0f;
    public DamageImpactTier impactTier = DamageImpactTier.BasicCombo;

    // ── Reaction & HitStop ──
    [Header("Reaction & HitStop")]
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
    public int hitstunFrames     = 2;
    public Vector2 reactionVelocity = new Vector2(2f, 0f);
    public int knockbackFrames   = 10;
}

[System.Serializable]
public class ProjectileHitbox : HitboxBase
{
    public override string HitboxTypeName => "Projectile";

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
}
