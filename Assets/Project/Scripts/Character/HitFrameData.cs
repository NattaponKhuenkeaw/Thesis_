using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// =========================================================
// HitFrameData — Polymorphic hitbox container for an ActionStep.
// Uses [SerializeReference] so the list can hold different
// concrete hitbox types while staying serializable.
//
// Hierarchy:
//   HitboxBase (abstract)
//   ├── NormalHitbox   — standard melee hitbox
//   ├── GrabHitbox     — grab/throw hitbox
//   └── ProjectileSpawnHitbox — spawns a projectile prefab
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

    [Header("Damage")]
    public ElementType elementType = ElementType.None;
    public CombatDamageTag damageTags = CombatDamageTag.None;
    [Tooltip("Skip camera impulse and global hitstop, including the critical-hit minimum. Does not change damage or enemy reaction.")]
    public bool suppressImpactFeedback;
    [Tooltip("Skip generic contact sparks; the authored action VFX still plays.")]
    public bool suppressContactVfx;

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
    [FormerlySerializedAs("damageMultiplier")]
    [Tooltip("Share of the CombatAction damage budget. Explicit action budgets clamp this value to 0..1.")]
    public float damageRatio = 1f;
    public DamageImpactTier impactTier = DamageImpactTier.Low;

    // ── Reaction & HitStop ──
    [Header("Reaction & HitStop")]
    public bool useDeterministicReaction = true;
    public EnemyReactionKind reactionKind = EnemyReactionKind.Knockback;
    [Tooltip("Frozen frames on impact (both attacker and victim).")]
    public int impactFreezeFrames = 4;
    
    [Tooltip("Frames spent in the horizontal knockback state.")]
    public int knockbackFrames = 15;
    [Tooltip("Frames spent knocking up and staying airborne.")]
    public int knockupFrames = 12;
    [Tooltip("Frames spent falling before landing/recovery.")]
    public int knockfallFrames = 10;
    [Tooltip("Frames spent recovering after the core reaction state.")]
    public int recoveryFrames = 9;
    
    [Tooltip("Horizontal speed applied away from the attacker.")]
    public float horizontalSpeed = 3f;
    public float launchSpeed = 1f;
    
    [Header("Snapping")]
    [Tooltip("If true, snaps the enemy to the attacker's anchor on hit.")]
    public bool useSnapToAnchor = false;
    public Vector2 snapOffset = Vector2.zero;
    
    public EnemyLandingBehavior landingBehavior = EnemyLandingBehavior.Knockdown;
    public bool canAirJuggle = true;
    public float juggleLaunchScale = 0.85f;
    public float juggleStunScale = 0.9f;

    public Color flashColor = Color.red;
    public int flashFrames = 6;

    // ── Hit Feedback ──
    [Header("Hit Feedback")]
    public AudioClip hitSFX;
    [Range(0f, 2f)] public float hitSFXVolume = 1f;
    [Range(0.1f, 3f)] public float hitSFXPitch = 1f;
    public Project.Combat.VFXType hitVFXType = Project.Combat.VFXType.None;
    public GameObject hitVFXPrefab;
    public Vector2 hitVFXOffset;

    [Header("Grab Interaction")]
    [Tooltip("If true, this hit will knock the enemy out of a grab session even if from the same owner.")]
    public bool breaksGrab = false;

    [Header("Armor Interaction")]
    [Tooltip("If true, this hit breaks trash Super Armor (orange armored state).")]
    public bool breaksArmor = false;
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
    [Tooltip("If true, grab can affect airborne targets.")]
    public bool canGrabAirborne = false;

    [Header("Damage")]
    [FormerlySerializedAs("damageMultiplier")]
    [Tooltip("Share of the CombatAction damage budget. Explicit action budgets clamp this value to 0..1.")]
    public float damageRatio = 0f;
    public int impactFreezeFrames = 4;
    public int holdFrames = 24;
    public int recoveryFrames = 6;
    public Color flashColor = Color.red;
    public int flashFrames = 6;

    // ── Hit Feedback ──
    [Header("Hit Feedback")]
    public AudioClip hitSFX;
    [Range(0f, 2f)] public float hitSFXVolume = 1f;
    [Range(0.1f, 3f)] public float hitSFXPitch = 1f;
    public Project.Combat.VFXType hitVFXType = Project.Combat.VFXType.None;
    public GameObject hitVFXPrefab;
    public Vector2 hitVFXOffset;
    public DamageImpactTier impactTier = DamageImpactTier.Low;
}

// ─── Projectile Hitbox ───────────────────────────────────

[System.Serializable]
public class ProjectileSpawnHitbox : HitboxBase
{
    public override string HitboxTypeName => "Projectile";
    public override Color TimelineTrackColor => new Color(0.42f, 0.81f, 1f);

    public ProjectileSpawnHitbox()
    {
        debugColor = TimelineTrackColor;
    }

    [Header("Projectile")]
    public ProjectileType projectileType = ProjectileType.None;
    public ProjectileData projectileData;
    public bool overrideSpawnOffset = false;
    public Vector2 spawnOffset = Vector2.zero;

    public bool HasAuthoredSpawnOffset => overrideSpawnOffset || spawnOffset != Vector2.zero;

    public Vector2 ResolveSpawnOffset()
    {
        if (overrideSpawnOffset)
        {
            return spawnOffset;
        }

        return projectileData != null ? projectileData.spawnOffset : Vector2.zero;
    }

    public ProjectileType ResolveProjectileType()
    {
        if (projectileType != ProjectileType.None)
        {
            return projectileType;
        }

        return projectileData != null ? projectileData.projectileType : ProjectileType.None;
    }
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
                normal.knockbackFrames = Mathf.RoundToInt(normal.knockbackFrames * ratio);
                normal.knockupFrames = Mathf.RoundToInt(normal.knockupFrames * ratio);
                normal.knockfallFrames = Mathf.RoundToInt(normal.knockfallFrames * ratio);
                normal.recoveryFrames = Mathf.RoundToInt(normal.recoveryFrames * ratio);
                normal.flashFrames = Mathf.RoundToInt(normal.flashFrames * ratio);
            }
            else if (hb is GrabHitbox grab)
            {
                grab.impactFreezeFrames = Mathf.RoundToInt(grab.impactFreezeFrames * ratio);
                grab.holdFrames = Mathf.RoundToInt(grab.holdFrames * ratio);
                grab.recoveryFrames = Mathf.RoundToInt(grab.recoveryFrames * ratio);
                grab.flashFrames = Mathf.RoundToInt(grab.flashFrames * ratio);
            }
        }
    }
}
