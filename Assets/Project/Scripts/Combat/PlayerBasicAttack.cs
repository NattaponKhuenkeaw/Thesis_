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
            movement.OnDashStart += ResetCombo;
            movement.OnJumpStart += ResetCombo;
        }
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.OnDashStart -= ResetCombo;
            movement.OnJumpStart -= ResetCombo;
        }
    }

    void Update()
    {
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

        lastAttackInputTime = Time.time;

        if (!isAttacking)
        {
            if (comboStep > 0 && requestedMode != currentComboMode)
                ResetCombo();

            StartNextAttack(requestedMode);
            return true;
        }

        if (inputConsumedInCurrentStep)
            return false;

        // Reject input during Windup — the current attack hasn't even reached Impact yet
        if (currentFrameState == AttackFrameState.Windup)
            return false;

        // Buffer input during Impact — will be consumed when Recovery starts
        if (currentFrameState == AttackFrameState.Impact)
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
        // Player relies on Dash/Jump cancel, or returns to idle after Recovery
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

        currentComboMode = attackMode;
        comboStep++;
        isAttacking = true;
        comboQueued = false;
        queuedComboMode = attackMode;
        
        currentFrameState = AttackFrameState.Windup;
        currentHitFrameIndex = 0;
        hitboxActiveCount = 0;
        lastImpactFrame = -1;
        inputConsumedInCurrentStep = false;

        if (movement != null)
        {
            movement.CancelAttackMotion();
            movement.CanMove = false;
        }

        if (animHandler != null)
            animHandler.TriggerAttackCombo(comboStep);

        AttackStepData stepData = comboData.GetStep(comboStep);
        if (stepData != null && stepData.useForwardMotion)
        {
            StartCoroutine(AttackMotionRoutine(stepData));
        }
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
        
        BasicAttackComboData comboData = GetComboData(currentComboMode);
        AttackStepData stepData = comboData?.GetStep(comboStep);

        if (stepData == null)
        {
            Debug.LogWarning($"[PlayerBasicAttack] OnImpactStart: No StepData found for Step {comboStep} in {currentComboMode} combo!");
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
        
        ResetCombo();
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
            meleeHitboxDamage.SetHitData(finalDamage, finalKnockback, shakeStrength, isLastStep);

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


    // =========================================================
    // Routine removed in favor of Animation Events

}
