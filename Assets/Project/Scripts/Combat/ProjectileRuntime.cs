using UnityEngine;
using Project.Combat;

public class ProjectileRuntime : MonoBehaviour
{
    private interface IProjectileRuntimeState
    {
        void Enter();
        void Exit();
        void Tick(float deltaTime);
        void HandleTrigger(Collider2D other);
    }

    private sealed class SpawningState : IProjectileRuntimeState
    {
        private readonly ProjectileRuntime runtime;
        private bool waitingForAnimation;
        private float elapsed;

        public SpawningState(ProjectileRuntime runtime)
        {
            this.runtime = runtime;
        }

        public void Enter()
        {
            elapsed = 0f;
            runtime.SetWorldPosition(runtime.ProjectileData.ResolveSpawnPosition(runtime.spawnContext));
            runtime.ApplyFacingVisual();
            runtime.ConfigureSpawnHitbox();

            ProjectileFeedbackEvents.Raise(
                ProjectileFeedbackPhase.Spawn,
                runtime.ProjectileData.spawnFeedback,
                runtime.transform.position,
                runtime.GetFacingSign() < 0f);

            waitingForAnimation = runtime.ProjectileData.playSpawnAnimation && runtime.owner.TryPlaySpawnAnimation();
        }

        public void Exit()
        {
            runtime.DisableHitbox();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            bool timerComplete = runtime.ProjectileData.spawnDuration <= 0f || elapsed >= runtime.ProjectileData.spawnDuration;
            bool animationComplete = !waitingForAnimation || runtime.owner.IsSpawnAnimationComplete();

            if (timerComplete && animationComplete)
            {
                runtime.EnterState(new UpdatingState(runtime));
            }
        }

        public void HandleTrigger(Collider2D other)
        {
            runtime.TryProcessTrigger(other);
        }
    }

    private sealed class UpdatingState : IProjectileRuntimeState
    {
        private readonly ProjectileRuntime runtime;

        public UpdatingState(ProjectileRuntime runtime)
        {
            this.runtime = runtime;
        }

        public void Enter()
        {
            runtime.ConfigureRoutineHitbox();
            runtime.owner.PlayLoopAnimation();
        }

        public void Exit()
        {
        }

        public void Tick(float deltaTime)
        {
            runtime.UpdateLifetime(deltaTime);
            if (runtime.isExpired)
            {
                return;
            }

            runtime.UpdateHitCooldown();

            if (runtime.hitCooldownFramesRemaining > 0 && runtime.ProjectileData.stopOnImpact)
            {
                runtime.ApplyFacingVisual();
                return;
            }

            runtime.traversalStrategy.Tick(runtime, runtime.ProjectileData, runtime.spawnContext, deltaTime);
            runtime.ApplyFacingVisual();
        }

        public void HandleTrigger(Collider2D other)
        {
            runtime.TryProcessTrigger(other);
        }
    }

    private sealed class ExpiredState : IProjectileRuntimeState
    {
        private readonly ProjectileRuntime runtime;

        public ExpiredState(ProjectileRuntime runtime)
        {
            this.runtime = runtime;
        }

        public void Enter()
        {
            runtime.DisableHitbox();
            runtime.isExpired = true;

            ProjectileFeedbackEvents.Raise(
                ProjectileFeedbackPhase.Expire,
                runtime.ProjectileData != null ? runtime.ProjectileData.expireFeedback : null,
                runtime.transform.position,
                runtime.GetFacingSign() < 0f);

            runtime.releaseQueued = true;
        }

        public void Exit()
        {
        }

        public void Tick(float deltaTime)
        {
        }

        public void HandleTrigger(Collider2D other)
        {
        }
    }

    private Project.Combat.GenericProjectilePlayer owner;
    private ProjectileVFXManager manager;
    internal ProjectileSpawnContext spawnContext;
    private ProjectileType projectileType;
    private HitBoxController hitBoxController;
    private HitBoxDamage hitBoxDamage;
    private BoxCollider2D triggerCollider;
    private Rigidbody2D rb;
    private ProjectileHitboxModule hitboxModule;
    private IProjectileTraversalStrategy traversalStrategy;
    private IProjectileRuntimeState currentState;

    private Vector2 currentDirection = Vector2.right;
    private float lifeTimer;
    private bool currentHitConsumesMaxHits = true;
    private bool releaseQueued;
    private bool isExpired;
    internal int hitCooldownFramesRemaining;

    public ProjectileData ProjectileData { get; private set; }
    public Vector2 CurrentDirection => currentDirection.sqrMagnitude > 0.001f ? currentDirection.normalized : Vector2.right;
    public Transform AttackerTransform => spawnContext.casterTransform;
    public Vector2 AttackerPosition => spawnContext.casterTransform != null ? (Vector2)spawnContext.casterTransform.position : (Vector2)transform.position;

    public void Initialize(
        Project.Combat.GenericProjectilePlayer owner,
        ProjectileVFXManager manager,
        ProjectileType projectileType,
        ProjectileData projectileData,
        ProjectileSpawnContext context)
    {
        this.owner = owner;
        this.manager = manager;
        this.projectileType = projectileType;
        ProjectileData = projectileData;
        spawnContext = context;
        currentDirection = context.direction.sqrMagnitude > 0.001f ? context.direction.normalized : Vector2.right;
        lifeTimer = projectileData != null ? projectileData.lifetime : 0f;
        hitCooldownFramesRemaining = 0;
        currentHitConsumesMaxHits = true;
        releaseQueued = false;
        isExpired = false;

        EnsurePhysicsComponents();
        EnsureCombatComponents();
        EnsureDebugDrawer();

        rb.linearVelocity = Vector2.zero;
        traversalStrategy = ProjectileTraversalStrategyFactory.Create(projectileData != null
            ? projectileData.ResolveTraversalMode()
            : ProjectileTraversalMode.Linear);

        if (projectileData != null && projectileData.HasSpawningState())
        {
            EnterState(new SpawningState(this));
        }
        else
        {
            SetWorldPosition(projectileData != null ? projectileData.ResolveSpawnPosition(context) : context.origin);
            EnterState(new UpdatingState(this));
        }
    }

    public void ResetRuntime()
    {
        currentState?.Exit();
        currentState = null;
        isExpired = false;
        releaseQueued = false;
        hitCooldownFramesRemaining = 0;
        currentHitConsumesMaxHits = true;
        ProjectileData = null;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        DisableHitbox();
    }

    private void Update()
    {
        if (releaseQueued)
        {
            releaseQueued = false;
            owner.ReleaseToPool();
            return;
        }

        currentState?.Tick(Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        currentState?.HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        currentState?.HandleTrigger(other);
    }

    public void RegisterSuccessfulHit(EnemyController enemy)
    {
        if (ProjectileData == null)
        {
            return;
        }

        hitCooldownFramesRemaining = Mathf.Max(hitCooldownFramesRemaining, Mathf.Max(0, ProjectileData.hitIntervalFrames));

        if (currentHitConsumesMaxHits)
        {
            spawnContext.remainingHits = Mathf.Max(0, spawnContext.remainingHits - 1);
        }

        if (spawnContext.remainingHits <= 0 && ProjectileData.destroyWhenHitsExhausted)
        {
            Expire();
        }
    }

    public void Translate(Vector2 delta)
    {
        transform.position += (Vector3)delta;
    }

    public void SetWorldPosition(Vector2 worldPosition)
    {
        transform.position = worldPosition;
    }

    public void SetCurrentDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        currentDirection = direction.normalized;
    }

    public float GetFacingSign()
    {
        return CurrentDirection.x < 0f ? -1f : 1f;
    }

    public void SetCurrentHitConsumesMaxHits(bool value)
    {
        currentHitConsumesMaxHits = value;
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

        hitBoxDamage.SetProjectileOwner(this);
        hitboxModule = new ProjectileHitboxModule(this, hitBoxController, hitBoxDamage, triggerCollider);
    }

    private void EnsureDebugDrawer()
    {
        if (triggerCollider != null && GetComponent<ColliderDebugDrawer>() == null)
        {
            ColliderDebugDrawer drawer = gameObject.AddComponent<ColliderDebugDrawer>();
            drawer.debugColor = Color.cyan;
        }
    }

    private void EnterState(IProjectileRuntimeState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    private void UpdateLifetime(float deltaTime)
    {
        if (ProjectileData == null || ProjectileData.lifetime <= 0f)
        {
            return;
        }

        lifeTimer -= deltaTime;
        if (lifeTimer <= 0f && ProjectileData.destroyOnLifetimeExpire)
        {
            Expire();
        }
    }

    private void UpdateHitCooldown()
    {
        if (hitCooldownFramesRemaining > 0)
        {
            hitCooldownFramesRemaining--;
        }
    }

    private void ConfigureSpawnHitbox()
    {
        if (ProjectileData == null)
        {
            DisableHitbox();
            return;
        }

        HitboxBase spawnHitbox = ProjectileData.ResolveSpawningHitbox();
        if (spawnHitbox == null)
        {
            hitboxModule.Disable();
            return;
        }

        hitboxModule.Reset();
        hitboxModule.Activate(
            spawnHitbox,
            spawnContext,
            GetFacingSign(),
            ProjectileData.SpawningHitboxConsumesMaxHits());
    }

    private void ConfigureRoutineHitbox()
    {
        if (ProjectileData == null)
        {
            DisableHitbox();
            return;
        }

        HitboxBase routineHitbox = ProjectileData.ResolveRoutineHitbox();
        hitboxModule.Reset();

        if (routineHitbox == null)
        {
            hitboxModule.Disable();
            return;
        }

        hitboxModule.Activate(
            routineHitbox,
            spawnContext,
            GetFacingSign(),
            ProjectileData.RoutineHitboxConsumesMaxHits());
    }

    private void DisableHitbox()
    {
        hitboxModule?.Disable();
    }

    private void TryProcessTrigger(Collider2D other)
    {
        if (ProjectileData == null || isExpired)
        {
            return;
        }

        if (hitCooldownFramesRemaining > 0)
        {
            return;
        }

        if (currentHitConsumesMaxHits && spawnContext.remainingHits <= 0)
        {
            return;
        }

        int layerMask = ProjectileData.enemyLayerMask.value;
        if ((layerMask & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        hitboxModule.TryProcessHit(other);
    }

    private void Expire()
    {
        if (isExpired)
        {
            return;
        }

        EnterState(new ExpiredState(this));
    }

    private void ApplyFacingVisual()
    {
        if (ProjectileData != null && ProjectileData.inheritFacingDirection)
        {
            float angle = GetFacingSign() < 0f ? 180f : 0f;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
