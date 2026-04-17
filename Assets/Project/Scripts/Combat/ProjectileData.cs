using UnityEngine;

public enum ProjectileSpawnMode
{
    FromCaster,
    TrackTarget
}

[CreateAssetMenu(fileName = "ProjectileData_", menuName = "Game/Combat/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    [Header("Prefab / Visual")]
    public GameObject projectilePrefab;
    public Vector2 spawnOffset = new Vector2(1.2f, 0.6f);

    [Header("Spawn")]
    public ProjectileSpawnMode spawnMode = ProjectileSpawnMode.FromCaster;
    public bool inheritFacingDirection = true;
    public bool homeToTarget = false;
    public float homingTurnRate = 8f;

    [Header("Travel")]
    public float speed = 10f;
    public float lifetime = 2f;
    public int maxHits = 1;

    [Header("Detection")]
    public Vector2 travelColliderSize = new Vector2(0.5f, 0.5f);
    public LayerMask enemyLayerMask = Physics2D.DefaultRaycastLayers;

    [Header("Impact Routine")]
    public HitFrameData impactHitData = new HitFrameData();
    [Tooltip("Frames to wait between hits in a multi-hit routine.")]
    public int hitIntervalFrames = 5;
    public bool stopOnImpact = true;
    public bool anchorToCaster = false;
    public bool destroyWhenHitsExhausted = true;
    public bool destroyOnLifetimeExpire = true;

    [Header("Default Feedback")]
    public Project.Combat.VFXType impactVFXType = Project.Combat.VFXType.HitSpark_Medium;
    public AudioClip impactAudio;
}

public struct ProjectileSpawnContext
{
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
