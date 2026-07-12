using UnityEngine;
using System.Collections.Generic;

public class PlayerTeamSwitchState : MonoBehaviour
{
    private AttackStepData actionStep;
    private int currentFrame;
    private int facingDirection;
    private CharacterStats castingStats;
    private float sourceAttack;
    private float actionDamageSourceValue;
    private float actionDamageMultiplier;
    private ActionDamageSourceStat actionDamageSourceStat = ActionDamageSourceStat.Attack;
    private float auraEnergyReward;
    private AuraEnergyRewardClaim auraEnergyRewardClaim;
    private AdditionalDamageEffectDefinition[] additionalDamageEffects;
    private PlayerCombat sourceCombat;
    private CharacterObject sourceCharacter;
    private string sourceActionId;
    private string sourceActionName;
    private GameObject visualClone;
    private AttackCategory attackCategory;

    private HitBoxDamage cloneHitboxDamage;
    private Collider2D cloneCollider;
    private LayerMask enemyLayerMask;
    private bool isHitboxActive = false;
    private readonly Collider2D[] overlapResults = new Collider2D[20];

    private float logicTimeCounter = 0f;
    private const float LOGIC_FPS = 60f;

    public static PlayerTeamSwitchState Spawn(
        GameObject playerGameObject,
        AttackStepData currentStep,
        int currentFrame,
        int facing,
        CharacterStats stats,
        PlayerCombat sourceCombat,
        CharacterObject castingCharacter,
        string actionName,
        Sprite currentSprite,
        RuntimeAnimatorController animController,
        AttackCategory category)
    {
        GameObject executorGo = new GameObject("PlayerTeamSwitchState_" + (castingCharacter.metaData != null ? castingCharacter.metaData.displayName : "Clone"));
        executorGo.transform.position = playerGameObject.transform.position;
        executorGo.transform.rotation = playerGameObject.transform.rotation;

        // Copy Rigidbody2D velocity to preserve the motion trajectory
        Rigidbody2D playerRb = playerGameObject.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Rigidbody2D cloneRb = executorGo.AddComponent<Rigidbody2D>();
            cloneRb.bodyType = RigidbodyType2D.Dynamic;
            cloneRb.gravityScale = playerRb.gravityScale;
            cloneRb.linearVelocity = playerRb.linearVelocity;
            cloneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
            cloneRb.sharedMaterial = playerRb.sharedMaterial;
        }

        // Copy Colliders to preserve physical behavior (ground checks, wall collision)
        Collider2D[] playerColliders = playerGameObject.GetComponentsInChildren<Collider2D>();
        List<Collider2D> clonedColliders = new List<Collider2D>();
        foreach (var col in playerColliders)
        {
            if (col == null || col.isTrigger) continue; // Skip trigger colliders (like hitboxes/hurtboxes)

            Collider2D cloneCol = null;
            if (col is BoxCollider2D box)
            {
                BoxCollider2D cloneBox = executorGo.AddComponent<BoxCollider2D>();
                cloneBox.offset = box.offset;
                cloneBox.size = box.size;
                cloneBox.sharedMaterial = box.sharedMaterial;
                cloneBox.isTrigger = false;
                cloneCol = cloneBox;
            }
            else if (col is CircleCollider2D circle)
            {
                CircleCollider2D cloneCircle = executorGo.AddComponent<CircleCollider2D>();
                cloneCircle.offset = circle.offset;
                cloneCircle.radius = circle.radius;
                cloneCircle.sharedMaterial = circle.sharedMaterial;
                cloneCircle.isTrigger = false;
                cloneCol = cloneCircle;
            }
            else if (col is CapsuleCollider2D capsule)
            {
                CapsuleCollider2D cloneCapsule = executorGo.AddComponent<CapsuleCollider2D>();
                cloneCapsule.offset = capsule.offset;
                cloneCapsule.size = capsule.size;
                cloneCapsule.direction = capsule.direction;
                cloneCapsule.sharedMaterial = capsule.sharedMaterial;
                cloneCapsule.isTrigger = false;
                cloneCol = cloneCapsule;
            }

            if (cloneCol != null)
            {
                clonedColliders.Add(cloneCol);
            }
        }

        // Ignore collision between all cloned colliders and all player colliders
        foreach (var cloneCol in clonedColliders)
        {
            foreach (var playerCol in playerColliders)
            {
                if (playerCol != null)
                {
                    Physics2D.IgnoreCollision(cloneCol, playerCol, true);
                }
            }
        }

        PlayerTeamSwitchState executor = executorGo.AddComponent<PlayerTeamSwitchState>();
        executor.Initialize(
            currentStep,
            currentFrame,
            facing,
            stats,
            sourceCombat != null ? sourceCombat.CurrentSourceAttack : (stats != null ? stats.attack : 0f),
            sourceCombat != null ? sourceCombat.CurrentActionDamageSourceValue : ResolveActionDamageSourceValue(stats, ActionDamageSourceStat.Attack),
            sourceCombat != null ? sourceCombat.CurrentActionDamageMultiplier : 0f,
            sourceCombat != null ? sourceCombat.CurrentActionDamageSourceStat : ActionDamageSourceStat.Attack,
            sourceCombat != null ? sourceCombat.CurrentAuraEnergyReward : 0f,
            sourceCombat != null ? sourceCombat.CurrentAuraEnergyRewardClaim : null,
            sourceCombat != null ? sourceCombat.CurrentAdditionalDamageEffects : null,
            sourceCombat,
            castingCharacter,
            sourceCombat != null ? sourceCombat.CurrentActionId : null,
            sourceCombat != null ? sourceCombat.CurrentActionName : actionName,
            currentSprite,
            animController,
            category);
        return executor;
    }

    /// <summary>Initializes and starts the background action simulation.</summary>
    public void Initialize(
        AttackStepData step,
        int startFrame,
        int facing,
        CharacterStats stats,
        float resolvedSourceAttack,
        float resolvedActionDamageSourceValue,
        float resolvedActionDamageMultiplier,
        ActionDamageSourceStat resolvedActionDamageSourceStat,
        float resolvedAuraEnergyReward,
        AuraEnergyRewardClaim resolvedAuraEnergyRewardClaim,
        AdditionalDamageEffectDefinition[] resolvedAdditionalDamageEffects,
        PlayerCombat resolvedSourceCombat,
        CharacterObject resolvedSourceCharacter,
        string resolvedSourceActionId,
        string resolvedSourceActionName,
        Sprite characterSprite,
        RuntimeAnimatorController animatorController,
        AttackCategory category)
    {
        actionStep = step;
        currentFrame = startFrame;
        facingDirection = facing;
        castingStats = stats;
        sourceAttack = Mathf.Max(0f, resolvedSourceAttack);
        actionDamageSourceValue = Mathf.Max(0f, resolvedActionDamageSourceValue);
        actionDamageMultiplier = Mathf.Max(0f, resolvedActionDamageMultiplier);
        actionDamageSourceStat = resolvedActionDamageSourceStat;
        auraEnergyReward = Mathf.Max(0f, resolvedAuraEnergyReward);
        auraEnergyRewardClaim = resolvedAuraEnergyRewardClaim;
        additionalDamageEffects = resolvedAdditionalDamageEffects
            ?? System.Array.Empty<AdditionalDamageEffectDefinition>();
        sourceCombat = resolvedSourceCombat;
        sourceCharacter = resolvedSourceCharacter;
        sourceActionId = resolvedSourceActionId;
        sourceActionName = resolvedSourceActionName;
        attackCategory = category;

        // Set Y rotation for facing direction (since player flips via Y rotation)
        transform.rotation = Quaternion.Euler(0f, facingDirection == -1 ? 180f : 0f, 0f);
        transform.localScale = Vector3.one;

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

            PlayerCharacter player = PlayerCharacter.Instance;
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
        PlayerCharacter player = PlayerCharacter.Instance;
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
        if (actionStep == null) return;

        logicTimeCounter += deltaTime;
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * LOGIC_FPS + 0.001f);

        bool frameSequenceDone = false;
        while (currentFrame < targetFrame)
        {
            currentFrame++;
            ActionPhase currentPhase = actionStep.GetPhase(currentFrame);

            if (currentPhase == ActionPhase.Done)
            {
                frameSequenceDone = true;
                break;
            }

            int logicFrame = actionStep.GetLogicFrame(currentFrame);
            ProcessLogicFrame(logicFrame, currentPhase);
        }

        if (frameSequenceDone)
        {
            bool animatorDone = true;
            if (visualClone != null)
            {
                Animator anim = visualClone.GetComponent<Animator>();
                if (anim != null && anim.runtimeAnimatorController != null && !anim.IsInTransition(0))
                {
                    AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                    if (!stateInfo.loop && stateInfo.normalizedTime < 1.0f)
                    {
                        animatorDone = false;
                    }
                }
            }

            if (animatorDone)
            {
                Terminate();
            }
        }
    }

    /// <summary>Processes hitboxes, spawns VFX, and applies damage formulas.</summary>
    private void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        if (actionStep == null) return;

        int frameInActivePhase = frame - actionStep.startupFrames - 1;
        if (phase == ActionPhase.Active && actionStep.hitData != null && actionStep.hitData.hitboxes != null)
        {
            for (int i = 0; i < actionStep.hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = actionStep.hitData.hitboxes[i];
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
            if (cloneCollider is BoxCollider2D box)
            {
                box.offset = normalHitbox.offset;
                box.size = normalHitbox.size;
            }

            float baseDamage = GetBaseActionDamage(normalHitbox.damageRatio);
            if (cloneHitboxDamage != null)
            {
                cloneHitboxDamage.SetHitData(
                    baseDamage,
                    false,
                    false,
                    0f,
                    0f,
                    normalHitbox,
                    attackCategory,
                    sourceActionName ?? actionStep.stepName,
                    actionStep,
                    null,
                    sourceActionId);
                ConfigurePerEnemyDamageCalculation(cloneHitboxDamage);
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
            overlapResults[i] = null;
        }
    }

    private float GetBaseActionDamage(float hitFrameMultiplier)
    {
        if (castingStats == null)
        {
            return hitFrameMultiplier;
        }

        float multiplier = actionDamageMultiplier > 0f
            ? actionDamageMultiplier * Mathf.Clamp01(hitFrameMultiplier)
            : Mathf.Max(0f, hitFrameMultiplier);
        float sourceValue = actionDamageSourceValue > 0f
            ? actionDamageSourceValue
            : ResolveActionDamageSourceValue(castingStats, actionDamageSourceStat);
        return Mathf.Max(0f, sourceValue * multiplier);
    }

    private static float ResolveActionDamageSourceValue(CharacterStats stats, ActionDamageSourceStat sourceStat)
    {
        if (stats == null)
        {
            return 0f;
        }

        return sourceStat switch
        {
            ActionDamageSourceStat.MaxHP => Mathf.Max(0f, stats.maxHP),
            ActionDamageSourceStat.Defense => Mathf.Max(0f, stats.defense),
            _ => Mathf.Max(0f, stats.attack)
        };
    }

    private void ConfigurePerEnemyDamageCalculation(HitBoxDamage targetHitBoxDamage)
    {
        if (targetHitBoxDamage == null || castingStats == null)
        {
            return;
        }

        targetHitBoxDamage.ConfigurePerEnemyDamageCalculation(
            castingStats,
            sourceAttack,
            true,
            additionalDamageEffects,
            sourceCombat,
            sourceCharacter,
            auraEnergyRewardClaim);
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
