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
    public DamageImpactTier impactTier = DamageImpactTier.Low;

    // ── Reaction & HitStop ──
    [Header("Reaction & HitStop")]
    public bool useDeterministicReaction = true;
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

    public Color flashColor = Color.red;
    public int flashFrames = 6;

    // ── Hit Feedback ──
    [Header("Hit Feedback")]
    public AudioClip hitSFX;
    public GameObject hitVFXPrefab;
    public Vector2 hitVFXOffset;
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

    [Header("Grab Reaction")]
    public int impactFreezeFrames = 4;
    public int holdFrames = 24;
    public int recoveryFrames = 6;
    public Vector2 snapOffset = new Vector2(0.6f, 0f);
    public Color flashColor = Color.red;
    public int flashFrames = 6;

    // ── Hit Feedback ──
    [Header("Hit Feedback")]
    public AudioClip hitSFX;
    public GameObject hitVFXPrefab;
    public Vector2 hitVFXOffset;
    public DamageImpactTier impactTier = DamageImpactTier.Low;

}

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
    public ProjectileData projectileData;
    public bool overrideSpawnOffset = false;
    public Vector2 spawnOffset = Vector2.zero;
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
