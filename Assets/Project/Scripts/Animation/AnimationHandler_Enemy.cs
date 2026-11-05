using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationHandler_Enemy : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public Animator anim;
    public EnemyController enemyController;
    [Tooltip("Play the authored looping shield clip instead of holding a single pose.")]
    public bool animateShieldGuard;
    [Tooltip("Use separate windup and firing clips for ranged attacks.")]
    public bool useRangedAttackPoses;
    [Tooltip("Use separate preparation and strike clips for the supplied bite animation.")]
    public bool useMeleeAttackPoses;
    public bool gifShieldCharge;
    private bool gifChargePreparing, gifChargeMoving, gifChargeStopping;
    private float gifChargeClock;

    [System.Serializable]
    public class ReactionAnimationProfile
    {
        public bool enabled;
        public bool finishGetUpAnimation;
        public string lightHurt = "Base Layer.Hurt";
        public string heavyHurt = "Base Layer.Hurt";
        public string launch = "Base Layer.Launch";
        public string airHurt = "Base Layer.AirHurt";
        public string fall = "Base Layer.Fall";
        public string knockdown = "Base Layer.Knockdown";
        public string getUp = "Base Layer.GetUp";
        public string grabbed = "Base Layer.Hurt";
        [Min(0f)] public float replayInterval = 0.12f;
        [Range(0f, 1f)] public float minimumReplayProgress = 0.2f;
        [Tooltip("Keeps the ground hurt visual across short hit gaps without delaying AI or movement.")]
        [Range(0f, 0.15f)] public float comboReleaseDelay = 0.08f;
        [Range(0f, 0.1f)] public float transitionSeconds = 0.035f;
        [Range(0.1f, 2f)] public float minimumPlaybackSpeed = 0.65f;
        [Range(0.1f, 2f)] public float maximumPlaybackSpeed = 1.25f;
        [Header("Hit Accents (visual only)")]
        [Tooltip("Optional source character whose normal combo uses the stronger finisher accent.")]
        public CharacterObject comboAccentCharacter;
        public string comboFinisherStep = "Attack3";
        [Min(0f)] public float lightRecoil = 0.07f;
        [Min(0f)] public float finisherRecoil = 0.18f;
        [Min(0.01f)] public float lightAccentDuration = 0.12f;
        [Min(0.01f)] public float finisherAccentDuration = 0.22f;
        [Range(0f, 25f)] public float lightLean = 5f;
        [Range(0f, 25f)] public float finisherLean = 13f;
        [Range(0f, 0.15f)] public float accentStretch = 0.04f;
        [Range(0f, 0.5f)] public float finisherPoseStart = 0.25f;
    }

    [Header("Reaction Animation (visual only)")]
    public ReactionAnimationProfile reactionAnimations = new ReactionAnimationProfile();
    private HitReactionData visualReaction;
    private bool visualAirFollowup;
    private bool visualLandedKnockdown;
    private int reactionPoseHash;
    private float reactionPoseStartedAt = float.NegativeInfinity;
    private bool comboAccent;
    private bool comboFinisher;
    private bool hitAccent;
    private bool reactionReturnPending;
    private float reactionReturnAt;
    private float accentAge;
    private float accentDirection;

    [Header("Lifecycle Animation (optional until clips are assigned)")]
    [Tooltip("Full layer-0 state path, for example Base Layer.SpawnGround. Leave empty for legacy setup.")]
    public string spawnGroundState = "";
    public string spawnFallState = "";
    public string deathState = "";
    [Tooltip("Required return state when a spawn animation is configured.")]
    public string activeState = "Base Layer.Idle";

    private int lifecycleAnimationHash;
    private int lifecycleAnimationFrame;
    private bool lifecycleAnimationPlaying;
    private bool bodyChargeAnimationPlaying;
    private readonly List<AnimatorClipInfo> attackPoseClips = new List<AnimatorClipInfo>(2);
    private bool shieldPosePlaying;
    private float speedBeforeShieldPose;
    private AnimatorCullingMode previousCulling;

    public bool HasLifecycleDeath => !string.IsNullOrWhiteSpace(deathState);

    public void SetLifecyclePhase(EnemyLifecycleStateId phase)
    {
        if (anim == null)
            anim = GetComponent<Animator>();
        int hash = Animator.StringToHash("LifecyclePhase");
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var parameter in anim.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Int)
                {
                    anim.SetInteger(hash, (int)phase);
                    break;
                }
        }
    }

    public bool PlayLifecycleAnimation(string statePath)
    {
        EndLifecycleAnimation();
        if (string.IsNullOrWhiteSpace(statePath))
            return false;
        if (anim == null)
            anim = GetComponent<Animator>();
        int hash = Animator.StringToHash(statePath);
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null || !anim.HasState(0, hash))
        {
            Debug.LogWarning($"{name}: Missing lifecycle Animator state '{statePath}'. Using lifecycle fallback.", this);
            return false;
        }

        previousCulling = anim.cullingMode;
        FinishReactionPresentation(false);
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.speed = 1f;
        lifecycleAnimationHash = hash;
        lifecycleAnimationFrame = Time.frameCount;
        lifecycleAnimationPlaying = true;
        anim.Play(hash, 0, 0f);
        return true;
    }

    public bool IsLifecycleAnimationComplete()
    {
        if (!lifecycleAnimationPlaying)
            return true;
        if (anim == null || !anim.isActiveAndEnabled || Time.frameCount <= lifecycleAnimationFrame)
            return false;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash == lifecycleAnimationHash && !state.loop && state.normalizedTime >= 1f;
    }

    public void EndLifecycleAnimation()
    {
        if (lifecycleAnimationPlaying && anim != null)
            anim.cullingMode = previousCulling;
        lifecycleAnimationPlaying = false;
    }

    public void ReturnFromSpawn()
    {
        bool hadAnimation = lifecycleAnimationPlaying;
        EndLifecycleAnimation();
        if (hadAnimation && anim != null && anim.runtimeAnimatorController != null)
        {
            int hash = Animator.StringToHash(activeState);
            if (anim.HasState(0, hash))
                anim.Play(hash, 0, 0f);
            else
                Debug.LogWarning($"{name}: Missing spawn return state '{activeState}'.", this);
        }
    }

    // =========================================================
    // Animator Hashes
    // =========================================================
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int takeDamageHash = Animator.StringToHash("TakeDamage");
    private readonly int dieHash = Animator.StringToHash("Die");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int attackCleaveHash = Animator.StringToHash("AttackCleave");
    private readonly int attackJumpSmashHash = Animator.StringToHash("AttackJumpSmash");
    private readonly int attackChargeHash = Animator.StringToHash("AttackCharge");
    private readonly int chargePhaseHash = Animator.StringToHash("ChargePhase");
    private readonly int jumpPhaseHash = Animator.StringToHash("JumpPhase");
    private readonly int reactionStateHash = Animator.StringToHash("ReactionState");
    private readonly int manualReactionHash = Animator.StringToHash("ManualReactionAnimation");
    private readonly int reactionPlaybackHash = Animator.StringToHash("ReactionPlaybackSpeed");
    private readonly int airborneHash = Animator.StringToHash("IsAirborne");
    private readonly int grabbedHash = Animator.StringToHash("IsGrabbed");
    private readonly int deadHash = Animator.StringToHash("Dead");
    private readonly int bossPhaseHash = Animator.StringToHash("BossPhase");


    private HashSet<int> animParamHashes;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();

        animParamHashes = new HashSet<int>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var param in anim.parameters)
            {
                animParamHashes.Add(param.nameHash);
            }
        }
    }

    void OnEnable()
    {
        if (enemyController != null)
        {
            enemyController.OnDamaged += HandleDamageTaken;
            enemyController.OnDied += HandleDeath;
        }
    }

    void OnDisable()
    {
        gifChargePreparing = gifChargeMoving = gifChargeStopping = false;
        FinishReactionPresentation(false);
        if (enemyController != null)
        {
            enemyController.OnDamaged -= HandleDamageTaken;
            enemyController.OnDied -= HandleDeath;
        }
    }

    void LateUpdate()
    {
        TickReactionReturn(Time.time);
        TickGifShieldCharge(Time.deltaTime);
    }

    private void TickGifShieldCharge(float deltaTime)
    {
        if (!gifShieldCharge || (!gifChargePreparing && !gifChargeMoving && !gifChargeStopping)) return;
        if (lifecycleAnimationPlaying || (enemyController != null && (!enemyController.IsGameplayActive
            || enemyController.CurrentReactionState != EnemyReactionState.None)))
        {
            gifChargePreparing = gifChargeMoving = gifChargeStopping = false;
            return;
        }
        if (anim == null || !anim.isActiveAndEnabled || anim.speed <= 0f) return;
        gifChargeClock += deltaTime * anim.speed;
        if (gifChargeStopping && gifChargeClock >= .3f)
        {
            gifChargeStopping = false;
            anim.Play(Animator.StringToHash(activeState), 0, 0f);
            return;
        }
        float phase = gifChargePreparing ? 0f : gifChargeMoving
            ? (1f + Mathf.Repeat(gifChargeClock, .6f) / .15f) / 7f
            : (5f + gifChargeClock / .15f) / 7f;
        anim.Play(Animator.StringToHash("Base Layer.BodyChargeRush"), 0, Mathf.Min(phase, .999f));
    }

    public void PlayShieldChargeAnticipation()
    {
        if (!gifShieldCharge || anim == null || !anim.isActiveAndEnabled) return;
        EndShieldGuardPose();
        gifChargePreparing = true;
        gifChargeMoving = gifChargeStopping = false;
        gifChargeClock = 0f;
        bodyChargeAnimationPlaying = true;
        anim.Play(Animator.StringToHash("Base Layer.BodyChargeRush"), 0, 0f);
    }

    private void TickReactionReturn(float now)
    {
        if (!reactionReturnPending) return;
        if (!UsesReactionAnimationProfile || lifecycleAnimationPlaying
            || (enemyController != null && !enemyController.IsAlive))
        {
            FinishReactionPresentation(false);
            return;
        }
        if (now >= reactionReturnAt)
            FinishReactionPresentation(true);
    }

    // =========================================================
    // Event Handlers
    // =========================================================
    private void HandleDamageTaken(HitReactionData data)
    {
        if (enemyController != null && !enemyController.LastHitAppliedReaction)
            return;

        // ReactionState drives Hurt/Launch/Fall/Knockdown/GetUp.
        if (animParamHashes != null && animParamHashes.Contains(reactionStateHash))
            return;

        TriggerTakeDamage();
    }

    private void HandleDeath()
    {
        FinishReactionPresentation(false);
        EndShieldGuardPose();
        // The lifecycle starts the authored death state after all death listeners clean up.
        if (!HasLifecycleDeath)
            TriggerDie();
    }

    // =========================================================
    // Public Animation Triggers // Check hashes to prevent warnings
    // =========================================================
    public void TriggerTakeDamage()
    {
        if (animParamHashes != null && animParamHashes.Contains(takeDamageHash))
            anim.SetTrigger(takeDamageHash);
    }

    public void TriggerDie()
    {
        FinishReactionPresentation(false);
        if (animParamHashes != null && animParamHashes.Contains(deadHash))
            anim.SetBool(deadHash, true);

        if (animParamHashes != null && animParamHashes.Contains(dieHash))
            anim.SetTrigger(dieHash);
    }

    public void TriggerAttack()
    {
        if (reactionReturnPending) FinishReactionPresentation(true);
        if (animParamHashes != null && animParamHashes.Contains(attackHash))
            anim.SetTrigger(attackHash);
    }

    /// <summary>
    /// Fires a pattern-specific trigger when present; otherwise falls back to Attack.
    /// Safe no-op if the animator controller is still a stub.
    /// </summary>
    public void TriggerBossPattern(Project.AIBehaviour.Boss.BossPatternId patternId)
    {
        int hash = patternId switch
        {
            Project.AIBehaviour.Boss.BossPatternId.HeavyCleave => attackCleaveHash,
            Project.AIBehaviour.Boss.BossPatternId.JumpSmash => attackJumpSmashHash,
            Project.AIBehaviour.Boss.BossPatternId.BullCharge => attackChargeHash,
            _ => attackHash
        };

        if (animParamHashes != null && animParamHashes.Contains(hash))
        {
            anim.SetTrigger(hash);
            return;
        }

        TriggerAttack();
    }

    // Motion time for Charge: 0 = prepare, .3 = rush, .5..1 = stop; -1 exits the state.
    public void SetBossChargePhase(float phase)
    {
        if (anim != null && animParamHashes != null && animParamHashes.Contains(chargePhaseHash))
            anim.SetFloat(chargePhaseHash, phase);
    }

    // Motion time: 0 = takeoff, .25 = airborne, .6..1 = grounded impact, -1 = finished.
    public void SetBossJumpPhase(float phase)
    {
        if (anim != null && animParamHashes != null && animParamHashes.Contains(jumpPhaseHash))
            anim.SetFloat(jumpPhaseHash, phase);
    }

    public bool PlayBodyChargePose(bool rushing)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return false;
        int state = Animator.StringToHash(rushing ? "Base Layer.BodyChargeRush" : "Base Layer.BodyChargeWindup");
        if (!anim.HasState(0, state))
            return false;
        if (reactionReturnPending) FinishReactionPresentation(false);
        anim.ResetTrigger(attackHash);
        gifChargePreparing = gifChargeStopping = false;
        gifChargeMoving = gifShieldCharge && rushing;
        gifChargeClock = 0f;
        anim.Play(state, 0, gifChargeMoving ? 1f / 7f : 0f);
        bodyChargeAnimationPlaying = true;
        return true;
    }

    public void PlayShieldGuardPose(bool raised)
    {
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return;
        int state = Animator.StringToHash(animateShieldGuard && !raised
            ? activeState : "Base Layer.BodyChargeWindup");
        if (!anim.HasState(0, state))
            return;
        gifChargePreparing = gifChargeMoving = gifChargeStopping = false;
        if (reactionReturnPending) FinishReactionPresentation(false);
        bool wasPlaying = shieldPosePlaying;
        if (!shieldPosePlaying)
            speedBeforeShieldPose = anim.speed;
        shieldPosePlaying = true;
        bodyChargeAnimationPlaying = true;
        anim.ResetTrigger(attackHash);
        if (animateShieldGuard)
        {
            if (!wasPlaying || anim.GetCurrentAnimatorStateInfo(0).fullPathHash != state)
                anim.Play(state, 0, 0f);
            anim.speed = speedBeforeShieldPose;
            return;
        }
        // The existing shield clip starts raised and ends in the open anticipation pose.
        anim.Play(state, 0, raised ? 0f : 0.8f);
        anim.speed = 0f;
    }

    public void EndShieldGuardPose()
    {
        if (!shieldPosePlaying)
            return;
        shieldPosePlaying = false;
        if (anim != null)
            anim.speed = speedBeforeShieldPose;
    }

    public bool IsRangedAttackPoseComplete()
    {
        if (!bodyChargeAnimationPlaying || anim == null || !anim.isActiveAndEnabled
            || anim.runtimeAnimatorController == null)
            return true;
        var state = anim.GetCurrentAnimatorStateInfo(0);
        // Override controllers can report the source state's loop flag instead of the bite/shot clip's flag.
        anim.GetCurrentAnimatorClipInfo(0, attackPoseClips);
        bool looping = attackPoseClips.Count > 0 && attackPoseClips[0].clip != null
            ? attackPoseClips[0].clip.isLooping : state.loop;
        return state.fullPathHash != Animator.StringToHash("Base Layer.BodyChargeRush")
            || (!looping && state.normalizedTime >= 1f);
    }

    public void EndBodyChargePose()
    {
        if (!bodyChargeAnimationPlaying)
            return;
        bool wasMoving = gifChargeMoving;
        bodyChargeAnimationPlaying = false;
        gifChargePreparing = gifChargeMoving = gifChargeStopping = false;
        // Death/reaction animations own the pose when an attack is interrupted.
        if (enemyController == null || !enemyController.IsGameplayActive
            || enemyController.CurrentReactionState != EnemyReactionState.None
            || anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return;
        if (gifShieldCharge && wasMoving)
        {
            gifChargeStopping = true;
            gifChargeClock = 0f;
            anim.Play(Animator.StringToHash("Base Layer.BodyChargeRush"), 0, 5f / 7f);
            return;
        }
        int idle = Animator.StringToHash(activeState);
        if (anim.HasState(0, idle))
        {
            if (reactionReturnPending) FinishReactionPresentation(false);
            anim.Play(idle, 0, 0f);
        }
    }

    public void SetBossPhase(int phase)
    {
        if (animParamHashes != null && animParamHashes.Contains(bossPhaseHash))
            anim.SetInteger(bossPhaseHash, phase);
    }

    public void SetSpeed(float speed)
    {
        if (reactionReturnPending && Mathf.Abs(speed) > 0.15f)
            FinishReactionPresentation(true);
        if (animParamHashes != null && animParamHashes.Contains(speedHash))
            anim.SetFloat(speedHash, speed);
    }

    /// <summary>Sets the Animator playback speed (0 = frozen, 1 = normal).</summary>
    public void SetAnimatorSpeed(float speed)
    {
        if (anim != null)
            anim.speed = speed;
    }

    public bool UsesReactionAnimationProfile => reactionAnimations != null && reactionAnimations.enabled
        && anim != null && animParamHashes != null && animParamHashes.Contains(manualReactionHash);

    public bool IsFinishingGetUpAnimation
    {
        get
        {
            if (!UsesReactionAnimationProfile || !reactionAnimations.finishGetUpAnimation || !visualLandedKnockdown
                || !anim.isActiveAndEnabled || reactionPoseHash != Animator.StringToHash(reactionAnimations.getUp)) return false;
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).fullPathHash == reactionPoseHash) return true;
            // The shared placeholder motion is looping; finish one authored override cycle regardless.
            return state.fullPathHash == reactionPoseHash && state.normalizedTime < 1f;
        }
    }

    public void BeginReactionAnimation(HitReactionData data, bool airborneFollowup)
    {
        reactionReturnPending = false;
        visualReaction = data;
        visualAirFollowup = airborneFollowup;
        visualLandedKnockdown = false;
        string step = data.sourceStepData?.stepName;
        comboAccent = UsesReactionAnimationProfile && reactionAnimations.comboAccentCharacter != null
            && data.sourceCharacter == reactionAnimations.comboAccentCharacter
            && data.sourceCategory == AttackCategory.Normal && !airborneFollowup
            && (data.reactionKind == EnemyReactionKind.Flinch || data.reactionKind == EnemyReactionKind.Knockback)
            && (step == "Attack1" || step == "Attack2" || step == reactionAnimations.comboFinisherStep);
        comboFinisher = comboAccent && step == reactionAnimations.comboFinisherStep;
        hitAccent = UsesReactionAnimationProfile && data.reactionKind != EnemyReactionKind.Grab;
        accentAge = 0f;
        Vector3 away = data.attackerTransform != null ? transform.position - data.attackerTransform.position
            : new Vector3(data.knockbackDirection.x, 0f, 0f);
        if (Mathf.Abs(away.x) < .001f) away.x = 1f;
        accentDirection = Mathf.Sign(transform.InverseTransformVector(new Vector3(away.x, 0f, 0f)).x);
        if (!UsesReactionAnimationProfile) return;
        EndShieldGuardPose();
        bodyChargeAnimationPlaying = false;
        if (animParamHashes.Contains(attackHash)) anim.ResetTrigger(attackHash);
    }

    public void SetReactionState(EnemyReactionState state, bool preserveVisual = false)
    {
        if (anim == null || animParamHashes == null)
            return;

        if (animParamHashes.Contains(reactionStateHash))
            anim.SetInteger(reactionStateHash, (int)state);

        if (animParamHashes.Contains(airborneHash))
            anim.SetBool(airborneHash, state == EnemyReactionState.Knockup || state == EnemyReactionState.Knockfall);

        if (animParamHashes.Contains(grabbedHash))
            anim.SetBool(grabbedHash, state == EnemyReactionState.Grabbed);

        if (!UsesReactionAnimationProfile)
        {
            reactionReturnPending = false;
            hitAccent = false;
            if (animParamHashes.Contains(manualReactionHash)) anim.SetBool(manualReactionHash, false);
            if (animParamHashes.Contains(reactionPlaybackHash)) anim.SetFloat(reactionPlaybackHash, 1f);
            reactionPoseHash = 0;
            return;
        }
        anim.SetBool(manualReactionHash, true);
        if (preserveVisual) return;
        if (state == EnemyReactionState.None)
        {
            comboAccent = false;
            hitAccent = false;
            bool hadPose = reactionPoseHash != 0;
            visualLandedKnockdown = false;
            // Bridge short gaps between ground hits without extending gameplay hitstun.
            bool groundHurt = !visualAirFollowup
                && (visualReaction.reactionKind == EnemyReactionKind.Flinch
                    || visualReaction.reactionKind == EnemyReactionKind.Knockback);
            bool canReturn = hadPose && (enemyController == null || enemyController.IsGameplayActive)
                && anim.isActiveAndEnabled && !lifecycleAnimationPlaying;
            if (canReturn && groundHurt && reactionAnimations.comboReleaseDelay > 0f)
            {
                if (!reactionReturnPending)
                    reactionReturnAt = Time.time + reactionAnimations.comboReleaseDelay;
                reactionReturnPending = true;
            }
            else
                FinishReactionPresentation(canReturn);
            return;
        }
        reactionReturnPending = false;

        bool heavyPose = comboAccent ? comboFinisher : (int)visualReaction.impactTier >= (int)DamageImpactTier.Medium;
        string hurt = heavyPose
            ? reactionAnimations.heavyHurt : reactionAnimations.lightHurt;
        string pose;
        float duration = (visualReaction.ActiveReactionFrames + visualReaction.RecoveryFrames)
            / (float)CombatCore.FrameDataConstants.LOGIC_FPS;
        switch (state)
        {
            case EnemyReactionState.ImpactFreeze:
                pose = visualReaction.reactionKind == EnemyReactionKind.Grab ? reactionAnimations.grabbed
                    : visualAirFollowup ? reactionAnimations.airHurt
                    : visualReaction.reactionKind == EnemyReactionKind.Knockup ? reactionAnimations.launch : hurt;
                break;
            case EnemyReactionState.Knockup:
                pose = visualAirFollowup ? reactionAnimations.airHurt : reactionAnimations.launch;
                duration = visualReaction.knockupFrames / (float)CombatCore.FrameDataConstants.LOGIC_FPS;
                break;
            case EnemyReactionState.Knockfall:
                pose = reactionAnimations.fall;
                duration = visualReaction.knockfallFrames / (float)CombatCore.FrameDataConstants.LOGIC_FPS;
                break;
            case EnemyReactionState.Knockdown:
                visualLandedKnockdown = true;
                pose = reactionAnimations.knockdown;
                break;
            case EnemyReactionState.Grabbed:
                pose = reactionAnimations.grabbed;
                break;
            case EnemyReactionState.Recovering:
                if (enemyController != null && enemyController.hitReactionHandler != null
                    && !enemyController.hitReactionHandler.IsGroundedForCombat && visualAirFollowup)
                    pose = reactionAnimations.fall;
                else if (visualLandedKnockdown || (reactionAnimations.finishGetUpAnimation
                    && reactionPoseHash == Animator.StringToHash(reactionAnimations.fall)
                    && enemyController != null && enemyController.hitReactionHandler != null
                    && enemyController.hitReactionHandler.IsGroundedForCombat))
                {
                    visualLandedKnockdown = true;
                    pose = reactionAnimations.getUp;
                }
                else
                    return; // Ground flinch recovery continues the current hurt clip.
                duration = visualReaction.RecoveryFrames / (float)CombatCore.FrameDataConstants.LOGIC_FPS;
                break;
            default:
                pose = visualAirFollowup ? reactionAnimations.airHurt : hurt;
                break;
        }
        PlayReactionPose(pose, state == EnemyReactionState.ImpactFreeze, duration);
    }

    private void PlayReactionPose(string statePath, bool impact, float duration)
    {
        if (!anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        int hash = Animator.StringToHash(string.IsNullOrWhiteSpace(statePath) ? reactionAnimations.lightHurt : statePath);
        if (!anim.HasState(0, hash)) hash = Animator.StringToHash("Base Layer.Hurt");
        if (!anim.HasState(0, hash)) return;
        var current = anim.GetCurrentAnimatorStateInfo(0);
        bool samePose = reactionPoseHash == hash;
        bool forceFinisherPose = impact && comboAccent && comboFinisher;
        if (samePose && !forceFinisherPose && (!impact || Time.time - reactionPoseStartedAt < reactionAnimations.replayInterval
            || (current.fullPathHash == hash && current.normalizedTime < reactionAnimations.minimumReplayProgress)))
            return;

        if (animParamHashes.Contains(reactionPlaybackHash)) anim.SetFloat(reactionPlaybackHash, 1f);
        if (impact)
        {
            // Sample the hit pose before ReactionHandler freezes the animator for hitstop.
            float poseStart = forceFinisherPose ? reactionAnimations.finisherPoseStart : 0f;
            anim.Play(hash, 0, poseStart);
            anim.Update(0f);
            var clips = anim.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0 && !clips[0].clip.isLooping && duration > 0f
                && animParamHashes.Contains(reactionPlaybackHash))
                anim.SetFloat(reactionPlaybackHash, Mathf.Clamp(clips[0].clip.length * (1f - poseStart) / duration,
                    reactionAnimations.minimumPlaybackSpeed,
                    Mathf.Max(reactionAnimations.minimumPlaybackSpeed, reactionAnimations.maximumPlaybackSpeed)));
        }
        else
            anim.CrossFadeInFixedTime(hash, reactionAnimations.transitionSeconds, 0, 0f);
        reactionPoseHash = hash;
        reactionPoseStartedAt = Time.time;
    }

    private void FinishReactionPresentation(bool returnToActive)
    {
        reactionReturnPending = false;
        reactionPoseHash = 0;
        comboAccent = false;
        hitAccent = false;
        visualLandedKnockdown = false;
        if (anim == null || animParamHashes == null || anim.runtimeAnimatorController == null) return;
        if (animParamHashes.Contains(manualReactionHash)) anim.SetBool(manualReactionHash, false);
        if (animParamHashes.Contains(reactionPlaybackHash)) anim.SetFloat(reactionPlaybackHash, 1f);
        if (returnToActive && anim.isActiveAndEnabled && anim.HasState(0, Animator.StringToHash(activeState)))
            anim.CrossFadeInFixedTime(activeState, reactionAnimations.transitionSeconds, 0);
    }

    public void SampleComboHitAccent(float deltaTime, out Vector3 offset, out Quaternion rotation, out Vector3 scale)
    {
        offset = Vector3.zero;
        rotation = Quaternion.identity;
        scale = Vector3.one;
        if (!hitAccent || !UsesReactionAnimationProfile) return;
        float duration = comboFinisher ? reactionAnimations.finisherAccentDuration : reactionAnimations.lightAccentDuration;
        float decay = 1f - Mathf.Clamp01(accentAge / Mathf.Max(.01f, duration));
        decay *= decay;
        float distance = comboFinisher ? reactionAnimations.finisherRecoil : reactionAnimations.lightRecoil;
        float lean = comboFinisher ? reactionAnimations.finisherLean : reactionAnimations.lightLean;
        offset.x = accentDirection * distance * decay;
        rotation = Quaternion.Euler(0f, 0f, -accentDirection * lean * decay);
        float stretch = reactionAnimations.accentStretch * decay * (comboFinisher ? 1f : .5f);
        scale = new Vector3(1f + stretch, 1f - stretch * .6f, 1f);
        accentAge += Mathf.Max(0f, deltaTime);
    }
}
