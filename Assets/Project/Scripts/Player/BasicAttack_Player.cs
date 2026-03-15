using UnityEngine;
using System.Collections;

public class BasicAttack : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public AnimationHandler animHandler;
    public PlayerMovement movement;
    public GameObject meleeHitbox;

    // =========================================================
    // Combo Settings
    // =========================================================
    [Header("Combo Settings")]
    [SerializeField] private int maxCombo = 3;
    [SerializeField] private float comboResetDelay = 0.8f;
    [SerializeField] private float comboQueueOpenTime = 0.2f;

    [SerializeField] private float attack1Duration = 0.45f;
    [SerializeField] private float attack2Duration = 0.45f;
    [SerializeField] private float attack3Duration = 0.45f;

    // =========================================================
    // Runtime States
    // =========================================================
    private int comboStep = 0;
    private bool isAttacking = false;
    private bool comboQueued = false;
    private bool canQueueNext = false;
    private float lastAttackInputTime = -999f;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        if (meleeHitbox != null)
            meleeHitbox.SetActive(false);
    }

    void Update()
    {
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
    public void OnAttackInput()
    {
        if (movement != null && !movement.IsGrounded())
            return;

        lastAttackInputTime = Time.time;

        if (!isAttacking)
        {
            StartNextAttack();
            return;
        }

        if (canQueueNext)
        {
            comboQueued = true;
        }
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

    public void ResetCombo()
    {
        StopAllCoroutines();

        comboStep = 0;
        isAttacking = false;
        comboQueued = false;
        canQueueNext = false;

        DisableHitbox();

        if (movement != null)
            movement.CanMove = true;

        if (animHandler != null)
            animHandler.ResetCombo();
    }

    public void SetAttackData(
        int newMaxCombo,
        float newComboResetDelay,
        float newComboQueueOpenTime,
        float newAttack1Duration,
        float newAttack2Duration,
        float newAttack3Duration
    )
    {
        maxCombo = newMaxCombo;
        comboResetDelay = newComboResetDelay;
        comboQueueOpenTime = newComboQueueOpenTime;

        attack1Duration = newAttack1Duration;
        attack2Duration = newAttack2Duration;
        attack3Duration = newAttack3Duration;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void StartNextAttack()
    {
        if (comboStep >= maxCombo)
            comboStep = 0;

        comboStep++;
        isAttacking = true;
        comboQueued = false;
        canQueueNext = false;

        if (movement != null)
            movement.CanMove = false;

        if (animHandler != null)
            animHandler.TriggerAttackCombo(comboStep);

        StartCoroutine(AttackRoutine());
    }

    private float GetCurrentAttackDuration()
    {
        switch (comboStep)
        {
            case 1: return attack1Duration;
            case 2: return attack2Duration;
            case 3: return attack3Duration;
            default: return attack1Duration;
        }
    }

    // =========================================================
    // Coroutines
    // =========================================================
    private IEnumerator AttackRoutine()
    {
        float currentAttackDuration = GetCurrentAttackDuration();

        float waitBeforeQueue = Mathf.Max(currentAttackDuration - comboQueueOpenTime, 0.05f);
        yield return new WaitForSeconds(waitBeforeQueue);

        canQueueNext = true;

        float remain = currentAttackDuration - waitBeforeQueue;
        if (remain > 0f)
            yield return new WaitForSeconds(remain);

        canQueueNext = false;
        isAttacking = false;

        if (comboQueued && comboStep < maxCombo)
        {
            StartNextAttack();
        }
        else
        {
            if (comboStep >= maxCombo)
            {
                ResetCombo();
            }
            else
            {
                if (movement != null)
                    movement.CanMove = true;
            }
        }
    }

    // =========================================================
    // Debug Helpers
    // =========================================================
    public int GetComboStep()
    {
        return comboStep;
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }

    public bool CanQueueNext()
    {
        return canQueueNext;
    }
}