using UnityEngine;
using CombatCore;

public class ProjectileRuntime : MonoBehaviour
{
    private ProjectileSpawnContext spawnContext;
    private ProjectileData projectileData;
    private HitBoxController hitBoxController;
    private HitBoxDamage hitBoxDamage;
    private BoxCollider2D triggerCollider;
    private Rigidbody2D rb;

    private Vector2 currentDirection = Vector2.right;
    private float lifeTimer;
    private float logicTimeCounter;
    private int currentImpactFrame;
    private int activeHitboxCount;
    private bool isImpacting;
    private EnemyController pendingEnemy;
    private bool destroyQueued;

    public Vector2 CurrentDirection => currentDirection.sqrMagnitude > 0.001f ? currentDirection.normalized : Vector2.right;
    public Transform AttackerTransform => spawnContext.casterTransform;
    public Vector2 AttackerPosition => AttackerTransform != null ? AttackerTransform.position : transform.position;

    public static ProjectileRuntime Spawn(ProjectileSpawnContext context)
    {
        GameObject projectileObject;
        if (context.projectileData != null && context.projectileData.projectilePrefab != null)
        {
            projectileObject = Instantiate(
                context.projectileData.projectilePrefab,
                context.origin,
                Quaternion.identity,
                context.projectileParent);
        }
        else
        {
            projectileObject = new GameObject(context.projectileData != null ? context.projectileData.name : "Projectile");
            projectileObject.transform.position = context.origin;
            if (context.projectileParent != null)
            {
                projectileObject.transform.SetParent(context.projectileParent);
            }
        }

        ProjectileRuntime runtime = projectileObject.GetComponent<ProjectileRuntime>();
        if (runtime == null)
        {
            runtime = projectileObject.AddComponent<ProjectileRuntime>();
        }

        runtime.Initialize(context);
        return runtime;
    }

    public void Initialize(ProjectileSpawnContext context)
    {
        spawnContext = context;
        projectileData = context.projectileData;
        lifeTimer = projectileData != null ? projectileData.lifetime : 0f;
        currentDirection = context.direction.sqrMagnitude > 0.001f ? context.direction.normalized : Vector2.right;

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
        triggerCollider.size = projectileData != null ? projectileData.travelColliderSize : Vector2.one * 0.5f;
        triggerCollider.offset = Vector2.zero;

        hitBoxController = GetComponent<HitBoxController>();
        if (hitBoxController == null)
        {
            hitBoxController = gameObject.AddComponent<HitBoxController>();
        }

        hitBoxController.hitboxCollider = triggerCollider;
        hitBoxController.SetWorldAnchor(transform);

        hitBoxDamage = GetComponent<HitBoxDamage>();
        if (hitBoxDamage == null)
        {
            hitBoxDamage = gameObject.AddComponent<HitBoxDamage>();
        }

        hitBoxDamage.enabled = false;
        hitBoxDamage.SetProjectileOwner(this);
    }

    void Update()
    {
        if (destroyQueued)
        {
            Destroy(gameObject);
            return;
        }

        if (!isImpacting)
        {
            UpdateTravel();
        }

        if (projectileData != null && projectileData.lifetime > 0f)
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f && projectileData.destroyOnLifetimeExpire)
            {
                destroyQueued = true;
            }
        }
    }

    void FixedUpdate()
    {
        if (isImpacting)
        {
            ProcessImpactFrames();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isImpacting)
        {
            return;
        }

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null && enemy.IsAlive)
        {
            StartImpact(enemy);
        }
    }

    public void RegisterSuccessfulHit(EnemyController enemy)
    {
        if (spawnContext.remainingHits <= 0)
        {
            return;
        }

        spawnContext.remainingHits--;
        if (spawnContext.remainingHits <= 0 && projectileData != null && projectileData.destroyWhenHitsExhausted)
        {
            destroyQueued = true;
        }
    }

    private void UpdateTravel()
    {
        if (projectileData == null)
        {
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
    }

    private void StartImpact(EnemyController enemy)
    {
        pendingEnemy = enemy;
        isImpacting = true;
        logicTimeCounter = 0f;
        currentImpactFrame = 0;
        activeHitboxCount = 0;
        hitBoxDamage.enabled = true;

        if (projectileData == null || projectileData.impactHitData == null || projectileData.impactHitData.hitboxes.Count == 0)
        {
            destroyQueued = true;
        }
    }

    private void ProcessImpactFrames()
    {
        if (projectileData == null || projectileData.impactHitData == null)
        {
            destroyQueued = true;
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
            PerformOverlapCheck();
        }

        int maxFrame = GetImpactMaxFrame();
        if (currentImpactFrame > maxFrame && activeHitboxCount <= 0)
        {
            destroyQueued = true;
        }
    }

    private void ActivateImpactHitbox(HitboxBase hitbox)
    {
        switch (hitbox)
        {
            case NormalHitbox normalHitbox:
                hitBoxController.ApplyHitbox(normalHitbox);
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
                hitBoxController.ApplyHitbox(grabHitbox);
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
        triggerCollider.enabled = true;
    }

    private void DisableImpactHitbox()
    {
        activeHitboxCount = Mathf.Max(0, activeHitboxCount - 1);
        if (activeHitboxCount == 0)
        {
            triggerCollider.enabled = true;
        }
    }

    private void PerformOverlapCheck()
    {
        Vector2 worldPos = triggerCollider.transform.TransformPoint(triggerCollider.offset);
        Vector2 size = triggerCollider.size;
        float angle = triggerCollider.transform.eulerAngles.z;
        LayerMask enemyLayers = projectileData != null ? projectileData.enemyLayerMask : Physics2D.DefaultRaycastLayers;
        Collider2D[] results = Physics2D.OverlapBoxAll(worldPos, size, angle, enemyLayers);

        for (int i = 0; i < results.Length; i++)
        {
            EnemyController enemy = results[i].GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                hitBoxDamage.ProcessHit(enemy);
            }
        }
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
}
