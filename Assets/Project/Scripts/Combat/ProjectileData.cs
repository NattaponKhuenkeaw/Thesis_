using System;
using UnityEngine;

public enum SpawnPositionMode
{
    Caster,
    Target,
    Offset
}

public enum ProjectileTraversalMode
{
    Linear,
    Homing,
    Anchored
}

public enum ProjectileReferencePointMode
{
    Pivot,
    Center,
    GroundProjection
}

public enum ProjectileHitCountMode
{
    Shared,
    PerEnemy
}

[Serializable]
public class ProjectilePhaseHitboxData
{
    public bool enabled;
    public bool decrementsMaxHits = true;

    [SerializeReference]
    public HitboxBase hitbox;

    public bool HasHitbox => enabled && hitbox != null;
}

[Serializable]
public class ProjectileFeedbackDefinition
{
    public Project.Combat.VFXType vfxType = Project.Combat.VFXType.None;
    public AnimationClip vfxClip;
    public AudioClip audio;
    public Vector2 offset = Vector2.zero;
}

public static class ProjectileSpatialUtility
{
    private const float GroundProjectionPadding = 0.5f;
    private const float GroundProjectionDistance = 100f;

    public static Vector2 ResolveFacingAwareOffset(Vector2 offset, float facingSign)
    {
        float resolvedSign = facingSign < 0f ? -1f : 1f;
        offset.x = Mathf.Abs(offset.x) * resolvedSign;
        return offset;
    }

    public static bool TryGetHitboxGeometry(HitboxBase hitbox, float facingSign, out Vector2 offset, out Vector2 size)
    {
        offset = Vector2.zero;
        size = Vector2.zero;

        switch (hitbox)
        {
            case NormalHitbox normalHitbox:
                offset = ResolveFacingAwareOffset(normalHitbox.offset, facingSign);
                size = normalHitbox.size;
                return true;

            case GrabHitbox grabHitbox:
                offset = ResolveFacingAwareOffset(grabHitbox.offset, facingSign);
                size = grabHitbox.size;
                return true;

            default:
                return false;
        }
    }

    public static Vector2 ResolveReferencePosition(Transform actor, ProjectileReferencePointMode mode)
    {
        if (actor == null)
        {
            return Vector2.zero;
        }

        if (mode == ProjectileReferencePointMode.Pivot)
        {
            return actor.position;
        }

        Collider2D referenceCollider = ResolveReferenceCollider(actor);
        return mode switch
        {
            ProjectileReferencePointMode.Center => referenceCollider != null
                ? (Vector2)referenceCollider.bounds.center
                : (Vector2)actor.position,
            ProjectileReferencePointMode.GroundProjection => ResolveGroundProjection(actor, referenceCollider),
            _ => actor.position
        };
    }

    private static Collider2D ResolveReferenceCollider(Transform actor)
    {
        if (actor == null)
        {
            return null;
        }

        EnemyController enemy = actor.GetComponent<EnemyController>() ?? actor.GetComponentInParent<EnemyController>();
        if (enemy != null && enemy.bodyCollider != null)
        {
            return enemy.bodyCollider;
        }

        Collider2D directCollider = actor.GetComponent<Collider2D>();
        if (directCollider != null && !directCollider.isTrigger)
        {
            return directCollider;
        }

        Collider2D[] childColliders = actor.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider2D collider = childColliders[i];
            if (collider != null && !collider.isTrigger)
            {
                return collider;
            }
        }

        return childColliders.Length > 0 ? childColliders[0] : null;
    }

    private static Vector2 ResolveGroundProjection(Transform actor, Collider2D referenceCollider)
    {
        Vector2 pivot = actor.position;
        int layerMask = ResolveGroundLayerMask(actor);
        float originY = referenceCollider != null
            ? referenceCollider.bounds.max.y + GroundProjectionPadding
            : pivot.y + GroundProjectionPadding;
        Vector2 rayOrigin = new Vector2(pivot.x, originY);

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, GroundProjectionDistance, layerMask);
        if (hit.collider != null)
        {
            return new Vector2(pivot.x, hit.point.y);
        }

        if (referenceCollider != null)
        {
            return new Vector2(pivot.x, referenceCollider.bounds.min.y);
        }

        return pivot;
    }

    private static int ResolveGroundLayerMask(Transform actor)
    {
        if (actor == null)
        {
            return Physics2D.DefaultRaycastLayers;
        }

        PlayerMovement playerMovement = actor.GetComponent<PlayerMovement>() ?? actor.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null && playerMovement.groundLayer.value != 0)
        {
            return playerMovement.groundLayer.value;
        }

        EnemyHitReactionHandler enemyGrounding = actor.GetComponent<EnemyHitReactionHandler>() ?? actor.GetComponentInParent<EnemyHitReactionHandler>();
        if (enemyGrounding != null && enemyGrounding.groundLayers.value != 0)
        {
            return enemyGrounding.groundLayers.value;
        }

        return Physics2D.DefaultRaycastLayers;
    }
}

[CreateAssetMenu(fileName = "ProjectileData_", menuName = "Game/Combat/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    [Header("Identity")]
    public ProjectileType projectileType = ProjectileType.None;

    [Header("Prefab / Visual")]
    public GameObject projectilePrefab;
    public AnimationClip spawnClip;
    public AnimationClip loopClip;
    public Vector2 spawnOffset = new Vector2(1.2f, 0.6f);

    [Header("Spawning State")]
    public SpawnPositionMode spawnPositionMode = SpawnPositionMode.Offset;
    public ProjectileReferencePointMode spawnReferencePointMode = ProjectileReferencePointMode.Pivot;
    public bool playSpawnAnimation = true;
    public float spawnDuration = 0f;
    public bool inheritFacingDirection = true;
    public bool applyOffsetToTarget = true;
    public ProjectilePhaseHitboxData spawningHitbox = new ProjectilePhaseHitboxData
    {
        enabled = false,
        decrementsMaxHits = false
    };

    [Header("Updating State")]
    public ProjectileTraversalMode traversalMode = ProjectileTraversalMode.Linear;
    public ProjectileReferencePointMode trackingReferencePointMode = ProjectileReferencePointMode.Pivot;
    public ProjectileReferencePointMode anchorReferencePointMode = ProjectileReferencePointMode.Pivot;
    public float homingTurnRate = 8f;
    public float trackDelaySeconds = 0f;
    public float trackingRadius = 0f;
    public float speed = 10f;
    public float lifetime = 2f;
    public int maxHits = 1;
    public ProjectileHitCountMode hitCountMode = ProjectileHitCountMode.Shared;
    public ProjectilePhaseHitboxData routineHitbox = new ProjectilePhaseHitboxData
    {
        enabled = true,
        decrementsMaxHits = true
    };

    [Header("Detection")]
    public Vector2 travelColliderSize = new Vector2(0.5f, 0.5f);
    public Vector2 travelColliderOffset = Vector2.zero;
    public LayerMask enemyLayerMask = Physics2D.DefaultRaycastLayers;

    [Tooltip("Frames to wait between hits in a multi-hit routine.")]
    public int hitIntervalFrames = 5;
    public bool stopOnImpact = true;
    public bool destroyWhenHitsExhausted = true;
    public bool destroyOnLifetimeExpire = true;

    [Header("Feedback Events")]
    public ProjectileFeedbackDefinition spawnFeedback = new ProjectileFeedbackDefinition();
    public ProjectileFeedbackDefinition expireFeedback = new ProjectileFeedbackDefinition();

    [Header("Default Impact Feedback")]
    public Project.Combat.VFXType impactVFXType = Project.Combat.VFXType.HitSpark_Medium;
    public AudioClip impactAudio;

    public bool RequiresTarget => spawnPositionMode == SpawnPositionMode.Target || traversalMode == ProjectileTraversalMode.Homing;

    public Vector2 ResolveSpawnPosition(ProjectileSpawnContext context)
    {
        Vector2 facingOffset = ResolveRuntimeOffset(context, context.direction.x);

        switch (spawnPositionMode)
        {
            case SpawnPositionMode.Caster:
                return ResolvePositionWithOffset(context.casterTransform, spawnReferencePointMode, facingOffset, context.origin);

            case SpawnPositionMode.Target:
                if (context.targetTransform != null)
                {
                    Vector2 offsetToApply = applyOffsetToTarget ? facingOffset : Vector2.zero;
                    return ResolvePositionWithOffset(context.targetTransform, spawnReferencePointMode, offsetToApply, context.origin);
                }

                return context.origin;

            case SpawnPositionMode.Offset:
            default:
                return context.origin;
        }
    }

    public bool HasSpawningState()
    {
        return playSpawnAnimation || spawnDuration > 0f || ResolveSpawningHitbox() != null;
    }

    public HitboxBase ResolveSpawningHitbox()
    {
        return spawningHitbox != null && spawningHitbox.HasHitbox
            ? spawningHitbox.hitbox
            : null;
    }

    public HitboxBase ResolveRoutineHitbox()
    {
        return routineHitbox != null && routineHitbox.HasHitbox
            ? routineHitbox.hitbox
            : null;
    }

    public bool RoutineHitboxConsumesMaxHits()
    {
        return routineHitbox == null || !routineHitbox.enabled || routineHitbox.decrementsMaxHits;
    }

    public bool SpawningHitboxConsumesMaxHits()
    {
        return spawningHitbox != null && spawningHitbox.HasHitbox && spawningHitbox.decrementsMaxHits;
    }

    public bool UsesSharedHitCount()
    {
        return hitCountMode == ProjectileHitCountMode.Shared;
    }

    public Vector2 ResolveAnchoredPosition(ProjectileSpawnContext context, float facingSign)
    {
        Vector2 facingOffset = ResolveRuntimeOffset(context, facingSign);
        return ResolvePositionWithOffset(context.casterTransform, anchorReferencePointMode, facingOffset, context.origin);
    }

    public Vector2 ResolveTrackingReferencePosition(Transform targetTransform)
    {
        return ProjectileSpatialUtility.ResolveReferencePosition(targetTransform, trackingReferencePointMode);
    }

    public Vector2 ResolveTrackingFallbackPosition(ProjectileSpawnContext context)
    {
        if (context.casterTransform != null)
        {
            return ProjectileSpatialUtility.ResolveReferencePosition(context.casterTransform, trackingReferencePointMode);
        }

        return context.origin;
    }

    public Vector2 ResolvePassiveColliderOffset(float facingSign)
    {
        return ProjectileSpatialUtility.ResolveFacingAwareOffset(travelColliderOffset, facingSign);
    }

    private Vector2 ResolveRuntimeOffset(ProjectileSpawnContext context, float facingSign)
    {
        Vector2 offset = context.resolvedSpawnOffset;
        if (offset == Vector2.zero)
        {
            offset = spawnOffset;
        }

        return ProjectileSpatialUtility.ResolveFacingAwareOffset(offset, facingSign);
    }

    private static Vector2 ResolvePositionWithOffset(
        Transform referenceTransform,
        ProjectileReferencePointMode referencePointMode,
        Vector2 offset,
        Vector2 fallbackPosition)
    {
        if (referenceTransform == null)
        {
            return fallbackPosition;
        }

        return ProjectileSpatialUtility.ResolveReferencePosition(referenceTransform, referencePointMode) + offset;
    }
}

public struct ProjectileSpawnContext
{
    public ProjectileType projectileType;
    public ProjectileData projectileData;
    public Transform casterTransform;
    public Transform projectileParent;
    public Transform targetTransform;
    public CharacterPassiveController passiveController;
    public AttackStepData sourceStep;
    public string sourceActionName;
    public AttackCategory category;
    public Vector2 origin;
    public Vector2 direction;
    public Vector2 resolvedSpawnOffset;
    public float sourceAttack;
    public CharacterStats attackerStats;
    public float actionDamageMultiplier;
    public float auraEnergyReward;
    public AuraEnergyRewardClaim auraEnergyRewardClaim;
    public AdditionalDamageEffectDefinition[] additionalDamageEffects;
    public bool isSkillOrUltimate;
    public int remainingHits;
    public PlayerCombat sourceCombat;
    public CharacterObject sourceCharacter;
}
