using UnityEngine;
using System.Collections;

public class BasicAttack : MonoBehaviour
{
    [Header("Dependencies")]
    public AnimationHandler animHandler;
    public PlayerMovement movement;
    public GameObject meleeHitbox;

    [Header("Combo Settings")]
    [SerializeField] private int maxCombo = 3;
    [SerializeField] private float comboResetDelay = 0.8f;   // ถ้าไม่กดต่อภายในเวลานี้ รีเซ็ต
    [SerializeField] private float attackDuration = 0.45f;   // เวลารวมของแต่ละท่า
    [SerializeField] private float comboQueueOpenTime = 0.2f; // เปิดรับ input ตอนท้ายท่า

    private int comboStep = 0;
    private bool isAttacking = false;
    private bool comboQueued = false;
    private bool canQueueNext = false;
    private float lastAttackInputTime = -999f;

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

    public void OnAttackInput()
    {
        if (movement != null && !movement.IsGrounded())
            return;

        lastAttackInputTime = Time.time;

        // ยังไม่ได้ตี -> เริ่ม Attack 1
        if (!isAttacking)
        {
            StartNextAttack();
            return;
        }

        // กำลังตีอยู่ ถ้าอยู่ในหน้าต่างที่ให้กดต่อคอมโบได้ ให้ queue ไว้
        if (canQueueNext)
        {
            comboQueued = true;
        }
    }

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

        StartCoroutine(AttackRoutine(comboStep));
    }

    private IEnumerator AttackRoutine(int currentStep)
    {
        // ช่วงต้นท่า ยังไม่เปิดให้กดต่อ
        float waitBeforeQueue = Mathf.Max(attackDuration - comboQueueOpenTime, 0.05f);
        yield return new WaitForSeconds(waitBeforeQueue);

        canQueueNext = true;

        // รอจนจบท่า
        float remain = attackDuration - waitBeforeQueue;
        if (remain > 0f)
            yield return new WaitForSeconds(remain);

        canQueueNext = false;
        isAttacking = false;

        // ถ้ามีการกดต่อไว้ และยังไม่เกิน maxCombo -> ไปท่าถัดไป
        if (comboQueued && comboStep < maxCombo)
        {
            StartNextAttack();
        }
        else
        {
            // ถ้าถึงท่าสุดท้าย หรือไม่มีการกดต่อ รอสักพักแล้วรีเซ็ต
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
}