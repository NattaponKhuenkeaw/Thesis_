using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private EnemyChargeTelegraph chargeTelegraph;
        [SerializeField] private string playerTag = PlayerTag;

        [Header("HUD")]
        [SerializeField] private bool revealOnSpawn = true;
        [SerializeField, Min(0f)] private float spawnPanDuration = 0.5f;
        [SerializeField, Min(0f)] private float spawnRevealDuration = 1.2f;
        [SerializeField, Min(0f)] private float spawnReturnDuration = 0.6f;
        [SerializeField] private string bossDisplayName = "Executioner";
        [SerializeField] private bool bindScreenHealthHud = true;
        [SerializeField, Min(1)] private int healthBarCount = 5;

        [Header("Default Stats (applied once on Awake if useDefaultStats)")]
        [SerializeField] private bool useDefaultStats = true;
        [SerializeField] private float defaultMaxHp = 4000f;
        [SerializeField] private float defaultAttackDamage = 165f;
        [SerializeField] private float defaultDefense = 0f;

        [Header("Movement")]
        [SerializeField] private float chaseSpeed = 2.2f;
        [SerializeField] private float cleaveRange = 2.4f;
        [SerializeField] private float decideInterval = 0.2f;
        [Tooltip("Once chase is chosen, keep walking until in cleave range or this timeout.")]
        [SerializeField] private float maxChaseDuration = 1.4f;
        [Tooltip("Chance to walk into cleave range at medium distance. Far targets use gap closers instead.")]
        [SerializeField] [Range(0f, 1f)] private float phase1ChaseChance = 0.45f;
        [SerializeField] [Range(0f, 1f)] private float phase2ChaseChance = 0.25f;
        [SerializeField, Range(1, 3)] private int maxConsecutivePattern = 2;

        [Header("Phases")]
        [SerializeField] [Range(0.05f, 0.95f)] private float phase2HpRatio = 0.5f;
        [Tooltip("Chance to follow a landed jump or completed charge with cleave, only while the player is close.")]
        [SerializeField] [Range(0f, 1f)] private float phase2ChainCleaveChance = 0.65f;
        [SerializeField, Min(0f)] private float comboLinkPause = 0.2f;
        [SerializeField, Min(0f)] private float comboRecoveryDuration = 1.6f;

        [Header("Patterns")]
        [SerializeField] private BossPatternTiming cleave = new BossPatternTiming
        {
            telegraphDuration = 1.2f,
            activeDuration = 0.4f,
            recoveryDuration = 1f,
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
            recoveryDuration = 1.8f,
            hitboxLocalOffset = new Vector2(0.8f, 0f),
            hitboxSizeOverride = new Vector2(2.2f, 1.6f)
        };

        [Header("Cleave Audio")]
        [SerializeField] private AudioClip cleaveSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float cleaveSfxVolume = 1f;
        [Tooltip("Seconds relative to the cleave hitbox opening: negative = earlier during windup; positive = later. Cancelled if this attack ends.")]
        [SerializeField, Range(-2f, 2f)] private float cleaveSfxOffset;

        [Header("Alternating Cleave")]
        [SerializeField] private BossPatternTiming alternatingCleave = new BossPatternTiming
        {
            telegraphDuration = 1.2f, activeDuration = .22f, recoveryDuration = 1.8f,
            hitboxLocalOffset = new Vector2(1.4f, .2f), hitboxSizeOverride = new Vector2(2.6f, 1.4f)
        };
        [SerializeField, Range(2, 6)] private int alternatingCleaveCount = 4;
        [SerializeField, Min(.15f)] private float alternatingCleaveInterval = .36f;
        [SerializeField, Min(.1f)] private float alternatingCleaveFirstHold = .3f;
        [SerializeField, Min(.15f)] private float alternatingCleaveRoarDuration = 1.2f;

        [Header("Seismic Barrage (Phase 2)")]
        [SerializeField] private BossPatternTiming seismicBarrage = new BossPatternTiming
        {
            telegraphDuration = 1.5f, activeDuration = .3f, recoveryDuration = 2.2f
        };
        [SerializeField, Range(2, 5)] private int seismicJumpCount = 3;
        [SerializeField, Min(.2f)] private float seismicJumpInterval = .65f;
        [SerializeField, Min(.1f)] private float seismicFirstCrouchHold = .3f;
        [SerializeField] private BossGroundWave groundWavePrefab;
        [SerializeField, Min(1f)] private float groundWaveSpeed = 14f;
        [SerializeField, Min(1f)] private float groundWaveDistance = 80f;

        [Header("Attack Warning Audio")]
        [Tooltip("Additional warning at the start of every attack windup. Layers over existing pattern sounds.")]
        [SerializeField] private AudioClip attackWarningSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float attackWarningSfxVolume = 1f;

        [Header("Charge Hit Audio")]
        [Tooltip("Only plays when Bull Charge successfully damages the player, not when the charge starts or misses.")]
        [SerializeField] private AudioClip chargeHitSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float chargeHitSfxVolume = 1f;

        [Header("Charge Rush Audio")]
        [Tooltip("Starts with actual Bull Charge movement and stops when the charge ends. Separate from player impact audio.")]
        [SerializeField] private AudioClip chargeRushSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float chargeRushSfxVolume = 1f;

        [Header("Death Audio")]
        [Tooltip("Plays once when the boss dies, before its separate spirit dissolve sound.")]
        [SerializeField] private AudioClip deathSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float deathSfxVolume = 1f;

        [Header("Jump Takeoff Audio")]
        [Tooltip("Plays once as Jump Smash starts moving, after its warning. Not a landing sound.")]
        [SerializeField] private AudioClip jumpTakeoffSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float jumpTakeoffSfxVolume = 1f;

        [Header("Jump Landing Audio")]
        [Tooltip("Plays once when Jump Smash confirms ground contact and starts its impact, even if it misses the player.")]
        [SerializeField] private AudioClip jumpLandSfx;
        [SerializeField, Range(0f, GameAudioManager.MaxSFXGain)] private float jumpLandSfxVolume = 1f;

        [Header("Jump Smash")]
        [SerializeField] private float jumpArcHeight = 5f;
        [SerializeField] private float jumpTravelDuration = 0.75f;
        [Tooltip("Separate height for the stationary shockwave jumps; does not follow the traveling jump tuning.")]
        [SerializeField, Min(0f)] private float seismicJumpArcHeight = 3.5f;
        [Tooltip("Fallback world radius when hitboxSizeOverride is unset. Prefer deriving from jump smash hitbox.")]
        [SerializeField] private float smashMarkerRadius = 1.6f;

        [Header("Bull Charge")]
        [SerializeField] private float chargeSpeed = 16f;
        [Tooltip("Safety cap if no wall is found ahead.")]
        [SerializeField] private float chargeDistance = 40f;
        [Tooltip("Stop this far before the blocking wall collider.")]
        [SerializeField] private float chargeWallPadding = 0.35f;
        [Tooltip("Duration of the three stopping poses, played within charge recovery.")]
        [SerializeField, Min(0.01f)] private float chargeStopAnimationDuration = 0.36f;

        [Header("Telegraph Feedback")]
        [SerializeField] private bool flashDuringTelegraph = true;
        [SerializeField] private Color telegraphFlashColor = new Color(1f, 0.45f, 0.35f, 1f);

        [Header("Edit Preview (Scene View)")]
        [Tooltip("Which pattern hitbox to preview while selected in the editor.")]
        [SerializeField] private BossHitboxPreviewMode hitboxPreviewMode = BossHitboxPreviewMode.CleaveOnly;
        [SerializeField] private bool previewRanges = true;

        private Transform target;
        private Coroutine brainRoutine;
        private bool trainingControl;
        private bool trainingAuto;
        private Coroutine spawnRevealRoutine;
        private bool spawnRevealStarted;
        private bool spawnRevealPending;
        private CameraManager spawnRevealCamera;
        private Coroutine flashRoutine;
        private int phase = 1;
        private bool isBusy;
        private bool statsApplied;
        private BossPatternId lastPattern;
        private int consecutivePatternCount;
        private readonly EnemyAttackAudioCue cleaveAudioCue = new EnemyAttackAudioCue();
        private float cleaveAudioUpdatedAt;
        private bool deathAudioPlayed;
        private AudioSource attackWarningAudioSource;
        private AudioSource chargeRushAudioSource;
        private readonly List<BossGroundWave> groundWaves = new List<BossGroundWave>();
        private bool samplingCleaveSequence;

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
        public EnemyController Enemy => enemy;
        public bool TrainingAuto => trainingAuto;
        internal IReadOnlyList<BossGroundWave> TrainingWaves => groundWaves;
        public BossHitboxPreviewMode HitboxPreviewMode => hitboxPreviewMode;
        public bool PreviewRanges => previewRanges;
        public BossPatternTiming CleaveTiming => cleave;
        public BossPatternTiming JumpSmashTiming => jumpSmash;
        public BossPatternTiming BullChargeTiming => bullCharge;
        public BossPatternTiming AlternatingCleaveTiming => alternatingCleave;
        public BossPatternTiming SeismicBarrageTiming => seismicBarrage;
        public float CleaveRange => cleaveRange;
        public float ChargeDistance => chargeDistance;
        public AudioClip CleaveSfx => cleaveSfx;
        public float CleaveSfxVolume => GameAudioManager.ClampSFXGain(cleaveSfxVolume);
        public float CleaveSfxOffset => cleaveSfxOffset;
        public AudioClip AttackWarningSfx => attackWarningSfx;
        public float AttackWarningSfxVolume => GameAudioManager.ClampSFXGain(attackWarningSfxVolume);
        public AudioClip ChargeHitSfx => chargeHitSfx;
        public float ChargeHitSfxVolume => GameAudioManager.ClampSFXGain(chargeHitSfxVolume);
        public AudioClip ChargeRushSfx => chargeRushSfx;
        public float ChargeRushSfxVolume => GameAudioManager.ClampSFXGain(chargeRushSfxVolume);
        public AudioClip DeathSfx => deathSfx;
        public float DeathSfxVolume => GameAudioManager.ClampSFXGain(deathSfxVolume);
        public AudioClip JumpTakeoffSfx => jumpTakeoffSfx;
        public float JumpTakeoffSfxVolume => GameAudioManager.ClampSFXGain(jumpTakeoffSfxVolume);
        public AudioClip JumpLandSfx => jumpLandSfx;
        public float JumpLandSfxVolume => GameAudioManager.ClampSFXGain(jumpLandSfxVolume);
        public float JumpArcHeight => jumpArcHeight;
        public float SeismicJumpArcHeight => seismicJumpArcHeight;
        public float JumpTravelDuration => jumpTravelDuration;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeStopAnimationDuration => chargeStopAnimationDuration;

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
            consecutivePatternCount = 0;
            if (enemy != null && enemy.IsAlive) deathAudioPlayed = false;
            if (enemy != null)
            {
                enemy.OnDamaged += HandleDamaged;
                enemy.OnDied += HandleDied;
            }

            if (bindScreenHealthHud && enemy != null)
                BossHealthHUD.EnsureInstance()?.Bind(enemy, bossDisplayName, healthBarCount);

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
                ClearGroundWaves();
                RestorePhysics();
                ZeroVelocity();
            }
            // OnEnable runs before EnemyController.Start, so retry once the spawn state enters Active.
            StartBrainIfActive();
            TickCleaveAudio();
        }

        private void StartBrainIfActive()
        {
            if (enemy == null
                || !isActiveAndEnabled
                || (trainingControl && !trainingAuto)
                || !enemy.IsCombatReady
                || spawnRevealPending
                || brainRoutine != null)
            {
                return;
            }

            enemy.SetInCombat(true);
            brainRoutine = StartCoroutine(BrainLoop());
        }

        internal void ConfigureTrainingControl()
        {
            trainingControl = true;
            revealOnSpawn = false;
        }

        internal bool TryStartTrainingPattern(BossPatternId pattern)
        {
            if (!trainingControl || !isActiveAndEnabled || !CanContinuePattern() || isBusy
                || (pattern == BossPatternId.SeismicBarrage && phase < 2) || !TryAcquireTarget()) return false;
            SetTrainingAuto(false);
            brainRoutine = StartCoroutine(RunTrainingPattern(pattern));
            return true;
        }

        private IEnumerator RunTrainingPattern(BossPatternId pattern)
        {
            yield return ExecutePattern(pattern);
            brainRoutine = null;
        }

        internal void SetTrainingAuto(bool enabled)
        {
            if (!trainingControl) return;
            trainingAuto = enabled;
            ForceClearCombatHazards();
        }

        internal void ToggleTrainingPhase()
        {
            if (!trainingControl || enemy == null || !enemy.IsCombatReady) return;
            SetTrainingAuto(false);
            enemy.currentHP = enemy.maxHP * (phase == 1 ? phase2HpRatio : 1f);
            EvaluatePhase();
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
            ClearGroundWaves();
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
            ClearGroundWaves();
            RestorePhysics();
            groundTelegraph?.Hide();

            if (brainRoutine != null)
            {
                StopCoroutine(brainRoutine);
                brainRoutine = null;
            }

            PlayDeathAudio();
            if (!trainingControl) BossDeathSequence.EnsureInstance().BeginFromBoss(enemy);
        }

        private void PlayDeathAudio()
        {
            if (!Application.isPlaying || deathAudioPlayed || enemy == null || enemy.IsAlive) return;
            deathAudioPlayed = true;
            if (deathSfx != null && DeathSfxVolume > 0f)
                GameAudioManager.EnsureInstance().PlayHitSFX(deathSfx, DeathSfxVolume);
        }

        /// <summary>
        /// Called by BossDeathSequence to strip leftover attack windows immediately.
        /// </summary>
        public void ForceClearCombatHazards()
        {
            CancelSpawnReveal();
            isBusy = false;
            CleanupAttackState();
            ClearGroundWaves();
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
                    yield return ExecutePattern(ChoosePattern(distance));
                    yield break;
                }

                yield return ChaseTowardTargetStep();
                elapsed += Time.fixedDeltaTime;
            }

            animHandler?.SetSpeed(0f);

            // Player kited past timeout — use a gap closer instead of stalling.
            if (enemy == null || !enemy.IsAlive || !TryAcquireTarget())
                yield break;

            BossPatternId closer = ChoosePattern(cleaveRange * 3f + 1f);
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
            float barrageWeight = phase >= 2 && groundWavePrefab != null ? .24f : 0f;
            float alternatingWeight = distance <= cleaveRange ? .3f : 0f;
            float cleaveWeight, jumpWeight, chargeWeight;
            if (distance <= cleaveRange)
            {
                cleaveWeight = phase < 2 ? .7f : .55f;
                jumpWeight = 1f - cleaveWeight;
                chargeWeight = 0f;
            }
            else if (distance <= cleaveRange * 3f)
            {
                // Cleave here means a short committed chase, not a swing out of range.
                chargeWeight = phase < 2 ? .15f : .4f;
                cleaveWeight = Mathf.Min(phase < 2 ? phase1ChaseChance : phase2ChaseChance, 1f - chargeWeight);
                jumpWeight = 1f - cleaveWeight - chargeWeight;
            }
            else
            {
                cleaveWeight = 0f;
                chargeWeight = phase < 2 ? .55f : .65f;
                jumpWeight = 1f - chargeWeight;
            }

            if (consecutivePatternCount >= Mathf.Max(1, maxConsecutivePattern))
            {
                if (lastPattern == BossPatternId.HeavyCleave) cleaveWeight = 0f;
                if (lastPattern == BossPatternId.JumpSmash) jumpWeight = 0f;
                if (lastPattern == BossPatternId.BullCharge) chargeWeight = 0f;
                if (lastPattern == BossPatternId.AlternatingCleave) alternatingWeight = 0f;
                if (lastPattern == BossPatternId.SeismicBarrage) barrageWeight = 0f;
            }
            float roll = Random.value * (cleaveWeight + jumpWeight + chargeWeight + alternatingWeight + barrageWeight);
            if (roll < barrageWeight) return BossPatternId.SeismicBarrage;
            roll -= barrageWeight;
            if (roll < alternatingWeight) return BossPatternId.AlternatingCleave;
            roll -= alternatingWeight;
            if (roll < cleaveWeight) return BossPatternId.HeavyCleave;
            if (jumpWeight > 0f && (roll < cleaveWeight + jumpWeight || chargeWeight <= 0f))
                return BossPatternId.JumpSmash;
            return BossPatternId.BullCharge;
        }

        private void RememberPattern(BossPatternId pattern)
        {
            consecutivePatternCount = consecutivePatternCount > 0 && lastPattern == pattern
                ? consecutivePatternCount + 1 : 1;
            lastPattern = pattern;
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
            RememberPattern(patternId);
            BossPatternTiming timing = patternId == BossPatternId.HeavyCleave ? cleave
                : patternId == BossPatternId.JumpSmash ? jumpSmash
                : patternId == BossPatternId.AlternatingCleave ? alternatingCleave
                : patternId == BossPatternId.SeismicBarrage ? seismicBarrage : bullCharge;
            try
            {
                switch (patternId)
                {
                    case BossPatternId.HeavyCleave:
                        yield return RunCleave();
                        break;
                    case BossPatternId.JumpSmash:
                        yield return RunJumpSmash();
                        break;
                    case BossPatternId.BullCharge:
                        yield return RunBullCharge();
                        break;
                    case BossPatternId.AlternatingCleave:
                        yield return RunAlternatingCleave();
                        break;
                    case BossPatternId.SeismicBarrage:
                        yield return RunSeismicBarrage();
                        break;
                }

                if (!CanContinuePattern()) yield break;
                bool charge = patternId == BossPatternId.BullCharge;
                if (ShouldChainCleave(patternId))
                {
                    yield return Recovery(charge ? Mathf.Max(comboLinkPause, chargeStopAnimationDuration) : comboLinkPause,
                        animateChargeStop: charge);
                    charge = false;
                    // Recheck after stopping: do not chase or turn an escaped target into a free hit.
                    if (CanContinuePattern() && IsTargetInCleaveRange())
                    {
                        RememberPattern(BossPatternId.HeavyCleave);
                        yield return RunCleave();
                        if (CanContinuePattern())
                            yield return Recovery(Mathf.Max(comboRecoveryDuration, ResolveRecovery(cleave)));
                        yield break;
                    }
                }
                if (CanContinuePattern())
                    yield return Recovery(ResolveRecovery(timing), animateChargeStop: charge);
            }
            finally
            {
                isBusy = false;
                CleanupAttackState();
                RestorePhysics();
            }
        }

        private bool IsTargetInCleaveRange()
        {
            return TryAcquireTarget() && Vector2.Distance(transform.position, target.position) <= cleaveRange;
        }

        private bool ShouldChainCleave(BossPatternId starter)
        {
            return (!trainingControl || trainingAuto) && phase >= 2
                && (starter == BossPatternId.JumpSmash || starter == BossPatternId.BullCharge) && CanContinuePattern()
                && enemy.HasSpawnGroundSupport() && IsTargetInCleaveRange()
                && Random.value < phase2ChainCleaveChance;
        }

        private IEnumerator RunAlternatingCleave()
        {
            FaceTarget();
            float initialFacing = GetFacingSign();
            samplingCleaveSequence = true;
            yield return Telegraph(alternatingCleave, BossPatternId.AlternatingCleave, null, 0f);
            for (int i = 0; i < Mathf.Clamp(alternatingCleaveCount, 2, 6) && CanContinuePattern(); i++)
            {
                if (i == 1)
                {
                    StopAttackWarningAudio();
                    // Keep the first slash's follow-through and facing until the roar starts.
                    for (float held = 0f; held < alternatingCleaveFirstHold && CanContinuePattern(); held += Time.deltaTime)
                    {
                        animHandler?.SampleBossCleave(.9f);
                        ZeroVelocity();
                        yield return null;
                    }
                    if (!CanContinuePattern()) yield break;
                }

                // Alternate the committed sides, never home in on a player who escaped the sequence.
                enemy.ReleaseFacing();
                EnemyFacingUtil.FaceHorizontal(transform, initialFacing * (i % 2 == 0 ? 1f : -1f));
                enemy.LockFacing();
                if (i > 0)
                {
                    float duration = i == 1 ? alternatingCleaveRoarDuration : alternatingCleaveInterval;
                    float pause = 0f;
                    enemy.TriggerAttackWarning(duration);
                    if (i == 1) PlayAttackWarningAudio();
                    while (pause < duration && CanContinuePattern())
                    {
                        animHandler?.SampleBossCleave(0f);
                        ZeroVelocity();
                        pause += Time.deltaTime;
                        yield return null;
                    }
                    if (i == 1) StopAttackWarningAudio();
                }
                if (!CanContinuePattern()) yield break;
                EmitCleaveAudio();
                yield return ActiveMeleeWindow(alternatingCleave, animateCleave: true);
            }
        }

        private IEnumerator RunSeismicBarrage()
        {
            FaceTarget();
            for (int i = 0; i < Mathf.Clamp(seismicJumpCount, 2, 5) && CanContinuePattern(); i++)
            {
                yield return CrouchForSeismicJump(firstJump: i == 0);
                if (!CanContinuePattern()) yield break;
                yield return JumpSmashAt(transform.position, seismicBarrage, seismic: true);
            }
        }

        private IEnumerator CrouchForSeismicJump(bool firstJump)
        {
            enemy.LockFacing();
            BeginArmored();
            enemy.SetAttackHitboxActive(false);
            enemy.HideAttackWarning();
            groundTelegraph?.Hide();
            animHandler?.PlayBossJumpCrouch();

            float holdUntil = Time.time + (firstJump ? seismicFirstCrouchHold : seismicJumpInterval);
            while (Time.time < holdUntil && CanContinuePattern())
            {
                ZeroVelocity();
                yield return null;
            }

            if (!firstJump || !CanContinuePattern()) yield break;
            PlayAttackWarningAudio();
            float roarDuration = Mathf.Max(seismicBarrage.telegraphDuration - seismicFirstCrouchHold,
                attackWarningSfx != null && AttackWarningSfxVolume > 0f ? attackWarningSfx.length : 0f);
            float roarUntil = Time.time + roarDuration;
            while (Time.time < roarUntil && CanContinuePattern())
            {
                ZeroVelocity();
                yield return null;
            }
            StopAttackWarningAudio();
        }

        private void EmitGroundWaves()
        {
            if (groundWavePrefab == null || !CanContinuePattern()) return;
            groundWaves.RemoveAll(wave => wave == null);
            var hitTargets = new HashSet<int>();
            float groundY = enemy.bodyCollider != null ? enemy.bodyCollider.bounds.min.y : transform.position.y;
            for (int side = -1; side <= 1; side += 2)
            {
                var wave = Instantiate(groundWavePrefab);
                wave.Initialize(enemy, new Vector2(transform.position.x, groundY), side,
                    groundWaveSpeed, groundWaveDistance, hitTargets);
                groundWaves.Add(wave);
            }
        }

        private void ClearGroundWaves()
        {
            foreach (var wave in groundWaves)
                if (wave != null) wave.Cancel();
            groundWaves.Clear();
        }

        private IEnumerator RunCleave()
        {
            BossPatternTiming timing = cleave;
            FaceTarget();

            yield return Telegraph(timing, BossPatternId.HeavyCleave, null, 0f);

            if (!CanContinuePattern())
            {
                CleanupAttackState();
                yield break;
            }

            cleaveAudioUpdatedAt = Time.time;
            if (cleaveAudioCue.Confirm(cleaveSfxOffset)) EmitCleaveAudio();
            yield return ActiveMeleeWindow(timing);

            CleanupAttackState();
        }

        private IEnumerator RunJumpSmash()
        {
            if (!TryAcquireTarget())
                yield break;
            FaceTarget();
            yield return JumpSmashAt(target.position, jumpSmash, seismic: false);
        }

        private IEnumerator JumpSmashAt(Vector3 lockPoint, BossPatternTiming timing, bool seismic)
        {
            if (!seismic)
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
            if (!seismic) animHandler?.TriggerBossPattern(BossPatternId.JumpSmash);
            yield return JumpArc(lockPoint, seismic ? seismicJumpArcHeight : jumpArcHeight);

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
            animHandler?.SetBossJumpPhase(0.6f);
            if (Application.isPlaying && jumpLandSfx != null && JumpLandSfxVolume > 0f)
                GameAudioManager.EnsureInstance().PlayHitSFX(jumpLandSfx, JumpLandSfxVolume);
            groundTelegraph?.Hide();
            if (seismic)
            {
                EmitGroundWaves();
                float elapsed = 0f;
                while (elapsed < timing.activeDuration && CanContinuePattern())
                {
                    animHandler?.SetBossJumpPhase(Mathf.Lerp(.6f, .99f, Mathf.Clamp01(elapsed / .3f)));
                    elapsed += Time.deltaTime;
                    ZeroVelocity();
                    yield return null;
                }
            }
            else yield return ActiveMeleeWindow(timing, animateLanding: true);
            RestorePhysics();

            CleanupAttackState();
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
            enemy.attackHitbox?.GetComponent<EnemyAttackHitbox>()?.SetWindowImpactAudio(chargeHitSfx, chargeHitSfxVolume);
            enemy.SetAttackHitboxActive(true);

            float maxTravel = ResolveChargeTravelDistance(facing);
            float traveled = 0f;
            bool rushPoseStarted = false;
            float rushAnimationTime = 0f;
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
                    animHandler?.SetBossChargePhase(0.3f);
                    PlayChargeRushAudio();
                    rushPoseStarted = true;
                }
                if (rushPoseStarted)
                {
                    animHandler?.SetBossChargePhase(.3f + Mathf.Repeat(rushAnimationTime, .4f) * .5f);
                    rushAnimationTime += Time.fixedDeltaTime;
                }
            }

            StopChargeRushAudio();
            enemy.SetAttackHitboxActive(false);
            RestoreHitboxTransform();
            EndArmored();
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
            chargeTelegraph?.Hide();
            // Each pattern faces its target once before entering wind-up.
            enemy.LockFacing();
            BeginArmored();
            enemy.SetAttackHitboxActive(false);
            enemy.TriggerAttackWarning(timing.telegraphDuration);
            PlayAttackWarningAudio();

            if (markerWorldPos.HasValue)
                groundTelegraph?.Show(markerWorldPos.Value, markerRadius);

            if (flashDuringTelegraph)
                StartTelegraphFlash();

            // Wind-up anim cue at telegraph start for grounded swings / charge crouch.
            if (patternId == BossPatternId.AlternatingCleave)
                animHandler?.SampleBossCleave(0f);
            else if (patternId != BossPatternId.JumpSmash)
                animHandler?.TriggerBossPattern(patternId);

            if (patternId == BossPatternId.HeavyCleave)
            {
                cleaveAudioCue.Begin(timing.telegraphDuration, cleaveSfxOffset);
                cleaveAudioUpdatedAt = Time.time;
                TickCleaveAudio();
            }

            float t = 0f;
            float chargeTravel = patternId == BossPatternId.BullCharge ? ResolveChargeTravelDistance(GetFacingSign()) : 0f;
            while (t < timing.telegraphDuration)
            {
                if (!CanContinuePattern())
                    yield break;

                t += Time.deltaTime;
                if (patternId == BossPatternId.BullCharge && chargeTelegraph != null && enemy.bodyCollider != null)
                {
                    Bounds bounds = enemy.bodyCollider.bounds;
                    float facing = GetFacingSign();
                    Vector3 start = new Vector3(facing > 0f ? bounds.max.x : bounds.min.x, bounds.center.y, transform.position.z);
                    chargeTelegraph.ShowLane(start, start + Vector3.right * (facing * chargeTravel), bounds.size.y,
                        t / Mathf.Max(.001f, timing.telegraphDuration));
                }
                if (patternId == BossPatternId.BullCharge)
                    animHandler?.SetBossChargePhase(Mathf.Min(.2999f, t));
                if (patternId == BossPatternId.AlternatingCleave)
                    animHandler?.SampleBossCleave(0f);
                yield return null;
            }
            chargeTelegraph?.Hide();
        }

        private float ResolveSmashMarkerRadius()
        {
            Vector2 size = jumpSmash.hitboxSizeOverride;
            if (size.x > 0f && size.y > 0f)
                return Mathf.Max(size.x, size.y) * 0.5f;
            return smashMarkerRadius;
        }

        private IEnumerator ActiveMeleeWindow(BossPatternTiming timing, bool animateLanding = false, bool animateCleave = false)
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
                if (animateLanding)
                    animHandler?.SetBossJumpPhase(Mathf.Lerp(0.6f, 0.99f, Mathf.Clamp01(t / .3f)));
                if (animateCleave)
                    animHandler?.SampleBossCleave(Mathf.Lerp(.75f, .99f, Mathf.Clamp01(t / Mathf.Max(.01f, timing.activeDuration))));
                t += Time.deltaTime;
                ZeroVelocity();
                yield return null;
            }

            enemy.SetAttackHitboxActive(false);
            RestoreHitboxTransform();
            EndArmored();
        }

        private IEnumerator Recovery(float duration, bool animateChargeStop = false)
        {
            CleanupAttackState();
            animHandler?.SetSpeed(0f);

            float stopDuration = Mathf.Min(duration, Mathf.Max(0.01f, chargeStopAnimationDuration));
            float t = 0f;
            while (t < duration)
            {
                if (!CanContinuePattern())
                    yield break;
                if (animateChargeStop)
                {
                    // Motion-time sampling keeps stopping poses out of the moving charge.
                    animHandler?.SetBossChargePhase(t < stopDuration
                        ? Mathf.Lerp(0.5f, 0.99f, t / stopDuration)
                        : -1f);
                }
                t += Time.deltaTime;
                ZeroVelocity();
                yield return null;
            }

            if (animateChargeStop)
                animHandler?.SetBossChargePhase(-1f);
        }

        private float ResolveRecovery(BossPatternTiming timing)
        {
            if (phase >= 2)
                return Mathf.Max(.8f, timing.recoveryDuration * 0.85f);
            return timing.recoveryDuration;
        }

        private bool CanContinuePattern()
        {
            return enemy != null && enemy.IsCombatReady && !enemy.IsGrabbed;
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
            chargeTelegraph?.Hide();
            if (samplingCleaveSequence)
            {
                samplingCleaveSequence = false;
                if (enemy != null && enemy.IsCombatReady && !enemy.IsGrabbed)
                    animHandler?.EndBossCleaveSequence();
            }
            cleaveAudioCue.Cancel();
            StopAttackWarningAudio();
            StopChargeRushAudio();
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

        private void PlayChargeRushAudio()
        {
            if (!Application.isPlaying || enemy == null || !enemy.IsCombatReady || enemy.IsGrabbed
                || chargeRushSfx == null || ChargeRushSfxVolume <= 0f) return;
            if (chargeRushAudioSource == null)
            {
                var host = new GameObject("BossChargeRushAudio");
                host.transform.SetParent(transform, false);
                chargeRushAudioSource = host.AddComponent<AudioSource>();
                chargeRushAudioSource.playOnAwake = false;
            }
            GameAudioManager.EnsureInstance().PlayControlledSFX(chargeRushAudioSource, chargeRushSfx, ChargeRushSfxVolume);
        }

        private void StopChargeRushAudio()
        {
            if (chargeRushAudioSource == null) return;
            chargeRushAudioSource.Stop();
            chargeRushAudioSource.clip = null;
        }

        private void PlayAttackWarningAudio()
        {
            if (!Application.isPlaying || enemy == null || !enemy.IsCombatReady || enemy.IsGrabbed
                || attackWarningSfx == null || AttackWarningSfxVolume <= 0f) return;
            if (attackWarningAudioSource == null)
            {
                var host = new GameObject("BossAttackWarningAudio");
                host.transform.SetParent(transform, false);
                attackWarningAudioSource = host.AddComponent<AudioSource>();
                attackWarningAudioSource.playOnAwake = false;
            }
            GameAudioManager.EnsureInstance().PlayControlledSFX(attackWarningAudioSource, attackWarningSfx, AttackWarningSfxVolume);
        }

        private void StopAttackWarningAudio()
        {
            if (attackWarningAudioSource == null) return;
            attackWarningAudioSource.Stop();
            attackWarningAudioSource.clip = null;
        }

        private void TickCleaveAudio()
        {
            float now = Time.time;
            float deltaTime = Mathf.Max(0f, now - cleaveAudioUpdatedAt);
            cleaveAudioUpdatedAt = now;
            if (!isActiveAndEnabled || enemy == null || !enemy.IsCombatReady || enemy.IsGrabbed)
            {
                cleaveAudioCue.Cancel();
                return;
            }
            if (cleaveAudioCue.Tick(deltaTime)) EmitCleaveAudio();
        }

        private void EmitCleaveAudio()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || enemy == null || !enemy.IsCombatReady
                || enemy.IsGrabbed || cleaveSfx == null) return;
            GameAudioManager.EnsureInstance().PlayHitSFX(cleaveSfx, GameAudioManager.ClampSFXGain(cleaveSfxVolume));
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

        private IEnumerator JumpArc(Vector3 landPoint, float arcHeight)
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

                if (t == 0f && Application.isPlaying && jumpTakeoffSfx != null && JumpTakeoffSfxVolume > 0f)
                    GameAudioManager.EnsureInstance().PlayHitSFX(jumpTakeoffSfx, JumpTakeoffSfxVolume);
                t += Time.fixedDeltaTime / duration;
                float u = Mathf.Clamp01(t);
                animHandler?.SetBossJumpPhase(u * duration < .1f ? 0f : 0.25f);
                Vector2 pos = Vector2.Lerp(start, end, u);
                pos.y += Mathf.Max(0f, arcHeight) * 4f * u * (1f - u);
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
