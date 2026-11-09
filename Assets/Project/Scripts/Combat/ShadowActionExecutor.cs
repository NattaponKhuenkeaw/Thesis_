using System.Collections.Generic;
using UnityEngine;

public class ShadowActionExecutor : MonoBehaviour
{
    private static readonly Dictionary<CharacterObject, int> activeCountByCharacter = new Dictionary<CharacterObject, int>();

    private CharacterObject sourceCharacter;
    private EnemyController targetEnemy;
    private PassiveCoordinatedActionDefinition definition;
    private AttackStepData actionStep;
    private int currentFrame;
    private int facingDirection = 1;
    private float sourceAttack;
    private float actionDamageMultiplier;
    private string actionName;
    private string actionId;

    private GameObject visualClone;
    private SpriteRenderer cloneSr;
    private Animator cloneAnim;
    private HitBoxDamage cloneHitboxDamage;
    private HitBoxController cloneHitBoxController;
    private BoxCollider2D cloneBoxCollider;
    private LayerMask enemyLayerMask;
    private bool isHitboxActive;
    private bool hasRewardedAura;
    private CombatActionType presentationActionType;

    private readonly Collider2D[] overlapResults = new Collider2D[20];
    private float logicTimeCounter;
    private float lifetimeCounter;
    private const float LOGIC_FPS = 60f;
    private const float MAX_LIFETIME = 4.0f;

    public static int GetActiveCount(CharacterObject character)
    {
        if (character == null) return 0;
        return activeCountByCharacter.TryGetValue(character, out int count) ? count : 0;
    }

    private static void IncrementCount(CharacterObject character)
    {
        if (character == null) return;
        activeCountByCharacter[character] = GetActiveCount(character) + 1;
    }

    private static void DecrementCount(CharacterObject character)
    {
        if (character == null) return;
        int count = GetActiveCount(character);
        if (count <= 1)
        {
            activeCountByCharacter.Remove(character);
        }
        else
        {
            activeCountByCharacter[character] = count - 1;
        }
    }

    public static bool TryResolveAction(
        CharacterObject character,
        string desiredActionId,
        out AttackStepData stepData,
        out float damageMultiplier,
        out string resolvedActionName,
        out string resolvedActionId)
        => TryResolveAction(character, desiredActionId, out stepData, out damageMultiplier,
            out resolvedActionName, out resolvedActionId, out _);

    private static bool TryResolveAction(
        CharacterObject character, string desiredActionId, out AttackStepData stepData,
        out float damageMultiplier, out string resolvedActionName, out string resolvedActionId,
        out CombatActionType presentationType)
    {
        presentationType = CombatActionType.FromInput;
        stepData = null;
        damageMultiplier = 1f;
        resolvedActionName = desiredActionId;
        resolvedActionId = desiredActionId;

        if (character == null || character.actionResolverTable == null)
        {
            return false;
        }

        ActionResolverTable table = character.actionResolverTable;

        // Search for exact desired actionId in all resolver actions
        List<CombatAction> allActions = new List<CombatAction>();
        if (table.skillActions != null) allActions.AddRange(table.skillActions);
        if (table.comboActions != null) allActions.AddRange(table.comboActions);
        if (table.ultimateActions != null) allActions.AddRange(table.ultimateActions);
        if (table.standaloneActions != null) allActions.AddRange(table.standaloneActions);

        for (int i = 0; i < allActions.Count; i++)
        {
            CombatAction item = allActions[i];
            if (item == null) continue;

            bool matches = string.Equals(item.actionId, desiredActionId, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.actionName, desiredActionId, System.StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(item.actionId) && !string.IsNullOrEmpty(desiredActionId) &&
                    (item.actionId.EndsWith("." + desiredActionId, System.StringComparison.OrdinalIgnoreCase)
                     || desiredActionId.EndsWith("." + item.actionId, System.StringComparison.OrdinalIgnoreCase)));

            if (matches)
            {
                if (item.comboTree != null && item.comboTree.rootNode != null && item.comboTree.rootNode.stepData != null)
                {
                    stepData = item.comboTree.rootNode.stepData;
                    damageMultiplier = item.actionDamageMultiplier > 0f ? item.actionDamageMultiplier : 1f;
                    resolvedActionName = item.actionName;
                    resolvedActionId = item.actionId;
                    presentationType = item.EffectiveActionType;
                    return true;
                }
            }
        }

        // Graceful fallback if desired actionId (e.g. "skill.ex") is not yet authored
        if (table.skillActions != null && table.skillActions.Count > 0)
        {
            CombatAction fallback = table.skillActions[0];
            if (fallback != null && fallback.comboTree != null && fallback.comboTree.rootNode != null && fallback.comboTree.rootNode.stepData != null)
            {
                Debug.Log($"[ShadowActionExecutor] ActionId '{desiredActionId}' not found for {character.characterId}; falling back gracefully to '{fallback.actionName}'.");
                stepData = fallback.comboTree.rootNode.stepData;
                damageMultiplier = fallback.actionDamageMultiplier > 0f ? fallback.actionDamageMultiplier : 1f;
                resolvedActionName = fallback.actionName;
                resolvedActionId = fallback.actionId;
                presentationType = fallback.EffectiveActionType;
                return true;
            }
        }

        if (table.comboActions != null && table.comboActions.Count > 0)
        {
            CombatAction fallback = table.comboActions[0];
            if (fallback != null && fallback.comboTree != null && fallback.comboTree.rootNode != null && fallback.comboTree.rootNode.stepData != null)
            {
                stepData = fallback.comboTree.rootNode.stepData;
                damageMultiplier = fallback.actionDamageMultiplier > 0f ? fallback.actionDamageMultiplier : 1f;
                resolvedActionName = fallback.actionName;
                resolvedActionId = fallback.actionId;
                presentationType = fallback.EffectiveActionType;
                return true;
            }
        }

        return false;
    }

    public static ShadowActionExecutor Spawn(
        CharacterObject character,
        EnemyController targetEnemy,
        PassiveCoordinatedActionDefinition coordDef)
    {
        if (character == null || targetEnemy == null || coordDef == null)
        {
            return null;
        }

        int maxActive = coordDef.maxSimultaneousActive > 0 ? coordDef.maxSimultaneousActive : 1;
        if (GetActiveCount(character) >= maxActive)
        {
            return null;
        }

        if (!TryResolveAction(
            character,
            coordDef.actionId,
            out AttackStepData step,
            out float damageMult,
            out string actName,
            out string actId,
            out CombatActionType presentationType))
        {
            return null;
        }

        Vector3 enemyPos = targetEnemy.transform.position;
        float enemyFacing = EnemyFacingUtil.GetFacingSign(targetEnemy.transform);
        int shadowFacing = enemyFacing >= 0 ? 1 : -1;
        // Spawn 1.0 unit behind enemy, facing towards the enemy so hitbox covers enemy directly
        Vector3 spawnPos = enemyPos - new Vector3(enemyFacing * 1.0f, 0f, 0f);
        spawnPos.y = enemyPos.y;

        GameObject executorGo = new GameObject($"ShadowActionExecutor_{character.characterId}");
        executorGo.transform.position = spawnPos;
        executorGo.transform.rotation = Quaternion.Euler(0f, shadowFacing == -1 ? 180f : 0f, 0f);

        ShadowActionExecutor executor = executorGo.AddComponent<ShadowActionExecutor>();
        executor.presentationActionType = presentationType;
        IncrementCount(character);

        executor.Initialize(
            character,
            targetEnemy,
            coordDef,
            step,
            shadowFacing,
            damageMult,
            actName,
            actId);

        return executor;
    }

    private void Initialize(
        CharacterObject character,
        EnemyController target,
        PassiveCoordinatedActionDefinition coordDef,
        AttackStepData step,
        int facing,
        float damageMult,
        string actName,
        string actId)
    {
        sourceCharacter = character;
        targetEnemy = target;
        definition = coordDef;
        actionStep = step;
        facingDirection = facing;
        actionDamageMultiplier = damageMult;
        actionName = actName;
        actionId = actId;

        sourceAttack = character.stats != null ? character.stats.attack : 10f;
        if (PlayerCharacter.Instance != null && PlayerCharacter.Instance.characterObject == character)
        {
            sourceAttack = PlayerCharacter.Instance.Combat != null
                ? PlayerCharacter.Instance.Combat.CurrentSourceAttack
                : (character.stats != null ? character.stats.attack : 10f);
        }

        // Setup Visual Clone
        visualClone = new GameObject("VisualClone");
        visualClone.transform.SetParent(transform, false);
        cloneSr = visualClone.AddComponent<SpriteRenderer>();

        Sprite initialSprite = null;
        RuntimeAnimatorController animController = character.metaData != null ? character.metaData.animatorController : null;

        if (character.previewPrefab != null)
        {
            SpriteRenderer prefabSr = character.previewPrefab.GetComponentInChildren<SpriteRenderer>();
            if (prefabSr != null)
            {
                initialSprite = prefabSr.sprite;
                cloneSr.sortingLayerID = prefabSr.sortingLayerID;
                cloneSr.sortingOrder = prefabSr.sortingOrder + 1;
            }
        }
        else if (PlayerCharacter.Instance != null && PlayerCharacter.Instance.characterObject == character)
        {
            SpriteRenderer playerSr = PlayerCharacter.Instance.GetComponentInChildren<SpriteRenderer>();
            if (playerSr != null)
            {
                initialSprite = playerSr.sprite;
                cloneSr.sortingLayerID = playerSr.sortingLayerID;
                cloneSr.sortingOrder = playerSr.sortingOrder + 1;
            }
        }

        cloneSr.sprite = initialSprite;
        Color tint = definition.shadowColor.a > 0.01f ? definition.shadowColor : new Color(0.12f, 0.08f, 0.18f, 0.85f);
        cloneSr.color = tint;

        if (animController != null)
        {
            cloneAnim = visualClone.AddComponent<Animator>();
            cloneAnim.runtimeAnimatorController = animController;
            if (actionStep.animationClip != null)
            {
                cloneAnim.Play(actionStep.animationClip.name, 0, 0f);
            }
        }

        SetupHitbox();
    }

    private void SetupHitbox()
    {
        PlayerCharacter player = PlayerCharacter.Instance;
        if (player != null && player.Combat != null && player.Combat.hitboxObject != null)
        {
            enemyLayerMask = player.Combat.enemyLayerMask;
            GameObject hitboxGo = Object.Instantiate(player.Combat.hitboxObject, transform, false);
            hitboxGo.layer = player.gameObject.layer;
            hitboxGo.transform.localPosition = Vector3.zero;
            hitboxGo.transform.localRotation = Quaternion.identity;
            hitboxGo.transform.localScale = Vector3.one;
            hitboxGo.SetActive(false);

            cloneHitboxDamage = hitboxGo.GetComponent<HitBoxDamage>();
            cloneHitBoxController = hitboxGo.GetComponent<HitBoxController>();
            if (cloneHitBoxController != null)
            {
                cloneHitBoxController.worldAnchor = transform;
                cloneHitBoxController.attackAnchor = transform;
                cloneHitBoxController.ConfigureAnchoring(HitboxAnchorMode.Self, transform);
            }

            cloneBoxCollider = cloneHitBoxController != null && cloneHitBoxController.hitboxCollider != null
                ? cloneHitBoxController.hitboxCollider
                : hitboxGo.GetComponent<BoxCollider2D>();
        }
        else
        {
            enemyLayerMask = LayerMask.GetMask("Enemy");
            GameObject hitboxGo = new GameObject("HitBox");
            hitboxGo.transform.SetParent(transform, false);
            hitboxGo.transform.localPosition = Vector3.zero;
            hitboxGo.transform.localRotation = Quaternion.identity;
            hitboxGo.transform.localScale = Vector3.one;

            cloneBoxCollider = hitboxGo.AddComponent<BoxCollider2D>();
            cloneBoxCollider.isTrigger = true;
            cloneHitboxDamage = hitboxGo.AddComponent<HitBoxDamage>();
            hitboxGo.SetActive(false);
        }

        if (targetEnemy != null)
        {
            enemyLayerMask |= (1 << targetEnemy.gameObject.layer);
        }
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            enemyLayerMask |= (1 << enemyLayer);
        }
    }

    private void FixedUpdate()
    {
        lifetimeCounter += Time.fixedDeltaTime;
        if (lifetimeCounter >= MAX_LIFETIME)
        {
            Destroy(gameObject);
            return;
        }

        AdvanceFrames(Time.fixedDeltaTime);
    }

    private void AdvanceFrames(float deltaTime)
    {
        logicTimeCounter += deltaTime;
        int targetLogicFrame = Mathf.FloorToInt(logicTimeCounter * LOGIC_FPS);
        int maxFrames = GetMaxExecutionFrame();

        bool frameSequenceDone = false;
        while (currentFrame < targetLogicFrame)
        {
            currentFrame++;
            if (currentFrame >= maxFrames)
            {
                frameSequenceDone = true;
            }

            ActionPhase currentPhase = actionStep != null ? actionStep.GetPhase(currentFrame) : ActionPhase.Recovery;
            int logicFrame = actionStep != null ? actionStep.GetLogicFrame(currentFrame) : currentFrame;
            ProcessLogicFrame(logicFrame, currentPhase);

            if (frameSequenceDone)
            {
                break;
            }
        }

        if (frameSequenceDone)
        {
            Destroy(gameObject);
        }
    }

    private int GetMaxExecutionFrame()
    {
        if (actionStep == null) return 60;
        return actionStep.startupFrames + actionStep.activeFrames + actionStep.recoveryFrames;
    }

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

        if (isHitboxActive && cloneBoxCollider != null)
        {
            PerformHitboxOverlapCheck();
        }
    }

    private void TriggerHitbox(HitboxBase hitbox)
    {
        if (hitbox is NormalHitbox normalHitbox)
        {
            float scaleMult = definition != null && definition.hitboxScaleMultiplier > 0f
                ? definition.hitboxScaleMultiplier
                : 1f;

            if (cloneHitBoxController != null)
            {
                cloneHitBoxController.ApplyHitbox(normalHitbox, 0f, scaleMult);
            }
            else if (cloneBoxCollider != null)
            {
                cloneBoxCollider.offset = normalHitbox.offset;
                cloneBoxCollider.size = normalHitbox.size * scaleMult;
            }

            float baseDamage;
            var liveStats = CombatSourceStatResolver.Resolve(sourceCharacter, sourceCharacter?.stats, SourceStatReadMode.LiveOnResolve);
            float damageSource = definition != null && definition.overrideDamageSource
                ? definition.damageSourceStat == ActionDamageSourceStat.Defense ? liveStats?.defense ?? 0f
                    : definition.damageSourceStat == ActionDamageSourceStat.MaxHP ? liveStats?.maxHP ?? 0f : liveStats?.attack ?? sourceAttack
                : liveStats?.attack ?? sourceAttack;
            if (definition != null && definition.damageMultiplierOverride > 0f)
            {
                float totalWeight = 0f;
                foreach (var authoredHit in actionStep.hitData.hitboxes)
                    if (authoredHit is NormalHitbox normal)
                        totalWeight += normal.damageRatio > 0f ? normal.damageRatio : 1f;
                float weight = normalHitbox.damageRatio > 0f ? normalHitbox.damageRatio : 1f;
                baseDamage = damageSource * definition.damageMultiplierOverride * weight / Mathf.Max(weight, totalWeight);
            }
            else
            {
                baseDamage = damageSource * actionDamageMultiplier * (normalHitbox.damageRatio > 0f ? normalHitbox.damageRatio : 1f);
            }

            if (cloneHitboxDamage != null)
            {
                cloneHitboxDamage.SetHitData(
                    baseDamage,
                    false,
                    false,
                    0f,
                    0f,
                    normalHitbox,
                    AttackCategory.Skill,
                    actionName,
                    actionStep,
                    null,
                    actionId);

                CharacterStats stats = null;
                if (TeamLoader.Instance != null && sourceCharacter != null)
                {
                    stats = TeamLoader.Instance.GetCharacterRuntimeStats(sourceCharacter);
                }
                if (stats == null && sourceCharacter != null)
                {
                    stats = sourceCharacter.stats;
                }
                PlayerCombat sourceCombat = PlayerCharacter.Instance != null ? PlayerCharacter.Instance.Combat : null;

                cloneHitboxDamage.ConfigurePerEnemyDamageCalculation(
                    stats,
                    sourceAttack,
                    true,
                    null,
                    sourceCombat,
                    sourceCharacter,
                    null,
                    presentationType: presentationActionType,
                    suppressNativeAuraEnergy: definition != null && definition.auraEnergyRewardOnHit > 0f);

                cloneHitboxDamage.ResetActivationState();
                cloneHitboxDamage.gameObject.SetActive(true);
            }

            isHitboxActive = true;
        }
    }

    private void DisableHitbox()
    {
        if (cloneHitboxDamage != null && cloneHitboxDamage.gameObject != null)
        {
            cloneHitboxDamage.gameObject.SetActive(false);
        }
        isHitboxActive = false;
    }

    private void PerformHitboxOverlapCheck()
    {
        if (cloneBoxCollider == null || !cloneBoxCollider.gameObject.activeInHierarchy) return;

        Vector2 worldPos = cloneBoxCollider.transform.TransformPoint(cloneBoxCollider.offset);
        Vector2 size = cloneBoxCollider.size;
        float angle = cloneBoxCollider.transform.eulerAngles.z;

        int hitCount = Physics2D.OverlapBoxNonAlloc(worldPos, size, angle, overlapResults, enemyLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit == null) continue;

            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                if (cloneHitboxDamage != null)
                {
                    cloneHitboxDamage.ProcessHit(enemy);
                }

                // Aura Energy reward: single grant per shadow execution
                if (cloneHitboxDamage != null && cloneHitboxDamage.LastDamageDealt > 0f && definition != null && definition.auraEnergyRewardOnHit > 0f && !hasRewardedAura)
                {
                    hasRewardedAura = true;
                    if (TeamLoader.Instance != null && sourceCharacter != null)
                    {
                        TeamLoader.Instance.AddCharacterAE(sourceCharacter, definition.auraEnergyRewardOnHit);
                    }
                }
            }
            overlapResults[i] = null;
        }

        // Direct guarantee check for target enemy
        if (targetEnemy != null && targetEnemy.IsAlive)
        {
            Collider2D enemyCol = targetEnemy.bodyCollider != null ? targetEnemy.bodyCollider : targetEnemy.GetComponentInChildren<Collider2D>();
            if (enemyCol != null && cloneBoxCollider.bounds.Intersects(enemyCol.bounds))
            {
                if (cloneHitboxDamage != null)
                {
                    cloneHitboxDamage.ProcessHit(targetEnemy);
                }

                if (definition != null && definition.auraEnergyRewardOnHit > 0f && !hasRewardedAura)
                {
                    hasRewardedAura = true;
                    if (TeamLoader.Instance != null && sourceCharacter != null)
                    {
                        TeamLoader.Instance.AddCharacterAE(sourceCharacter, definition.auraEnergyRewardOnHit);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        DecrementCount(sourceCharacter);
    }
}
