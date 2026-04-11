using UnityEngine;
using CombatCore;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Combat engine for basic attacks. Handles hitbox activation, motion processing,
/// damage application, combo step management, and hit detection.
///
/// Phase tracking (Startup→Active→Recovery→Cancel→Done) is now driven by
/// PlayerAttackState via FrameDataPlayerState. This class focuses on:
///   - Hitbox activation/deactivation based on frame index
///   - Motion curve processing
///   - Combo step management (chain, reset, preserve)
///   - Hit detection via OverlapBox
///   - Hitstop handling
/// </summary>
public class PlayerBasicAttack : MonoBehaviour, ICharacterCombat
{
    public enum AttackFrameState
    {
        None,
        Windup,
        Impact,
        Recovery
    }

    private enum AttackComboMode
    {
        Ground,
        AirNeutral
    }

    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerAnimator animHandler;
    public PlayerMovement movement;
    public GameObject meleeHitbox;
    public MeleeHitboxController meleeHitboxController;
    public MeleeHitboxDamage meleeHitboxDamage;

    // Runtime state
    private int comboStep = 0;
    private int currentAttackFrame = 0;
    private float logicTimeCounter = 0f;
    private int hitstopFramesLeft = 0;
    private AttackFrameState currentFrameState = AttackFrameState.None;

    private float lastAttackInputTime = -999f;
    private AttackComboMode currentComboMode = AttackComboMode.Ground;
    
    // Cached Data for current step
    private AttackStepData currentStepData;
    private int cachedPhase1Frames;
    private int cachedPhase2Frames;
    private int cachedPhase3Frames;
    
    // Prevent 'Chain Skip' bug where new attack consumes remaining 'targetFrame' ticks 
    // from the previous attack's while loop.
    private bool shouldResetUpdateLoop = false;
    
    // Track Hitbox state
    private int hitboxActiveCount = 0;
    
    // Layer mask for enemy detection via OverlapBox
    [Header("Detection")]
    public LayerMask enemyLayerMask;
    private HashSet<EnemyController> hitEnemiesThisStep = new HashSet<EnemyController>();

    private bool isPlungeAttacking = false;
    private bool plungeImpactTriggered = false;
    private GrabHitbox currentGrabHitbox;
    private EnemyController currentGrabbedEnemy;

    public event System.Action OnComboFinished;
    public event System.Action<AttackStepData> OnBranchToSubStep;

    private bool isExecutingSubStep = false;
    private List<AttackStepData> activeSubSteps;
    private int currentSubStepIndex = 0;

    public bool IsExecutingSubStep() { return isExecutingSubStep; }

    void Awake()
    {
        playerCharacter = playerCharacter ?? GetComponent<PlayerCharacter>();
        animHandler = animHandler ?? GetComponentInChildren<PlayerAnimator>();
        movement = movement ?? GetComponent<PlayerMovement>();
        if (meleeHitbox != null)
        {
            meleeHitboxController = meleeHitboxController ?? meleeHitbox.GetComponent<MeleeHitboxController>();
            meleeHitboxDamage = meleeHitboxDamage ?? meleeHitbox.GetComponent<MeleeHitboxDamage>();
        }
    }

    void Start()
    {
        if (meleeHitbox != null) meleeHitbox.SetActive(false);
    }

    void OnEnable()
    {
        if (movement != null)
        {
            movement.OnDashStart += HandleDashStart;
            movement.OnJumpStart += HandleJumpStart;
            movement.OnLand += HandlePlungeLanding;
            movement.OnLand += HandleLandingComboReset;
        }
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.OnDashStart -= HandleDashStart;
            movement.OnJumpStart -= HandleJumpStart;
            movement.OnLand -= HandlePlungeLanding;
            movement.OnLand -= HandleLandingComboReset;
        }

        ReleaseGrabbedEnemy();
    }

    void Update()
    {
        if (IsAttacking()) return;

        if (comboStep > 0)
        {
            float delay = GetComboResetDelay();

            // Guard: Air combo uses a specific character-defined timeout when cancelled/finished
            if (currentComboMode == AttackComboMode.AirNeutral && !isPlungeAttacking)
            {
                if (playerCharacter != null && playerCharacter.characterObject != null)
                    delay = playerCharacter.characterObject.maxAirComboTime;
            }

            if (Time.time - lastAttackInputTime > delay)
            {
                ResetCombo();
            }
        }
    }

    void FixedUpdate()
    {
        if (hitstopFramesLeft > 0)
        {
            hitstopFramesLeft--;
            HandleAnimatorHitstop();
            return;
        }

        if (IsAttacking())
        {
            // Only run legacy processing if NOT being driven by PlayerAttackState
            // (PlayerAttackState calls ProcessFrameFromState directly)
            if (!isDrivenByState)
                ProcessAttackFrame();
        }
    }

    private void CombatLog(string message)
    {
        if (playerCharacter != null && playerCharacter.StateMachine != null && playerCharacter.StateMachine.showCombatLogs)
        {
            playerCharacter.StateMachine.Log("Combat", message, new Color(1f, 0.55f, 0f));
        }
    }

    // =========================================================
    // State-Driven Mode
    // When PlayerAttackState drives this engine, it calls these methods
    // instead of the engine running its own FixedUpdate frame loop.
    // =========================================================

    private bool isDrivenByState = false;

    /// <summary>
    /// Called by PlayerAttackState to signal that phase tracking is external.
    /// </summary>
    public void SetDrivenByState(bool driven)
    {
        isDrivenByState = driven;
    }

    /// <summary>
    /// Returns the current AttackStepData (used by PlayerAttackState.GetFrameData).
    /// </summary>
    public AttackStepData GetCurrentStepData()
    {
        return currentStepData;
    }

    /// <summary>
    /// Called by PlayerAttackState each logic frame to process hitbox/motion/VFX.
    /// The phase is determined by the state's FrameDataPlayerState, not internally.
    /// </summary>
    public void ProcessFrameFromState(int frame, ActionPhase phase)
    {
        if (HasActiveHitstop)
        {
            HandleAnimatorHitstop();
            return;
        }

        currentAttackFrame = frame;

        RestoreAnimatorSpeedIfNeeded();

        // Map ActionPhase to internal tracking
        switch (phase)
        {
            case ActionPhase.Startup:
                currentFrameState = AttackFrameState.Windup;
                break;

            case ActionPhase.Active:
                if (currentFrameState != AttackFrameState.Impact)
                    currentFrameState = AttackFrameState.Impact;

                // Process hitframes within active phase
                ProcessHitFrames(frame);
                break;

            case ActionPhase.Recovery:
            case ActionPhase.Cancel:
                if (currentFrameState != AttackFrameState.Recovery)
                {
                    currentFrameState = AttackFrameState.Recovery;
                    DisableHitbox(true);
                }
                break;
        }

        // Motion Processing (MotionFrameData sub-component)
        ProcessMotion(frame);

        // Perform physics overlap check while any hitbox is active
        if (hitboxActiveCount > 0 && meleeHitbox != null && meleeHitbox.activeSelf)
        {
            PerformHitboxOverlapCheck();
        }
    }

    /// <summary>
    /// Called by PlayerAttackState when entering Recovery phase.
    /// </summary>
    public void OnRecoveryPhaseFromState()
    {
        DisableHitbox(true);
    }

    /// <summary>
    /// Attempts to chain to the next combo step. Returns true if successful.
    /// Called by PlayerAttackState during Recovery/Cancel phases.
    /// </summary>
    public bool TryChainCombo()
    {
        if (isExecutingSubStep && activeSubSteps != null)
        {
            if (currentSubStepIndex + 1 < activeSubSteps.Count)
            {
                currentSubStepIndex++;
                TriggerSubStep(activeSubSteps[currentSubStepIndex]);
                return true;
            }
        }

        AttackComboMode requestedMode = GetRequestedComboMode();
        BasicAttackComboData comboData = GetComboData(requestedMode);
        if (comboData == null) return false;

        int maxCombo = comboData.GetMaxCombo();
        if (maxCombo <= 0) return false;

        if (requestedMode == currentComboMode && comboStep < maxCombo)
        {
            isExecutingSubStep = false;
            StartNextAttack(currentComboMode);
            return true;
        }
        else if (requestedMode != currentComboMode)
        {
            isExecutingSubStep = false;
            comboStep = 0;
            StartNextAttack(requestedMode);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether air combo progress should be preserved when the action completes
    /// (player still airborne with combo steps remaining).
    /// </summary>
    public bool ShouldPreserveAirComboOnComplete()
    {
        if (isPlungeAttacking) return false;
        if (currentComboMode != AttackComboMode.AirNeutral) return false;
        if (movement == null || movement.IsGrounded()) return false;

        BasicAttackComboData comboData = GetComboData(AttackComboMode.AirNeutral);
        int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;
        return comboStep < maxCombo;
    }

    // =========================================================
    // Hitframe Processing (extracted from ProcessSingleLogicFrame)
    // =========================================================

    private void ProcessHitFrames(int frame)
    {
        if (currentStepData == null) return;

        int frameInActivePhase = frame - cachedPhase1Frames - 1;

        if (HasStructuredHitboxes(currentStepData))
        {
            ProcessStructuredHitboxes(frameInActivePhase);
            return;
        }

        ProcessLegacyHitFrames(frameInActivePhase);
    }

    private void ProcessStructuredHitboxes(int frameInActivePhase)
    {
        List<HitboxBase> hitboxes = currentStepData.hitData.hitboxes;
        for (int i = 0; i < hitboxes.Count; i++)
        {
            HitboxBase hitbox = hitboxes[i];
            if (hitbox == null) continue;

            if (frameInActivePhase == hitbox.startFrameOffset) TriggerHitbox(hitbox);
            if (frameInActivePhase == hitbox.startFrameOffset + hitbox.activeFrames) DisableHitbox();
        }
    }

    private void ProcessLegacyHitFrames(int frameInActivePhase)
    {
        if (currentStepData.hitFrames == null) return;

        for (int i = 0; i < currentStepData.hitFrames.Length; i++)
        {
            var h = currentStepData.hitFrames[i];
            if (frameInActivePhase == h.startFrameOffset) TriggerHitFrame(legacyHitFrame: h);
            if (frameInActivePhase == h.startFrameOffset + h.activeFrames) DisableHitbox();
        }
    }

    private void TriggerHitbox(HitboxBase hitbox)
    {
        if (hitbox is NormalHitbox normalHitbox)
        {
            TriggerHitFrame(normalHitbox: normalHitbox);
            return;
        }

        if (hitbox is GrabHitbox grabHitbox)
        {
            TriggerGrabFrame(grabHitbox);
            return;
        }

        if (hitbox is ProjectileHitbox projectileHitbox)
        {
            // Future implementation: Projectile logic
            CombatLog($"Hitbox '{hitbox.HitboxTypeName}' (Projectile) triggered. Logic pending.");
            return;
        }

        CombatLog($"Hitbox '{hitbox.HitboxTypeName}' is not yet supported by the runtime attack processor.");
    }

    private bool HasStructuredHitboxes(AttackStepData step)
    {
        return step != null
            && step.hitData != null
            && step.hitData.hitboxes != null
            && step.hitData.hitboxes.Count > 0;
    }

    private bool HasAnyHitboxData(AttackStepData step)
    {
        if (HasStructuredHitboxes(step)) return true;
        return step != null && step.hitFrames != null && step.hitFrames.Length > 0;
    }

    private void ProcessMotion(int frame)
    {
        if (currentStepData == null || currentStepData.motionData == null 
            || currentStepData.motionData.keyframes == null) return;

        foreach (var kf in currentStepData.motionData.keyframes)
        {
            if (frame == kf.startFrame && movement != null)
            {
                movement.ApplyAttackMovement(kf);
            }
        }
    }

    // =========================================================
    // Native Hitstop
    // =========================================================
    public void ApplyHitstop(int frames)
    {
        if (frames > hitstopFramesLeft)
        {
            hitstopFramesLeft = frames;
            HandleAnimatorHitstop();
        }
    }

    private void HandleAnimatorHitstop()
    {
        if (animHandler != null && animHandler.GetComponent<Animator>() != null)
        {
            Animator anim = animHandler.GetComponent<Animator>();
            if (hitstopFramesLeft > 0)
            {
                anim.speed = 0f;
            }
            else
            {
                anim.speed = 1f;
            }
        }
    }

    private void RestoreAnimatorSpeedIfNeeded()
    {
        if (HasActiveHitstop)
        {
            return;
        }

        if (animHandler != null && animHandler.GetComponent<Animator>() != null)
        {
            animHandler.GetComponent<Animator>().speed = 1f;
        }
    }

    // =========================================================
    // Public Logic Methods
    // =========================================================
    public bool OnAttackInput()
    {
        AttackComboMode requestedMode = GetRequestedComboMode();
        BasicAttackComboData comboData = GetComboData(requestedMode);
        
        if (comboData == null || comboData.GetMaxCombo() <= 0) return false;
        if (!IsAttacking() && requestedMode == AttackComboMode.AirNeutral && !CanAcceptAirAttackRequest()) return false;

        lastAttackInputTime = Time.time;

        if (!IsAttacking())
        {
            if (comboStep > 0 && requestedMode != currentComboMode) ResetCombo();
            StartNextAttack(requestedMode);
            return true;
        }

        // Without input buffer window, input is ONLY accepted directly inside the cancel window
        if (currentStepData != null)
        {
            if (currentAttackFrame >= currentStepData.cancelWindowStartFrame)
            {
                int maxCombo = comboData.GetMaxCombo();
                if (requestedMode == currentComboMode && comboStep < maxCombo)
                {
                    StartNextAttack(currentComboMode);
                    return true;
                }
                else if (requestedMode != currentComboMode)
                {
                    comboStep = 0;
                    StartNextAttack(requestedMode);
                    return true;
                }
            }
        }

        return false;
    }

    private void ProcessAttackFrame()
    {
        logicTimeCounter += Time.fixedDeltaTime;
        // Use RoundToInt so the first frame fires on the first FixedUpdate
        int targetFrame = Mathf.RoundToInt(logicTimeCounter * FrameDataConstants.LOGIC_FPS);

        while (currentAttackFrame < targetFrame)
        {
            currentAttackFrame++;
            ProcessSingleLogicFrame();
            
            if (shouldResetUpdateLoop || currentFrameState == AttackFrameState.None) 
            {
                shouldResetUpdateLoop = false;
                break;
            }
        }

        // Perform physics overlap check while any hitbox is active
        if (hitboxActiveCount > 0 && meleeHitbox != null && meleeHitbox.activeSelf)
        {
            PerformHitboxOverlapCheck();
        }
    }

    private void ProcessSingleLogicFrame()
    {
        if (currentStepData == null) 
        {
            ResetCombo();
            return;
        }

        RestoreAnimatorSpeedIfNeeded();

        // Contextual state update Based on Data
        if (currentAttackFrame <= cachedPhase1Frames)
        {
            currentFrameState = AttackFrameState.Windup;
        }
        else if (currentAttackFrame <= cachedPhase2Frames)
        {
            if (currentFrameState != AttackFrameState.Impact)
            {
                currentFrameState = AttackFrameState.Impact;
            }

            ProcessHitFrames(currentAttackFrame);
        }
        else if (currentAttackFrame <= cachedPhase3Frames)
        {
            if (currentFrameState != AttackFrameState.Recovery)
            {
                currentFrameState = AttackFrameState.Recovery;
                DisableHitbox(true); 
            }
        }
        else
        {
            bool isAir = !isPlungeAttacking && currentComboMode == AttackComboMode.AirNeutral && movement != null && !movement.IsGrounded();
            BasicAttackComboData comboData = GetComboData(currentComboMode);
            int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;

            if (isAir && comboStep < maxCombo) CancelCurrentAttackButKeepComboProgress();
            else ResetCombo();

            if (isAir && animHandler != null) animHandler.RestoreAirborneState();
            return;
        }

        // Process buffered input if cancel window is open
        if (currentStepData != null && currentAttackFrame >= currentStepData.cancelWindowStartFrame)
        {
            if (Time.time - lastAttackInputTime <= 0.15f)
            {
                AttackComboMode requestedMode = GetRequestedComboMode();
                BasicAttackComboData comboData = GetComboData(requestedMode);
                int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;

                if (requestedMode == currentComboMode && comboStep < maxCombo)
                {
                    lastAttackInputTime = -999f;
                    StartNextAttack(currentComboMode);
                    return;
                }
                else if (requestedMode != currentComboMode)
                {
                    lastAttackInputTime = -999f;
                    comboStep = 0;
                    StartNextAttack(requestedMode);
                    return;
                }
            }
        }

        // Motion Processing (MotionFrameData sub-component)
        ProcessMotion(currentAttackFrame);
    }

    private void StartNextAttack(AttackComboMode attackMode)
    {
        BasicAttackComboData comboData = GetComboData(attackMode);
        if (comboData == null) return;
        
        int maxCombo = comboData.GetMaxCombo();
        if (maxCombo <= 0) return;

        if (comboStep >= maxCombo || attackMode != currentComboMode) comboStep = 0;

        currentComboMode = attackMode;
        comboStep++;
        
        // Setup Step Data Cache
        currentStepData = comboData.GetStep(comboStep);
        if (currentStepData != null)
        {
            cachedPhase1Frames = currentStepData.startupFrames;
            cachedPhase2Frames = cachedPhase1Frames + currentStepData.activeFrames;
            cachedPhase3Frames = cachedPhase2Frames + currentStepData.recoveryFrames;
        }

        currentFrameState = AttackFrameState.Windup;
        currentAttackFrame = 0; 
        logicTimeCounter = 0f;
        hitstopFramesLeft = 0; 
        hitboxActiveCount = 0;
        shouldResetUpdateLoop = true;

        if (movement != null) movement.CancelAttackMotion();

        CombatLog($"Combo Step {comboStep} Start ({attackMode})");

        string clipName = currentStepData?.animationClip != null ? currentStepData.animationClip.name : null;
        if (attackMode == AttackComboMode.AirNeutral) animHandler.TriggerAirAttack(comboStep, clipName);
        else animHandler.TriggerAttackCombo(comboStep, clipName);
    }

    private void TriggerHitFrame(NormalHitbox normalHitbox = null, AttackHitFrameData legacyHitFrame = null)
    {
        float damageMult = 1f;
        currentGrabHitbox = null;

        if (normalHitbox != null)
        {
            damageMult = normalHitbox.damageMultiplier;
            if (meleeHitboxController != null) meleeHitboxController.ApplyHitbox(normalHitbox);
        }
        else if (legacyHitFrame != null)
        {
            damageMult = legacyHitFrame.damageMultiplier;
            if (meleeHitboxController != null) meleeHitboxController.ApplyHitFrame(legacyHitFrame);
        }

        float finalDamage = GetFinalAttackDamage(damageMult);

        BasicAttackComboData comboData = GetComboData(currentComboMode);
        int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;
        bool isLastStep = (comboStep >= maxCombo);

        if (meleeHitboxDamage != null) 
            meleeHitboxDamage.SetHitData(finalDamage, isLastStep, 0f, 0f, hitbox: normalHitbox, hitFrameData: legacyHitFrame);

        CombatLog($"Hitbox Active: {currentFrameState} (Frame {currentAttackFrame})");
        EnableHitbox();
        
        // Clear the per-step hit tracking when a new hitframe activates,
        // so each distinct hitframe window can hit targets again
        hitEnemiesThisStep.Clear();
    }

    private void TriggerGrabFrame(GrabHitbox grabHitbox)
    {
        if (grabHitbox == null)
        {
            return;
        }

        currentGrabHitbox = grabHitbox;

        if (meleeHitboxController != null)
        {
            meleeHitboxController.ApplyHitbox(grabHitbox);
        }

        float finalDamage = GetFinalAttackDamage(1f);
        BasicAttackComboData comboData = GetComboData(currentComboMode);
        int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;
        bool isLastStep = comboStep >= maxCombo;

        if (meleeHitboxDamage != null)
        {
            meleeHitboxDamage.SetGrabData(finalDamage, grabHitbox, isLastStep);
        }

        CombatLog($"Grab Hitbox Active: {currentFrameState} (Frame {currentAttackFrame})");
        EnableHitbox();
        hitEnemiesThisStep.Clear();
    }

    public void EnableHitbox()
    {
        hitboxActiveCount++;
        if (meleeHitbox != null) meleeHitbox.SetActive(true);
    }

    public void DisableHitbox(bool force = false)
    {
        if (force) hitboxActiveCount = 0;
        else hitboxActiveCount--;

        if (hitboxActiveCount <= 0)
        {
            hitboxActiveCount = 0;
            currentGrabHitbox = null;
            if (meleeHitbox != null) meleeHitbox.SetActive(false);
        }
    }

    public void ResetCombo()
    {
        bool wasAirCombo = (currentComboMode == AttackComboMode.AirNeutral && comboStep > 0 && !isPlungeAttacking);

        if (wasAirCombo && movement != null && !movement.IsGrounded())
            movement.TriggerAirComboCooldown();

        comboStep = 0;
        currentFrameState = AttackFrameState.None;
        currentComboMode = AttackComboMode.Ground;
        currentStepData = null;

        hitboxActiveCount = 0;
        hitstopFramesLeft = 0;
        currentAttackFrame = 0;
        logicTimeCounter = 0f;
        isPlungeAttacking = false;
        plungeImpactTriggered = false;
        isExecutingSubStep = false;
        activeSubSteps = null;
        currentSubStepIndex = 0;
        DisableHitbox(true);

        if (movement != null) movement.CancelAttackMotion();
        if (animHandler != null) animHandler.ResetCombo();

        if (animHandler != null && animHandler.GetComponent<Animator>() != null)
        {
            animHandler.GetComponent<Animator>().speed = 1f;
        }

        CombatLog("Combo Reset");
        OnComboFinished?.Invoke();
    }

    public void RegisterGrabbedEnemy(EnemyController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (currentGrabbedEnemy != null && currentGrabbedEnemy != enemy)
        {
            ReleaseGrabbedEnemy();
        }

        currentGrabbedEnemy = enemy;
    }

    public void ClearGrabbedEnemy(EnemyController enemy = null)
    {
        if (enemy != null && currentGrabbedEnemy != enemy)
        {
            return;
        }

        currentGrabbedEnemy = null;
    }

    public bool IsHoldingEnemy(EnemyController enemy)
    {
        return enemy != null && currentGrabbedEnemy == enemy;
    }

    public void ReleaseGrabbedEnemy()
    {
        if (currentGrabbedEnemy == null)
        {
            return;
        }

        currentGrabbedEnemy.ReleaseGrab(transform.root.GetInstanceID());
        currentGrabbedEnemy = null;
    }

    public bool TryStartPlungeAttack()
    {
        PlungeAttackData plungeData = GetPlungeAttackData();
        if (plungeData == null || !plungeData.isEnabled) return false;
        if (movement == null || movement.IsGrounded() || movement.IsDashing || movement.IsLanding) return false;

        AttackStepData impactStep = plungeData.impactStep;
        if (impactStep == null || !HasAnyHitboxData(impactStep)) return false;

        ResetCombo();

        isPlungeAttacking = true;
        plungeImpactTriggered = false;
        currentFrameState = AttackFrameState.Windup;
        currentAttackFrame = 0;
        logicTimeCounter = 0f;
        hitstopFramesLeft = 0;
        hitboxActiveCount = 0;
        shouldResetUpdateLoop = true;
        
        currentStepData = impactStep;
        cachedPhase1Frames = currentStepData.startupFrames;
        cachedPhase2Frames = cachedPhase1Frames + currentStepData.activeFrames;
        cachedPhase3Frames = cachedPhase2Frames + currentStepData.recoveryFrames;

        if (movement != null)
        {
            movement.CancelAttackMotion();
            movement.StartPlungeMotion(plungeData.downwardSpeed);
        }
        if (animHandler != null) animHandler.TriggerPlungeFall(plungeData.fallAnimationStateName);

        return true;
    }

    private void HandlePlungeLanding()
    {
        if (!isPlungeAttacking || plungeImpactTriggered) return;

        plungeImpactTriggered = true;
        currentFrameState = AttackFrameState.Windup;
        currentAttackFrame = 0;
        logicTimeCounter = 0f;
        hitstopFramesLeft = 0;
        
        PlungeAttackData plungeData = GetPlungeAttackData();
        if (animHandler != null)
        {
            string stateName = plungeData != null ? plungeData.impactAnimationStateName : null;
            animHandler.TriggerPlungeImpact(stateName);
        }
    }

    private void HandleLandingComboReset()
    {
        if (currentComboMode == AttackComboMode.AirNeutral && comboStep > 0) ResetCombo();
    }

    private void HandleDashStart()
    {
        if (CanPreserveAirComboProgress()) CancelCurrentAttackButKeepComboProgress();
        else ResetCombo();
    }

    private void HandleJumpStart()
    {
        ResetCombo();
    }

    private bool CanAcceptAirAttackRequest()
    {
        if (currentComboMode == AttackComboMode.AirNeutral && comboStep > 0)
        {
            BasicAttackComboData airComboData = GetComboData(AttackComboMode.AirNeutral);
            int maxCombo = airComboData != null ? airComboData.GetMaxCombo() : 0;
            if (comboStep < maxCombo) return true;
        }
        return movement != null && movement.CanStartAirCombo();
    }

    public bool IsAttacking() { return currentFrameState != AttackFrameState.None; }
    public bool HasActiveHitstop => hitstopFramesLeft > 0;
    public bool ShouldInterruptAttackOnGrounded()
    {
        if (!IsAttacking()) return false;
        if (isPlungeAttacking) return IsPlungeInAirPhase();
        return currentComboMode == AttackComboMode.AirNeutral && comboStep > 0;
    }
    public bool ShouldSuppressLandingFeedback() { return isPlungeAttacking && !plungeImpactTriggered; }
    public bool IsPlungeInAirPhase() { return isPlungeAttacking && !plungeImpactTriggered; }
    public bool ShouldLockAirComboInPlace() { return IsAttacking() && !isPlungeAttacking && currentComboMode == AttackComboMode.AirNeutral && movement != null && !movement.IsGrounded(); }
    public int GetComboStep() { return comboStep; }
    public AttackFrameState GetCurrentFrameState() { return currentFrameState; }
    public bool CanSwitchDirection() { return currentFrameState == AttackFrameState.None || currentFrameState == AttackFrameState.Windup; }
    public bool CanMoveAfterAttack() { return !IsAttacking(); }

    public bool IsLastComboStep()
    {
        BasicAttackComboData comboData = GetComboData(currentComboMode);
        return comboData != null && comboStep >= comboData.GetMaxCombo();
    }

    private float GetFinalAttackDamage(float hitFrameMultiplier)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null) return hitFrameMultiplier;
        return playerCharacter.characterObject.stats.attack * hitFrameMultiplier;
    }

    private float GetComboResetDelay()
    {
        if (isPlungeAttacking) return 0.8f;
        BasicAttackComboData comboData = GetComboData(currentComboMode);
        if (comboData == null) return 0.8f;
        return comboData.comboResetDelay;
    }

    private AttackComboMode GetRequestedComboMode()
    {
        if (movement != null && !movement.IsGrounded()) return AttackComboMode.AirNeutral;
        return AttackComboMode.Ground;
    }

    private BasicAttackComboData GetComboData(AttackComboMode comboMode)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null) return null;
        if (comboMode == AttackComboMode.AirNeutral) return playerCharacter.characterObject.neutralAirComboData;
        return playerCharacter.characterObject.basicAttackComboData;
    }

    private PlungeAttackData GetPlungeAttackData()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null) return null;
        return playerCharacter.characterObject.plungeAttackData;
    }

    private bool CanPreserveAirComboProgress()
    {
        return movement != null && !movement.IsGrounded() && currentComboMode == AttackComboMode.AirNeutral && comboStep > 0 && !isPlungeAttacking;
    }

    public void CancelCurrentAttackButKeepComboProgress()
    {
        currentFrameState = AttackFrameState.None;
        currentAttackFrame = 0;
        logicTimeCounter = 0f;
        hitstopFramesLeft = 0;
        hitboxActiveCount = 0;
        lastAttackInputTime = Time.time;
        DisableHitbox(true);

        if (animHandler != null && animHandler.GetComponent<Animator>() != null)
        {
            animHandler.GetComponent<Animator>().speed = 1f;
        }

        if (movement != null) movement.CancelAttackMotion();
        OnComboFinished?.Invoke();
    }

    private Vector2 GetGrabOrigin()
    {
        if (meleeHitboxController != null && meleeHitboxController.attackAnchor != null)
            return meleeHitboxController.attackAnchor.position;

        if (movement != null)
            return movement.transform.position;

        return transform.position;
    }

    private int GetFacingDirectionForGrab()
    {
        if (movement != null)
            return movement.FacingDirection;

        float yRotation = transform.eulerAngles.y;
        return (yRotation > 90f && yRotation < 270f) ? -1 : 1;
    }

    private bool IsEnemyAirborneForGrab(EnemyController enemy)
    {
        if (enemy == null)
            return false;

        EnemyReactionState reactionState = enemy.CurrentReactionState;
        if (reactionState == EnemyReactionState.Knockup || reactionState == EnemyReactionState.Knockfall)
            return true;

        return enemy.rb != null && Mathf.Abs(enemy.rb.linearVelocity.y) > 0.05f;
    }

    private bool CanGrabEnemy(EnemyController enemy)
    {
        if (enemy == null || currentGrabHitbox == null || !enemy.IsAlive)
            return false;

        if (!currentGrabHitbox.canGrabAirborne && IsEnemyAirborneForGrab(enemy))
            return false;

        Vector2 origin = GetGrabOrigin();
        Vector2 toEnemy = (Vector2)enemy.transform.position - origin;

        if (currentGrabHitbox.grabRange > 0f && toEnemy.magnitude > currentGrabHitbox.grabRange)
            return false;

        if (toEnemy.sqrMagnitude <= Mathf.Epsilon)
            return true;

        Vector2 forward = new Vector2(GetFacingDirectionForGrab(), 0f);
        float angleToEnemy = Vector2.Angle(forward, toEnemy.normalized);
        return angleToEnemy <= currentGrabHitbox.grabAngle;
    }

    private bool CanProcessHitboxTarget(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsAlive)
            return false;

        if (currentGrabHitbox == null)
            return true;

        if (currentGrabbedEnemy != null && currentGrabbedEnemy != enemy)
            return false;

        return CanGrabEnemy(enemy);
    }

    private void PerformHitboxOverlapCheck()
    {
        if (meleeHitbox == null || meleeHitboxController == null) return;

        BoxCollider2D col = meleeHitboxController.hitboxCollider;
        if (col == null) return;

        // Get the world-space bounding box for the hitbox
        Vector2 worldPos = col.transform.TransformPoint(col.offset);
        Vector2 size = col.size;
        float angle = col.transform.eulerAngles.z;

        Collider2D[] results = Physics2D.OverlapBoxAll(worldPos, size, angle, enemyLayerMask);

        foreach (var hit in results)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && !hitEnemiesThisStep.Contains(enemy) && CanProcessHitboxTarget(enemy))
            {
                hitEnemiesThisStep.Add(enemy);
                CombatLog($"HIT: {enemy.gameObject.name} (Damage: {meleeHitboxDamage.DamageValue})");
                
                // Trigger the actual damage and hit effects on the damage component
                if (meleeHitboxDamage != null)
                {
                    meleeHitboxDamage.ProcessHit(enemy);
                }

                // NEW LOGIC: Conditional Branching
                if (currentGrabHitbox != null && currentGrabHitbox.branchOnSuccess && currentGrabHitbox.successSteps != null && currentGrabHitbox.successSteps.Count > 0)
                {
                    TriggerGrabSuccessBranch(currentGrabHitbox.successSteps);
                    break;
                }

                if (currentGrabHitbox != null && currentGrabbedEnemy != null)
                {
                    break;
                }
            }
        }
    }

    private void TriggerGrabSuccessBranch(List<AttackStepData> successSteps)
    {
        isExecutingSubStep = true;
        activeSubSteps = successSteps;
        currentSubStepIndex = 0;
        
        TriggerSubStep(activeSubSteps[currentSubStepIndex]);
    }

    private void TriggerSubStep(AttackStepData subStep)
    {
        currentStepData = subStep;
        
        cachedPhase1Frames = currentStepData.startupFrames;
        cachedPhase2Frames = cachedPhase1Frames + currentStepData.activeFrames;
        cachedPhase3Frames = cachedPhase2Frames + currentStepData.recoveryFrames;
        
        currentAttackFrame = 0;
        logicTimeCounter = 0f;
        hitboxActiveCount = 0;
        currentFrameState = AttackFrameState.Windup;

        if (animHandler != null && currentStepData.animationClip != null)
        {
            animHandler.TriggerSubStepAnimation(currentStepData.animationClip);
        }

        OnBranchToSubStep?.Invoke(currentStepData);
    }
}
