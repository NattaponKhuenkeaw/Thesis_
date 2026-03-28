using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerBasicAttack : MonoBehaviour
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

    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public AnimationHandler animHandler;
    public PlayerMovement movement;
    public GameObject meleeHitbox;
    public MeleeHitboxController meleeHitboxController;
    public MeleeHitboxDamage meleeHitboxDamage;
    public PlayerActionController actionController;

    // =========================================================
    // Runtime States
    // =========================================================
    private int comboStep = 0;
    private bool isAttacking = false;
    private bool comboQueued = false;
    
    // New Frame Data State
    private AttackFrameState currentFrameState = AttackFrameState.None;

    private float lastAttackInputTime = -999f;
    private AttackComboMode currentComboMode = AttackComboMode.Ground;
    private AttackComboMode queuedComboMode = AttackComboMode.Ground;
    private int currentHitFrameIndex = 0;
    private int hitboxActiveCount = 0;
    private int lastImpactFrame = -1;
    private bool inputConsumedInCurrentStep = false;
    private bool pendingAirComboAdvance = false;
    private bool isPlungeAttacking = false;
    private bool plungeImpactTriggered = false;
    private int airComboSetsUsedUntilLanding = 0;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (actionController == null)
            actionController = GetComponent<PlayerActionController>();

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (meleeHitboxController == null && meleeHitbox != null)
            meleeHitboxController = meleeHitbox.GetComponent<MeleeHitboxController>();

        if (meleeHitboxDamage == null && meleeHitbox != null)
            meleeHitboxDamage = meleeHitbox.GetComponent<MeleeHitboxDamage>();
    }

    void Start()
    {
        if (meleeHitbox != null)
            meleeHitbox.SetActive(false);
    }

    void OnEnable()
    {
        if (movement != null)
        {
            movement.OnDashStart += HandleDashStart;
            movement.OnJumpStart += HandleJumpStart;
            movement.OnLand += HandlePlungeLanding;
            movement.OnLand += ResetAirComboUsage;
        }
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.OnDashStart -= HandleDashStart;
            movement.OnJumpStart -= HandleJumpStart;
            movement.OnLand -= HandlePlungeLanding;
            movement.OnLand -= ResetAirComboUsage;
        }
    }

    void Update()
    {
        if (ShouldKeepAirComboProgressUntilLanding() && currentComboMode == AttackComboMode.AirNeutral && comboStep > 0)
            return;

        float comboResetDelay = GetComboResetDelay();

        if (!isAttacking && comboStep > 0)
        {
            if (Time.time - lastAttackInputTime > comboResetDelay)
            {
                ResetCombo();
            }
        }
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public bool OnAttackInput()
    {
        if (actionController != null && !actionController.CanStartAttack() && !isAttacking)
            return false;

        AttackComboMode requestedMode = GetRequestedComboMode();
        BasicAttackComboData comboData = GetComboData(requestedMode);
        if (comboData == null || comboData.GetMaxCombo() <= 0)
            return false;

        if (!isAttacking && requestedMode == AttackComboMode.AirNeutral && !CanAcceptAirAttackRequest())
            return false;

        lastAttackInputTime = Time.time;

        if (!isAttacking)
        {
            if (comboStep > 0 && requestedMode != currentComboMode)
                ResetCombo();

            StartNextAttack(requestedMode);
            return true;
        }

        if (inputConsumedInCurrentStep)
        {
            if (currentComboMode == AttackComboMode.AirNeutral && requestedMode == AttackComboMode.AirNeutral)
            {
                pendingAirComboAdvance = true;
                return true;
            }

            return false;
        }

        // Buffer input during Windup or Impact — will be consumed when Recovery starts
        if (currentFrameState == AttackFrameState.Windup || currentFrameState == AttackFrameState.Impact)
        {
            comboQueued = true;
            queuedComboMode = requestedMode;
            inputConsumedInCurrentStep = true;
            return true;
        }

        // If in Recovery, immediately start next combo
        if (currentFrameState == AttackFrameState.Recovery)
        {
            inputConsumedInCurrentStep = true;
            StartNextAttack(requestedMode);
            return true;
        }

        return false;
    }

    public void ResetCombo()
    {
        if (actionController != null)
            actionController.SetState(PlayerActionState.Normal);

        StopAllCoroutines();

        comboStep = 0;
        inputConsumedInCurrentStep = false;
        isAttacking = false;
        comboQueued = false;
        currentFrameState = AttackFrameState.None;
        currentComboMode = AttackComboMode.Ground;
        queuedComboMode = AttackComboMode.Ground;
        currentHitFrameIndex = 0;
        hitboxActiveCount = 0;
        lastImpactFrame = -1;
        pendingAirComboAdvance = false;
        isPlungeAttacking = false;
        plungeImpactTriggered = false;
        DisableHitbox(true);

        if (movement != null)
        {
            movement.CancelAttackMotion();
            movement.CanMove = true;
        }

        if (animHandler != null)
            animHandler.ResetCombo();
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }

    public bool TryStartPlungeAttack()
    {
        PlungeAttackData plungeData = GetPlungeAttackData();
        if (plungeData == null || !plungeData.isEnabled)
            return false;

        if (movement == null || movement.IsGrounded() || movement.IsDashing || movement.IsLanding)
            return false;

        AttackStepData impactStep = plungeData.impactStep;
        if (impactStep == null || impactStep.hitFrames == null || impactStep.hitFrames.Length == 0)
            return false;

        if (actionController != null && !actionController.CanStartAttack())
            return false;

        ResetCombo();

        if (actionController != null)
            actionController.SetState(PlayerActionState.Attack);

        isAttacking = true;
        isPlungeAttacking = true;
        plungeImpactTriggered = false;
        currentFrameState = AttackFrameState.Windup;
        currentHitFrameIndex = 0;
        hitboxActiveCount = 0;
        lastImpactFrame = -1;
        inputConsumedInCurrentStep = true;

        if (movement != null)
        {
            movement.CancelAttackMotion();
            movement.CanMove = false;
            movement.StartPlungeMotion(plungeData.downwardSpeed);
        }

        if (animHandler != null)
            animHandler.TriggerPlungeFall(plungeData.fallAnimationStateName);

        return true;
    }

    public bool ShouldSuppressLandingFeedback()
    {
        return isPlungeAttacking && !plungeImpactTriggered;
    }

    public bool IsPlungeInAirPhase()
    {
        return isPlungeAttacking && !plungeImpactTriggered;
    }

    public bool ShouldLockAirComboInPlace()
    {
        return isAttacking
            && !isPlungeAttacking
            && currentComboMode == AttackComboMode.AirNeutral
            && movement != null
            && !movement.IsGrounded();
    }

    public int GetComboStep()
    {
        return comboStep;
    }

    public AttackFrameState GetCurrentFrameState()
    {
        return currentFrameState;
    }

    public bool CanSwitchDirection()
    {
        return currentFrameState == AttackFrameState.Windup;
    }

    public bool CanMoveAfterAttack()
    {
        return !isAttacking;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void StartNextAttack(AttackComboMode attackMode)
    {
        BasicAttackComboData comboData = GetComboData(attackMode);

        if (actionController != null)
            actionController.SetState(PlayerActionState.Attack);

        if (comboData == null)
            return;

        int maxCombo = comboData.GetMaxCombo();
        if (maxCombo <= 0)
            return;

        if (comboStep >= maxCombo || attackMode != currentComboMode)
            comboStep = 0;

        if (attackMode == AttackComboMode.AirNeutral && comboStep == 0)
            airComboSetsUsedUntilLanding++;

        bool carryBufferedAirAdvance = attackMode == AttackComboMode.AirNeutral && pendingAirComboAdvance;

        currentComboMode = attackMode;
        comboStep++;
        isAttacking = true;
        comboQueued = carryBufferedAirAdvance;
        queuedComboMode = attackMode;
        
        currentFrameState = AttackFrameState.Windup;
        currentHitFrameIndex = 0;
        hitboxActiveCount = 0;
        lastImpactFrame = -1;
        inputConsumedInCurrentStep = carryBufferedAirAdvance;
        pendingAirComboAdvance = false;

        if (movement != null)
        {
            movement.CancelAttackMotion();
            movement.CanMove = false;
        }

        if (attackMode == AttackComboMode.AirNeutral)
            animHandler.TriggerAirAttack(comboStep);
        else
            animHandler.TriggerAttackCombo(comboStep);

        AttackStepData stepData = comboData.GetStep(comboStep);
        if (stepData != null && stepData.useForwardMotion)
        {
            StartCoroutine(AttackMotionRoutine(stepData));
        }
    }

    private void HandlePlungeLanding()
    {
        if (!isPlungeAttacking || plungeImpactTriggered)
            return;

        plungeImpactTriggered = true;
        currentFrameState = AttackFrameState.Windup;
        currentHitFrameIndex = 0;
        lastImpactFrame = -1;

        PlungeAttackData plungeData = GetPlungeAttackData();
        AttackStepData impactStep = plungeData != null ? plungeData.impactStep : null;

        if (impactStep != null && impactStep.useForwardMotion)
            StartCoroutine(AttackMotionRoutine(impactStep));

        if (animHandler != null)
        {
            string stateName = plungeData != null ? plungeData.impactAnimationStateName : null;
            animHandler.TriggerPlungeImpact(stateName);
        }
    }

    private void ResetAirComboUsage()
    {
        airComboSetsUsedUntilLanding = 0;

        if (currentComboMode == AttackComboMode.AirNeutral && comboStep > 0)
            ResetCombo();
    }

    private void HandleDashStart()
    {
        if (CanPreserveAirComboAcrossDash())
        {
            CancelCurrentAttackButKeepComboProgress();
            return;
        }

        ResetCombo();
    }

    private void HandleJumpStart()
    {
        if (CanPreserveAirComboAcrossDash())
        {
            CancelCurrentAttackButKeepComboProgress();
            return;
        }

        ResetCombo();
    }

    private bool CanStartNewAirComboSet()
    {
        int maxAirComboSets = GetMaxAirComboSetsPerAirtime();
        if (maxAirComboSets <= 0)
            return false;

        return airComboSetsUsedUntilLanding < maxAirComboSets;
    }

    private bool CanAcceptAirAttackRequest()
    {
        if (currentComboMode == AttackComboMode.AirNeutral && comboStep > 0)
        {
            BasicAttackComboData airComboData = GetComboData(AttackComboMode.AirNeutral);
            int maxCombo = airComboData != null ? airComboData.GetMaxCombo() : 0;

            // Continue within the current set freely, but once the set is complete
            // a new set must obey Max Air Combo Sets Per Airtime.
            if (comboStep < maxCombo)
                return true;
        }

        return CanStartNewAirComboSet();
    }

    private int GetMaxAirComboSetsPerAirtime()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null || playerCharacter.characterObject.stats == null)
            return 1;

        return Mathf.Max(0, playerCharacter.characterObject.stats.maxAirComboSetsPerAirtime);
    }

    // =========================================================
    // Animation Event Receivers
    // =========================================================
    public void OnImpactStart()
    {
        if (!isAttacking) return;

        // Deduplicate calls in the same frame (caused by multiple relay components)
        if (lastImpactFrame == Time.frameCount)
            return;
        
        lastImpactFrame = Time.frameCount;
        currentFrameState = AttackFrameState.Impact;
        
        AttackStepData stepData = GetCurrentStepData();

        if (stepData == null)
        {
            Debug.LogWarning("[PlayerBasicAttack] OnImpactStart: No StepData found for current attack.");
            return;
        }

        if (stepData.hitFrames == null || stepData.hitFrames.Length == 0)
        {
            Debug.LogWarning($"[PlayerBasicAttack] OnImpactStart: Step {comboStep} has no HitFrames defined!");
            return;
        }

        // Trigger ONLY the current hit frame sequentially
        if (currentHitFrameIndex < stepData.hitFrames.Length)
        {
            AttackHitFrameData hitFrame = stepData.hitFrames[currentHitFrameIndex];
            if (hitFrame != null)
            {
                StartCoroutine(TriggerHitFrameRoutine(hitFrame));
            }
            currentHitFrameIndex++;
        }
        else
        {
            Debug.LogWarning($"[PlayerBasicAttack] OnImpactStart: All {stepData.hitFrames.Length} hit frames already triggered for Step {comboStep}. Check animation events!");
        }
    }

    public void OnRecoveryStart()
    {
        if (!isAttacking) return;

        currentFrameState = AttackFrameState.Recovery;

        if (isPlungeAttacking)
            return;
        
        // If attack was queued during Impact, immediately proceed
        if (comboQueued)
        {
            BasicAttackComboData comboData = GetComboData(currentComboMode);
            int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;

            if (queuedComboMode == currentComboMode && comboStep < maxCombo)
            {
                StartNextAttack(currentComboMode);
            }
            else if (queuedComboMode != currentComboMode)
            {
                comboStep = 0;
                StartNextAttack(queuedComboMode);
            }
        }
    }

    public void OnAttackEnd()
    {
        if (!isAttacking) return;
        
        // IMPORTANT: We only honor OnAttackEnd if the current attack state is Recovery.
        // If we are in Windup or Impact, then this event is "stale" from a previous 
        // animation that was interrupted/short-canceled by the next combo step.
        if (currentFrameState != AttackFrameState.Recovery)
            return;

        bool shouldRestoreAirborneState = !isPlungeAttacking
            && currentComboMode == AttackComboMode.AirNeutral
            && movement != null
            && !movement.IsGrounded();

        ResetCombo();

        if (shouldRestoreAirborneState && animHandler != null)
            animHandler.RestoreAirborneState();
    }

    private IEnumerator TriggerHitFrameRoutine(AttackHitFrameData hitFrame)
    {
        // Hit Time is now respected relative to ImpactStart, but usually you'd want hitTime = 0 
        // if using Animation Events perfectly. We keep hitTime support if user still uses it.
        if (hitFrame.hitTime > 0f)
            yield return new WaitForSeconds(hitFrame.hitTime);

        float finalDamage = GetFinalAttackDamage(hitFrame.damageMultiplier);
        float finalKnockback = hitFrame.knockbackForce;
        float shakeStrength = Mathf.Max(0f, hitFrame.cameraShakeStrength);

        BasicAttackComboData comboData = GetComboData(currentComboMode);
        int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;
        bool isLastStep = (comboStep >= maxCombo);

        if (meleeHitboxController != null)
            meleeHitboxController.ApplyHitFrame(hitFrame);

        if (meleeHitboxDamage != null)
            meleeHitboxDamage.SetHitData(finalDamage, finalKnockback, shakeStrength, isLastStep, hitFrameData: hitFrame);

        EnableHitbox();

        yield return new WaitForSeconds(hitFrame.activeDuration);

        DisableHitbox();
    }

    private IEnumerator AttackMotionRoutine(AttackStepData stepData)
    {
        if (stepData == null)
            yield break;

        yield return new WaitForSeconds(stepData.motionStartTime);

        if (movement != null)
            movement.ApplyAttackMovement(stepData.forwardDistance, stepData.motionDuration);
    }

    public void EnableHitbox()
    {
        hitboxActiveCount++;
        if (meleeHitbox != null)
            meleeHitbox.SetActive(true);
    }

    public void DisableHitbox(bool force = false)
    {
        if (force)
        {
            hitboxActiveCount = 0;
            if (meleeHitbox != null)
                meleeHitbox.SetActive(false);
            return;
        }

        hitboxActiveCount--;
        
        if (hitboxActiveCount < 0)
        {
            // This happens if there are redundant 'DisableHitbox' animation events.
            // We clamp it to 0 and log a tip for the user.
            hitboxActiveCount = 0;
            // Debug.LogTip("PlayerBasicAttack: Hitbox count went negative. Consider removing legacy 'DisableHitbox' events from your animation.");
        }

        if (hitboxActiveCount == 0)
        {
            if (meleeHitbox != null)
                meleeHitbox.SetActive(false);
        }
    }

    private float GetFinalAttackDamage(float hitFrameMultiplier)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return hitFrameMultiplier;

        return playerCharacter.characterObject.stats.attack * hitFrameMultiplier;
    }

    private float GetComboResetDelay()
    {
        if (isPlungeAttacking)
            return 0.8f;

        BasicAttackComboData comboData = GetComboData(currentComboMode);
        if (comboData == null)
            return 0.8f;

        return comboData.comboResetDelay;
    }

    private AttackComboMode GetRequestedComboMode()
    {
        if (movement != null && !movement.IsGrounded())
            return AttackComboMode.AirNeutral;

        return AttackComboMode.Ground;
    }

    private BasicAttackComboData GetComboData(AttackComboMode comboMode)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return null;

        if (comboMode == AttackComboMode.AirNeutral)
            return playerCharacter.characterObject.neutralAirComboData;

        return playerCharacter.characterObject.basicAttackComboData;
    }

    private PlungeAttackData GetPlungeAttackData()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return null;

        return playerCharacter.characterObject.plungeAttackData;
    }

    private AttackStepData GetCurrentStepData()
    {
        if (isPlungeAttacking)
        {
            PlungeAttackData plungeData = GetPlungeAttackData();
            return plungeData != null ? plungeData.impactStep : null;
        }

        BasicAttackComboData comboData = GetComboData(currentComboMode);
        return comboData?.GetStep(comboStep);
    }

    private void FinishAirComboStep()
    {
        bool shouldRestoreAirborneState = movement != null && !movement.IsGrounded();

        if (ShouldKeepAirComboProgressUntilLanding() && currentComboMode == AttackComboMode.AirNeutral && comboStep > 0)
        {
            CancelCurrentAttackButKeepComboProgress();

            if (shouldRestoreAirborneState && animHandler != null)
                animHandler.RestoreAirborneState();

            return;
        }

        ResetCombo();

        if (shouldRestoreAirborneState && animHandler != null)
            animHandler.RestoreAirborneState();
    }

    private bool ShouldKeepAirComboProgressUntilLanding()
    {
        return movement != null
            && !movement.IsGrounded()
            && playerCharacter != null
            && playerCharacter.characterObject != null
            && playerCharacter.characterObject.stats != null
            && playerCharacter.characterObject.stats.keepAirComboProgressUntilLanding;
    }

    private bool CanPreserveAirComboAcrossDash()
    {
        return movement != null
            && !movement.IsGrounded()
            && currentComboMode == AttackComboMode.AirNeutral
            && comboStep > 0
            && !isPlungeAttacking;
    }

    private void CancelCurrentAttackButKeepComboProgress()
    {
        StopAllCoroutines();

        isAttacking = false;
        comboQueued = false;
        inputConsumedInCurrentStep = false;
        pendingAirComboAdvance = false;
        currentFrameState = AttackFrameState.None;
        currentHitFrameIndex = 0;
        hitboxActiveCount = 0;
        lastImpactFrame = -1;
        lastAttackInputTime = Time.time;

        DisableHitbox(true);

        if (movement != null)
        {
            movement.CancelAttackMotion();
            movement.CanMove = true;
        }

        if (actionController != null)
            actionController.SetState(PlayerActionState.Normal);
    }

    // =========================================================
    // Routine removed in favor of Animation Events
    // =========================================================


    // =========================================================
    // Routine removed in favor of Animation Events

}
