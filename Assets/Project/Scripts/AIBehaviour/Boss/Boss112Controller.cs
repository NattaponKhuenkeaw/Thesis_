using System.Collections;
using System.Collections.Generic;
using Project.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace Project.AIBehaviour.Boss
{
    public enum Boss112Pattern { PrecisionBeam, HeavySlam, DelayedSlam, SweepingBeam, TripleSlam }
    public enum Boss112Phase
    {
        Idle, BeamTracking, BeamLocked, BeamActive, SlamTracking, SlamLocked, SlamFalling, SlamImpact, Recovery,
        SlamFeint, SlamCommit, SweepPositioning, SweepCharging, SweepActive, Retreat
    }

    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(EnemyController))]
    public sealed class Boss112Controller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyController enemy;
        [SerializeField] private Animator animator;
        [SerializeField] private Boss112Beam beam;
        [SerializeField] private SpriteRenderer landingMarker;
        [SerializeField] private EnemyChargeTelegraph chargeTelegraph;
        [SerializeField] private BoxCollider2D impactCollider;
        [SerializeField] private EnemyAttackHitbox impactDamage;
        [SerializeField] private bool bindHealthHud = true;
        [Header("Precision Beam")]
        [SerializeField, Min(0f)] private float beamOriginHeight = .84f;
        [SerializeField, Min(.1f)] private float beamTrackingDuration = 1.1f;
        [SerializeField, Min(.1f)] private float beamLockDuration = .4f;
        [SerializeField, Min(.1f)] private float beamActiveDuration = .5f;
        [Header("Heavy Slam")]
        [SerializeField, Min(.1f)] private float slamTrackingDuration = .35f;
        [SerializeField, Min(.1f)] private float slamLockDuration = .35f;
        [SerializeField, Min(.1f)] private float slamFallDuration = .5f;
        [SerializeField, Min(.05f)] private float slamImpactDuration = .15f;
        [SerializeField, Min(.1f)] private float slamRecoveryDuration = 3.5f;
        [Header("Phase Two")]
        [SerializeField, Min(.1f)] private float feintPauseDuration = .5f;
        [SerializeField, Min(.5f)] private float feintHeight = 4f;
        [SerializeField, Min(.1f)] private float feintCommitDuration = .25f;
        [SerializeField, Min(.1f)] private float delayedRecoveryDuration = 4f;
        [SerializeField, Min(.1f)] private float sweepPositionDuration = .6f;
        [SerializeField, Min(.1f)] private float sweepChargeDuration = 1.5f;
        [SerializeField, Min(.1f)] private float sweepActiveDuration = 2f;
        [SerializeField] private BossGroundWave shockwavePrefab;
        [SerializeField, Min(1f)] private float shockwaveSpeed = 10f;
        [Header("Movement")]
        [SerializeField, Min(1f)] private float hoverHeight = 7f;
        [SerializeField, Min(1f)] private float trackingSpeed = 12f;
        [SerializeField, Min(0f)] private float targetInset = 3.5f;
        [SerializeField] private float arenaLeft = -18f, arenaRight = 18f, floorY;
        [SerializeField] private float departureHeight = 23f;
        [SerializeField] private Transform targetOverride;
        [SerializeField] private bool autoAttack;
        [SerializeField] private bool useRunBossFlow;
        [Header("Spawn Reveal (Run Only)")]
        [SerializeField, Min(0f)] private float spawnPanDuration = .5f;
        [SerializeField, Min(0f)] private float spawnRevealDuration = 1.2f;
        [SerializeField, Min(0f)] private float spawnReturnDuration = .6f;
        [SerializeField, Min(.1f)] private float idleDelay = 1f;
        [SerializeField, Min(.1f)] private float retreatDuration = .6f;
        [SerializeField, Min(0f)] private float retreatDistance = 3.5f;
        [Header("Optional Audio")]
        [SerializeField] private AudioSource warningAudio;
        [SerializeField] private AudioClip beamChargeSfx;
        [SerializeField] private AudioClip lockSfx;
        [SerializeField] private AudioClip impactSfx;

        private Rigidbody2D body;
        private Vector2 phaseOrigin, retreatTarget;
        private float phaseAge, lockedX, recoveryDuration, sweepStartX, sweepEndX;
        private bool manualPose, stunPoseHeld;
        private bool spawnRevealStarted, spawnRevealPending;
        private Coroutine spawnRevealRoutine;
        private CameraManager spawnRevealCamera;
        private CinemachineCamera spawnReturnCamera;
        private Boss112Pattern lastPattern = Boss112Pattern.HeavySlam;
        private readonly List<BossGroundWave> waves = new List<BossGroundWave>(2);
        public Boss112Phase Phase { get; private set; }
        public Boss112Pattern Pattern { get; private set; }
        public bool IsBusy => Phase != Boss112Phase.Idle;
        public bool IsSecondPhase => enemy != null && enemy.currentHP <= enemy.maxHP * .5f;
        public bool IsStaggered => Phase == Boss112Phase.Recovery;
        public bool IsStunPoseHeld => IsStaggered && stunPoseHeld;
        public int SlamImpactCount { get; private set; }
        public bool AutoAttack { get => autoAttack; set => autoAttack = value; }
        public bool IsSpawnRevealing => spawnRevealPending;
        public EnemyController Enemy => enemy;
        public Boss112Beam Beam => beam;
        public BoxCollider2D ImpactCollider => impactCollider;
        public float LockedX => lockedX;
        public float FloorY => floorY;
        public float ArenaLeft => arenaLeft;
        public float ArenaRight => arenaRight;
        public float BeamWarningDuration => beamTrackingDuration + beamLockDuration;
        public float SlamWarningDuration => slamTrackingDuration + slamLockDuration;
        public float SlamImpactDuration => slamImpactDuration;
        public float PhaseElapsed => phaseAge;
        public float BeamOriginHeight => beamOriginHeight;
        public float RecoveryProgress => Mathf.Clamp01(phaseAge / Mathf.Max(.1f, recoveryDuration));
        public float BeamChargeProgress => Phase == Boss112Phase.BeamTracking
            ? Mathf.Clamp01(phaseAge / BeamWarningDuration)
            : Phase == Boss112Phase.BeamLocked ? Mathf.Clamp01((beamTrackingDuration + phaseAge) / BeamWarningDuration)
            : Phase == Boss112Phase.SweepCharging ? Mathf.Clamp01(phaseAge / sweepChargeDuration) : 0f;

        private void Awake()
        {
            if (enemy == null) enemy = GetComponent<EnemyController>();
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            enemy.isEliteOrBoss = true;
            enemy.ignoreHitReactions = true;
            enemy.SetBehaviorAgentActive(false);
            CleanupHazards();
        }

        private void OnEnable()
        {
            if (enemy == null) enemy = GetComponent<EnemyController>();
            enemy.ignoreHitReactions = true;
            enemy.OnDied += HandleDeath;
            if (bindHealthHud && Application.isPlaying) BossHealthHUD.EnsureInstance()?.Bind(enemy, "BOSS 112", 5);
            if (useRunBossFlow && !spawnRevealStarted && enemy.IsAlive && !Project.Training.TrainingSession.IsTrainingContext)
            {
                spawnRevealStarted = spawnRevealPending = true;
                spawnRevealRoutine = StartCoroutine(RevealSpawn());
            }
        }

        private void OnDisable()
        {
            CancelSpawnReveal();
            CancelPattern();
            if (enemy != null) enemy.OnDied -= HandleDeath;
            BossHealthHUD.Instance?.Unbind(enemy);
        }

        public void ConfigureArena(float left, float right, float floor, float offscreenHeight = 23f,
            CinemachineCamera gameplayCamera = null)
        {
            arenaLeft = Mathf.Min(left, right);
            arenaRight = Mathf.Max(left, right);
            floorY = floor;
            departureHeight = Mathf.Max(hoverHeight + 4f, offscreenHeight);
            spawnReturnCamera = gameplayCamera;
        }

        private IEnumerator RevealSpawn()
        {
            // The spawner assigns the arena, camera and final position after OnEnable.
            yield return null;
            while (enemy != null && enemy.IsAlive && !enemy.IsCombatReady)
                yield return null;
            spawnRevealCamera = CameraManager.instance;
            if (enemy != null && enemy.IsAlive && spawnRevealCamera != null
                && spawnRevealCamera.TryBeginBossSpawnFocus(transform, spawnPanDuration, spawnReturnCamera))
            {
                float elapsed = 0f;
                while (enemy != null && enemy.IsAlive && elapsed < spawnPanDuration + spawnRevealDuration)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (spawnRevealCamera != null)
                    spawnRevealCamera.EndBossSpawnFocus(transform, spawnReturnDuration);
                while (spawnRevealCamera != null && spawnRevealCamera.IsBossSpawnFocusActive(transform))
                    yield return null;
            }
            spawnRevealCamera = null;
            spawnRevealPending = false;
            spawnRevealRoutine = null;
        }

        private void CancelSpawnReveal()
        {
            if (spawnRevealRoutine != null) StopCoroutine(spawnRevealRoutine);
            spawnRevealRoutine = null;
            if (spawnRevealCamera != null) spawnRevealCamera.EndBossSpawnFocus(transform, 0f);
            spawnRevealCamera = null;
            spawnRevealPending = false;
        }

        public void SetTarget(Transform target) => targetOverride = target;

        public bool TryStartPattern(Boss112Pattern pattern)
        {
            if (!isActiveAndEnabled || enemy == null || !enemy.IsCombatReady || spawnRevealPending || IsBusy || Target == null) return false;
            if ((pattern == Boss112Pattern.DelayedSlam || pattern == Boss112Pattern.SweepingBeam) && !IsSecondPhase) return false;
            Pattern = lastPattern = pattern;
            SlamImpactCount = 0;
            CleanupHazards();
            PlayIdle();
            if (pattern == Boss112Pattern.SweepingBeam)
            {
                bool fromLeft = TargetX < (arenaLeft + arenaRight) * .5f;
                sweepStartX = fromLeft ? arenaLeft + targetInset : arenaRight - targetInset;
                sweepEndX = fromLeft ? arenaRight - targetInset : arenaLeft + targetInset;
                BeginPhase(Boss112Phase.SweepPositioning);
            }
            else if (pattern == Boss112Pattern.PrecisionBeam) BeginPhase(Boss112Phase.BeamTracking);
            else BeginSlamSequence();
            if (pattern == Boss112Pattern.PrecisionBeam) PlayCharge();
            return true;
        }

        private Transform Target => targetOverride != null ? targetOverride : PlayerCharacter.Instance != null ? PlayerCharacter.Instance.transform : null;
        private float TargetX => ClampX(Target != null ? Target.position.x : transform.position.x);
        private float ClampX(float x) => Mathf.Clamp(x, arenaLeft + targetInset, arenaRight - targetInset);

        private void FixedUpdate()
        {
            if (enemy == null || !enemy.IsAlive) return;
            if (spawnRevealPending) return;
            if (!enemy.IsCombatReady || enemy.IsGrabbed || (PlayerCharacter.Instance != null && PlayerCharacter.Instance.IsDead))
            {
                if (IsBusy || waves.Count > 0) CancelPattern();
                return;
            }
            if (!enemy.InCombat) enemy.SetInCombat(true);
            phaseAge += Time.fixedDeltaTime;
            switch (Phase)
            {
                case Boss112Phase.Idle:
                    if (autoAttack && phaseAge >= idleDelay)
                        TryStartPattern(IsSecondPhase
                            ? (lastPattern == Boss112Pattern.DelayedSlam ? Boss112Pattern.SweepingBeam
                                : lastPattern == Boss112Pattern.SweepingBeam ? Boss112Pattern.TripleSlam : Boss112Pattern.DelayedSlam)
                            : (lastPattern == Boss112Pattern.PrecisionBeam ? Boss112Pattern.HeavySlam : Boss112Pattern.PrecisionBeam));
                    break;
                case Boss112Phase.BeamTracking:
                    Move(Vector2.MoveTowards(body.position, new Vector2(TargetX, floorY + hoverHeight), trackingSpeed * Time.fixedDeltaTime));
                    ShowBeam(body.position.x, false, false);
                    if (phaseAge >= beamTrackingDuration) { lockedX = body.position.x; BeginPhase(Boss112Phase.BeamLocked); }
                    break;
                case Boss112Phase.BeamLocked:
                    ShowBeam(lockedX, false, true);
                    if (phaseAge >= beamLockDuration) BeginPhase(Boss112Phase.BeamActive);
                    break;
                case Boss112Phase.BeamActive:
                    ShowBeam(lockedX, true, true);
                    if (phaseAge >= beamActiveDuration) BeginSlamSequence();
                    break;
                case Boss112Phase.SweepPositioning:
                    Move(Vector2.Lerp(phaseOrigin, new Vector2(sweepStartX, floorY + hoverHeight),
                        Mathf.SmoothStep(0f, 1f, phaseAge / sweepPositionDuration)));
                    if (phaseAge >= sweepPositionDuration)
                    {
                        lockedX = sweepStartX;
                        PlayCharge();
                        BeginPhase(Boss112Phase.SweepCharging);
                    }
                    break;
                case Boss112Phase.SweepCharging:
                    ShowBeam(sweepStartX, false, phaseAge >= sweepChargeDuration - .4f);
                    if (phaseAge >= sweepChargeDuration) BeginPhase(Boss112Phase.SweepActive);
                    break;
                case Boss112Phase.SweepActive:
                    lockedX = Mathf.Lerp(sweepStartX, sweepEndX, Mathf.Clamp01(phaseAge / sweepActiveDuration));
                    Move(new Vector2(lockedX, floorY + hoverHeight));
                    ShowBeam(lockedX, true, true);
                    if (phaseAge >= sweepActiveDuration)
                    {
                        body.position = new Vector2(lockedX, floorY + hoverHeight);
                        BeginSlamSequence();
                    }
                    break;
                case Boss112Phase.SlamTracking:
                    SetSlamPose(1);
                    Move(Vector2.Lerp(phaseOrigin, new Vector2(lockedX, floorY + departureHeight), Mathf.SmoothStep(0f, 1f, phaseAge / .3f)));
                    ShowLanding(true);
                    if (phaseAge >= slamTrackingDuration) BeginPhase(Boss112Phase.SlamLocked);
                    break;
                case Boss112Phase.SlamLocked:
                    Move(new Vector2(lockedX, floorY + departureHeight));
                    ShowLanding(true);
                    if (phaseAge >= slamLockDuration) BeginPhase(Boss112Phase.SlamFalling);
                    break;
                case Boss112Phase.SlamFalling:
                    SetSlamPose(2);
                    float t = Mathf.Clamp01(phaseAge / slamFallDuration);
                    float height = Pattern == Boss112Pattern.DelayedSlam ? feintHeight : 0f;
                    Move(Vector2.Lerp(phaseOrigin, new Vector2(lockedX, floorY + height), t * t));
                    if (phaseAge >= slamFallDuration)
                    {
                        if (Pattern == Boss112Pattern.DelayedSlam)
                        {
                            body.position = new Vector2(lockedX, floorY + feintHeight);
                            BeginPhase(Boss112Phase.SlamFeint);
                        }
                        else LandSlam();
                    }
                    break;
                case Boss112Phase.SlamFeint:
                    ShowLanding(true);
                    if (phaseAge >= feintPauseDuration) BeginPhase(Boss112Phase.SlamCommit);
                    break;
                case Boss112Phase.SlamCommit:
                    float commit = Mathf.Clamp01(phaseAge / feintCommitDuration);
                    Move(Vector2.Lerp(phaseOrigin, new Vector2(lockedX, floorY), commit * commit));
                    if (phaseAge >= feintCommitDuration) LandSlam();
                    break;
                case Boss112Phase.SlamImpact:
                    HoldFinishedLandingPose();
                    if (phaseAge >= slamImpactDuration)
                    {
                        if (Pattern == Boss112Pattern.TripleSlam && SlamImpactCount < 3)
                            BeginSlamSequence();
                        else BeginRecovery(Pattern == Boss112Pattern.DelayedSlam ? delayedRecoveryDuration : slamRecoveryDuration);
                    }
                    break;
                case Boss112Phase.Recovery:
                    // The punish window belongs to the landing point, never the arena center.
                    Move(new Vector2(lockedX, floorY));
                    HoldFinishedLandingPose();
                    if (phaseAge >= recoveryDuration) BeginRetreat();
                    break;
                case Boss112Phase.Retreat:
                    PlayIdle();
                    float retreat = Mathf.Clamp01(phaseAge / retreatDuration);
                    Move(Vector2.Lerp(phaseOrigin, retreatTarget, Mathf.SmoothStep(0f, 1f, retreat)));
                    if (phaseAge >= retreatDuration) BeginPhase(Boss112Phase.Idle);
                    break;
            }
        }

        private void ShowBeam(float x, bool active, bool locked) =>
            beam.Show(new Vector2(x, body.position.y + beamOriginHeight), floorY, active, locked, phaseAge);

        private void BeginSlamSequence()
        {
            CloseAttackWindows();
            stunPoseHeld = false;
            body.linearVelocity = Vector2.zero;
            // Standalone attacks and laser combos share the exact same slam sequence.
            lockedX = TargetX;
            SetSlamPose(1);
            BeginPhase(Boss112Phase.SlamTracking);
            ShowLanding(true);
        }

        private void SnapToFloor()
        {
            body.position = new Vector2(lockedX, floorY);
            transform.position = new Vector3(lockedX, floorY, transform.position.z);
            Physics2D.SyncTransforms();
        }

        private void LandSlam()
        {
            SlamImpactCount++;
            SnapToFloor();
            PlayLandingAnimation();
            landingMarker.enabled = false;
            impactDamage.SetHitboxWindowActive(true);
            impactCollider.enabled = true;
            PlayImpact();
            if (Pattern == Boss112Pattern.DelayedSlam && shockwavePrefab != null)
            {
                var targets = new HashSet<int>();
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    var wave = Instantiate(shockwavePrefab);
                    Vector2 origin = new Vector2(lockedX + direction * impactCollider.size.x * .5f, floorY);
                    wave.Initialize(enemy, origin, direction, shockwaveSpeed, arenaRight - arenaLeft, targets);
                    waves.Add(wave);
                }
            }
            BeginPhase(Boss112Phase.SlamImpact);
        }

        private void PlayCharge()
        {
            if (warningAudio != null && beamChargeSfx != null)
                GameAudioManager.EnsureInstance().PlayControlledSFX(warningAudio, beamChargeSfx, .45f);
        }

        private void PlayImpact()
        {
            if (impactSfx != null) GameAudioManager.EnsureInstance().PlayHitSFX(impactSfx, .65f);
            CameraManager.instance?.ShakeByImpact(DamageImpactTier.Heavy);
        }

        private void BeginPhase(Boss112Phase phase)
        {
            Phase = phase;
            phaseAge = 0f;
            phaseOrigin = body != null ? body.position : (Vector2)transform.position;
            if ((phase == Boss112Phase.BeamLocked || phase == Boss112Phase.SlamLocked || phase == Boss112Phase.SlamFeint)
                && warningAudio != null && lockSfx != null)
                GameAudioManager.EnsureInstance().PlayControlledSFX(warningAudio, lockSfx, .55f);
        }

        private void BeginRecovery(float duration)
        {
            CloseAttackWindows();
            recoveryDuration = duration;
            BeginPhase(Boss112Phase.Recovery);
        }

        private void BeginRetreat()
        {
            stunPoseHeld = false;
            float away = Target != null && Target.position.x < body.position.x ? 1f : -1f;
            float x = ClampX(body.position.x + away * retreatDistance);
            if (Mathf.Abs(x - body.position.x) < .5f) x = ClampX(body.position.x - away * retreatDistance);
            retreatTarget = new Vector2(x, floorY + hoverHeight);
            BeginPhase(Boss112Phase.Retreat);
        }

        private void Move(Vector2 position)
        {
            body.linearVelocity = Vector2.zero;
            body.MovePosition(position);
        }

        private void PlayLandingAnimation()
        {
            stunPoseHeld = false;
            if (animator == null || !animator.isActiveAndEnabled) return;
            manualPose = true;
            animator.speed = 1f;
            animator.Play("Base Layer.Slam", 0, 3f / 9f);
            animator.Update(0f);
        }

        private void HoldFinishedLandingPose()
        {
            if (stunPoseHeld || animator == null || !animator.isActiveAndEnabled) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Base Layer.Slam") || state.normalizedTime < 8f / 9f) return;
            SetSlamPose(8);
            stunPoseHeld = true;
        }

        private void SetSlamPose(int frame)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            manualPose = true;
            animator.speed = 0f;
            animator.Play("Base Layer.Slam", 0, Mathf.Clamp(frame, 0, 8) / 9f);
            animator.Update(0f);
        }

        private void PlayIdle()
        {
            if (!manualPose || animator == null || !animator.isActiveAndEnabled) return;
            manualPose = false;
            animator.speed = 1f;
            animator.Play("Base Layer.Idle", 0, 0f);
        }

        private void ShowLanding(bool locked)
        {
            if (chargeTelegraph != null)
            {
                landingMarker.enabled = false;
                return;
            }
            landingMarker.enabled = true;
            landingMarker.transform.position = new Vector3(lockedX, floorY + .06f, transform.position.z);
            landingMarker.color = locked ? new Color(1f, .85f, .2f, .7f) : new Color(.1f, .03f, .12f, .65f);
        }

        private void LateUpdate()
        {
            if (chargeTelegraph != null)
            {
                if (enemy != null && enemy.IsCombatReady && (Phase == Boss112Phase.SlamTracking
                    || Phase == Boss112Phase.SlamLocked || Phase == Boss112Phase.SlamFeint))
                {
                    float progress = Phase == Boss112Phase.SlamTracking ? phaseAge / SlamWarningDuration
                        : Phase == Boss112Phase.SlamLocked ? (slamTrackingDuration + phaseAge) / SlamWarningDuration
                        : phaseAge / Mathf.Max(.001f, feintPauseDuration);
                    Vector2 size = Vector2.Scale(impactCollider.size, impactCollider.transform.lossyScale);
                    float width = Mathf.Abs(size.x);
                    Vector3 end = new Vector3(lockedX, floorY, transform.position.z);
                    float height = Phase == Boss112Phase.SlamFeint ? feintHeight + Mathf.Abs(size.y) : departureHeight;
                    chargeTelegraph.ShowLane(end + Vector3.up * height, end, width, progress);
                }
                else chargeTelegraph.Hide();
            }
            if (landingMarker != null && landingMarker.enabled)
                landingMarker.transform.position = new Vector3(lockedX, floorY + .06f, transform.position.z);
        }

        private void CloseAttackWindows()
        {
            chargeTelegraph?.Hide();
            if (beam != null) beam.Hide();
            if (impactCollider != null) impactCollider.enabled = false;
            if (impactDamage != null) impactDamage.SetHitboxWindowActive(false);
            if (landingMarker != null) landingMarker.enabled = false;
            if (warningAudio != null) warningAudio.Stop();
        }

        private void CleanupHazards()
        {
            CloseAttackWindows();
            foreach (var wave in waves) if (wave != null) wave.Cancel();
            waves.Clear();
        }

        public void CancelPattern()
        {
            SlamImpactCount = 0;
            stunPoseHeld = false;
            CleanupHazards();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                if (enemy != null && enemy.IsAlive && gameObject.activeInHierarchy)
                    body.position = new Vector2((arenaLeft + arenaRight) * .5f, floorY + .1f);
            }
            PlayIdle();
            BeginPhase(Boss112Phase.Idle);
        }

        private void HandleDeath()
        {
            CancelSpawnReveal();
            autoAttack = false;
            CancelPattern();
            if (useRunBossFlow && !Project.Training.TrainingSession.IsTrainingContext)
                BossDeathSequence.EnsureInstance().BeginFromBoss(enemy);
        }

        public void ForceClearCombatHazards()
        {
            autoAttack = false;
            CancelPattern();
        }
    }
}
