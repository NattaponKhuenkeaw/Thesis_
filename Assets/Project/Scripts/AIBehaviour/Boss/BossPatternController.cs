using System.Collections;
using UnityEngine;
using Unity.Behavior;
using Project.UI;

namespace Project.AIBehaviour.Boss
{
    /// <summary>
    /// Pattern/phase driver for the Executioner boss MVP.
    /// Owns detect/chase/attack while disabling BehaviorGraphAgent on this prefab.
    /// Reuses EnemyController hitbox, warning, and death pipelines.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public class BossPatternController : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [Header("References")]
        [SerializeField] private EnemyController enemy;
        [SerializeField] private AnimationHandler_Enemy animHandler;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private BossGroundTelegraph groundTelegraph;
        [SerializeField] private string playerTag = PlayerTag;

        [Header("HUD")]
        [SerializeField] private bool revealOnSpawn = true;
        [SerializeField, Min(0f)] private float spawnPanDuration = 0.5f;
        [SerializeField, Min(0f)] private float spawnRevealDuration = 1.2f;
        [SerializeField, Min(0f)] private float spawnReturnDuration = 0.6f;
        [SerializeField] private string bossDisplayName = "Executioner";
        [SerializeField] private bool bindScreenHealthHud = true;

        [Header("Default Stats (applied once on Awake if useDefaultStats)")]
        [SerializeField] private bool useDefaultStats = true;
        [SerializeField] private float defaultMaxHp = 800f;
        [SerializeField] private float defaultAttackDamage = 165f;
        [SerializeField] private float defaultDefense = 500f;

        [Header("Movement")]
        [SerializeField] private float chaseSpeed = 2.2f;
        [SerializeField] private float cleaveRange = 2.4f;
        [SerializeField] private float decideInterval = 0.2f;
        [Tooltip("Once chase is chosen, keep walking until in cleave range or this timeout.")]
        [SerializeField] private float maxChaseDuration = 3.5f;
        [SerializeField] [Range(0f, 1f)] private float phase1ChaseChance = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float phase2ChaseChance = 0.35f;

        [Header("Phases")]
        [SerializeField] [Range(0.05f, 0.95f)] private float phase2HpRatio = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float phase2ChainCleaveChance = 0.65f;

        [Header("Patterns")]
        [SerializeField] private BossPatternTiming cleave = new BossPatternTiming
        {
            telegraphDuration = 1.2f,
            activeDuration = 0.4f,
            recoveryDuration = 1.75f,
            hitboxLocalOffset = new Vector2(1.4f, 0.2f),
            hitboxSizeOverride = new Vector2(2.6f, 1.4f)
        };

        [SerializeField] private BossPatternTiming jumpSmash = new BossPatternTiming
        {
            telegraphDuration = 1.35f,
            activeDuration = 0.45f,
            recoveryDuration = 1.9f,
            hitboxLocalOffset = Vector2.zero,
            hitboxSizeOverride = new Vector2(3.2f, 1.2f)
        };

        [SerializeField] private BossPatternTiming bullCharge = new BossPatternTiming
        {
            telegraphDuration = 1.1f,
            activeDuration = 0.55f,
            recoveryDuration = 1.5f,
            hitboxLocalOffset = new Vector2(0.8f, 0f),
            hitboxSizeOverride = new Vector2(2.2f, 1.6f)
        };

        [Header("Jump Smash")]
        [SerializeField] private float jumpArcHeight = 3.5f;
        [SerializeField] private float jumpTravelDuration = 0.75f;
        [Tooltip("Fallback world radius when hitboxSizeOverride is unset. Prefer deriving from jump smash hitbox.")]
        [SerializeField] private float smashMarkerRadius = 1.6f;

        [Header("Bull Charge")]
        [SerializeField] private float chargeSpeed = 16f;
        [Tooltip("Safety cap if no wall is found ahead.")]
        [SerializeField] private float chargeDistance = 40f;
        [Tooltip("Stop this far before the blocking wall collider.")]
        [SerializeField] private float chargeWallPadding = 0.35f;

        [Header("Telegraph Feedback")]
        [SerializeField] private bool flashDuringTelegraph = true;
        [SerializeField] private Color telegraphFlashColor = new Color(1f, 0.45f, 0.35f, 1f);

        [Header("Edit Preview (Scene View)")]
        [Tooltip("Which pattern hitbox to preview while selected in the editor.")]
        [SerializeField] private BossHitboxPreviewMode hitboxPreviewMode = BossHitboxPreviewMode.CleaveOnly;
        [SerializeField] private bool previewRanges = true;

        private Transform target;
        private Coroutine brainRoutine;
        private Coroutine spawnRevealRoutine;
        private bool spawnRevealStarted;
        private bool spawnRevealPending;
        private CameraManager spawnRevealCamera;
        private Coroutine flashRoutine;
        private int phase = 1;
        private bool isBusy;
        private bool statsApplied;
        private bool pendingChainCleave;

        private Collider2D activePatternHitbox;
        private Vector2 cachedHitboxOffset;
        private Vector2 cachedHitboxSize;
        private bool hasCachedHitboxSize;
        private float cachedGravityScale;
        private RigidbodyType2D cachedBodyType;
        private bool physicsCached;

        private Color[] flashOriginalColors;
        private SpriteRenderer[] flashRenderers;
        private bool flashRenderersCached;
        private readonly WaitForSeconds flashWait = new WaitForSeconds(0.12f);
        private readonly WaitForFixedUpdate waitFixed = new WaitForFixedUpdate();

        public int Phase => phase;
        public bool IsBusy => isBusy;
        public BossHitboxPreviewMode HitboxPreviewMode => hitboxPreviewMode;
        public bool PreviewRanges => previewRanges;
        public BossPatternTiming CleaveTiming => cleave;
        public BossPatternTiming JumpSmashTiming => jumpSmash;
        public BossPatternTiming BullChargeTiming => bullCharge;
        public float CleaveRange => cleaveRange;
        public float ChargeDistance => chargeDistance;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            ApplyDefaultStatsIfNeeded();

            // Boss owns AI; trash enemies keep Behavior Graph.
            enemy.SetBehaviorAgentActive(false);
            var agent = GetComponent<BehaviorGraphAgent>();
            if (agent != null)
                agent.enabled = false;

            enemy.isEliteOrBoss = true;
            // GDD: bosses resist stagger-lock / juggle. Recovery is a damage window only.
            enemy.ignoreHitReactions = true;
            enemy.SetAttackHitboxActive(false);
            enemy.ConfigureDeathDespawn(3f);
            groundTelegraph?.Hide();

            // Screen HUD owns boss HP; keep world warning icon but hide world bars.
            if (bindScreenHealthHud)
            {
                EnemyWorldUI worldUi = GetComponentInChildren<EnemyWorldUI>(true);
                worldUi?.SetShowHealthBar(false);
            }
        }

        private void OnEnable()
        {
            if (enemy != null)
            {
                enemy.OnDamaged += HandleDamaged;
                enemy.OnDied += HandleDied;
            }

            if (bindScreenHealthHud && enemy != null)
                BossHealthHUD.EnsureInstance().Bind(enemy, bossDisplayName);

            // Spawn lifecycle blocks camera membership and the boss loop until Active.
            if (revealOnSpawn && !spawnRevealStarted && enemy != null && enemy.IsAlive)
            {
                spawnRevealStarted = true;
                spawnRevealPending = true;
                spawnRevealRoutine = StartCoroutine(RevealSpawn());
            }
            StartBrainIfActive();
        }

        private void Update()
        {
            if (enemy != null && EnemyController.IsRoomTraversalBlockingCombat && brainRoutine != null)
            {
                StopCoroutine(brainRoutine);
                brainRoutine = null;
                isBusy = false;
                CleanupAttackState();
                RestorePhysics();
                ZeroVelocity();
            }
            // OnEnable runs before EnemyController.Start, so retry once the spawn state enters Active.
            StartBrainIfActive();
        }

        private void StartBrainIfActive()
        {
            if (enemy == null
                || !isActiveAndEnabled
                || !enemy.IsCombatReady
                || spawnRevealPending
                || brainRoutine != null)
            {
                return;
            }

            enemy.SetInCombat(true);
            brainRoutine = StartCoroutine(BrainLoop());
        }

        private void OnDisable()
        {
            CancelSpawnReveal();
            if (bindScreenHealthHud && enemy != null && BossHealthHUD.Instance != null)
                BossHealthHUD.Instance.Unbind(enemy);

            if (enemy != null)
            {
                enemy.OnDamaged -= HandleDamaged;
                enemy.OnDied -= HandleDied;
                enemy.ignoreHitReactions = false;
            }

            if (brainRoutine != null)
            {
                StopCoroutine(brainRoutine);
                brainRoutine = null;
            }

            CleanupAttackState();
            RestorePhysics();
            groundTelegraph?.Hide();
        }

        private void CacheReferences()
        {
            if (enemy == null)
                enemy = GetComponent<EnemyController>();
            if (animHandler == null)
                animHandler = GetComponentInChildren<AnimationHandler_Enemy>(true);
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();
            if (groundTelegraph == null)
                groundTelegraph = GetComponentInChildren<BossGroundTelegraph>(true);

            // Scripted boss motion is applied via MovePosition; interpolate removes camera/render jitter.
            if (rb != null)
            {
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                if (rb.collisionDetectionMode == CollisionDetectionMode2D.Discrete)
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                // EnemyCore freezes X so Behavior Graph can drive transform.position.
                // Boss patterns use MovePosition, which respects FreezePositionX and would
                // only arc on Y (looks like hopping in place). Allow X+Y; keep rotation locked.
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        private void ApplyDefaultStatsIfNeeded()
        {
            if (!useDefaultStats || statsApplied || enemy == null)
                return;

            enemy.maxHP = defaultMaxHp;
            enemy.currentHP = defaultMaxHp;
            enemy.attackDamage = defaultAttackDamage;
            enemy.defense = defaultDefense;
            enemy.isEliteOrBoss = true;
            statsApplied = true;
        }

        private void HandleDamaged(HitReactionData _)
        {
            EvaluatePhase();
            if (enemy != null && enemy.IsGrabbed)
                return;
            // Kill leftover launch/juggle if anything slipped through before armor applied.
            SuppressStaggerCarryover();
        }

        private void HandleDied()
        {
            CancelSpawnReveal();
            isBusy = false;
            CleanupAttackState();
            RestorePhysics();
            groundTelegraph?.Hide();

            if (brainRoutine != null)
            {
                StopCoroutine(brainRoutine);
                brainRoutine = null;
            }

            BossDeathSequence.EnsureInstance().BeginFromBoss(enemy);
        }

        /// <summary>
        /// Called by BossDeathSequence to strip leftover attack windows immediately.
        /// </summary>
        public void ForceClearCombatHazards()
        {
            CancelSpawnReveal();
            isBusy = false;
            CleanupAttackState();
            RestorePhysics();
            groundTelegraph?.Hide();

            if (brainRoutine != null)
            {
                StopCoroutine(brainRoutine);
                brainRoutine = null;
            }
        }

        private IEnumerator RevealSpawn()
        {
            // Spawner applies the final position after OnEnable.
            yield return null;
            spawnRevealCamera = CameraManager.instance;
            if (spawnRevealCamera != null && spawnRevealCamera.TryBeginBossSpawnFocus(transform, spawnPanDuration))
            {
                float elapsed = 0f;
                while (enemy != null && enemy.IsAlive
                    && (elapsed < spawnPanDuration + spawnRevealDuration || !enemy.IsGameplayActive))
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (spawnRevealCamera != null)
                    spawnRevealCamera.EndBossSpawnFocus(transform, spawnReturnDuration);
                yield return new WaitForSecondsRealtime(spawnReturnDuration);
            }
            spawnRevealCamera = null;
            spawnRevealPending = false;
            spawnRevealRoutine = null;
        }

        private void CancelSpawnReveal()
        {
            if (spawnRevealRoutine != null)
                StopCoroutine(spawnRevealRoutine);
            spawnRevealRoutine = null;
            if (spawnRevealCamera != null)
                spawnRevealCamera.EndBossSpawnFocus(transform, 0f);
            spawnRevealCamera = null;
            spawnRevealPending = false;
        }

        private void EvaluatePhase()
        {
            if (enemy == null || enemy.maxHP <= 0f)
                return;

            float ratio = enemy.currentHP / enemy.maxHP;
            int nextPhase = ratio <= phase2HpRatio ? 2 : 1;
            if (nextPhase == phase)
                return;

            phase = nextPhase;
            animHandler?.SetBossPhase(phase);
        }

        private IEnumerator BrainLoop()
        {
            var wait = new WaitForSeconds(decideInterval);

            while (enemy != null && enemy.IsAlive)
            {
                EvaluatePhase();

                if (enemy.IsGrabbed)
                {
                    yield return wait;
                    continue;
                }

                if (isBusy)
                {
                    yield return wait;
                    continue;
                }

                if (!TryAcquireTarget())
                {
                    animHandler?.SetSpeed(0f);
                    yield return wait;
                    continue;
                }

                // Bosses ignore juggle; clear any leftover reaction instead of soft-locking AI.
                if (enemy.CurrentReactionState != EnemyReactionState.None)
                    SuppressStaggerCarryover();

                float distance = Vector2.Distance(transform.position, target.position);
                BossPatternId pattern = ChoosePattern(distance);

                // Commit to walking — do not re-roll JumpSmash every step (that made chase nearly impossible).
                if (pattern == BossPatternId.HeavyCleave && distance > cleaveRange)
                {
                    yield return ChaseThenCleave();
                    continue;
                }

                yield return ExecutePattern(pattern);
            }
        }

        private IEnumerator ChaseThenCleave()
        {
            float elapsed = 0f;
            while (enemy != null && enemy.IsAlive && TryAcquireTarget() && elapsed < maxChaseDuration)
            {
                if (enemy.IsGrabbed)
                    yield break;

                if (enemy.CurrentReactionState != EnemyReactionState.None)
                    SuppressStaggerCarryover();

                float distance = Vector2.Distance(transform.position, target.position);
                if (distance <= cleaveRange)
                {
                    animHandler?.SetSpeed(0f);
                    yield return ExecutePattern(BossPatternId.HeavyCleave);
                    yield break;
                }

                yield return ChaseTowardTargetStep();
                elapsed += Time.fixedDeltaTime;
            }

            animHandler?.SetSpeed(0f);

            // Player kited past timeout — use a gap closer instead of stalling.
            if (enemy == null || !enemy.IsAlive || !TryAcquireTarget())
                yield break;

            BossPatternId closer = phase >= 2 && Random.value < 0.55f
                ? BossPatternId.BullCharge
                : BossPatternId.JumpSmash;
            yield return ExecutePattern(closer);
        }

        private bool TryAcquireTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy)
                return true;

            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                target = null;
                return false;
            }

            target = player.transform;
            return true;
        }

        private BossPatternId ChoosePattern(float distance)
        {
            bool inCleaveRange = distance <= cleaveRange;

            // HeavyCleave while out of range means "commit chase, then cleave".
            if (phase < 2)
            {
                if (inCleaveRange)
                    return BossPatternId.HeavyCleave;

                return Random.value < phase1ChaseChance
                    ? BossPatternId.HeavyCleave
                    : BossPatternId.JumpSmash;
            }

            if (!inCleaveRange)
            {
                float roll = Random.value;
                float chargeWeight = 0.4f;
                float chaseWeight = phase2ChaseChance;
                if (roll < chargeWeight)
                    return BossPatternId.BullCharge;
                if (roll < chargeWeight + chaseWeight)
                    return BossPatternId.HeavyCleave;
                return BossPatternId.JumpSmash;
            }

            return Random.value < 0.7f ? BossPatternId.HeavyCleave : BossPatternId.JumpSmash;
        }

        private IEnumerator ChaseTowardTargetStep()
        {
            if (target == null || (enemy != null && enemy.IsGrabbed))
                yield break;

            FaceTarget();
            Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 goal = new Vector2(target.position.x, current.y);
            Vector2 next = Vector2.MoveTowards(current, goal, chaseSpeed * Time.fixedDeltaTime);
            MoveBossPosition(next);
            animHandler?.SetSpeed(chaseSpeed);
            yield return waitFixed;
        }

        private IEnumerator ExecutePattern(BossPatternId patternId)
        {
            isBusy = true;
            animHandler?.SetSpeed(0f);
            enemy.SetInCombat(true);

            switch (patternId)
            {
                case BossPatternId.HeavyCleave:
                    yield return RunCleave(usePhaseRecovery: true);
                    break;
                case BossPatternId.JumpSmash:
                    pendingChainCleave = false;
                    yield return RunJumpSmash();
                    if (pendingChainCleave)
                        yield return RunCleave(usePhaseRecovery: true);
                    break;
                case BossPatternId.BullCharge:
                    yield return RunBullCharge();
                    break;
            }

            isBusy = false;
        }

        private IEnumerator RunCleave(bool usePhaseRecovery)
        {
            BossPatternTiming timing = cleave;
            FaceTarget();

            yield return Telegraph(timing, BossPatternId.HeavyCleave, null, 0f);

            if (!CanContinuePattern())
            {
                CleanupAttackState();
                yield break;
            }

            yield return ActiveMeleeWindow(timing);

            float recovery = usePhaseRecovery ? ResolveRecovery(timing) : 0f;
            if (recovery > 0f)
                yield return Recovery(recovery);
            else
                CleanupAttackState();
        }

        private IEnumerator RunJumpSmash()
        {
            pendingChainCleave = false;
            BossPatternTiming timing = jumpSmash;

            if (!TryAcquireTarget())
                yield break;

            Vector3 lockPoint = target.position;
            FaceTarget();

            yield return Telegraph(timing, BossPatternId.JumpSmash, lockPoint, ResolveSmashMarkerRadius());

            if (!CanContinuePattern())
            {
                CleanupAttackState();
                groundTelegraph?.Hide();
                yield break;
            }

            // Flight — no attack hitbox.
            enemy.HideAttackWarning();
            StopTelegraphFlash();
            animHandler?.SetBossJumpPhase(0f);
            animHandler?.TriggerBossPattern(BossPatternId.JumpSmash);
            yield return JumpArc(lockPoint);

            // MovePosition is applied by physics; don't show impact on its request frame.
            yield return waitFixed;
            RestorePhysics();
            float landingDeadline = Time.time + 3f;
            while (CanContinuePattern() && !enemy.HasSpawnGroundSupport() && Time.time < landingDeadline)
                yield return waitFixed;

            if (!CanContinuePattern() || !enemy.HasSpawnGroundSupport())
            {
                CleanupAttackState();
                RestorePhysics();
                groundTelegraph?.Hide();
                yield break;
            }

            // Impact AoE.
            animHandler?.SetBossJumpPhase(1f);
            groundTelegraph?.Hide();
            yield return ActiveMeleeWindow(timing);
            RestorePhysics();

            bool wantsChain = phase >= 2 && Random.value <= phase2ChainCleaveChance;
            if (wantsChain)
            {
                // Skip recovery; caller may chain Cleave immediately.
                CleanupAttackState();
                pendingChainCleave = true;
                yield break;
            }

            yield return Recovery(ResolveRecovery(timing));
        }

        private IEnumerator RunBullCharge()
        {
            BossPatternTiming timing = bullCharge;
            animHandler?.SetBossChargePhase(0f);
            // Commit facing at telegraph start; do not retarget during wind-up (see Telegraph).
            FaceTarget();
            float facing = GetFacingSign();

            yield return Telegraph(timing, BossPatternId.BullCharge, null, 0f);

            if (!CanContinuePattern())
            {
                CleanupAttackState();
                yield break;
            }

            enemy.HideAttackWarning();
            StopTelegraphFlash();

            BeginArmored();
            ConfigureHitbox(timing);
            enemy.SetAttackHitboxActive(true);

            float maxTravel = ResolveChargeTravelDistance(facing);
            float traveled = 0f;
            bool rushPoseStarted = false;
            while (traveled < maxTravel)
            {
                if (!CanContinuePattern())
                    break;

                float remaining = maxTravel - traveled;
                float step = Mathf.Min(chargeSpeed * Time.fixedDeltaTime, remaining);
                if (step <= 0.0001f)
                    break;

                Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
                MoveBossPosition(new Vector2(current.x + facing * step, current.y));
                traveled += step;
                yield return waitFixed;

                // MovePosition is deferred; don't show the rush pose before physics moves the boss.
                Vector2 actual = rb != null && rb.simulated ? rb.position : (Vector2)transform.position;
                if (!rushPoseStarted && CanContinuePattern() && Mathf.Abs(actual.x - current.x) > 0.0001f)
                {
                    animHandler?.SetBossChargePhase(1f);
                    rushPoseStarted = true;
                }
            }

            enemy.SetAttackHitboxActive(false);
            RestoreHitboxTransform();
            EndArmored();

            yield return Recovery(ResolveRecovery(timing));
        }

        /// <summary>
        /// Charge until the nearest solid wall ahead (or <see cref="chargeDistance"/> safety cap).
        /// </summary>
        private float ResolveChargeTravelDistance(float facing)
        {
            float maxRange = Mathf.Max(0.5f, chargeDistance);
            Collider2D body = enemy != null ? enemy.bodyCollider : null;
            if (body == null)
                return maxRange;

            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };

            RaycastHit2D[] hits = new RaycastHit2D[12];
            int hitCount = body.Cast(new Vector2(facing, 0f), filter, hits, maxRange);
            float nearest = maxRange;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = hits[i].collider;
                if (hit == null)
                    continue;

                // Ignore self / attached colliders.
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                // Don't stop on the player — charge through and keep going to the wall.
                if (hit.GetComponentInParent<PlayerCharacter>() != null)
                    continue;

                if (hits[i].distance < nearest)
                    nearest = hits[i].distance;
            }

            return Mathf.Max(0.1f, nearest - Mathf.Max(0f, chargeWallPadding));
        }

        private IEnumerator Telegraph(BossPatternTiming timing, BossPatternId patternId, Vector3? markerWorldPos, float markerRadius)
        {
            // Each pattern faces its target once before entering wind-up.
            enemy.LockFacing();
            BeginArmored();
            enemy.SetAttackHitboxActive(false);
            enemy.TriggerAttackWarning(timing.telegraphDuration);

            if (markerWorldPos.HasValue)
                groundTelegraph?.Show(markerWorldPos.Value, markerRadius);

            if (flashDuringTelegraph)
                StartTelegraphFlash();

            // Wind-up anim cue at telegraph start for grounded swings / charge crouch.
            if (patternId != BossPatternId.JumpSmash)
                animHandler?.TriggerBossPattern(patternId);

            float t = 0f;
            while (t < timing.telegraphDuration)
            {
                if (!enemy.IsAlive || (enemy != null && enemy.IsGrabbed))
                    yield break;

                t += Time.deltaTime;
                yield return null;
            }
        }

        private float ResolveSmashMarkerRadius()
        {
            Vector2 size = jumpSmash.hitboxSizeOverride;
            if (size.x > 0f && size.y > 0f)
                return Mathf.Max(size.x, size.y) * 0.5f;
            return smashMarkerRadius;
        }

        private IEnumerator ActiveMeleeWindow(BossPatternTiming timing)
        {
            enemy.HideAttackWarning();
            StopTelegraphFlash();

            BeginArmored();
            ConfigureHitbox(timing);
            enemy.SetAttackHitboxActive(true);

            float t = 0f;
            while (t < timing.activeDuration)
            {
                if (!CanContinuePattern())
                    break;
                t += Time.deltaTime;
                ZeroVelocity();
                yield return null;
            }

            enemy.SetAttackHitboxActive(false);
            RestoreHitboxTransform();
            EndArmored();
        }

        private IEnumerator Recovery(float duration)
        {
            CleanupAttackState();
            animHandler?.SetSpeed(0f);

            float t = 0f;
            while (t < duration)
            {
                if (!enemy.IsAlive)
                    yield break;
                t += Time.deltaTime;
                ZeroVelocity();
                yield return null;
            }
        }

        private float ResolveRecovery(BossPatternTiming timing)
        {
            if (phase >= 2)
                return Mathf.Max(1f, timing.recoveryDuration * 0.85f);
            return timing.recoveryDuration;
        }

        private bool CanContinuePattern()
        {
            return enemy != null && enemy.IsAlive && !enemy.IsGrabbed;
        }

        private void BeginArmored()
        {
            ApplyBossStaggerArmor();
        }

        private void EndArmored()
        {
            // Keep stagger armor after active frames; punish window is damage-only.
            ApplyBossStaggerArmor();
        }

        private void CleanupAttackState()
        {
            animHandler?.SetBossChargePhase(-1f);
            animHandler?.SetBossJumpPhase(-1f);
            if (enemy != null)
            {
                enemy.ReleaseFacing();
                enemy.SetAttackHitboxActive(false);
                enemy.HideAttackWarning();
                ApplyBossStaggerArmor();
            }

            RestoreHitboxTransform();
            StopTelegraphFlash();
            groundTelegraph?.Hide();
        }

        private void ApplyBossStaggerArmor()
        {
            if (enemy != null)
                enemy.ignoreHitReactions = true;
        }

        private void SuppressStaggerCarryover()
        {
            ApplyBossStaggerArmor();

            // Don't fight Jump Smash kinematic flight or grab session binding.
            if (physicsCached || (enemy != null && enemy.IsGrabbed))
                return;

            enemy?.hitReactionHandler?.ForceClearReaction(zeroVelocity: true);

            if (rb != null)
            {
                Vector2 velocity = rb.linearVelocity;
                if (velocity.y > 0f)
                    rb.linearVelocity = new Vector2(velocity.x, 0f);
            }
        }

        private void ConfigureHitbox(BossPatternTiming timing)
        {
            Collider2D hitbox = timing.hitbox != null ? timing.hitbox : enemy.attackHitbox;
            if (hitbox == null)
                return;

            activePatternHitbox = hitbox;
            cachedHitboxOffset = hitbox.offset;

            if (hitbox is BoxCollider2D box)
            {
                cachedHitboxSize = box.size;
                hasCachedHitboxSize = true;
                if (timing.hitboxSizeOverride.x > 0f && timing.hitboxSizeOverride.y > 0f)
                    box.size = timing.hitboxSizeOverride;
            }
            else
            {
                hasCachedHitboxSize = false;
            }

            hitbox.offset = cachedHitboxOffset + timing.hitboxLocalOffset;

            // Ensure EnemyController toggles the same collider the pattern configured.
            enemy.attackHitbox = hitbox;
        }

        /// <summary>
        /// Shared formula for edit-preview and runtime: collider local offset/size only.
        /// Parent facing (lossyScale.x) already mirrors BoxCollider2D world space — do not flip offset again.
        /// </summary>
        public static bool TryGetPatternHitboxLocal(
            BossPatternTiming timing,
            Collider2D source,
            out Vector2 localCenter,
            out Vector2 localSize)
        {
            localCenter = Vector2.zero;
            localSize = Vector2.zero;
            if (timing == null || source == null)
                return false;

            Vector2 baseOffset;
            if (source is BoxCollider2D box)
            {
                baseOffset = box.offset;
                localSize = timing.hitboxSizeOverride.x > 0f && timing.hitboxSizeOverride.y > 0f
                    ? timing.hitboxSizeOverride
                    : box.size;
            }
            else if (source is CapsuleCollider2D capsule)
            {
                baseOffset = capsule.offset;
                localSize = timing.hitboxSizeOverride.x > 0f && timing.hitboxSizeOverride.y > 0f
                    ? timing.hitboxSizeOverride
                    : capsule.size;
            }
            else
            {
                return false;
            }

            // Match ConfigureHitbox exactly.
            localCenter = baseOffset + timing.hitboxLocalOffset;
            return true;
        }

        private void RestoreHitboxTransform()
        {
            if (activePatternHitbox == null)
                return;

            activePatternHitbox.offset = cachedHitboxOffset;
            if (hasCachedHitboxSize && activePatternHitbox is BoxCollider2D box)
                box.size = cachedHitboxSize;

            activePatternHitbox = null;
            hasCachedHitboxSize = false;
        }

        private IEnumerator JumpArc(Vector3 landPoint)
        {
            CachePhysics();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            BeginArmored();
            enemy.SetAttackHitboxActive(false);

            Vector2 start = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 end = new Vector2(landPoint.x, start.y);
            float duration = Mathf.Max(0.1f, jumpTravelDuration);
            float t = 0f;

            while (t < 1f)
            {
                if (!enemy.IsAlive || (enemy != null && enemy.IsGrabbed))
                    yield break;

                t += Time.fixedDeltaTime / duration;
                float u = Mathf.Clamp01(t);
                animHandler?.SetBossJumpPhase(u < (1f / 3f) ? 0f : 0.25f);
                Vector2 pos = Vector2.Lerp(start, end, u);
                pos.y += jumpArcHeight * 4f * u * (1f - u);
                MoveBossPosition(pos);
                yield return waitFixed;
            }

            MoveBossPosition(end);
            EndArmored();
        }

        private void CachePhysics()
        {
            if (physicsCached || rb == null)
                return;

            cachedGravityScale = rb.gravityScale;
            cachedBodyType = rb.bodyType;
            physicsCached = true;
        }

        private void RestorePhysics()
        {
            if (!physicsCached || rb == null)
                return;

            rb.bodyType = cachedBodyType;
            rb.gravityScale = cachedGravityScale;
            // Keep X free after scripted moves so chase/charge/jump keep working.
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearVelocity = Vector2.zero;
            physicsCached = false;
        }

        private void ZeroVelocity()
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        private void MoveBossPosition(Vector2 worldPos)
        {
            if (enemy != null && enemy.IsGrabbed)
                return;

            if (rb != null && rb.simulated)
            {
                // MovePosition honors FreezePositionX; strip it so patterns can travel.
                if ((rb.constraints & RigidbodyConstraints2D.FreezePositionX) != 0)
                    rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;

                rb.linearVelocity = Vector2.zero;
                rb.MovePosition(worldPos);
                return;
            }

            transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        }

        private void FaceTarget()
        {
            if (target == null)
                return;

            float dirX = target.position.x - transform.position.x;
            if (Mathf.Abs(dirX) <= 0.01f)
                return;

            EnemyFacingUtil.FaceHorizontal(transform, dirX);
        }

        private float GetFacingSign()
        {
            return EnemyFacingUtil.GetFacingSign(transform);
        }

        private void EnsureFlashRenderersCached()
        {
            if (flashRenderersCached)
                return;

            flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (flashRenderers == null || flashRenderers.Length == 0)
            {
                flashOriginalColors = null;
                flashRenderersCached = true;
                return;
            }

            flashOriginalColors = new Color[flashRenderers.Length];
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] != null)
                    flashOriginalColors[i] = flashRenderers[i].color;
            }

            flashRenderersCached = true;
        }

        private void StartTelegraphFlash()
        {
            StopTelegraphFlash();
            flashRoutine = StartCoroutine(TelegraphFlashRoutine());
        }

        private void StopTelegraphFlash()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            RestoreFlashColors();
        }

        private void RestoreFlashColors()
        {
            if (flashRenderers == null || flashOriginalColors == null)
                return;

            for (int i = 0; i < flashRenderers.Length; i++)
            {
                if (flashRenderers[i] != null && i < flashOriginalColors.Length)
                    flashRenderers[i].color = flashOriginalColors[i];
            }
        }

        private IEnumerator TelegraphFlashRoutine()
        {
            EnsureFlashRenderersCached();
            if (flashRenderers == null || flashRenderers.Length == 0)
                yield break;

            while (true)
            {
                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    if (flashRenderers[i] != null)
                        flashRenderers[i].color = telegraphFlashColor;
                }

                yield return flashWait;

                for (int i = 0; i < flashRenderers.Length; i++)
                {
                    if (flashRenderers[i] != null)
                        flashRenderers[i].color = flashOriginalColors[i];
                }

                yield return flashWait;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (previewRanges)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, cleaveRange);
                Gizmos.color = new Color(0.85f, 0.2f, 0.85f, 0.25f);
                Gizmos.DrawWireSphere(transform.position, chargeDistance);
            }

            if (ShouldPreview(BossPatternId.HeavyCleave))
                DrawPatternGizmo(cleave, new Color(1f, 0.45f, 0.05f, 0.45f));
            if (ShouldPreview(BossPatternId.JumpSmash))
                DrawPatternGizmo(jumpSmash, new Color(1f, 0.15f, 0.15f, 0.45f));
            if (ShouldPreview(BossPatternId.BullCharge))
                DrawPatternGizmo(bullCharge, new Color(0.75f, 0.25f, 1f, 0.45f));
        }

        private bool ShouldPreview(BossPatternId patternId)
        {
            return hitboxPreviewMode switch
            {
                BossHitboxPreviewMode.None => false,
                BossHitboxPreviewMode.CleaveOnly => patternId == BossPatternId.HeavyCleave,
                BossHitboxPreviewMode.JumpSmashOnly => patternId == BossPatternId.JumpSmash,
                BossHitboxPreviewMode.BullChargeOnly => patternId == BossPatternId.BullCharge,
                BossHitboxPreviewMode.All => true,
                _ => false
            };
        }

        private void DrawPatternGizmo(BossPatternTiming timing, Color color)
        {
            if (timing == null)
                return;

            if (enemy == null)
                enemy = GetComponent<EnemyController>();

            Collider2D source = timing.hitbox != null
                ? timing.hitbox
                : (enemy != null ? enemy.attackHitbox : null);
            if (!TryGetPatternHitboxLocal(timing, source, out Vector2 localCenter, out Vector2 localSize))
                return;

            Gizmos.color = color;
            Gizmos.matrix = source.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localCenter, localSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
