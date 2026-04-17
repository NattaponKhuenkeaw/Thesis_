using UnityEngine;
using CombatCore;
using Project.Combat;

public class ProjectileRuntime : MonoBehaviour
{
    private enum ProjectileRuntimeState
    {
        Spawning,
        Traveling,
        ContactFreeze,
        BetweenHits,
        Expired
    }

    private ProjectileManager manager;
    private ProjectileSpawnContext spawnContext;
    private ProjectileData projectileData;
    private HitBoxController hitBoxController;
    private HitBoxDamage hitBoxDamage;
    private BoxCollider2D triggerCollider;
    private Rigidbody2D rb;

    private ProjectileRuntimeState runtimeState = ProjectileRuntimeState.Spawning;
    private Vector2 currentDirection = Vector2.right;
    private float lifeTimer;
    private float logicTimeCounter;
    private int currentImpactFrame;
    private int activeHitboxCount;
    private int betweenHitsFramesRemaining;
    private EnemyController pendingEnemy;
    private EnemyController trackedEnemy;
    private bool destroyQueued;

    public ProjectileData ProjectileData => projectileData;
    public Vector2 CurrentDirection => currentDirection.sqrMagnitude > 0.001f ? currentDirection.normalized : Vector2.right;
    public Transform AttackerTransform => spawnContext.casterTransform;
    public Vector2 AttackerPosition => transform.position;

    public void Initialize(ProjectileManager manager, ProjectileSpawnContext context)
    {
        this.manager = manager;
        spawnContext = context;
        projectileData = context.projectileData;
        runtimeState = ProjectileRuntimeState.Spawning;
        destroyQueued = false;
        trackedEnemy = null;
        pendingEnemy = null;
        activeHitboxCount = 0;
        betweenHitsFramesRemaining = 0;
        currentImpactFrame = 0;
        logicTimeCounter = 0f;
        lifeTimer = projectileData != null ? projectileData.lifetime : 0f;
        currentDirection = context.direction.sqrMagnitude > 0.001f ? context.direction.normalized : Vector2.right;

        EnsurePhysicsComponents();
        EnsureCombatComponents();
        EnsureDebugDrawer();

        transform.position = context.origin;
        ApplyFacingVisual();
        ConfigureTravelCollider();
        EnterState(ProjectileRuntimeState.Traveling);
    }

    void Update()
    {
        if (destroyQueued)
        {
            Destroy(gameObject);
            return;
        }

        if (runtimeState == ProjectileRuntimeState.Expired)
        {
            return;
        }

        UpdateLifetime();

        if (ShouldTravel())
        {
            UpdateTravel();
        }
    }

    void FixedUpdate()
    {
        if (runtimeState == ProjectileRuntimeState.Expired)
        {
            return;
        }

        switch (runtimeState)
        {
            case ProjectileRuntimeState.ContactFreeze:
                ProcessImpactFrames();
                break;

            case ProjectileRuntimeState.BetweenHits:
                ProcessBetweenHits();
                break;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryStartContact(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryStartContact(other);
    }

    void OnDestroy()
    {
        manager?.UnregisterProjectile(this);
    }

    public void RegisterSuccessfulHit(EnemyController enemy)
    {
        if (spawnContext.remainingHits <= 0)
        {
            return;
        }

        trackedEnemy = enemy != null ? enemy : trackedEnemy;
        spawnContext.remainingHits--;
    }

    private void EnsurePhysicsComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
    }

    private void EnsureCombatComponents()
    {
        hitBoxController = GetComponent<HitBoxController>();
        if (hitBoxController == null)
        {
            hitBoxController = gameObject.AddComponent<HitBoxController>();
        }

        hitBoxController.hitboxCollider = triggerCollider;
        hitBoxController.ConfigureAnchoring(HitboxAnchorMode.Self, transform);

        hitBoxDamage = GetComponent<HitBoxDamage>();
        if (hitBoxDamage == null)
        {
            hitBoxDamage = gameObject.AddComponent<HitBoxDamage>();
        }

        hitBoxDamage.enabled = false;
        hitBoxDamage.SetProjectileOwner(this);
    }

    private void EnsureDebugDrawer()
    {
        if (triggerCollider != null && GetComponent<ColliderDebugDrawer>() == null)
        {
            ColliderDebugDrawer drawer = gameObject.AddComponent<ColliderDebugDrawer>();
            drawer.debugColor = Color.cyan;
        }
    }

    private void UpdateLifetime()
    {
        if (projectileData == null || projectileData.lifetime <= 0f)
        {
            return;
        }

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f && projectileData.destroyOnLifetimeExpire)
        {
            EnterState(ProjectileRuntimeState.Expired);
        }
    }

    private bool ShouldTravel()
    {
        if (runtimeState == ProjectileRuntimeState.Traveling)
        {
            return true;
        }

        return runtimeState == ProjectileRuntimeState.BetweenHits
            && projectileData != null
            && !projectileData.stopOnImpact;
    }

    private void UpdateTravel()
    {
        if (projectileData == null)
        {
            return;
        }

        if (projectileData.anchorToCaster && spawnContext.casterTransform != null)
        {
            Vector2 resolvedOffset = spawnContext.resolvedSpawnOffset;
            resolvedOffset.x *= GetFacingSign();
            transform.position = (Vector2)spawnContext.casterTransform.position + resolvedOffset;
            ApplyFacingVisual();
            return;
        }

        if (projectileData.homeToTarget && spawnContext.targetTransform != null)
        {
            Vector2 toTarget = (Vector2)(spawnContext.targetTransform.position - transform.position);
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Vector2 desiredDirection = toTarget.normalized;
                currentDirection = Vector2.Lerp(
                    currentDirection,
                    desiredDirection,
                    projectileData.homingTurnRate * Time.deltaTime).normalized;
            }
        }

        transform.position += (Vector3)(CurrentDirection * projectileData.speed * Time.deltaTime);
        ApplyFacingVisual();
    }

    private void TryStartContact(Collider2D other)
    {
        if (runtimeState != ProjectileRuntimeState.Traveling || spawnContext.remainingHits <= 0)
        {
            return;
        }

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null || !enemy.IsAlive)
        {
            return;
        }

        BeginContact(enemy);
    }

    private void BeginContact(EnemyController enemy)
    {
        if (projectileData == null
            || projectileData.impactHitData == null
            || projectileData.impactHitData.hitboxes == null
            || projectileData.impactHitData.hitboxes.Count == 0)
        {
            EnterState(ProjectileRuntimeState.Expired);
            return;
        }

        trackedEnemy = enemy;
        pendingEnemy = enemy;
        logicTimeCounter = 0f;
        currentImpactFrame = 0;
        activeHitboxCount = 0;
        hitBoxDamage.ResetActivationState();
        EnterState(ProjectileRuntimeState.ContactFreeze);
    }

    private void ProcessImpactFrames()
    {
        if (projectileData == null || projectileData.impactHitData == null)
        {
            FinalizeContactFreeze();
            return;
        }

        logicTimeCounter += Time.fixedDeltaTime;
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * FrameDataConstants.LOGIC_FPS + 0.999f);
        while (currentImpactFrame < targetFrame)
        {
            currentImpactFrame++;

            for (int i = 0; i < projectileData.impactHitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = projectileData.impactHitData.hitboxes[i];
                if (hitbox == null)
                {
                    continue;
                }

                if (currentImpactFrame - 1 == hitbox.startFrameOffset)
                {
                    ActivateImpactHitbox(hitbox);
                }

                if (hitbox is NormalHitbox or GrabHitbox)
                {
                    if (currentImpactFrame - 1 == hitbox.startFrameOffset + hitbox.activeFrames)
                    {
                        DisableImpactHitbox();
                    }
                }
            }
        }

        if (activeHitboxCount > 0)
        {
            PerformImpactOverlapCheck();
        }

        if (currentImpactFrame > GetImpactMaxFrame() && activeHitboxCount <= 0)
        {
            FinalizeContactFreeze();
        }
    }

    private void ProcessBetweenHits()
    {
        if (betweenHitsFramesRemaining > 0)
        {
            betweenHitsFramesRemaining--;
            return;
        }

        if (spawnContext.remainingHits <= 0)
        {
            EnterState(projectileData != null && projectileData.destroyWhenHitsExhausted
                ? ProjectileRuntimeState.Expired
                : ProjectileRuntimeState.Traveling);
            return;
        }

        EnemyController nextEnemy = TryFindEnemyInTravelCollider(trackedEnemy);
        if (nextEnemy != null)
        {
            BeginContact(nextEnemy);
            return;
        }

        EnterState(ProjectileRuntimeState.Traveling);
    }

    private void ActivateImpactHitbox(HitboxBase hitbox)
    {
        float facing = GetFacingSign();

        switch (hitbox)
        {
            case NormalHitbox normalHitbox:
                hitBoxController.ApplyHitbox(normalHitbox, facing);
                hitBoxDamage.SetHitData(
                    spawnContext.damage * normalHitbox.damageMultiplier,
                    false,
                    0f,
                    0f,
                    normalHitbox,
                    spawnContext.category,
                    spawnContext.sourceActionName,
                    spawnContext.sourceStep,
                    spawnContext.passiveController);
                hitBoxDamage.ResetActivationState();
                EnableImpactHitbox();
                break;

            case GrabHitbox grabHitbox:
                hitBoxController.ApplyHitbox(grabHitbox, facing);
                hitBoxDamage.SetGrabData(
                    spawnContext.damage,
                    grabHitbox,
                    false,
                    spawnContext.category,
                    spawnContext.sourceActionName,
                    spawnContext.sourceStep,
                    spawnContext.passiveController);
                hitBoxDamage.ResetActivationState();
                EnableImpactHitbox();
                break;
        }

        if (pendingEnemy != null)
        {
            hitBoxDamage.ProcessHit(pendingEnemy);
            pendingEnemy = null;
        }
    }

    private void EnableImpactHitbox()
    {
        activeHitboxCount++;
        hitBoxDamage.enabled = true;
        triggerCollider.enabled = true;
    }

    private void DisableImpactHitbox()
    {
        activeHitboxCount = Mathf.Max(0, activeHitboxCount - 1);
        if (activeHitboxCount == 0)
        {
            hitBoxDamage.enabled = false;
        }
    }

    private void FinalizeContactFreeze()
    {
        activeHitboxCount = 0;
        pendingEnemy = null;
        hitBoxDamage.enabled = false;

        if (spawnContext.remainingHits <= 0 && projectileData != null && projectileData.destroyWhenHitsExhausted)
        {
            EnterState(ProjectileRuntimeState.Expired);
            return;
        }

        EnterState(ProjectileRuntimeState.BetweenHits);
    }

    private void PerformImpactOverlapCheck()
    {
        Collider2D[] results = GetOverlapResults();
        for (int i = 0; i < results.Length; i++)
        {
            EnemyController enemy = results[i].GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                trackedEnemy = enemy;
                hitBoxDamage.ProcessHit(enemy);
            }
        }
    }

    private Collider2D[] GetOverlapResults()
    {
        Vector2 worldPos = triggerCollider.transform.TransformPoint(triggerCollider.offset);
        float angle = triggerCollider.transform.eulerAngles.z;
        LayerMask enemyLayers = projectileData != null ? projectileData.enemyLayerMask : Physics2D.DefaultRaycastLayers;
        return Physics2D.OverlapBoxAll(worldPos, triggerCollider.size, angle, enemyLayers);
    }

    private EnemyController TryFindEnemyInTravelCollider(EnemyController preferredEnemy = null)
    {
        Collider2D[] results = GetOverlapResults();

        if (preferredEnemy != null)
        {
            for (int i = 0; i < results.Length; i++)
            {
                EnemyController enemy = results[i].GetComponentInParent<EnemyController>();
                if (enemy == preferredEnemy && enemy.IsAlive)
                {
                    return enemy;
                }
            }
        }

        for (int i = 0; i < results.Length; i++)
        {
            EnemyController enemy = results[i].GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                return enemy;
            }
        }

        return null;
    }

    private void ConfigureTravelCollider()
    {
        if (triggerCollider == null)
        {
            return;
        }

        Vector2 travelSize = projectileData != null ? projectileData.travelColliderSize : Vector2.one * 0.5f;
        triggerCollider.size = travelSize;
        triggerCollider.offset = Vector2.zero;
        triggerCollider.enabled = true;
    }

    private int GetImpactMaxFrame()
    {
        if (projectileData == null || projectileData.impactHitData == null || projectileData.impactHitData.hitboxes == null)
        {
            return 0;
        }

        int maxFrame = 0;
        for (int i = 0; i < projectileData.impactHitData.hitboxes.Count; i++)
        {
            HitboxBase hitbox = projectileData.impactHitData.hitboxes[i];
            if (hitbox != null)
            {
                maxFrame = Mathf.Max(maxFrame, hitbox.startFrameOffset + hitbox.activeFrames + 1);
            }
        }

        return maxFrame;
    }

    private void EnterState(ProjectileRuntimeState newState)
    {
        runtimeState = newState;

        switch (runtimeState)
        {
            case ProjectileRuntimeState.Traveling:
                trackedEnemy = null;
                pendingEnemy = null;
                betweenHitsFramesRemaining = 0;
                hitBoxDamage.enabled = false;
                ConfigureTravelCollider();
                break;

            case ProjectileRuntimeState.ContactFreeze:
                hitBoxDamage.enabled = true;
                break;

            case ProjectileRuntimeState.BetweenHits:
                betweenHitsFramesRemaining = projectileData != null ? Mathf.Max(0, projectileData.hitIntervalFrames) : 0;
                hitBoxDamage.enabled = false;
                ConfigureTravelCollider();
                break;

            case ProjectileRuntimeState.Expired:
                hitBoxDamage.enabled = false;
                if (triggerCollider != null)
                {
                    triggerCollider.enabled = false;
                }
                destroyQueued = true;
                break;
        }
    }

    private void ApplyFacingVisual()
    {
        if (projectileData != null && projectileData.inheritFacingDirection)
        {
            float angle = GetFacingSign() < 0f ? 180f : 0f;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    private float GetFacingSign()
    {
        return currentDirection.x < 0f ? -1f : 1f;
    }
}
