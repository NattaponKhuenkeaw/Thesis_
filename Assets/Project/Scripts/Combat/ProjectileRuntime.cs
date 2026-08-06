using System.Collections.Generic;
using UnityEngine;
using Project.Combat;
using Project.ShopSystem;

public class ProjectileRuntime : MonoBehaviour
{
    private struct TargetPositionSample
    {
        public float time;
        public Vector2 position;
    }

    private const float TargetSampleRetentionPadding = 0.25f;

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
            runtime.ProcessTrackedEnemiesIfReady();

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
            runtime.ProcessTrackedEnemiesIfReady();
            runtime.ApplyAreaEffectsIfReady(deltaTime);
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
    private readonly List<TargetPositionSample> targetSamples = new List<TargetPositionSample>();
    private readonly HashSet<EnemyController> trackedEnemies = new HashSet<EnemyController>();
    private readonly List<EnemyController> trackedEnemyBuffer = new List<EnemyController>();
    private readonly Dictionary<int, int> remainingHitsByEnemy = new Dictionary<int, int>();

    private Vector2 currentDirection = Vector2.right;
    private float lifeTimer;
    private float areaEffectTimer;
    private bool currentHitConsumesMaxHits = true;
    private bool processedHitsThisFrame;
    private bool releaseQueued;
    private bool isExpired;
    private bool hitOccurredInCurrentBatch;
    private Transform trackedTargetTransform;
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
        processedHitsThisFrame = false;
        hitOccurredInCurrentBatch = false;
        areaEffectTimer = 0f;
        trackedTargetTransform = null;
        targetSamples.Clear();
        trackedEnemies.Clear();
        trackedEnemyBuffer.Clear();
        remainingHitsByEnemy.Clear();

        EnsurePhysicsComponents();
        EnsureCombatComponents();
        EnsureDebugDrawer();

        rb.linearVelocity = Vector2.zero;
        traversalStrategy = ProjectileTraversalStrategyFactory.Create(projectileData != null
            ? projectileData.traversalMode
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
        processedHitsThisFrame = false;
        hitOccurredInCurrentBatch = false;
        areaEffectTimer = 0f;
        ProjectileData = null;
        trackedTargetTransform = null;
        targetSamples.Clear();
        trackedEnemies.Clear();
        trackedEnemyBuffer.Clear();
        remainingHitsByEnemy.Clear();

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

        processedHitsThisFrame = false;
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

    private void OnTriggerExit2D(Collider2D other)
    {
        EnemyController enemy = other != null ? other.GetComponentInParent<EnemyController>() : null;
        if (enemy != null)
        {
            trackedEnemies.Remove(enemy);
        }
    }

    public void RegisterSuccessfulHit(EnemyController enemy)
    {
        if (ProjectileData == null || enemy == null)
        {
            return;
        }

        hitOccurredInCurrentBatch = true;

        if (currentHitConsumesMaxHits)
        {
            if (ProjectileData.UsesSharedHitCount())
            {
                spawnContext.remainingHits = Mathf.Max(0, spawnContext.remainingHits - 1);
            }
            else
            {
                int enemyId = enemy.GetInstanceID();
                int currentRemainingHits = GetRemainingHitsForEnemy(enemy);
                remainingHitsByEnemy[enemyId] = Mathf.Max(0, currentRemainingHits - 1);
            }
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

    public Vector2 ResolveTrackingAimPosition()
    {
        if (ProjectileData == null)
        {
            return transform.position;
        }

        Transform trackingTarget = ResolveTrackingTarget();
        if (trackingTarget == null)
        {
            ClearTrackingSamples();
            return ProjectileData.ResolveTrackingFallbackPosition(spawnContext);
        }

        Vector2 targetPosition = ProjectileData.ResolveTrackingReferencePosition(trackingTarget);
        RecordTrackingSample(trackingTarget, targetPosition);
        spawnContext.targetTransform = trackingTarget;

        float delay = Mathf.Max(0f, ProjectileData.trackDelaySeconds);
        if (delay <= 0f || targetSamples.Count == 0)
        {
            return targetPosition;
        }

        float desiredSampleTime = Time.time - delay;
        Vector2 delayedPosition = targetSamples[0].position;
        for (int i = 0; i < targetSamples.Count; i++)
        {
            TargetPositionSample sample = targetSamples[i];
            if (sample.time > desiredSampleTime)
            {
                break;
            }

            delayedPosition = sample.position;
        }

        return delayedPosition;
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
        if (ProjectileData == null || isExpired || other == null)
        {
            return;
        }

        int layerMask = ProjectileData.enemyLayerMask.value;
        if ((layerMask & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null || !enemy.IsAlive)
        {
            return;
        }

        trackedEnemies.Add(enemy);
        ProcessTrackedEnemiesIfReady();
    }

    /// <summary>
    /// Immediately expires this projectile (used by boss-death hazard clear).
    /// </summary>
    public void ForceExpire()
    {
        Expire();
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
        hitboxModule?.RefreshColliderGeometry(GetFacingSign());

        if (ProjectileData != null && ProjectileData.inheritFacingDirection)
        {
            float angle = GetFacingSign() < 0f ? 180f : 0f;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    private Transform ResolveTrackingTarget()
    {
        if (ProjectileData == null)
        {
            return null;
        }

        Vector2 searchOrigin = spawnContext.casterTransform != null
            ? (Vector2)spawnContext.casterTransform.position
            : spawnContext.origin;
        float facingSign = spawnContext.direction.x < 0f ? -1f : 1f;

        if (CameraManager.instance != null)
        {
            EnemyController candidate = CameraManager.instance.GetProjectileTrackingTarget(
                searchOrigin,
                facingSign,
                ProjectileData.trackingRadius);
            return candidate != null ? candidate.transform : null;
        }

        return IsTrackingTargetValid(spawnContext.targetTransform, searchOrigin, ProjectileData.trackingRadius)
            ? spawnContext.targetTransform
            : null;
    }

    private void ProcessTrackedEnemiesIfReady()
    {
        if (processedHitsThisFrame || ProjectileData == null || isExpired || hitboxModule == null)
        {
            return;
        }

        if (hitCooldownFramesRemaining > 0)
        {
            return;
        }

        if (currentHitConsumesMaxHits && ProjectileData.UsesSharedHitCount() && spawnContext.remainingHits <= 0)
        {
            return;
        }

        if (currentHitConsumesMaxHits
            && !ProjectileData.UsesSharedHitCount()
            && trackedEnemies.Count > 0
            && !HasHittableTrackedEnemy())
        {
            if (ProjectileData.destroyWhenHitsExhausted)
                Expire();
            return;
        }

        trackedEnemyBuffer.Clear();
        foreach (EnemyController enemy in trackedEnemies)
        {
            if (enemy == null || !enemy.IsAlive || !IsEnemyTouchingTrigger(enemy))
            {
                continue;
            }

            trackedEnemyBuffer.Add(enemy);
        }

        if (trackedEnemyBuffer.Count == 0)
        {
            trackedEnemies.Clear();
            return;
        }

        processedHitsThisFrame = true;
        hitOccurredInCurrentBatch = false;
        hitboxModule.BeginHitCycle();

        for (int i = 0; i < trackedEnemyBuffer.Count; i++)
        {
            EnemyController enemy = trackedEnemyBuffer[i];
            if (!CanHitEnemy(enemy))
            {
                continue;
            }

            hitboxModule.TryProcessHit(enemy);

            if (ProjectileData.UsesSharedHitCount() && currentHitConsumesMaxHits && spawnContext.remainingHits <= 0)
            {
                break;
            }
        }

        trackedEnemyBuffer.Clear();

        if (!hitOccurredInCurrentBatch)
        {
            return;
        }

        hitCooldownFramesRemaining = Mathf.Max(hitCooldownFramesRemaining, Mathf.Max(0, ProjectileData.hitIntervalFrames));

        if (ProjectileData.UsesSharedHitCount()
            && ProjectileData.destroyWhenHitsExhausted
            && currentHitConsumesMaxHits
            && spawnContext.remainingHits <= 0)
        {
            Expire();
            return;
        }

        if (!ProjectileData.UsesSharedHitCount()
            && ProjectileData.destroyWhenHitsExhausted
            && currentHitConsumesMaxHits
            && !HasHittableTrackedEnemy())
        {
            Expire();
        }
    }

    private void ApplyAreaEffectsIfReady(float deltaTime)
    {
        if (ProjectileData == null
            || !ProjectileData.trackAreaOccupants
            || ProjectileData.areaEffects == null
            || ProjectileData.areaEffects.Count == 0
            || triggerCollider == null
            || !triggerCollider.enabled)
        {
            return;
        }

        areaEffectTimer -= Mathf.Max(0f, deltaTime);
        if (areaEffectTimer > 0f)
        {
            return;
        }

        areaEffectTimer = Mathf.Max(0.05f, ProjectileData.areaEffectRefreshInterval);
        ApplyAreaEffectsToActivePlayer();
        ApplyAreaEffectsToTrackedEnemies();
    }

    private void ApplyAreaEffectsToActivePlayer()
    {
        PlayerCharacter activePlayer = PlayerCharacter.Instance;
        if (activePlayer == null || !IsPlayerTouchingTrigger(activePlayer))
        {
            return;
        }

        CharacterObject sourceCharacter = spawnContext.sourceCharacter;
        CharacterStats sourceStats = spawnContext.attackerStats;
        TeamLoader teamLoader = TeamLoader.Instance;
        PlayerRunInventory inventory = teamLoader != null ? teamLoader.RunInventory : null;
        CharacterObject[] team = teamLoader != null
            ? teamLoader.GetTeamSnapshot()
            : new[] { activePlayer.characterObject };

        for (int i = 0; i < ProjectileData.areaEffects.Count; i++)
        {
            RuntimeAreaEffectApplication application = ProjectileData.areaEffects[i];
            RuntimeEffectApplyTarget applyTarget = GetAreaApplyTarget(application);
            if (application == null
                || application.ResolvedEffect == null
                || applyTarget == RuntimeEffectApplyTarget.Enemy
                || !IsAreaEffectUnlocked(application, activePlayer))
            {
                continue;
            }

            for (int teamIndex = 0; teamIndex < team.Length; teamIndex++)
            {
                CharacterObject target = team[teamIndex];
                if (target == null)
                {
                    continue;
                }

                bool shouldApply = applyTarget switch
                {
                    RuntimeEffectApplyTarget.Self => target == activePlayer.characterObject,
                    RuntimeEffectApplyTarget.Allies => true,
                    _ => false
                };

                if (!shouldApply)
                {
                    continue;
                }

                if (inventory != null)
                {
                    RuntimeEffectRecipe recipe = application.ResolvedRecipe;
                    if (recipe != null)
                    {
                        inventory.ApplyRuntimeEffect(target, recipe, sourceCharacter, sourceStats);
                    }
                    else
                    {
                        inventory.ApplyRuntimeEffect(
                            target,
                            application.ResolvedEffect,
                            sourceCharacter,
                            sourceStats,
                            application.durationOverride,
                            application.stackMode,
                            application.stackLimit,
                            application.refreshDurationOnReapply);
                    }
                    continue;
                }

                if (target == activePlayer.characterObject)
                {
                    RuntimeEffectRecipe recipe = application.ResolvedRecipe;
                    if (recipe != null)
                    {
                        activePlayer.RuntimeEffects?.ApplyEffect(recipe, sourceCharacter, sourceStats);
                    }
                    else
                    {
                        activePlayer.RuntimeEffects?.ApplyEffect(
                            application.ResolvedEffect,
                            sourceCharacter,
                            sourceStats,
                            application.durationOverride,
                            application.stackMode,
                            application.stackLimit,
                            application.refreshDurationOnReapply);
                    }
                }
            }
        }
    }

    private void ApplyAreaEffectsToTrackedEnemies()
    {
        PlayerCharacter activePlayer = PlayerCharacter.Instance;
        trackedEnemyBuffer.Clear();
        foreach (EnemyController enemy in trackedEnemies)
        {
            if (enemy != null && enemy.IsAlive && IsEnemyTouchingTrigger(enemy))
            {
                trackedEnemyBuffer.Add(enemy);
            }
        }

        for (int i = 0; i < trackedEnemyBuffer.Count; i++)
        {
            EnemyController enemy = trackedEnemyBuffer[i];
            for (int effectIndex = 0; effectIndex < ProjectileData.areaEffects.Count; effectIndex++)
            {
                RuntimeAreaEffectApplication application = ProjectileData.areaEffects[effectIndex];
                RuntimeEffectApplyTarget applyTarget = GetAreaApplyTarget(application);
                if (application == null
                    || application.ResolvedEffect == null
                    || applyTarget != RuntimeEffectApplyTarget.Enemy
                    || !IsAreaEffectUnlocked(application, activePlayer))
                {
                    continue;
                }

                RuntimeEffectRecipe recipe = application.ResolvedRecipe;
                if (recipe != null)
                {
                    enemy.RuntimeEffects?.ApplyEffect(recipe, spawnContext.sourceCharacter, spawnContext.attackerStats);
                }
                else
                {
                    enemy.RuntimeEffects?.ApplyEffect(
                        application.ResolvedEffect,
                        spawnContext.sourceCharacter,
                        spawnContext.attackerStats,
                        application.durationOverride,
                        application.stackMode,
                        application.stackLimit,
                        application.refreshDurationOnReapply);
                }
            }
        }

        trackedEnemyBuffer.Clear();
    }

    private static RuntimeEffectApplyTarget GetAreaApplyTarget(RuntimeAreaEffectApplication application)
    {
        if (application != null && application.ResolvedRecipe != null && application.ResolvedRecipe.policy != null)
        {
            return application.ResolvedRecipe.policy.appliesOn;
        }

        return application != null
            ? application.scope switch
            {
                PassiveEffectScope.Enemy => RuntimeEffectApplyTarget.Enemy,
                PassiveEffectScope.Allies => RuntimeEffectApplyTarget.Allies,
                _ => RuntimeEffectApplyTarget.Self
            }
            : RuntimeEffectApplyTarget.Self;
    }

    private bool IsAreaEffectUnlocked(RuntimeAreaEffectApplication application, PlayerCharacter activePlayer)
    {
        if (application == null || string.IsNullOrWhiteSpace(application.requiredNodeId))
        {
            return true;
        }

        CharacterPassiveController passiveController = activePlayer != null
            ? activePlayer.PassiveController
            : (PlayerCharacter.Instance != null ? PlayerCharacter.Instance.PassiveController : null);

        return passiveController != null && passiveController.IsNodeUnlocked(application.requiredNodeId);
    }

    private bool HasHittableTrackedEnemy()
    {
        foreach (EnemyController enemy in trackedEnemies)
        {
            if (enemy != null && enemy.IsAlive && GetRemainingHitsForEnemy(enemy) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanHitEnemy(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsAlive)
        {
            return false;
        }

        if (!currentHitConsumesMaxHits || ProjectileData == null)
        {
            return true;
        }

        if (ProjectileData.UsesSharedHitCount())
        {
            return spawnContext.remainingHits > 0;
        }

        return GetRemainingHitsForEnemy(enemy) > 0;
    }

    private int GetRemainingHitsForEnemy(EnemyController enemy)
    {
        if (enemy == null)
        {
            return 0;
        }

        int enemyId = enemy.GetInstanceID();
        if (remainingHitsByEnemy.TryGetValue(enemyId, out int remainingHits))
        {
            return remainingHits;
        }

        remainingHits = ProjectileData != null ? Mathf.Max(1, ProjectileData.maxHits) : 1;
        remainingHitsByEnemy[enemyId] = remainingHits;
        return remainingHits;
    }

    private bool IsEnemyTouchingTrigger(EnemyController enemy)
    {
        if (enemy == null || triggerCollider == null || !triggerCollider.enabled)
        {
            return false;
        }

        if (enemy.bodyCollider != null)
        {
            return enemy.bodyCollider.IsTouching(triggerCollider)
                || enemy.bodyCollider.bounds.Intersects(triggerCollider.bounds);
        }

        Collider2D[] colliders = enemy.GetCachedColliders();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null
                && (collider.IsTouching(triggerCollider) || collider.bounds.Intersects(triggerCollider.bounds)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPlayerTouchingTrigger(PlayerCharacter player)
    {
        if (player == null || triggerCollider == null || !triggerCollider.enabled)
        {
            return false;
        }

        Collider2D[] colliders = player.GetCachedColliders();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (collider.IsTouching(triggerCollider) || collider.bounds.Intersects(triggerCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordTrackingSample(Transform target, Vector2 position)
    {
        if (trackedTargetTransform != target)
        {
            trackedTargetTransform = target;
            targetSamples.Clear();
        }

        targetSamples.Add(new TargetPositionSample
        {
            time = Time.time,
            position = position
        });

        float minSampleTime = Time.time - Mathf.Max(0f, ProjectileData != null ? ProjectileData.trackDelaySeconds : 0f) - TargetSampleRetentionPadding;
        while (targetSamples.Count > 1 && targetSamples[0].time < minSampleTime)
        {
            targetSamples.RemoveAt(0);
        }
    }

    private void ClearTrackingSamples()
    {
        trackedTargetTransform = null;
        targetSamples.Clear();
        spawnContext.targetTransform = null;
    }

    private static bool IsTrackingTargetValid(Transform targetTransform, Vector2 searchOrigin, float maxRadius)
    {
        if (targetTransform == null)
        {
            return false;
        }

        EnemyController enemy = targetTransform.GetComponentInParent<EnemyController>();
        if (enemy == null || !enemy.IsAlive)
        {
            return false;
        }

        if (maxRadius > 0f)
        {
            float maxRadiusSqr = maxRadius * maxRadius;
            if (((Vector2)enemy.transform.position - searchOrigin).sqrMagnitude > maxRadiusSqr)
            {
                return false;
            }
        }

        return true;
    }
}
