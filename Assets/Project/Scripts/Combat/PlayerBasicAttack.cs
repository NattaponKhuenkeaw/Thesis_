using UnityEngine;
using System.Collections;

public class PlayerBasicAttack : MonoBehaviour
{
    private enum AttackComboMode
    {
        Ground,
        AirNeutral
    }

    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public AnimationHandler animHandler;
    public PlayerMovement movement;
    public GameObject meleeHitbox;
    public MeleeHitboxController meleeHitboxController;
    public MeleeHitboxDamage meleeHitboxDamage;
    public PlayerActionController actionController;

    private int comboStep = 0;
    private bool isAttacking = false;
    private bool comboQueued = false;
    private bool canQueueNext = false;
    private bool canMoveAfterAttack = false;
    private bool canDashCancel = false;
    private bool canJumpCancel = false;

    private float lastAttackInputTime = -999f;
    private AttackComboMode currentComboMode = AttackComboMode.Ground;
    private AttackComboMode queuedComboMode = AttackComboMode.Ground;
    private AttackRequestType currentAttackRequest = AttackRequestType.None;

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

    void OnEnable()
    {
        if (movement != null)
            movement.OnAirDashPerformed += HandleAirDashPerformed;
    }

    void OnDisable()
    {
        if (movement != null)
            movement.OnAirDashPerformed -= HandleAirDashPerformed;
    }

    void Start()
    {
        if (meleeHitbox != null)
            meleeHitbox.SetActive(false);
    }

    void Update()
    {
        float comboResetDelay = GetComboResetDelay();

        if (!isAttacking && comboStep > 0)
        {
            if (Time.time - lastAttackInputTime > comboResetDelay)
                ResetCombo();
        }
    }

    public bool OnAttackInput()
    {
        return OnAttackInput(AttackRequestType.None);
    }

    public bool OnAttackInput(AttackRequestType attackRequest)
    {
        if (attackRequest == AttackRequestType.None)
            attackRequest = GetDefaultAttackRequest();

        if (IsComboRequest(attackRequest))
            return HandleComboAttackInput(attackRequest);

        return HandleSpecialAttackInput(attackRequest);
    }

    public void ResetCombo()
    {
        if (actionController != null)
            actionController.SetState(PlayerActionState.Normal);

        StopAllCoroutines();

        comboStep = 0;
        isAttacking = false;
        comboQueued = false;
        canQueueNext = false;
        canMoveAfterAttack = false;
        canDashCancel = false;
        canJumpCancel = false;
        currentComboMode = AttackComboMode.Ground;
        queuedComboMode = AttackComboMode.Ground;
        currentAttackRequest = AttackRequestType.None;

        DisableHitbox();

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

    public bool CanMoveAfterAttack()
    {
        return canMoveAfterAttack;
    }

    public bool CanDashCancel()
    {
        return canDashCancel;
    }

    public bool CanJumpCancel()
    {
        return canJumpCancel;
    }

    public float GetDashAttackInputWindow()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null || playerCharacter.characterObject.airCombatRules == null)
            return 0.12f;

        return playerCharacter.characterObject.airCombatRules.dashAttackInputWindow;
    }

    public float GetFallKickOverrideWindow()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null || playerCharacter.characterObject.airCombatRules == null)
            return 0.12f;

        return playerCharacter.characterObject.airCombatRules.fallKickOverrideWindow;
    }

    private bool HandleComboAttackInput(AttackRequestType attackRequest)
    {
        if (actionController != null && !actionController.CanStartAttack() && !isAttacking)
            return false;

        AttackComboMode requestedMode = GetComboModeForRequest(attackRequest);
        BasicAttackComboData comboData = GetComboData(requestedMode);
        if (comboData == null || comboData.GetMaxCombo() <= 0)
            return false;

        if (isAttacking && !canQueueNext)
            return false;

        lastAttackInputTime = Time.time;

        if (!isAttacking)
        {
            if (comboStep > 0 && requestedMode != currentComboMode)
                ResetCombo();

            StartNextAttack(requestedMode);
            return true;
        }

        comboQueued = true;
        queuedComboMode = requestedMode;
        return true;
    }

    private bool HandleSpecialAttackInput(AttackRequestType attackRequest)
    {
        if (isAttacking)
            return false;

        if (!CanStartSpecialAttack(attackRequest))
            return false;

        SpecialAttackData specialData = GetSpecialAttackData(attackRequest);
        if (specialData == null || !specialData.enabled || specialData.step == null)
            return false;

        StartSpecialAttack(attackRequest, specialData);
        return true;
    }

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
        currentAttackRequest = attackMode == AttackComboMode.AirNeutral ? AttackRequestType.AirCombo : AttackRequestType.GroundCombo;
        comboStep++;
        isAttacking = true;
        comboQueued = false;
        queuedComboMode = attackMode;
        canQueueNext = false;
        canMoveAfterAttack = false;
        canDashCancel = false;
        canJumpCancel = false;

        if (movement != null)
            movement.CanMove = false;

        if (animHandler != null)
            animHandler.TriggerAttackCombo(comboStep);

        StartCoroutine(AttackRoutine(comboData.GetStep(comboStep)));
    }

    private void StartSpecialAttack(AttackRequestType attackRequest, SpecialAttackData specialData)
    {
        AttackStepData stepData = specialData.step;
        if (stepData == null)
            return;

        if (actionController != null)
            actionController.SetState(PlayerActionState.Attack);

        if (specialData.resetAirComboOnUse)
            ResetAirComboProgress();

        currentAttackRequest = attackRequest;
        currentComboMode = AttackComboMode.Ground;
        queuedComboMode = AttackComboMode.Ground;
        comboStep = 0;
        isAttacking = true;
        comboQueued = false;
        canQueueNext = false;
        canMoveAfterAttack = false;
        canDashCancel = false;
        canJumpCancel = false;
        lastAttackInputTime = Time.time;

        if (movement != null)
            movement.CanMove = false;

        if (animHandler != null)
            animHandler.TriggerSpecialAttack(attackRequest);

        StartCoroutine(SpecialAttackRoutine(specialData));
    }

    private IEnumerator AttackRoutine(AttackStepData stepData)
    {
        if (stepData == null)
        {
            ResetCombo();
            yield break;
        }

        ExecuteAttackStep(stepData);

        float waitBeforeQueue = Mathf.Max(stepData.duration - stepData.comboQueueOpenTime, 0.05f);
        yield return new WaitForSeconds(waitBeforeQueue);

        canQueueNext = true;

        float remain = stepData.duration - waitBeforeQueue;
        if (remain > 0f)
            yield return new WaitForSeconds(remain);

        canQueueNext = false;
        isAttacking = false;

        BasicAttackComboData comboData = GetComboData(currentComboMode);
        int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;

        if (comboQueued && queuedComboMode == currentComboMode && comboStep < maxCombo)
        {
            StartNextAttack(currentComboMode);
        }
        else if (comboQueued && queuedComboMode != currentComboMode)
        {
            comboStep = 0;
            StartNextAttack(queuedComboMode);
        }
        else
        {
            ResetCombo();
        }
    }

    private IEnumerator SpecialAttackRoutine(SpecialAttackData specialData)
    {
        AttackStepData stepData = specialData.step;
        if (stepData == null)
        {
            ResetCombo();
            yield break;
        }

        if (specialData.triggerHitOnGroundContact && movement != null)
        {
            ExecuteAttackMotion(stepData.motionData);

            float waitUntil = Time.time + Mathf.Max(stepData.duration, 2f);
            while (!movement.IsGrounded() && Time.time < waitUntil)
                yield return null;

            if (movement.IsGrounded())
            {
                ExecuteHitFrames(stepData.hitFrames);

                float impactDuration = GetHitFramesTotalDuration(stepData.hitFrames);
                if (impactDuration > 0f)
                    yield return new WaitForSeconds(impactDuration);

                if (stepData.landingLag > 0f)
                    yield return new WaitForSeconds(stepData.landingLag);
            }

            ResetCombo();
            yield break;
        }

        ExecuteAttackStep(stepData);

        float elapsed = 0f;
        while (elapsed < stepData.duration)
        {
            if (specialData.endOnGroundContact && movement != null && movement.IsGrounded() && elapsed > 0.02f)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (movement != null && movement.IsGrounded() && stepData.landingLag > 0f)
            yield return new WaitForSeconds(stepData.landingLag);

        ResetCombo();
    }

    private void ExecuteAttackStep(AttackStepData stepData)
    {
        StartCoroutine(OpenMoveRecoveryRoutine(stepData.moveRecoveryTime));
        StartCoroutine(OpenDashCancelRoutine(stepData.dashCancelTime));
        StartCoroutine(OpenJumpCancelRoutine(stepData.jumpCancelTime));

        ExecuteAttackMotion(stepData.motionData);
        ExecuteHitFrames(stepData.hitFrames);
    }

    private void ExecuteAttackMotion(AttackMotionData motionData)
    {
        if (motionData != null && motionData.useForwardMotion)
            StartCoroutine(AttackMotionRoutine(motionData));
    }

    private void ExecuteHitFrames(AttackHitFrameData[] hitFrames)
    {
        if (hitFrames == null)
            return;

        for (int i = 0; i < hitFrames.Length; i++)
        {
            AttackHitFrameData hitFrame = hitFrames[i];
            if (hitFrame != null)
                StartCoroutine(TriggerHitFrameRoutine(hitFrame));
        }
    }

    private float GetHitFramesTotalDuration(AttackHitFrameData[] hitFrames)
    {
        if (hitFrames == null)
            return 0f;

        float totalDuration = 0f;

        for (int i = 0; i < hitFrames.Length; i++)
        {
            AttackHitFrameData hitFrame = hitFrames[i];
            if (hitFrame == null)
                continue;

            totalDuration = Mathf.Max(totalDuration, hitFrame.hitTime + hitFrame.activeDuration);
        }

        return totalDuration;
    }

    private IEnumerator TriggerHitFrameRoutine(AttackHitFrameData hitFrame)
    {
        yield return new WaitForSeconds(hitFrame.hitTime);

        float finalDamage = GetFinalAttackDamage(hitFrame.damageMultiplier);
        float finalKnockback = hitFrame.knockbackForce;
        float shakeStrength = Mathf.Max(0f, hitFrame.cameraShakeStrength);

        bool isLastStep = !IsComboRequest(currentAttackRequest);
        if (!isLastStep)
        {
            BasicAttackComboData comboData = GetComboData(currentComboMode);
            int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;
            isLastStep = comboStep >= maxCombo;
        }

        if (meleeHitboxController != null)
            meleeHitboxController.ApplyHitFrame(hitFrame);

        if (meleeHitboxDamage != null)
            meleeHitboxDamage.SetHitData(finalDamage, finalKnockback, shakeStrength, isLastStep);

        EnableHitbox();

        yield return new WaitForSeconds(hitFrame.activeDuration);

        DisableHitbox();
    }

    private IEnumerator AttackMotionRoutine(AttackMotionData motionData)
    {
        if (motionData == null)
            yield break;

        yield return new WaitForSeconds(motionData.motionStartTime);

        if (movement != null)
            movement.ApplyAttackMovement(motionData);
    }

    public void EnableHitbox()
    {
        if (meleeHitbox != null)
            meleeHitbox.SetActive(true);
    }

    public void DisableHitbox()
    {
        if (meleeHitbox != null)
            meleeHitbox.SetActive(false);
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

    private AttackRequestType GetDefaultAttackRequest()
    {
        if (movement != null && (movement.IsJumping || !movement.IsGrounded()))
            return AttackRequestType.AirCombo;

        return AttackRequestType.GroundCombo;
    }

    private AttackComboMode GetComboModeForRequest(AttackRequestType attackRequest)
    {
        return attackRequest == AttackRequestType.AirCombo
            ? AttackComboMode.AirNeutral
            : AttackComboMode.Ground;
    }

    private BasicAttackComboData GetComboData(AttackComboMode comboMode)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return null;

        if (comboMode == AttackComboMode.AirNeutral)
            return playerCharacter.characterObject.neutralAirComboData;

        return playerCharacter.characterObject.basicAttackComboData;
    }

    private SpecialAttackData GetSpecialAttackData(AttackRequestType attackRequest)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return null;

        switch (attackRequest)
        {
            case AttackRequestType.DashAttack:
                return playerCharacter.characterObject.dashAttackData;
            case AttackRequestType.PlungeAttack:
                return playerCharacter.characterObject.plungeAttackData;
            case AttackRequestType.FallKick:
                return playerCharacter.characterObject.fallKickData;
            default:
                return null;
        }
    }

    private bool CanStartSpecialAttack(AttackRequestType attackRequest)
    {
        if (actionController == null)
            return false;

        switch (attackRequest)
        {
            case AttackRequestType.DashAttack:
                return movement != null
                    && movement.IsDashing
                    && (actionController.IsState(PlayerActionState.Dash) || actionController.CanStartAttack());

            case AttackRequestType.PlungeAttack:
            case AttackRequestType.FallKick:
                return movement != null
                    && !movement.IsGrounded()
                    && actionController.CanStartAttack();

            default:
                return false;
        }
    }

    private bool IsComboRequest(AttackRequestType attackRequest)
    {
        return attackRequest == AttackRequestType.GroundCombo || attackRequest == AttackRequestType.AirCombo;
    }

    private void HandleAirDashPerformed()
    {
        if (!ShouldResetAirComboOnAirDash())
            return;

        ResetAirComboProgress();
    }

    private void ResetAirComboProgress()
    {
        comboStep = 0;

        if (currentComboMode == AttackComboMode.AirNeutral)
            currentComboMode = AttackComboMode.Ground;

        if (queuedComboMode == AttackComboMode.AirNeutral)
            queuedComboMode = AttackComboMode.Ground;
    }

    private bool ShouldResetAirComboOnAirDash()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return true;

        AirCombatRulesData rules = playerCharacter.characterObject.airCombatRules;
        return rules == null || rules.resetAirComboOnAirDash;
    }

    private IEnumerator OpenMoveRecoveryRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        canMoveAfterAttack = true;
    }

    private IEnumerator OpenDashCancelRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        canDashCancel = true;
    }

    private IEnumerator OpenJumpCancelRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        canJumpCancel = true;
    }
}
