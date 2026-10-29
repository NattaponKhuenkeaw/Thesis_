using UnityEngine;
using System.Collections.Generic;

public class PlayerTeamSwitchState : MonoBehaviour
{
    private static readonly List<PlayerTeamSwitchState> activeClones = new List<PlayerTeamSwitchState>();
    private readonly List<Collider2D> myClonedColliders = new List<Collider2D>();

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
    private float lifetimeCounter = 0f;
    private const float LOGIC_FPS = 60f;
    private float maximumCloneLifetime = 3f;
    private MotionKeyframe capturedTargetMotion;
    private Vector2 capturedTargetMotionDelta;
    private Rigidbody2D cachedBody;
    private Animator cachedCloneAnimator;
    private int syncedAnimationLayer = -1;
    private int syncedAnimationStateHash;

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
        executorGo.layer = playerGameObject.layer;
        executorGo.transform.position = playerGameObject.transform.position;
        executorGo.transform.rotation = playerGameObject.transform.rotation;

        // Copy Rigidbody2D velocity to preserve the motion trajectory
        Rigidbody2D playerRb = playerGameObject.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            Rigidbody2D cloneRb = executorGo.AddComponent<Rigidbody2D>();
            cloneRb.bodyType = RigidbodyType2D.Dynamic;
            cloneRb.gravityScale = playerRb.gravityScale;

            if (currentStep != null && currentStep.motionData != null && currentStep.motionData.keyframes != null && currentStep.motionData.keyframes.Count > 0)
            {
                cloneRb.linearVelocity = Vector2.zero;
            }
            else
            {
                cloneRb.linearVelocity = playerRb.linearVelocity;
            }

            cloneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
            cloneRb.sharedMaterial = playerRb.sharedMaterial;
        }

        // Copy Colliders to preserve physical behavior (ground checks, wall collision)
        Collider2D[] playerColliders = playerGameObject.GetComponentsInChildren<Collider2D>(true);
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

        // Ignore collision between all cloned colliders and all current/future player colliders
        IgnoreCollisionsWithPlayer(playerGameObject, clonedColliders);

        // Ignore collision between clone colliders and all existing active clones' colliders
        IgnoreCollisionsWithOtherClones(clonedColliders);

        // Ignore collision between clone colliders and all enemy colliders
        IgnoreCollisionsWithEnemies(clonedColliders);

        SpriteRenderer playerSr = playerGameObject.GetComponentInChildren<SpriteRenderer>();

        PlayerTeamSwitchState executor = executorGo.AddComponent<PlayerTeamSwitchState>();
        activeClones.Add(executor);
        executor.myClonedColliders.AddRange(clonedColliders);

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
            currentSprite != null ? currentSprite : (playerSr != null ? playerSr.sprite : null),
            animController,
            category,
            playerSr);
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
        AttackCategory category,
        SpriteRenderer sourceSpriteRenderer = null)
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
        cachedBody = GetComponent<Rigidbody2D>();
        if (resolvedSourceCombat != null && resolvedSourceCombat.movement != null)
            resolvedSourceCombat.movement.TryGetResolvedTargetMotion(out capturedTargetMotion, out capturedTargetMotionDelta);

        // Capture source sprite world position and rotation BEFORE modifying executor root
        Vector3 sourceWorldPos = sourceSpriteRenderer != null ? sourceSpriteRenderer.transform.position : transform.position;
        Quaternion sourceWorldRot = sourceSpriteRenderer != null ? sourceSpriteRenderer.transform.rotation : transform.rotation;
        Vector3 sourceLocalScale = sourceSpriteRenderer != null ? sourceSpriteRenderer.transform.localScale : Vector3.one;

        // Set Y rotation for facing direction (since player flips via Y rotation)
        transform.rotation = Quaternion.Euler(0f, facingDirection == -1 ? 180f : 0f, 0f);

        // Create visual clone preserving exact world position and orientation of original sprite transform
        visualClone = new GameObject("VisualClone");
        visualClone.layer = gameObject.layer;
        visualClone.transform.SetParent(transform, true);

        if (sourceSpriteRenderer != null)
        {
            visualClone.transform.position = sourceWorldPos;
            visualClone.transform.rotation = sourceWorldRot;
            visualClone.transform.localScale = sourceLocalScale;
        }

        SpriteRenderer sr = visualClone.AddComponent<SpriteRenderer>();
        sr.sprite = characterSprite;

        if (sourceSpriteRenderer != null)
        {
            sr.sortingLayerID = sourceSpriteRenderer.sortingLayerID;
            sr.sortingOrder = sourceSpriteRenderer.sortingOrder;
            sr.flipX = sourceSpriteRenderer.flipX;
            sr.flipY = sourceSpriteRenderer.flipY;
            sr.color = sourceSpriteRenderer.color;
            if (sourceSpriteRenderer.sharedMaterial != null)
            {
                sr.sharedMaterial = sourceSpriteRenderer.sharedMaterial;
            }
        }
        else
        {
            sr.sortingOrder = 5;
        }

        if (animatorController != null)
        {
            Animator anim = visualClone.AddComponent<Animator>();
            cachedCloneAnimator = anim;
            anim.runtimeAnimatorController = animatorController;

            PlayerCharacter player = PlayerCharacter.Instance;
            if (player != null && player.Animator != null && player.Animator.anim != null)
            {
                Animator playerAnim = player.Animator.anim;
                AnimatorStateInfo stateInfo = playerAnim.GetCurrentAnimatorStateInfo(0);
                anim.Play(stateInfo.shortNameHash, 0, stateInfo.normalizedTime);
            }

            if (step != null && step.syncAnimationToActionFrames && step.animationClip != null)
            {
                int stateHash = Animator.StringToHash(step.animationClip.name);
                for (int layer = 0; layer < anim.layerCount; layer++)
                {
                    if (!anim.HasState(layer, stateHash)) continue;
                    syncedAnimationLayer = layer;
                    syncedAnimationStateHash = stateHash;
                    // These states are driven by the action clock on the live actor too.
                    anim.speed = 0f;
                    SampleSyncedAnimation(startFrame);
                    break;
                }
            }
        }

        SetupHitbox();

        logicTimeCounter = (float)startFrame / LOGIC_FPS;
        float clipLength = step?.animationClip != null ? step.animationClip.length : 0f;
        maximumCloneLifetime = Mathf.Max(3f, GetMaxExecutionFrame() / LOGIC_FPS + clipLength + 3f);
        RestoreOngoingHitbox(startFrame);
        ProcessMotionData(startFrame);
    }

    private void RestoreOngoingHitbox(int frame)
    {
        if (actionStep?.hitData?.hitboxes == null) return;
        int activeFrame = frame - actionStep.startupFrames - 1;
        foreach (HitboxBase hitbox in actionStep.hitData.hitboxes)
        {
            if (!(hitbox is NormalHitbox normal) || activeFrame < hitbox.startFrameOffset
                || activeFrame >= hitbox.startFrameOffset + hitbox.activeFrames) continue;
            TriggerHitbox(hitbox);
            if (sourceCombat?.hitBoxDamage != null && sourceCombat.hitBoxDamage.IsConfiguredFor(normal))
                sourceCombat.hitBoxDamage.CopyHitHistoryTo(cloneHitboxDamage);
        }
    }

    private void SetupHitbox()
    {
        PlayerCharacter player = PlayerCharacter.Instance;
        if (player != null && player.Combat != null && player.Combat.hitboxObject != null)
        {
            enemyLayerMask = player.Combat.enemyLayerMask;

            GameObject hitboxGo = Object.Instantiate(player.Combat.hitboxObject, transform);
            hitboxGo.layer = gameObject.layer;
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

    private Vector3 initialVisualLocalPos = Vector3.zero;
    private float baselineVisualLocalY = 0f;
    private Vector3 initialRootPosition = Vector3.zero;
    private float baselineRootPositionX = 0f;
    private bool isMotionBaselineInitialized = false;

    /// <summary>Runs the deterministic physics and frame ticking logic.</summary>
    private void FixedUpdate()
    {
        PlayerCharacter player = PlayerCharacter.Instance;
        if (player != null && player.gameObject != null)
        {
            IgnoreCollisionsWithPlayer(player.gameObject, myClonedColliders);
        }

        AdvanceFrames(Time.fixedDeltaTime);
    }

    private int GetMaxExecutionFrame()
    {
        if (actionStep == null) return 0;
        int maxFrame = actionStep.startupFrames + actionStep.activeFrames + actionStep.recoveryFrames;
        if (actionStep.motionData != null && actionStep.motionData.keyframes != null)
        {
            foreach (var k in actionStep.motionData.keyframes)
            {
                if (k != null)
                {
                    maxFrame = Mathf.Max(maxFrame, k.endFrame);
                }
            }
        }
        return maxFrame;
    }

    private void AdvanceFrames(float deltaTime)
    {
        lifetimeCounter += deltaTime;
        if (lifetimeCounter >= maximumCloneLifetime)
        {
            Terminate();
            return;
        }

        if (actionStep == null) return;

        logicTimeCounter += deltaTime;
        int targetFrame = Mathf.FloorToInt(logicTimeCounter * LOGIC_FPS + 0.001f);
        int maxExecutionFrame = GetMaxExecutionFrame();

        targetFrame = Mathf.Min(targetFrame, maxExecutionFrame);
        bool frameSequenceDone = currentFrame >= maxExecutionFrame;
        while (currentFrame < targetFrame)
        {
            currentFrame++;
            if (currentFrame >= maxExecutionFrame)
            {
                frameSequenceDone = true;
            }

            ActionPhase currentPhase = actionStep.GetPhase(currentFrame);
            int logicFrame = actionStep.GetLogicFrame(currentFrame);
            ProcessLogicFrame(logicFrame, currentPhase);

            if (frameSequenceDone)
            {
                break;
            }
        }

        if (frameSequenceDone)
        {
            bool animatorDone = true;
            if (cachedCloneAnimator != null && syncedAnimationLayer < 0)
            {
                Animator anim = cachedCloneAnimator;
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

    private void SampleSyncedAnimation(int frame)
    {
        if (cachedCloneAnimator == null || syncedAnimationLayer < 0 || actionStep.TotalFrames <= 0) return;
        cachedCloneAnimator.Play(syncedAnimationStateHash, syncedAnimationLayer,
            Mathf.Clamp01((float)frame / actionStep.TotalFrames));
        cachedCloneAnimator.Update(0f);
    }

    /// <summary>Processes hitboxes, spawns VFX, and applies damage formulas.</summary>
    private void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        if (actionStep == null) return;

        SampleSyncedAnimation(frame);
        ProcessMotionData(frame);

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

    private void ProcessMotionData(int frame)
    {
        if (actionStep == null || actionStep.motionData == null || actionStep.motionData.keyframes == null)
        {
            return;
        }

        if (!isMotionBaselineInitialized)
        {
            float startFrameAuthoredY = EvaluateAuthoredVisualOffsetY(currentFrame);
            float startFrameAuthoredX = EvaluateAuthoredMotionOffsetX(currentFrame);

            if (visualClone != null)
            {
                initialVisualLocalPos = visualClone.transform.localPosition;
                baselineVisualLocalY = initialVisualLocalPos.y - startFrameAuthoredY;
            }

            initialRootPosition = transform.position;
            baselineRootPositionX = initialRootPosition.x - startFrameAuthoredX;
            isMotionBaselineInitialized = true;
        }

        float currentOffsetY = EvaluateAuthoredVisualOffsetY(frame);
        if (visualClone != null)
        {
            visualClone.transform.localPosition = new Vector3(
                initialVisualLocalPos.x,
                baselineVisualLocalY + currentOffsetY,
                initialVisualLocalPos.z);
        }

        float currentOffsetX = EvaluateAuthoredMotionOffsetX(frame);
        Rigidbody2D cloneRb = cachedBody;
        if (cloneRb != null)
        {
            cloneRb.position = new Vector2(baselineRootPositionX + currentOffsetX, cloneRb.position.y);
        }
        else
        {
            transform.position = new Vector3(baselineRootPositionX + currentOffsetX, transform.position.y, transform.position.z);
        }
    }

    private float EvaluateAuthoredVisualOffsetY(int frame)
    {
        if (actionStep == null || actionStep.motionData == null || actionStep.motionData.keyframes == null)
            return 0f;

        float totalOffsetY = 0f;
        foreach (MotionKeyframe k in actionStep.motionData.keyframes)
        {
            if (k == null || Mathf.Abs(k.position.y) < 0.0001f) continue;

            if (frame <= k.startFrame)
            {
                continue;
            }
            else if (frame >= k.endFrame)
            {
                totalOffsetY += k.position.y;
            }
            else
            {
                float duration = k.endFrame - k.startFrame;
                float progress = Mathf.Clamp01((float)(frame - k.startFrame) / duration);
                float easedProgress = TweenEase(0f, 1f, progress, k.easing);
                totalOffsetY += k.position.y * easedProgress;
            }
        }
        return totalOffsetY;
    }

    private float EvaluateAuthoredMotionOffsetX(int frame)
    {
        if (actionStep == null || actionStep.motionData == null || actionStep.motionData.keyframes == null)
            return 0f;

        float totalOffsetX = 0f;
        float dir = facingDirection;

        foreach (MotionKeyframe k in actionStep.motionData.keyframes)
        {
            if (k == null) continue;
            // Targeted dash displacement was resolved against the original captured enemy,
            // and must not revert to the generic authored fallback when control changes.
            float deltaX = ReferenceEquals(k, capturedTargetMotion) ? capturedTargetMotionDelta.x : k.position.x * dir;
            if (Mathf.Abs(deltaX) < 0.0001f) continue;

            if (frame <= k.startFrame)
            {
                continue;
            }
            else if (frame >= k.endFrame)
            {
                totalOffsetX += deltaX;
            }
            else
            {
                float duration = k.endFrame - k.startFrame;
                float progress = Mathf.Clamp01((float)(frame - k.startFrame) / duration);
                float easedProgress = TweenEase(0f, 1f, progress, k.easing);
                totalOffsetX += deltaX * easedProgress;
            }
        }
        return totalOffsetX;
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

    private static void IgnoreCollisionsWithPlayer(GameObject playerGo, List<Collider2D> cloneCols)
    {
        if (playerGo == null || cloneCols == null) return;
        Collider2D[] playerColliders = playerGo.GetComponentsInChildren<Collider2D>(true);
        foreach (var cloneCol in cloneCols)
        {
            if (cloneCol == null) continue;
            foreach (var playerCol in playerColliders)
            {
                if (playerCol != null)
                {
                    Physics2D.IgnoreCollision(cloneCol, playerCol, true);
                }
            }
        }
    }

    private static void IgnoreCollisionsWithOtherClones(List<Collider2D> cloneCols)
    {
        if (cloneCols == null) return;
        for (int i = activeClones.Count - 1; i >= 0; i--)
        {
            PlayerTeamSwitchState otherClone = activeClones[i];
            if (otherClone == null || otherClone.myClonedColliders == null) continue;
            foreach (var myCol in cloneCols)
            {
                if (myCol == null) continue;
                foreach (var otherCol in otherClone.myClonedColliders)
                {
                    if (otherCol != null)
                    {
                        Physics2D.IgnoreCollision(myCol, otherCol, true);
                    }
                }
            }
        }
    }

    private static void IgnoreCollisionsWithEnemies(List<Collider2D> cloneCols)
    {
        if (cloneCols == null) return;
        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            Collider2D[] enemyCols = enemy.GetComponentsInChildren<Collider2D>(true);
            foreach (var myCol in cloneCols)
            {
                if (myCol == null) continue;
                foreach (var enemyCol in enemyCols)
                {
                    if (enemyCol != null)
                    {
                        Physics2D.IgnoreCollision(myCol, enemyCol, true);
                    }
                }
            }
        }
    }

    private static float TweenEase(float f, float t, float time, LeanTweenType e)
    {
        switch (e)
        {
            case LeanTweenType.linear: return LeanTween.linear(f, t, time);
            case LeanTweenType.easeInQuad: return LeanTween.easeInQuad(f, t, time);
            case LeanTweenType.easeOutQuad: return LeanTween.easeOutQuad(f, t, time);
            case LeanTweenType.easeInOutQuad: return LeanTween.easeInOutQuad(f, t, time);
            case LeanTweenType.easeInCubic: return LeanTween.easeInCubic(f, t, time);
            case LeanTweenType.easeOutCubic: return LeanTween.easeOutCubic(f, t, time);
            case LeanTweenType.easeInOutCubic: return LeanTween.easeInOutCubic(f, t, time);
            case LeanTweenType.easeInSine: return LeanTween.easeInSine(f, t, time);
            case LeanTweenType.easeOutSine: return LeanTween.easeOutSine(f, t, time);
            case LeanTweenType.easeInOutSine: return LeanTween.easeInOutSine(f, t, time);
            default: return Mathf.Lerp(f, t, time);
        }
    }

    private void OnDestroy()
    {
        activeClones.Remove(this);
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
