using System;
using UnityEngine;

public enum ProjectileSpawnMode
{
    FromCaster,
    TrackTarget
}

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
    public bool playSpawnAnimation = true;
    public float spawnDuration = 0f;
    public bool inheritFacingDirection = true;
    public ProjectilePhaseHitboxData spawningHitbox = new ProjectilePhaseHitboxData
    {
        enabled = false,
        decrementsMaxHits = false
    };

    [Header("Legacy Spawn Support")]
    public ProjectileSpawnMode spawnMode = ProjectileSpawnMode.FromCaster;

    [Header("Updating State")]
    public ProjectileTraversalMode traversalMode = ProjectileTraversalMode.Linear;
    public bool homeToTarget = false;
    public float homingTurnRate = 8f;
    public bool anchorToCaster = false;
    public float speed = 10f;
    public float lifetime = 2f;
    public int maxHits = 1;
    public ProjectilePhaseHitboxData routineHitbox = new ProjectilePhaseHitboxData
    {
        enabled = true,
        decrementsMaxHits = true
    };

    [Header("Detection")]
    public Vector2 travelColliderSize = new Vector2(0.5f, 0.5f);
    public LayerMask enemyLayerMask = Physics2D.DefaultRaycastLayers;

    [Header("Legacy Impact Routine")]
    public HitFrameData impactHitData = new HitFrameData();
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

    public ProjectileTraversalMode ResolveTraversalMode()
    {
        if (anchorToCaster)
        {
            return ProjectileTraversalMode.Anchored;
        }

        if (homeToTarget)
        {
            return ProjectileTraversalMode.Homing;
        }

        return traversalMode;
    }

    public Vector2 ResolveSpawnPosition(ProjectileSpawnContext context)
    {
        Vector2 facingOffset = ResolveFacingAwareOffset(context);

        switch (spawnPositionMode)
        {
            case SpawnPositionMode.Caster:
                return context.casterTransform != null
                    ? (Vector2)context.casterTransform.position + facingOffset
                    : context.origin;

            case SpawnPositionMode.Target:
                return context.targetTransform != null
                    ? (Vector2)context.targetTransform.position + facingOffset
                    : context.origin;

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
        if (routineHitbox != null && routineHitbox.HasHitbox)
        {
            return routineHitbox.hitbox;
        }

        return ResolveLegacyImpactHitbox();
    }

    public bool RoutineHitboxConsumesMaxHits()
    {
        return routineHitbox == null || !routineHitbox.enabled || routineHitbox.decrementsMaxHits;
    }

    public bool SpawningHitboxConsumesMaxHits()
    {
        return spawningHitbox != null && spawningHitbox.HasHitbox && spawningHitbox.decrementsMaxHits;
    }

    private HitboxBase ResolveLegacyImpactHitbox()
    {
        if (impactHitData == null || impactHitData.hitboxes == null)
        {
            return null;
        }

        for (int i = 0; i < impactHitData.hitboxes.Count; i++)
        {
            HitboxBase hitbox = impactHitData.hitboxes[i];
            if (hitbox is NormalHitbox or GrabHitbox)
            {
                return hitbox;
            }
        }

        return null;
    }

    private Vector2 ResolveFacingAwareOffset(ProjectileSpawnContext context)
    {
        Vector2 offset = context.resolvedSpawnOffset;
        if (offset == Vector2.zero)
        {
            offset = spawnOffset;
        }

        if (context.direction.x < 0f)
        {
            offset.x = -Mathf.Abs(offset.x);
        }
        else
        {
            offset.x = Mathf.Abs(offset.x);
        }

        return offset;
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
    public float damage;
    public int remainingHits;
}
