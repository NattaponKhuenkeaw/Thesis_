using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerBasicAttack : MonoBehaviour
{
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
    private bool canQueueNext = false;
    private bool canMoveAfterAttack = false;
    private bool canDashCancel = false;
    private bool canJumpCancel = false;

    private float lastAttackInputTime = -999f;

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

        if (movement != null && !movement.IsGrounded())
            return false;

        if (isAttacking && !canQueueNext)
            return false;

        lastAttackInputTime = Time.time;

        if (!isAttacking)
        {
            StartNextAttack();
            return true;
        }

        comboQueued = true;
        return true;
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

        DisableHitbox();

        if (movement != null)
            movement.CanMove = true;

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

    // =========================================================
    // Private Methods
    // =========================================================
    private void StartNextAttack()
    {
        BasicAttackComboData comboData = GetComboData();

        if (actionController != null)
            actionController.SetState(PlayerActionState.Attack);

        if (comboData == null)
            return;

        int maxCombo = comboData.GetMaxCombo();
        if (maxCombo <= 0)
            return;

        if (comboStep >= maxCombo)
            comboStep = 0;

        comboStep++;
        isAttacking = true;
        comboQueued = false;
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

    private IEnumerator AttackRoutine(AttackStepData stepData)
    {
        if (stepData == null)
        {
            ResetCombo();
            yield break;
        }

        StartCoroutine(OpenMoveRecoveryRoutine(stepData.moveRecoveryTime));
        StartCoroutine(OpenDashCancelRoutine(stepData.dashCancelTime));
        StartCoroutine(OpenJumpCancelRoutine(stepData.jumpCancelTime));

        if (stepData.motionData != null && stepData.motionData.useForwardMotion)
        {
            StartCoroutine(AttackMotionRoutine(stepData.motionData));
        }

        if (stepData.hitFrames != null)
        {
            for (int i = 0; i < stepData.hitFrames.Length; i++)
            {
                AttackHitFrameData hitFrame = stepData.hitFrames[i];
                if (hitFrame != null)
                {
                    StartCoroutine(TriggerHitFrameRoutine(hitFrame));
                }
            }
        }

        float waitBeforeQueue = Mathf.Max(stepData.duration - stepData.comboQueueOpenTime, 0.05f);
        yield return new WaitForSeconds(waitBeforeQueue);

        canQueueNext = true;

        float remain = stepData.duration - waitBeforeQueue;
        if (remain > 0f)
            yield return new WaitForSeconds(remain);

        canQueueNext = false;
        isAttacking = false;

        BasicAttackComboData comboData = GetComboData();
        int maxCombo = comboData != null ? comboData.GetMaxCombo() : 0;

        if (comboQueued && comboStep < maxCombo)
        {
            StartNextAttack();
        }
        else
        {
            ResetCombo();
        }
    }

    private IEnumerator TriggerHitFrameRoutine(AttackHitFrameData hitFrame)
    {
        yield return new WaitForSeconds(hitFrame.hitTime);

        float finalDamage = GetFinalAttackDamage(hitFrame.damageMultiplier);
        float finalKnockback = hitFrame.knockbackForce;

        if (meleeHitboxController != null)
            meleeHitboxController.ApplyHitFrame(hitFrame);

        if (meleeHitboxDamage != null)
            meleeHitboxDamage.SetHitData(finalDamage, finalKnockback);

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
            movement.ApplyAttackMovement(motionData.forwardDistance, motionData.motionDuration);
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
        BasicAttackComboData comboData = GetComboData();
        if (comboData == null)
            return 0.8f;

        return comboData.comboResetDelay;
    }

    private BasicAttackComboData GetComboData()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return null;

        return playerCharacter.characterObject.basicAttackComboData;
    }


    // =========================================================
    // Routine
    // =========================================================

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