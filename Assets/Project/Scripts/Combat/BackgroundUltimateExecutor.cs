using UnityEngine;
using System.Collections.Generic;

public class BackgroundUltimateExecutor : MonoBehaviour
{
    private AttackStepData ultimateStep;
    private int currentFrame;
    private int facingDirection;
    private CharacterStats castingStats;
    private GameObject visualClone;

    private HitBoxDamage cloneHitboxDamage;
    private Collider2D cloneCollider;
    private LayerMask enemyLayerMask;
    private bool isHitboxActive = false;
    private readonly Collider2D[] overlapResults = new Collider2D[20];

    private float logicTimeCounter = 0f;
    private const float LOGIC_FPS = 60f;

    /// <summary>Initializes and starts the background ultimate simulation.</summary>
    public void Initialize(AttackStepData step, int startFrame, int facing, CharacterStats stats, Sprite characterSprite, RuntimeAnimatorController animatorController)
    {
        ultimateStep = step;
        currentFrame = startFrame;
        facingDirection = facing;
        castingStats = stats;

        // Set local scale for facing direction
        transform.localScale = new Vector3(facingDirection, 1f, 1f);

        // Create visual clone
        visualClone = new GameObject("VisualClone");
        visualClone.transform.SetParent(transform, false);

        SpriteRenderer sr = visualClone.AddComponent<SpriteRenderer>();
        sr.sprite = characterSprite;
        sr.sortingOrder = 5;

        if (animatorController != null)
        {
            Animator anim = visualClone.AddComponent<Animator>();
            anim.runtimeAnimatorController = animatorController;

            PlayerCharacter player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player != null && player.Animator != null && player.Animator.anim != null)
            {
                Animator playerAnim = player.Animator.anim;
                AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                anim.Play(stateInfo.shortNameHash, 0, stateInfo.normalizedTime);
            }
        }

        SetupHitbox();

        logicTimeCounter = (float)startFrame / LOGIC_FPS;
    }

    private void SetupHitbox()
    {
        PlayerCharacter player = Object.FindFirstObjectByType<PlayerCharacter>();
        if (player != null && player.Combat != null && player.Combat.hitboxObject != null)
        {
            enemyLayerMask = player.Combat.enemyLayerMask;

            GameObject hitboxGo = Object.Instantiate(player.Combat.hitboxObject, transform);
            hitboxGo.SetActive(false);
            cloneHitboxDamage = hitboxGo.GetComponent<HitBoxDamage>();

            cloneCollider = hitboxGo.GetComponent<Collider2D>();
            HitBoxController controller = hitboxGo.GetComponent<HitBoxController>();
            if (controller != null && controller.hitboxCollider != null)
            {
                cloneCollider = controller.hitboxCollider;
            }
        }
    }

    /// <summary>Runs the deterministic physics and frame ticking logic.</summary>
    private void FixedUpdate()
    {
        AdvanceFrames(Time.fixedDeltaTime);
    }

    private void AdvanceFrames(float deltaTime)
    {
        if (ultimateStep == null) return;

        logicTimeCounter += deltaTime;
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * LOGIC_FPS + 0.001f);

        while (currentFrame < targetFrame)
        {
            currentFrame++;
            ActionPhase currentPhase = ultimateStep.GetPhase(currentFrame);

            if (currentPhase == ActionPhase.Done)
            {
                Terminate();
                break;
            }

            int logicFrame = ultimateStep.GetLogicFrame(currentFrame);
            ProcessLogicFrame(logicFrame, currentPhase);
        }
    }

    /// <summary>Processes hitboxes, spawns VFX, and applies damage formulas.</summary>
    private void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        if (ultimateStep == null) return;

        int frameInActivePhase = frame - ultimateStep.startupFrames - 1;
        if (phase == ActionPhase.Active && ultimateStep.hitData != null && ultimateStep.hitData.hitboxes != null)
        {
            for (int i = 0; i < ultimateStep.hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = ultimateStep.hitData.hitboxes[i];
                if (hitbox == null) continue;

                if (frameInActivePhase == hitbox.startFrameOffset)
                {
                    TriggerHitbox(hitbox);
                }

                if (frameInActivePhase == hitbox.startFrameOffset + hitbox.activeFrames)
                {
                    DisableHitbox();
                }
            }
        }
        else
        {
            DisableHitbox();
        }

        if (isHitboxActive && cloneCollider != null)
        {
            PerformHitboxOverlapCheck();
        }
    }

    private void TriggerHitbox(HitboxBase hitbox)
    {
        if (hitbox is NormalHitbox normalHitbox)
        {
            if (cloneCollider != null)
            {
                if (cloneCollider is BoxCollider2D box && normalHitbox.colliderType == HitboxColliderType.Box)
                {
                    box.offset = normalHitbox.offset;
                    box.size = normalHitbox.size;
                }
                else if (cloneCollider is CircleCollider2D circle && normalHitbox.colliderType == HitboxColliderType.Circle)
                {
                    circle.offset = normalHitbox.offset;
                    circle.radius = normalHitbox.radius;
                }
            }

            var damageData = GetFinalAttackDamage(normalHitbox.damageMultiplier);
            if (cloneHitboxDamage != null)
            {
                cloneHitboxDamage.SetHitData(
                    damageData.damage,
                    damageData.isCrit,
                    false,
                    0f,
                    0f,
                    normalHitbox,
                    AttackCategory.Ultimate,
                    ultimateStep.stepName,
                    ultimateStep,
                    null);
                cloneHitboxDamage.ResetActivationState();
            }

            if (cloneHitboxDamage != null && cloneHitboxDamage.gameObject != null)
            {
                cloneHitboxDamage.gameObject.SetActive(true);
            }
            isHitboxActive = true;
        }
    }

    private void DisableHitbox()
    {
        isHitboxActive = false;
        if (cloneHitboxDamage != null && cloneHitboxDamage.gameObject != null)
        {
            cloneHitboxDamage.gameObject.SetActive(false);
        }
    }

    private void PerformHitboxOverlapCheck()
    {
        if (cloneCollider == null) return;

        Vector2 worldPos = cloneCollider.transform.TransformPoint(cloneCollider.offset);
        Vector2 size = Vector2.one;
        float angle = cloneCollider.transform.eulerAngles.z;

        if (cloneCollider is BoxCollider2D box)
        {
            size = box.size;
        }
        else if (cloneCollider is CircleCollider2D circle)
        {
            size = new Vector2(circle.radius * 2f, circle.radius * 2f);
        }

        int count = Physics2D.OverlapBoxNonAlloc(worldPos, size, angle, overlapResults, enemyLayerMask);
        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = overlapResults[i].GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                cloneHitboxDamage?.ProcessHit(enemy);
            }
            overlapResults[i] = null; // Prevent memory leak of Unity GameObjects
        }
    }

    private (float damage, bool isCrit) GetFinalAttackDamage(float hitFrameMultiplier)
    {
        if (castingStats == null)
        {
            return (hitFrameMultiplier, false);
        }

        float attackStat = castingStats.attack;
        float finalDamage = CombatFormula.CalculateOutgoingDamage(
            baseAttack: attackStat,
            actionMultiplier: hitFrameMultiplier,
            critRate: castingStats.critRate,
            critDmg: castingStats.critDamage,
            skillMastery: castingStats.skillMastery,
            dmgMastery: castingStats.damageMastery,
            isSkillOrUlt: true,
            out bool isCrit);

        return (finalDamage, isCrit);
    }

    /// <summary>Cleans up the executor and destroys the clone GameObject.</summary>
    private void Terminate()
    {
        if (visualClone != null)
        {
            Destroy(visualClone);
        }
        Destroy(gameObject);
    }
}
