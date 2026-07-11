using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.ShopSystem;
using UnityEngine;

public interface IPassiveEffectHook
{
    void Initialize(CharacterPassiveController controller, PotentialNodeDefinition node);
    void OnActionStarted(CharacterPassiveController controller, PassiveEventContext context);
    void OnHitDealt(CharacterPassiveController controller, PassiveEventContext context);
}

public class CharacterPassiveController : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    [SerializeField] private PlayerMetaProgression metaProgression;

    [Header("Runtime State")]
    [SerializeField] private CombatRuntimeState runtimeState = new CombatRuntimeState();

    private readonly List<(PotentialNodeDefinition node, IPassiveEffectHook hook)> activeHooks =
        new List<(PotentialNodeDefinition node, IPassiveEffectHook hook)>();
    private readonly Dictionary<string, List<EnemyController>> runtimeTagHitTargets =
        new Dictionary<string, List<EnemyController>>();

    public CombatRuntimeState RuntimeState => runtimeState;
    public PlayerMetaProgression MetaProgression => metaProgression;

    void Awake()
    {
        if (playerCharacter == null)
        {
            playerCharacter = GetComponent<PlayerCharacter>();
        }

        ResolveMetaProgression();
        InitializeFromCharacterData();
    }

    void Update()
    {
        runtimeState?.Tick();
    }

    public void SetMetaProgression(PlayerMetaProgression progression)
    {
        metaProgression = progression;
        RebuildHooks();
    }

    public void InitializeFromCharacterData()
    {
        ResolveMetaProgression();
        runtimeState.Reset();
        runtimeTagHitTargets.Clear();
        RebuildHooks();
        RefreshConditionalStateEffects();
    }

    public CharacterKitDefinition GetKitDefinition()
    {
        return playerCharacter != null && playerCharacter.characterObject != null
            ? playerCharacter.characterObject.kitDefinition
            : null;
    }

    public int GetPotentialLevel(PotentialTrack track)
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        return metaProgression != null && character != null
            ? metaProgression.GetPotentialLevel(character, track)
            : 0;
    }

    public PotentialNodeDefinition GetNodeById(string nodeId)
    {
        return GetKitDefinition()?.GetNodeById(nodeId);
    }

    public int GetRuntimeEffectStackCount(string effectIdOrStackKey)
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        if (character == null || string.IsNullOrWhiteSpace(effectIdOrStackKey))
        {
            return 0;
        }

        PlayerRunInventory inventory = ResolveRunInventory(ResolveTeamLoader());
        if (inventory != null)
        {
            return inventory.GetRuntimeEffectStackCount(character, effectIdOrStackKey);
        }

        return playerCharacter.RuntimeEffects != null
            ? playerCharacter.RuntimeEffects.GetStackCount(effectIdOrStackKey)
            : 0;
    }

    public IEnumerable<PotentialNodeDefinition> GetActiveNodes()
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        CharacterKitDefinition kit = character != null ? character.kitDefinition : null;
        if (kit == null)
        {
            yield break;
        }

        foreach (PotentialNodeDefinition node in kit.EnumerateAllNodes())
        {
            if (IsNodeActive(character, node))
            {
                yield return node;
            }
        }
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        PotentialNodeDefinition node = GetNodeById(nodeId);
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        return IsNodeActive(character, node);
    }

    public bool CanUnlockNode(PotentialNodeDefinition node)
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        if (character == null || node == null || node.level <= 0 || metaProgression == null)
        {
            return false;
        }

        int currentLevel = metaProgression.GetPotentialLevel(character, node.track);
        if (currentLevel >= node.level)
        {
            return false;
        }

        if (node.level != currentLevel + 1)
        {
            return false;
        }

        int cost = Mathf.Max(0, node.fragmentCost);
        return cost == 0 || metaProgression.GetFragmentCount(character.characterId) >= cost;
    }

    public bool TryUpgradePotential(PotentialTrack track)
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        if (character == null || metaProgression == null)
        {
            return false;
        }

        bool changed = metaProgression.TryUpgradePotential(character, track);
        if (changed)
        {
            RebuildHooks();
        }

        return changed;
    }

    public bool UnlockNodeById(string nodeId)
    {
        PotentialNodeDefinition node = GetNodeById(nodeId);
        if (node == null || !CanUnlockNode(node))
        {
            return false;
        }

        return TryUpgradePotential(node.track);
    }

    public bool LockNodeById(string nodeId)
    {
        PotentialNodeDefinition node = GetNodeById(nodeId);
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        if (character == null || node == null || node.level <= 0 || metaProgression == null)
        {
            return false;
        }

        int currentLevel = metaProgression.GetPotentialLevel(character, node.track);
        if (currentLevel < node.level)
        {
            return false;
        }

        bool changed = metaProgression.SetPotentialLevel(character, node.track, node.level - 1);
        if (changed)
        {
            RebuildHooks();
        }

        return changed;
    }

    public void ResetPotentialLevels()
    {
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        if (metaProgression != null && character != null)
        {
            metaProgression.SetPotentialLevel(character, PotentialTrack.Skill, 0);
            metaProgression.SetPotentialLevel(character, PotentialTrack.Ultimate, 0);
            metaProgression.SetPotentialLevel(character, PotentialTrack.Passive, 0);
        }

        runtimeState.Reset();
        RebuildHooks();
    }

    public float ModifyValue(PassiveModifierTarget target, float baseValue, PassiveEffectScope scope = PassiveEffectScope.Self)
    {
        return ModifyValueInternal(target, baseValue, scope, runtimeOnly: false);
    }

    public float ModifyRuntimeValue(PassiveModifierTarget target, float baseValue, PassiveEffectScope scope = PassiveEffectScope.Self)
    {
        return ModifyValueInternal(target, baseValue, scope, runtimeOnly: true);
    }

    private float ModifyValueInternal(
        PassiveModifierTarget target,
        float baseValue,
        PassiveEffectScope scope,
        bool runtimeOnly)
    {
        float additive = 0f;
        float multiplier = 1f;

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveStatModifierDefinition> modifiers = node != null ? node.statModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveStatModifierDefinition modifier = modifiers[i];
                if (modifier == null || modifier.target != target || modifier.scope != scope)
                {
                    continue;
                }

                if (runtimeOnly && !modifier.HasRuntimeRequirements)
                {
                    continue;
                }

                if (!modifier.RuntimeRequirementsMet(runtimeState))
                {
                    continue;
                }

                switch (modifier.operation)
                {
                    case PassiveNumericOperation.Add:
                        additive += ResolveStatModifierValue(modifier);
                        break;
                    case PassiveNumericOperation.Multiply:
                        multiplier *= ResolveStatModifierValue(modifier);
                        break;
                }
            }
        }

        return (baseValue + additive) * multiplier;
    }

    private float ResolveStatModifierValue(PassiveStatModifierDefinition modifier)
    {
        if (modifier == null)
        {
            return 0f;
        }

        float resolvedValue;
        if (modifier.valueSource == PassiveModifierValueSource.SourceStatRatio)
        {
            float sourceValue = ResolveResourceValue(modifier.sourceStat, modifier.statRatioValueSource);
            resolvedValue = sourceValue * modifier.value;
        }
        else
        {
            resolvedValue = modifier.value;
        }

        if (modifier.scaleValueByCounter)
        {
            int counterValue = runtimeState != null ? runtimeState.GetCounter(modifier.valueCounterId) : 0;
            if (modifier.maxCounterScale > 0)
            {
                counterValue = Mathf.Min(counterValue, modifier.maxCounterScale);
            }

            resolvedValue *= Mathf.Max(0, counterValue);
        }

        return resolvedValue;
    }

    public float ModifyCooldown(AttackCategory category, string actionName, float baseValue)
    {
        return ModifyCooldown(category, null, actionName, baseValue);
    }

    public float ModifyCooldown(AttackCategory category, string actionId, string actionName, float baseValue)
    {
        float result = baseValue;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            result = ApplyCooldownModifiers(node, category, actionId, actionName, result);

            List<PassiveActionModifierDefinition> actionModifiers = node != null ? node.actionModifiers : null;
            if (actionModifiers == null)
            {
                continue;
            }

            for (int i = 0; i < actionModifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = actionModifiers[i];
                if (modifier != null && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    result = ApplyValueModifier(result, modifier.cooldownOperation, modifier.cooldownValue);
                }
            }
        }

        return Mathf.Max(0.05f, result);
    }

    public float ModifyActionDamageMultiplier(AttackCategory category, string actionId, string actionName, float baseValue)
    {
        float result = baseValue;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    result = ApplyValueModifier(result, modifier.damageMultiplierOperation, modifier.damageMultiplierValue);
                }
            }
        }

        return Mathf.Max(0f, result);
    }

    public float ModifyActionCriticalDamage(AttackCategory category, string actionId, string actionName, float baseValue)
    {
        float result = baseValue;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    result = ApplyValueModifier(result, modifier.criticalDamageOperation, modifier.criticalDamageValue);
                }
            }
        }

        return Mathf.Max(0f, result);
    }

    public float ModifyActionFinalDamage(
        AttackCategory category,
        string actionId,
        string actionName,
        float baseValue,
        bool isCriticalHit)
    {
        float result = baseValue;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null
                    || !modifier.Matches(category, actionId, actionName, runtimeState)
                    || (modifier.finalDamageRequiresCriticalHit && !isCriticalHit))
                {
                    continue;
                }

                result = ApplyValueModifier(result, modifier.finalDamageOperation, modifier.finalDamageValue);
            }
        }

        return Mathf.Max(0f, result);
    }

    public float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float baseValue)
    {
        if (context == null)
        {
            return Mathf.Max(0f, baseValue);
        }

        float additive = 0f;
        float multiplier = 1f;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveStatModifierDefinition> modifiers = node != null ? node.statModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveStatModifierDefinition modifier = modifiers[i];
                if (!MatchesOutgoingDamageModifier(modifier, context.category)
                    || modifier.scope != PassiveEffectScope.Self
                    || !modifier.RuntimeRequirementsMet(runtimeState))
                {
                    continue;
                }

                switch (modifier.operation)
                {
                    case PassiveNumericOperation.Add:
                        additive += ResolveStatModifierValue(modifier);
                        break;
                    case PassiveNumericOperation.Multiply:
                        multiplier *= ResolveStatModifierValue(modifier);
                        break;
                }
            }
        }

        return Mathf.Max(0f, (baseValue + additive) * multiplier);
    }

    private static bool MatchesOutgoingDamageModifier(
        PassiveStatModifierDefinition modifier,
        AttackCategory category)
    {
        if (modifier == null)
        {
            return false;
        }

        if (modifier.target == PassiveModifierTarget.OutgoingDamage)
        {
            return !modifier.filterOutgoingDamageByCategory
                || modifier.outgoingDamageCategory == category;
        }

        // Serialized compatibility for authored Potential nodes created before
        // Basic/Skill/Ultimate damage were consolidated into Outgoing Damage.
        return modifier.target switch
        {
            PassiveModifierTarget.BasicAttackDamage => category == AttackCategory.Normal,
            PassiveModifierTarget.SkillDamage => category == AttackCategory.Skill,
            PassiveModifierTarget.UltimateDamage => category == AttackCategory.Ultimate,
            _ => false
        };
    }

    public void ModifyActionDamageInput(
        AttackCategory category,
        string actionId,
        string actionName,
        string stepName,
        RuntimeEffectDamageInput input)
    {
        if (input == null)
        {
            return;
        }

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null
                    || !modifier.Matches(category, actionId, actionName, stepName, runtimeState))
                {
                    continue;
                }

                if (modifier.ignoreDefense)
                {
                    input.defenderDefense = 0f;
                }

                if (modifier.ignoreDamageResistance)
                {
                    input.defenderDamageResistance = 0f;
                }

                if (modifier.ignoreElementalIncomingModifiers)
                {
                    input.ignoreElementalIncomingModifiers = true;
                }
            }
        }
    }

    public float ModifyAuraEnergyReward(AttackCategory category, string actionId, string actionName, float baseValue)
    {
        float result = baseValue;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    result += modifier.auraEnergyRewardAdd;
                }
            }
        }

        return Mathf.Max(0f, result);
    }

    public float ModifyUltimateEnergyCost(string actionId, string actionName, float baseValue)
    {
        float result = baseValue;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null && modifier.Matches(AttackCategory.Ultimate, actionId, actionName, runtimeState))
                {
                    result = ApplyValueModifier(result, modifier.energyCostOperation, modifier.energyCostValue);
                }
            }
        }

        return Mathf.Max(0f, result);
    }

    public bool IsActionUnlocked(string actionName)
    {
        return IsActionUnlocked(AttackCategory.Normal, null, actionName);
    }

    public bool IsActionUnlocked(AttackCategory category, string actionId, string actionName)
    {
        bool isUnlocked = true;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    isUnlocked = modifier.unlocked;
                }
            }
        }

        return isUnlocked;
    }

    public bool TryGetActionAvailabilityWindow(
        AttackCategory category,
        string actionId,
        string actionName,
        out float remainingSeconds,
        out float durationSeconds,
        bool ignoreRuntimeRequirements = false)
    {
        remainingSeconds = 0f;
        durationSeconds = 0f;

        if (runtimeState == null)
        {
            return false;
        }

        bool foundWindow = false;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null
                    || !modifier.unlocked
                    || !modifier.Matches(category, actionId, actionName, runtimeState, ignoreRuntimeRequirements)
                    || modifier.requiredTags == null
                    || modifier.requiredTags.Count == 0)
                {
                    continue;
                }

                float candidateRemaining = 0f;
                float candidateDuration = 0f;
                bool hasTimedRequirement = false;

                for (int requirementIndex = 0; requirementIndex < modifier.requiredTags.Count; requirementIndex++)
                {
                    RuntimeTagRequirement requirement = modifier.requiredTags[requirementIndex];
                    if (requirement == null
                        || !requirement.expectedValue
                        || string.IsNullOrWhiteSpace(requirement.tag)
                        || !runtimeState.TryGetTagTiming(requirement.tag, out float tagRemaining, out float tagDuration))
                    {
                        continue;
                    }

                    if (!hasTimedRequirement || tagRemaining < candidateRemaining)
                    {
                        candidateRemaining = tagRemaining;
                        candidateDuration = tagDuration;
                    }

                    hasTimedRequirement = true;
                }

                if (!hasTimedRequirement || candidateRemaining <= remainingSeconds)
                {
                    continue;
                }

                remainingSeconds = candidateRemaining;
                durationSeconds = Mathf.Max(candidateDuration, candidateRemaining);
                foundWindow = true;
            }
        }

        return foundWindow;
    }

    public int GetActionPriorityModifier(string actionName)
    {
        return GetActionPriorityModifier(AttackCategory.Normal, null, actionName);
    }

    public int GetActionPriorityModifier(AttackCategory category, string actionId, string actionName)
    {
        int total = 0;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    total += modifier.priorityBonus;
                }
            }
        }

        return total;
    }

    public bool TryGetReplacementActionName(string actionName, out string replacementActionName)
    {
        return TryGetReplacementActionKey(AttackCategory.Normal, null, actionName, out _, out replacementActionName);
    }

    public bool TryGetReplacementActionKey(
        AttackCategory category,
        string actionId,
        string actionName,
        out string replacementActionId,
        out string replacementActionName)
    {
        replacementActionId = null;
        replacementActionName = null;

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null || !modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(modifier.replacementActionId))
                {
                    replacementActionId = modifier.replacementActionId;
                }

                if (!string.IsNullOrWhiteSpace(modifier.replacementActionName))
                {
                    replacementActionName = modifier.replacementActionName;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(replacementActionId) || !string.IsNullOrWhiteSpace(replacementActionName);
    }

    public bool TryGetProjectileReplacement(
        AttackCategory category,
        string actionId,
        string actionName,
        string stepName,
        out ProjectileType replacementProjectileType,
        out ProjectileData replacementProjectileData)
    {
        replacementProjectileType = ProjectileType.None;
        replacementProjectileData = null;

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null || !modifier.Matches(category, actionId, actionName, stepName, runtimeState))
                {
                    continue;
                }

                if (modifier.replacementProjectileType != ProjectileType.None)
                {
                    replacementProjectileType = modifier.replacementProjectileType;
                }

                if (modifier.replacementProjectileData != null)
                {
                    replacementProjectileData = modifier.replacementProjectileData;
                }
            }
        }

        return replacementProjectileType != ProjectileType.None || replacementProjectileData != null;
    }

    public bool TryGetProjectileReplacement(
        AttackCategory category,
        string actionId,
        string actionName,
        out ProjectileType replacementProjectileType,
        out ProjectileData replacementProjectileData)
    {
        return TryGetProjectileReplacement(
            category,
            actionId,
            actionName,
            null,
            out replacementProjectileType,
            out replacementProjectileData);
    }

    public int GetStartingComboDepthOverride(AttackCategory category, string actionId, string actionName)
    {
        int result = 0;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier != null
                    && modifier.startingComboDepthOverride > result
                    && modifier.Matches(category, actionId, actionName, runtimeState))
                {
                    result = modifier.startingComboDepthOverride;
                }
            }
        }

        return result;
    }

    public float GetTeamModifierValue(PassiveModifierTarget target)
    {
        return ModifyValue(target, 0f, PassiveEffectScope.Allies);
    }

    public void NotifyActionStarted(AttackCategory category, string actionName, AttackStepData stepData)
    {
        NotifyActionStarted(category, null, actionName, stepData);
    }

    public void NotifyActionStarted(AttackCategory category, string actionId, string actionName, AttackStepData stepData)
    {
        PassiveEventContext context = BuildContext(category, actionId, actionName, stepData, null);
        ApplyTriggeredEffects(PassiveTriggerType.OnActionStarted, context);

        for (int i = 0; i < activeHooks.Count; i++)
        {
            activeHooks[i].hook.OnActionStarted(this, context);
        }
    }

    public void NotifyActionEnded(AttackCategory category, string actionId, string actionName, AttackStepData stepData)
    {
        ApplyTriggeredEffects(PassiveTriggerType.OnActionEnded, BuildContext(category, actionId, actionName, stepData, null));
    }

    public void RefreshConditionalStateEffects()
    {
        ApplyConditionalEffects(
            GetActiveNodes(),
            PassiveTriggerType.Always,
            BuildContext(AttackCategory.Normal, null, "Runtime State Refresh", null, null));
    }

    public void NotifyAfterDash()
    {
        ApplyTriggeredEffects(PassiveTriggerType.OnAfterDash, BuildContext(AttackCategory.Normal, null, "Dash", null, null));
    }

    public void NotifyGrabSuccess(AttackCategory category, string actionId, string actionName, AttackStepData stepData, EnemyController enemy)
    {
        ApplyTriggeredEffects(PassiveTriggerType.OnGrabSuccess, BuildContext(category, actionId, actionName, stepData, enemy));
    }

    public void NotifyHitDealt(AttackCategory category, string actionName, AttackStepData stepData, EnemyController enemy)
    {
        NotifyHitDealt(category, null, actionName, stepData, enemy, ElementType.None, false, 0f, false);
    }

    public void NotifyHitDealt(
        AttackCategory category,
        string actionId,
        string actionName,
        AttackStepData stepData,
        EnemyController enemy,
        ElementType elementType,
        bool isCriticalHit,
        float damageDealt,
        bool killedEnemy)
    {
        PassiveEventContext context = BuildContext(category, actionId, actionName, stepData, enemy);
        context.elementType = elementType;
        context.isCriticalHit = isCriticalHit;
        context.damageDealt = damageDealt;
        context.killedEnemy = killedEnemy;
        context.enemyMaxHp = enemy != null ? enemy.maxHP : 0f;

        ApplyTriggeredEffects(PassiveTriggerType.OnHitDealt, context);
        if (isCriticalHit)
        {
            ApplyTriggeredEffects(PassiveTriggerType.OnCritHitDealt, context);
        }
        if (killedEnemy)
        {
            ApplyTriggeredEffects(PassiveTriggerType.OnKillDealt, context);
        }

        for (int i = 0; i < activeHooks.Count; i++)
        {
            activeHooks[i].hook.OnHitDealt(this, context);
        }
    }

    public bool TryEvadeIncomingDamage(float incomingDamage)
    {
        PassiveEventContext context = BuildContext(AttackCategory.Normal, null, "IncomingDamage", null, null);
        context.damageDealt = incomingDamage;
        ApplyTriggeredEffects(PassiveTriggerType.OnIncomingDamageCheck, context);

        float highestEvasionChance = 0f;
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveDefensiveEffectDefinition> effects = node != null ? node.defensiveEffects : null;
            if (effects == null)
            {
                continue;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                PassiveDefensiveEffectDefinition effect = effects[i];
                if (effect == null || effect.filter == null || !effect.filter.Matches(context))
                {
                    continue;
                }

                if (effect.grantsDamageImmunity)
                {
                    return true;
                }

                if (effect.evasionChance > highestEvasionChance)
                {
                    highestEvasionChance = effect.evasionChance;
                }
            }
        }

        return highestEvasionChance > 0f && UnityEngine.Random.value <= Mathf.Clamp01(highestEvasionChance);
    }

    public void NotifyDamageTaken(float damageTaken)
    {
        PassiveEventContext context = BuildContext(AttackCategory.Normal, null, "DamageTaken", null, null);
        context.damageDealt = damageTaken;
        ApplyTriggeredEffects(PassiveTriggerType.OnDamageTaken, context);
    }

    public bool HasDebuffImmunity()
    {
        PassiveEventContext context = BuildContext(AttackCategory.Normal, null, "DebuffCheck", null, null);
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveDefensiveEffectDefinition> effects = node != null ? node.defensiveEffects : null;
            if (effects == null)
            {
                continue;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                PassiveDefensiveEffectDefinition effect = effects[i];
                if (effect != null
                    && effect.grantsDebuffImmunity
                    && effect.filter != null
                    && effect.filter.Matches(context))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public AdditionalDamageEffectDefinition[] BuildAdditionalDamageEffectSnapshot(
        AttackCategory category,
        string actionName,
        AttackStepData stepData)
    {
        return BuildAdditionalDamageEffectSnapshot(category, null, actionName, stepData);
    }

    public AdditionalDamageEffectDefinition[] BuildAdditionalDamageEffectSnapshot(
        AttackCategory category,
        string actionId,
        string actionName,
        AttackStepData stepData)
    {
        List<AdditionalDamageEffectDefinition> effects = null;

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            AddAdditionalDamageEffects(node != null ? node.additionalDamageEffects : null, ref effects);
        }

        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            TeamLoader teamLoader = TeamLoader.Instance;
            if (teamLoader != null)
            {
                Artifact[] artifacts = teamLoader.GetEquippedArtifactsFor(playerCharacter.characterObject);
                if (artifacts != null)
                {
                    for (int i = 0; i < artifacts.Length; i++)
                    {
                        Artifact artifact = artifacts[i];
                        AddAdditionalDamageEffects(
                            artifact != null ? artifact.additionalDamageEffects : null,
                            ref effects);
                    }
                }
            }
        }

        return effects != null
            ? effects.ToArray()
            : Array.Empty<AdditionalDamageEffectDefinition>();
    }

    public PassiveDashThroughDamageDefinition[] BuildDashThroughDamageEffectSnapshot()
    {
        List<PassiveDashThroughDamageDefinition> effects = null;

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveDashThroughDamageDefinition> source = node != null ? node.dashThroughDamageEffects : null;
            if (source == null)
            {
                continue;
            }

            for (int i = 0; i < source.Count; i++)
            {
                PassiveDashThroughDamageDefinition effect = source[i];
                if (effect == null)
                {
                    continue;
                }

                effects ??= new List<PassiveDashThroughDamageDefinition>();
                effects.Add(effect);
            }
        }

        return effects != null
            ? effects.ToArray()
            : Array.Empty<PassiveDashThroughDamageDefinition>();
    }

    private void ResolveMetaProgression()
    {
        if (metaProgression == null)
        {
            metaProgression = Resources.Load<PlayerMetaProgression>("InventorySO/PlayerMetaProgression");
        }
    }

    private bool IsNodeActive(CharacterObject character, PotentialNodeDefinition node)
    {
        if (character == null || node == null)
        {
            return false;
        }

        if (node.unlockedByDefault || node.level <= 0)
        {
            return true;
        }

        return metaProgression != null && metaProgression.GetPotentialLevel(character, node.track) >= node.level;
    }

    private void ApplyTriggeredEffects(PassiveTriggerType trigger, PassiveEventContext context)
    {
        ApplyTriggeredEffects(GetActiveNodes(), trigger, context);
    }

    private void ApplyTriggeredEffects(IEnumerable<PotentialNodeDefinition> nodes, PassiveTriggerType trigger, PassiveEventContext context)
    {
        foreach (PotentialNodeDefinition node in nodes)
        {
            if (node == null)
            {
                continue;
            }

            ApplyRuntimeTagEffects(node.runtimeTagEffects, trigger, context);
            ApplyRuntimeEffects(node, node.runtimeEffects, trigger, context);
            ApplyCounterEffects(node.counterEffects, trigger, context);
            ApplyConditionalEffects(node.conditionalStateEffects, trigger, context);
            ApplyOnHitEffects(node.onHitStatusEffects, trigger, context);
            ApplyResourceEffects(node.resourceEffects, trigger, context);
            ApplyEnemyElementModifiers(node.enemyElementModifiers, trigger, context);
        }
    }

    private void ApplyConditionalEffects(IEnumerable<PotentialNodeDefinition> nodes, PassiveTriggerType trigger, PassiveEventContext context)
    {
        foreach (PotentialNodeDefinition node in nodes)
        {
            ApplyConditionalEffects(node != null ? node.conditionalStateEffects : null, trigger, context);
        }
    }

    private void ApplyRuntimeTagEffects(
        List<PassiveRuntimeTagEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveRuntimeTagEffectDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || effect.filter == null || !effect.filter.Matches(context))
            {
                continue;
            }

            ApplyRuntimeTagEffect(effect, context);
        }
    }

    private void ApplyRuntimeEffects(
        PotentialNodeDefinition ownerNode,
        List<PassiveRuntimeEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveRuntimeEffectDefinition effect = effects[i];
            if (effect == null
                || effect.ResolvedEffect == null
                || effect.trigger != trigger
                || effect.filter == null
                || !effect.filter.Matches(context)
                || !PassiveRequirementsMet(
                    effect.requiredTags,
                    effect.requiredCounters,
                    effect.requiredRuntimeEffectStacks,
                    effect.requiredValues,
                    context)
                || (!effect.rollChancePerEnemyTarget
                    && UnityEngine.Random.value > Mathf.Clamp01(effect.chance)))
            {
                continue;
            }

            if (IsRuntimeEffectSuperseded(ownerNode, effect, trigger, context))
            {
                continue;
            }

            bool applied = ApplyRuntimeEffect(effect, context);
            if (effect.consumeRuntimeTagOnApply
                && (applied || effect.enemyTargetSource == PassiveRuntimeEnemyTargetSource.RuntimeTagHitTargets))
            {
                ConsumeRuntimeEffectTag(effect);
            }
        }
    }

    private bool IsRuntimeEffectSuperseded(
        PotentialNodeDefinition ownerNode,
        PassiveRuntimeEffectDefinition effect,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        RuntimeEffectDefinition resolvedEffect = effect != null ? effect.ResolvedEffect : null;
        if (ownerNode == null || ownerNode.level <= 0 || resolvedEffect == null)
        {
            return false;
        }

        foreach (PotentialNodeDefinition candidateNode in GetActiveNodes())
        {
            if (candidateNode == null
                || candidateNode == ownerNode
                || candidateNode.track != ownerNode.track
                || candidateNode.level <= ownerNode.level
                || candidateNode.runtimeEffects == null)
            {
                continue;
            }

            for (int i = 0; i < candidateNode.runtimeEffects.Count; i++)
            {
                PassiveRuntimeEffectDefinition candidate = candidateNode.runtimeEffects[i];
                if (candidate == null
                    || candidate.trigger != trigger
                    || candidate.ResolvedEffect == null
                    || candidate.filter == null
                    || !candidate.filter.Matches(context))
                {
                    continue;
                }

                if (string.Equals(
                    candidate.ResolvedEffect.StableId,
                    resolvedEffect.StableId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ApplyRuntimeEffect(PassiveRuntimeEffectDefinition effect, PassiveEventContext context)
    {
        RuntimeEffectDefinition resolvedEffect = effect != null ? effect.ResolvedEffect : null;
        RuntimeEffectRecipe resolvedRecipe = effect != null ? effect.ResolvedRecipe : null;
        if (effect == null || resolvedEffect == null || playerCharacter == null)
        {
            return false;
        }

        if (effect.applicationMode != PassiveRuntimeEffectApplicationMode.Apply)
        {
            return RemoveRuntimeEffect(effect, context);
        }

        RuntimeEffectApplyTarget applyTarget = resolvedRecipe != null && resolvedRecipe.policy != null
            ? resolvedRecipe.policy.appliesOn
            : ScopeToApplyTarget(effect.scope);
        TeamLoader teamLoader = ResolveTeamLoader();
        PlayerRunInventory inventory = ResolveRunInventory(teamLoader);
        ResolveRuntimeEffectSource(effect, teamLoader, inventory, out CharacterObject sourceCharacter, out CharacterStats sourceStats);
        int effectiveStackLimit = ResolveRuntimeEffectStackLimit(effect);

        if (applyTarget == RuntimeEffectApplyTarget.Enemy)
        {
            return ApplyRuntimeEffectToEnemyTargets(
                effect,
                context,
                resolvedEffect,
                resolvedRecipe,
                sourceCharacter,
                sourceStats,
                effectiveStackLimit);
        }

        if (inventory == null)
        {
            if (applyTarget == RuntimeEffectApplyTarget.Self
                && MatchesRuntimeEffectTarget(effect, sourceCharacter))
            {
                if (resolvedRecipe != null)
                {
                    ApplyRuntimeEffectToReceiver(
                        playerCharacter.RuntimeEffects,
                        resolvedRecipe,
                        sourceCharacter,
                        sourceStats,
                        effect.applicationCount,
                        effectiveStackLimit);
                }
                else
                {
                    ApplyRuntimeEffectToReceiver(
                        playerCharacter.RuntimeEffects,
                        resolvedEffect,
                        sourceCharacter,
                        sourceStats,
                        effect.durationOverride,
                        effect.stackMode,
                        effectiveStackLimit,
                        effect.refreshDurationOnReapply,
                        effect.applicationCount);
                }

                return true;
            }

            return false;
        }

        bool applied = false;
        CharacterObject[] team = ResolveRuntimeEffectTargets(teamLoader, inventory);
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null)
            {
                continue;
            }

            bool shouldApply = applyTarget switch
            {
                RuntimeEffectApplyTarget.Self => target == sourceCharacter,
                RuntimeEffectApplyTarget.Allies => true,
                _ => false
            };

            if (shouldApply && MatchesRuntimeEffectTarget(effect, target))
            {
                if (resolvedRecipe != null)
                {
                    ApplyRuntimeEffectToInventory(
                        inventory,
                        target,
                        resolvedRecipe,
                        sourceCharacter,
                        sourceStats,
                        effect.applicationCount,
                        effectiveStackLimit);
                }
                else
                {
                    ApplyRuntimeEffectToInventory(
                        inventory,
                        target,
                        resolvedEffect,
                        sourceCharacter,
                        sourceStats,
                        effect.durationOverride,
                        effect.stackMode,
                        effectiveStackLimit,
                        effect.refreshDurationOnReapply,
                        effect.applicationCount);
                }

                applied = true;
            }
        }

        return applied;
    }

    private bool RemoveRuntimeEffect(PassiveRuntimeEffectDefinition effect, PassiveEventContext context)
    {
        RuntimeEffectDefinition resolvedEffect = effect != null ? effect.ResolvedEffect : null;
        RuntimeEffectRecipe resolvedRecipe = effect != null ? effect.ResolvedRecipe : null;
        if (effect == null || resolvedEffect == null || playerCharacter == null)
        {
            return false;
        }

        RuntimeEffectApplyTarget applyTarget = resolvedRecipe != null && resolvedRecipe.policy != null
            ? resolvedRecipe.policy.appliesOn
            : ScopeToApplyTarget(effect.scope);
        if (applyTarget == RuntimeEffectApplyTarget.Enemy)
        {
            return false;
        }

        TeamLoader teamLoader = ResolveTeamLoader();
        PlayerRunInventory inventory = ResolveRunInventory(teamLoader);
        ResolveRuntimeEffectSource(effect, teamLoader, inventory, out CharacterObject sourceCharacter, out _);
        int count = Mathf.Max(1, effect.applicationCount);

        if (inventory == null)
        {
            if (applyTarget == RuntimeEffectApplyTarget.Self
                && MatchesRuntimeEffectTarget(effect, sourceCharacter))
            {
                return effect.applicationMode == PassiveRuntimeEffectApplicationMode.RemoveAll
                    ? playerCharacter.RuntimeEffects != null
                        && playerCharacter.RuntimeEffects.RemoveEffectStack(resolvedEffect, resolvedRecipe, int.MaxValue, sourceCharacter)
                    : playerCharacter.RuntimeEffects != null
                        && playerCharacter.RuntimeEffects.RemoveEffectStack(resolvedEffect, resolvedRecipe, count, sourceCharacter);
            }

            return false;
        }

        bool removed = false;
        CharacterObject[] team = ResolveRuntimeEffectTargets(teamLoader, inventory);
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null)
            {
                continue;
            }

            bool shouldRemove = applyTarget switch
            {
                RuntimeEffectApplyTarget.Self => target == sourceCharacter,
                RuntimeEffectApplyTarget.Allies => true,
                _ => false
            };

            if (!shouldRemove || !MatchesRuntimeEffectTarget(effect, target))
            {
                continue;
            }

            removed |= effect.applicationMode == PassiveRuntimeEffectApplicationMode.RemoveAll
                ? inventory.RemoveRuntimeEffect(target, resolvedEffect, resolvedRecipe, sourceCharacter)
                : inventory.RemoveRuntimeEffectStack(target, resolvedEffect, resolvedRecipe, count, sourceCharacter);
        }

        return removed;
    }

    private static void ApplyRuntimeEffectToInventory(
        PlayerRunInventory inventory,
        CharacterObject target,
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        int applicationCount,
        int stackLimitOverride)
    {
        int count = Mathf.Max(1, applicationCount);
        for (int i = 0; i < count; i++)
        {
            inventory.ApplyRuntimeEffect(target, recipe, sourceCharacter, sourceStats, -1f, stackLimitOverride);
        }
    }

    private static void ApplyRuntimeEffectToInventory(
        PlayerRunInventory inventory,
        CharacterObject target,
        RuntimeEffectDefinition effect,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride,
        RuntimeEffectStackMode stackMode,
        int stackLimit,
        bool refreshDurationOnReapply,
        int applicationCount)
    {
        int count = Mathf.Max(1, applicationCount);
        for (int i = 0; i < count; i++)
        {
            inventory.ApplyRuntimeEffect(
                target,
                effect,
                sourceCharacter,
                sourceStats,
                durationOverride,
                stackMode,
                stackLimit,
                refreshDurationOnReapply);
        }
    }

    private static void ApplyRuntimeEffectToReceiver(
        RuntimeEffectReceiver receiver,
        RuntimeEffectRecipe recipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        int applicationCount,
        int stackLimitOverride)
    {
        if (receiver == null)
        {
            return;
        }

        int count = Mathf.Max(1, applicationCount);
        for (int i = 0; i < count; i++)
        {
            receiver.ApplyEffect(recipe, sourceCharacter, sourceStats, -1f, stackLimitOverride);
        }
    }

    private static void ApplyRuntimeEffectToReceiver(
        RuntimeEffectReceiver receiver,
        RuntimeEffectDefinition effect,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        float durationOverride,
        RuntimeEffectStackMode stackMode,
        int stackLimit,
        bool refreshDurationOnReapply,
        int applicationCount)
    {
        if (receiver == null)
        {
            return;
        }

        int count = Mathf.Max(1, applicationCount);
        for (int i = 0; i < count; i++)
        {
            receiver.ApplyEffect(
                effect,
                sourceCharacter,
                sourceStats,
                durationOverride,
                stackMode,
                stackLimit,
                refreshDurationOnReapply);
        }
    }

    private bool ApplyRuntimeEffectToEnemyTargets(
        PassiveRuntimeEffectDefinition effect,
        PassiveEventContext context,
        RuntimeEffectDefinition resolvedEffect,
        RuntimeEffectRecipe resolvedRecipe,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        int stackLimitOverride)
    {
        if (effect.enemyTargetSource == PassiveRuntimeEnemyTargetSource.RuntimeTagHitTargets)
        {
            List<EnemyController> targets = GetRememberedHitTargets(effect.enemyTargetRuntimeTag, effect.filter);
            if (targets == null || targets.Count == 0)
            {
                return false;
            }

            bool appliedAny = false;
            for (int i = 0; i < targets.Count; i++)
            {
                if (effect.rollChancePerEnemyTarget
                    && UnityEngine.Random.value > Mathf.Clamp01(effect.chance))
                {
                    continue;
                }

                appliedAny |= ApplyRuntimeEffectToEnemy(
                    targets[i],
                    resolvedEffect,
                    resolvedRecipe,
                    effect,
                    sourceCharacter,
                    sourceStats,
                    stackLimitOverride);
            }

            return appliedAny;
        }

        if (effect.rollChancePerEnemyTarget
            && UnityEngine.Random.value > Mathf.Clamp01(effect.chance))
        {
            return false;
        }

        return ApplyRuntimeEffectToEnemy(
            context.enemy,
            resolvedEffect,
            resolvedRecipe,
            effect,
            sourceCharacter,
            sourceStats,
            stackLimitOverride);
    }

    private bool ApplyRuntimeEffectToEnemy(
        EnemyController enemy,
        RuntimeEffectDefinition resolvedEffect,
        RuntimeEffectRecipe resolvedRecipe,
        PassiveRuntimeEffectDefinition effect,
        CharacterObject sourceCharacter,
        CharacterStats sourceStats,
        int stackLimitOverride)
    {
        RuntimeEffectReceiver enemyReceiver = enemy != null ? enemy.RuntimeEffects : null;
        if (enemyReceiver == null)
        {
            return false;
        }

        if (resolvedRecipe != null)
        {
            enemyReceiver.ApplyEffect(resolvedRecipe, sourceCharacter, sourceStats, -1f, stackLimitOverride);
        }
        else
        {
            enemyReceiver.ApplyEffect(
                resolvedEffect,
                sourceCharacter,
                sourceStats,
                effect.durationOverride,
                effect.stackMode,
                effect.stackLimit,
                effect.refreshDurationOnReapply);
        }

        return true;
    }

    private int ResolveRuntimeEffectStackLimit(PassiveRuntimeEffectDefinition effect)
    {
        if (effect == null || effect.ResolvedEffect == null)
        {
            return 1;
        }

        RuntimeEffectRecipe recipe = effect.ResolvedRecipe;
        int result = recipe != null
            ? RuntimeEffectRuntimeUtility.GetStackLimit(recipe, effect.stackLimit)
            : Mathf.Max(1, effect.stackLimit);
        string definitionId = effect.ResolvedEffect.StableId;
        string stableId = RuntimeEffectRuntimeUtility.GetStableId(effect.ResolvedEffect, recipe);
        string stackKey = RuntimeEffectRuntimeUtility.GetStackKey(effect.ResolvedEffect, recipe);

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveRuntimeEffectStackLimitModifierDefinition> modifiers =
                node != null ? node.runtimeEffectStackLimitModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveRuntimeEffectStackLimitModifierDefinition modifier = modifiers[i];
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.effectIdOrStackKey))
                {
                    continue;
                }

                if (string.Equals(modifier.effectIdOrStackKey, definitionId, StringComparison.Ordinal)
                    || string.Equals(modifier.effectIdOrStackKey, stableId, StringComparison.Ordinal)
                    || string.Equals(modifier.effectIdOrStackKey, stackKey, StringComparison.Ordinal))
                {
                    result = Mathf.Max(result, modifier.stackLimit);
                }
            }
        }

        return Mathf.Max(1, result);
    }

    private void ResolveRuntimeEffectSource(
        PassiveRuntimeEffectDefinition effect,
        TeamLoader teamLoader,
        PlayerRunInventory inventory,
        out CharacterObject sourceCharacter,
        out CharacterStats sourceStats)
    {
        sourceCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        sourceStats = playerCharacter != null && playerCharacter.Stats != null
            ? playerCharacter.Stats.Clone()
            : null;

        if (effect == null
            || !effect.overrideSourceCharacter
            || string.IsNullOrWhiteSpace(effect.sourceCharacterId))
        {
            return;
        }

        CharacterObject overrideCharacter = ResolveTeamCharacterById(teamLoader, inventory, effect.sourceCharacterId);
        if (overrideCharacter == null)
        {
            return;
        }

        sourceCharacter = overrideCharacter;
        if (playerCharacter != null && playerCharacter.characterObject == overrideCharacter && playerCharacter.Stats != null)
        {
            sourceStats = playerCharacter.Stats.Clone();
            return;
        }

        sourceStats = teamLoader != null
            ? teamLoader.GetCharacterRuntimeStats(overrideCharacter)
            : inventory != null
                ? inventory.GetCharacterStats(overrideCharacter)
            : overrideCharacter.GetLeveledStats();
    }

    private static PlayerRunInventory ResolveRunInventory(TeamLoader teamLoader)
    {
        if (teamLoader != null && teamLoader.RunInventory != null)
        {
            return teamLoader.RunInventory;
        }

        return Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory");
    }

    private TeamLoader ResolveTeamLoader()
    {
        TeamLoader[] loaders = UnityEngine.Object.FindObjectsByType<TeamLoader>(FindObjectsSortMode.None);
        if (loaders == null || loaders.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < loaders.Length; i++)
        {
            TeamLoader loader = loaders[i];
            if (loader != null && loader.playerCharacter == playerCharacter)
            {
                return loader;
            }
        }

        CharacterObject currentCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        if (currentCharacter != null)
        {
            for (int i = 0; i < loaders.Length; i++)
            {
                TeamLoader loader = loaders[i];
                if (loader != null && loader.ContainsCharacter(currentCharacter))
                {
                    return loader;
                }
            }
        }

        return loaders[0];
    }

    private static CharacterObject[] ResolveRuntimeEffectTargets(TeamLoader teamLoader, PlayerRunInventory inventory)
    {
        if (teamLoader != null)
        {
            CharacterObject[] team = teamLoader.GetTeamSnapshot();
            if (team != null && team.Length > 0)
            {
                return team;
            }
        }

        return inventory != null
            ? inventory.GetTeamSnapshot()
            : Array.Empty<CharacterObject>();
    }

    private static CharacterObject ResolveTeamCharacterById(TeamLoader teamLoader, PlayerRunInventory inventory, string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        CharacterObject[] team = ResolveRuntimeEffectTargets(teamLoader, inventory);
        if (team == null)
        {
            return null;
        }

        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject character = team[i];
            if (character != null && string.Equals(character.characterId, characterId, System.StringComparison.Ordinal))
            {
                return character;
            }
        }

        return null;
    }

    private static RuntimeEffectApplyTarget ScopeToApplyTarget(PassiveEffectScope scope)
    {
        return scope switch
        {
            PassiveEffectScope.Enemy => RuntimeEffectApplyTarget.Enemy,
            PassiveEffectScope.Allies => RuntimeEffectApplyTarget.Allies,
            _ => RuntimeEffectApplyTarget.Self
        };
    }

    private static bool MatchesRuntimeEffectTarget(PassiveRuntimeEffectDefinition effect, CharacterObject target)
    {
        if (effect == null || target == null)
        {
            return false;
        }

        if (effect.filterByTargetElement && target.elementType != effect.targetElement)
        {
            return false;
        }

        return true;
    }

    private bool PassiveRequirementsMet(
        List<RuntimeTagRequirement> requiredTags,
        List<RuntimeCounterRequirement> requiredCounters,
        List<RuntimeEffectStackRequirement> requiredRuntimeEffectStacks,
        List<PassiveRuntimeValueRequirement> requiredValues,
        PassiveEventContext context)
    {
        if (requiredTags != null)
        {
            for (int i = 0; i < requiredTags.Count; i++)
            {
                if (requiredTags[i] != null && !requiredTags[i].Matches(context.runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredCounters != null)
        {
            for (int i = 0; i < requiredCounters.Count; i++)
            {
                if (requiredCounters[i] != null && !requiredCounters[i].Matches(context.runtimeState))
                {
                    return false;
                }
            }
        }

        if (requiredValues != null)
        {
            for (int i = 0; i < requiredValues.Count; i++)
            {
                if (requiredValues[i] != null && !requiredValues[i].Matches(context))
                {
                    return false;
                }
            }
        }

        if (requiredRuntimeEffectStacks != null)
        {
            for (int i = 0; i < requiredRuntimeEffectStacks.Count; i++)
            {
                if (requiredRuntimeEffectStacks[i] != null
                    && !requiredRuntimeEffectStacks[i].Matches(this))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ApplyCounterEffects(
        List<PassiveCounterEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveCounterEffectDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || effect.filter == null || !effect.filter.Matches(context))
            {
                continue;
            }

            ApplyCounterEffect(effect);
        }
    }

    private void ApplyConditionalEffects(
        List<PassiveConditionalStateEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveConditionalStateEffectDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || !effect.Matches(context, this))
            {
                continue;
            }

            for (int tagIndex = 0; tagIndex < effect.runtimeTagEffects.Count; tagIndex++)
            {
                if (effect.runtimeTagEffects[tagIndex] != null)
                {
                    ApplyRuntimeTagEffect(effect.runtimeTagEffects[tagIndex], context);
                }
            }

            for (int counterIndex = 0; counterIndex < effect.counterEffects.Count; counterIndex++)
            {
                if (effect.counterEffects[counterIndex] != null)
                {
                    ApplyCounterEffect(effect.counterEffects[counterIndex]);
                }
            }
        }
    }

    private void ApplyOnHitEffects(
        List<PassiveOnHitStatusEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null || trigger != PassiveTriggerType.OnHitDealt || context.enemy == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveOnHitStatusEffectDefinition effect = effects[i];
            if (effect == null || effect.filter == null || !effect.filter.Matches(context) || UnityEngine.Random.value > effect.chance)
            {
                continue;
            }

            if (effect.scope == PassiveEffectScope.Enemy)
            {
                context.enemy.ApplyStatus(effect.status);
            }
            else if (effect.scope == PassiveEffectScope.Self && playerCharacter != null)
            {
                playerCharacter.ApplyStatus(effect.status);
            }
        }
    }

    private void ApplyResourceEffects(
        List<PassiveResourceEffectDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveResourceEffectDefinition effect = effects[i];
            if (effect == null
                || effect.trigger != trigger
                || effect.filter == null
                || !effect.filter.Matches(context)
                || !PassiveRequirementsMet(
                    effect.requiredTags,
                    effect.requiredCounters,
                    effect.requiredRuntimeEffectStacks,
                    effect.requiredValues,
                    context)
                || UnityEngine.Random.value > Mathf.Clamp01(effect.chance))
            {
                continue;
            }

            if (IsResourceEffectOnCooldown(effect))
            {
                continue;
            }

            if (ApplyResourceEffect(effect, context))
            {
                StartResourceEffectCooldown(effect);
            }
        }
    }

    private void ApplyEnemyElementModifiers(
        List<PassiveEnemyElementModifierDefinition> effects,
        PassiveTriggerType trigger,
        PassiveEventContext context)
    {
        if (effects == null || context.enemy == null)
        {
            return;
        }

        EnemyElementalState elementalState = context.enemy.GetComponent<EnemyElementalState>();
        if (elementalState == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            PassiveEnemyElementModifierDefinition effect = effects[i];
            if (effect == null || effect.trigger != trigger || effect.filter == null || !effect.filter.Matches(context))
            {
                continue;
            }

            elementalState.ApplyRuntimeElementModifier(effect);
        }
    }

    private bool ApplyResourceEffect(PassiveResourceEffectDefinition effect, PassiveEventContext context)
    {
        if (effect == null || playerCharacter == null)
        {
            return false;
        }

        float valueSource = effect.valueMode == PassiveResourceValueMode.TriggerDamageDealt
            ? Mathf.Max(0f, context.damageDealt)
            : ResolveResourceValue(effect.valueSource);
        float value = Mathf.Max(0f, effect.flatValue + valueSource * Mathf.Max(0f, effect.valueRatio));

        switch (effect.effectType)
        {
            case PassiveResourceEffectType.Heal:
                ApplyHeal(effect.scope, value);
                return value > 0f;
            case PassiveResourceEffectType.RefillUltimateAuraEnergy:
                ApplyAuraEnergy(effect.scope, value);
                return value > 0f;
            case PassiveResourceEffectType.WaiveUltimateAuraEnergyCost:
                runtimeState.SetTag(
                    string.IsNullOrWhiteSpace(effect.runtimeTag) ? "waive_next_ultimate_cost" : effect.runtimeTag,
                    effect.runtimeTagDuration);
                return true;
            case PassiveResourceEffectType.ResetSkillCooldown:
                playerCharacter.Combat?.ResetActionCooldown(
                    AttackCategory.Skill,
                    effect.cooldownActionName,
                    effect.cooldownActionId);
                return true;
            case PassiveResourceEffectType.ResetUltimateCooldown:
                playerCharacter.Combat?.ResetActionCooldown(
                    AttackCategory.Ultimate,
                    effect.cooldownActionName,
                    effect.cooldownActionId);
                return true;
            case PassiveResourceEffectType.ReduceSkillCooldown:
                playerCharacter.Combat?.ReduceActionCooldown(
                    AttackCategory.Skill,
                    effect.cooldownActionName,
                    effect.cooldownActionId,
                    value);
                return value > 0f;
            case PassiveResourceEffectType.ReduceUltimateCooldown:
                playerCharacter.Combat?.ReduceActionCooldown(
                    AttackCategory.Ultimate,
                    effect.cooldownActionName,
                    effect.cooldownActionId,
                    value);
                return value > 0f;
        }

        return false;
    }

    private void ApplyHeal(PassiveEffectScope scope, float value)
    {
        if (value <= 0f)
        {
            return;
        }

        if (scope == PassiveEffectScope.Self)
        {
            playerCharacter.Heal(value);
            return;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader == null || playerCharacter.characterObject == null)
        {
            playerCharacter.Heal(value);
            return;
        }

        CharacterObject[] team = teamLoader.GetTeamSnapshot();
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject character = team[i];
            if (character == null)
            {
                continue;
            }

            if (character == playerCharacter.characterObject)
            {
                playerCharacter.Heal(value);
                continue;
            }

            float maxHp = teamLoader.GetCharacterMaxHP(character);
            float currentHp = teamLoader.GetCharacterCurrentHP(character);
            teamLoader.SetCharacterCurrentHP(character, Mathf.Min(maxHp, currentHp + value));
        }
    }

    private void ApplyAuraEnergy(PassiveEffectScope scope, float value)
    {
        if (value <= 0f)
        {
            return;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader == null || playerCharacter.characterObject == null)
        {
            return;
        }

        if (scope == PassiveEffectScope.Self)
        {
            teamLoader.AddCharacterAE(playerCharacter.characterObject, value);
            return;
        }

        CharacterObject[] team = teamLoader.GetTeamSnapshot();
        for (int i = 0; i < team.Length; i++)
        {
            if (team[i] != null)
            {
                teamLoader.AddCharacterAE(team[i], value);
            }
        }
    }

    private float ResolveResourceValue(PassiveModifierTarget source)
    {
        return ResolveResourceValue(source, PassiveStatValueSource.CurrentValue);
    }

    private float ResolveResourceValue(PassiveModifierTarget source, PassiveStatValueSource valueSource)
    {
        CharacterStats stats = ResolvePassiveSourceStats(valueSource);
        if (stats == null)
        {
            return 0f;
        }

        return source switch
        {
            PassiveModifierTarget.MaxHP => stats.maxHP,
            PassiveModifierTarget.Defense => stats.defense,
            PassiveModifierTarget.CritRate => stats.critRate,
            PassiveModifierTarget.CritDamage => stats.critDamage,
            PassiveModifierTarget.ElementalMastery => stats.elementalMastery,
            PassiveModifierTarget.CooldownRate => stats.cooldownRate,
            PassiveModifierTarget.Impact => stats.impact,
            PassiveModifierTarget.AuraEnergyRecharge => stats.aeRecharge,
            _ => stats.attack
        };
    }

    private CharacterStats ResolvePassiveSourceStats(PassiveStatValueSource valueSource)
    {
        if (playerCharacter == null)
        {
            return null;
        }

        if (valueSource == PassiveStatValueSource.CurrentValue)
        {
            return playerCharacter.Stats;
        }

        CharacterObject character = playerCharacter.characterObject;
        if (character == null)
        {
            return playerCharacter.Stats;
        }

        PlayerRunInventory inventory = ResolveRunInventory(TeamLoader.Instance);
        if (inventory != null)
        {
            return inventory.GetCharacterBaseStats(character);
        }

        return character.GetLeveledStats();
    }

    private void RebuildHooks()
    {
        activeHooks.Clear();

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            if (node == null || string.IsNullOrEmpty(node.passiveHookTypeName))
            {
                continue;
            }

            Type hookType = Type.GetType(node.passiveHookTypeName);
            if (hookType == null || !typeof(IPassiveEffectHook).IsAssignableFrom(hookType))
            {
                continue;
            }

            if (Activator.CreateInstance(hookType) is IPassiveEffectHook hook)
            {
                hook.Initialize(this, node);
                activeHooks.Add((node, hook));
            }
        }
    }

    private PassiveEventContext BuildContext(
        AttackCategory category,
        string actionId,
        string actionName,
        AttackStepData stepData,
        EnemyController enemy)
    {
        return new PassiveEventContext
        {
            category = category,
            actionId = actionId,
            actionName = actionName,
            stepData = stepData,
            stepName = stepData != null ? stepData.stepName : null,
            runtimeState = runtimeState,
            enemy = enemy,
            source = playerCharacter
        };
    }

    private static float ApplyCooldownModifiers(
        PotentialNodeDefinition node,
        AttackCategory category,
        string actionId,
        string actionName,
        float value)
    {
        List<PassiveCooldownModifierDefinition> modifiers = node != null ? node.cooldownModifiers : null;
        if (modifiers == null)
        {
            return value;
        }

        float result = value;
        for (int i = 0; i < modifiers.Count; i++)
        {
            PassiveCooldownModifierDefinition modifier = modifiers[i];
            if (modifier == null || modifier.category != category)
            {
                continue;
            }

            bool hasId = !string.IsNullOrWhiteSpace(modifier.actionId);
            bool hasName = !string.IsNullOrWhiteSpace(modifier.actionName);
            if (hasId && !string.Equals(modifier.actionId, actionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!hasId && hasName && !string.Equals(modifier.actionName, actionName, StringComparison.Ordinal))
            {
                continue;
            }

            result = modifier.operation == PassiveNumericOperation.Add
                ? result + modifier.value
                : result * modifier.value;
        }

        return result;
    }

    private static float ApplyValueModifier(float baseValue, PassiveValueModifierOperation operation, float value)
    {
        return operation switch
        {
            PassiveValueModifierOperation.Add => baseValue + value,
            PassiveValueModifierOperation.Multiply => baseValue * value,
            PassiveValueModifierOperation.Override => value,
            _ => baseValue
        };
    }

    private static void AddAdditionalDamageEffects(
        List<AdditionalDamageEffectDefinition> source,
        ref List<AdditionalDamageEffectDefinition> destination)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            AdditionalDamageEffectDefinition effect = source[i];
            if (effect == null)
            {
                continue;
            }

            destination ??= new List<AdditionalDamageEffectDefinition>();
            destination.Add(effect);
        }
    }

    private void ApplyRuntimeTagEffect(PassiveRuntimeTagEffectDefinition effect, PassiveEventContext context)
    {
        bool changed;
        if (effect.addTag)
        {
            changed = runtimeState.SetTag(effect.tag, effect.duration);
            RememberHitTarget(effect, context);
        }
        else
        {
            changed = runtimeState.RemoveTag(effect.tag);
            runtimeTagHitTargets.Remove(effect.tag);
        }

        if (changed && playerCharacter != null && playerCharacter.characterObject != null)
        {
            CharacterEvents.NotifyStatsChanged(playerCharacter.characterObject);
        }
    }

    private void RememberHitTarget(PassiveRuntimeTagEffectDefinition effect, PassiveEventContext context)
    {
        if (effect == null
            || !effect.rememberEnemyTarget
            || context.enemy == null
            || string.IsNullOrWhiteSpace(effect.tag))
        {
            return;
        }

        if (!runtimeTagHitTargets.TryGetValue(effect.tag, out List<EnemyController> targets) || targets == null)
        {
            targets = new List<EnemyController>();
            runtimeTagHitTargets[effect.tag] = targets;
        }

        if (!targets.Contains(context.enemy))
        {
            targets.Add(context.enemy);
        }
    }

    private List<EnemyController> GetRememberedHitTargets(string tag, PassiveTriggerFilter fallbackFilter)
    {
        string resolvedTag = !string.IsNullOrWhiteSpace(tag)
            ? tag
            : fallbackFilter != null && fallbackFilter.requireRuntimeTag ? fallbackFilter.runtimeTag : null;
        if (string.IsNullOrWhiteSpace(resolvedTag))
        {
            return null;
        }

        if (runtimeState == null || !runtimeState.HasTag(resolvedTag))
        {
            runtimeTagHitTargets.Remove(resolvedTag);
            return null;
        }

        if (!runtimeTagHitTargets.TryGetValue(resolvedTag, out List<EnemyController> targets) || targets == null)
        {
            return null;
        }

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            EnemyController target = targets[i];
            if (target == null || !target.IsAlive)
            {
                targets.RemoveAt(i);
            }
        }

        return targets;
    }

    private void ConsumeRuntimeEffectTag(PassiveRuntimeEffectDefinition effect)
    {
        string tag = !string.IsNullOrWhiteSpace(effect.enemyTargetRuntimeTag)
            ? effect.enemyTargetRuntimeTag
            : effect.filter != null && effect.filter.requireRuntimeTag ? effect.filter.runtimeTag : null;
        if (string.IsNullOrWhiteSpace(tag) || runtimeState == null)
        {
            return;
        }

        runtimeState.RemoveTag(tag);
        runtimeTagHitTargets.Remove(tag);
    }

    private bool IsResourceEffectOnCooldown(PassiveResourceEffectDefinition effect)
    {
        return effect != null
            && runtimeState != null
            && !string.IsNullOrWhiteSpace(effect.cooldownTag)
            && runtimeState.HasTag(effect.cooldownTag);
    }

    private void StartResourceEffectCooldown(PassiveResourceEffectDefinition effect)
    {
        if (effect == null
            || runtimeState == null
            || string.IsNullOrWhiteSpace(effect.cooldownTag)
            || effect.cooldownDuration <= 0f)
        {
            return;
        }

        runtimeState.SetTag(effect.cooldownTag, effect.cooldownDuration);
    }

    private void ApplyCounterEffect(PassiveCounterEffectDefinition effect)
    {
        switch (effect.operation)
        {
            case PassiveCounterOperation.Set:
                runtimeState.SetCounter(effect.counterId, effect.value);
                break;
            case PassiveCounterOperation.Add:
                runtimeState.AddCounter(effect.counterId, effect.value);
                break;
            case PassiveCounterOperation.Subtract:
                runtimeState.AddCounter(effect.counterId, -effect.value);
                break;
            case PassiveCounterOperation.Reset:
                runtimeState.SetCounter(effect.counterId, 0);
                break;
        }
    }

    public void ClearActionAvailabilityWindow(AttackCategory category, string actionId, string actionName)
    {
        if (runtimeState == null)
        {
            return;
        }

        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            List<PassiveActionModifierDefinition> modifiers = node != null ? node.actionModifiers : null;
            if (modifiers == null)
            {
                continue;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                PassiveActionModifierDefinition modifier = modifiers[i];
                if (modifier == null || !modifier.unlocked)
                {
                    continue;
                }

                bool matchesOriginal = modifier.Matches(category, actionId, actionName, runtimeState, ignoreRuntimeRequirements: true);
                bool matchesReplacement = string.Equals(modifier.replacementActionId, actionId, StringComparison.Ordinal)
                    || string.Equals(modifier.replacementActionName, actionName, StringComparison.Ordinal);

                if ((!matchesOriginal && !matchesReplacement) || modifier.requiredTags == null)
                {
                    continue;
                }

                for (int requirementIndex = 0; requirementIndex < modifier.requiredTags.Count; requirementIndex++)
                {
                    RuntimeTagRequirement requirement = modifier.requiredTags[requirementIndex];
                    if (requirement != null && !string.IsNullOrWhiteSpace(requirement.tag))
                    {
                        runtimeState.RemoveTag(requirement.tag);
                    }
                }
            }
        }
    }
}
