using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.Progression;
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
        RebuildHooks();
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

                if (!modifier.RuntimeRequirementsMet(runtimeState))
                {
                    continue;
                }

                if (modifier.operation == PassiveNumericOperation.Add)
                {
                    additive += modifier.value;
                }
                else
                {
                    multiplier *= modifier.value;
                }
            }
        }

        return (baseValue + additive) * multiplier;
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
                if (modifier != null && modifier.Matches(category, actionId, actionName))
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
                if (modifier != null && modifier.Matches(category, actionId, actionName))
                {
                    result = ApplyValueModifier(result, modifier.damageMultiplierOperation, modifier.damageMultiplierValue);
                }
            }
        }

        return Mathf.Max(0f, result);
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
                if (modifier != null && modifier.Matches(category, actionId, actionName))
                {
                    result += modifier.auraEnergyRewardAdd;
                }
            }
        }

        return Mathf.Max(0f, result);
    }

    public float ModifyUltimateEnergyCost(string actionId, string actionName, float baseValue)
    {
        if (runtimeState != null && runtimeState.HasTag("waive_next_ultimate_cost"))
        {
            return 0f;
        }

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
                if (modifier != null && modifier.Matches(AttackCategory.Ultimate, actionId, actionName))
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
                if (modifier != null && modifier.Matches(category, actionId, actionName))
                {
                    isUnlocked = modifier.unlocked;
                }
            }
        }

        return isUnlocked;
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
                if (modifier != null && modifier.Matches(category, actionId, actionName))
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
                if (modifier == null || !modifier.Matches(category, actionId, actionName))
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

    public float GetTeamModifierValue(PassiveModifierTarget target)
    {
        return ModifyValue(target, 0f, PassiveEffectScope.Team);
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
            TeamLoader teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
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
        foreach (PotentialNodeDefinition node in GetActiveNodes())
        {
            ApplyRuntimeTagEffects(node.runtimeTagEffects, trigger, context);
            ApplyRuntimeEffects(node.runtimeEffects, trigger, context);
            ApplyCounterEffects(node.counterEffects, trigger, context);
            ApplyConditionalEffects(node.conditionalStateEffects, trigger, context);
            ApplyOnHitEffects(node.onHitStatusEffects, trigger, context);
            ApplyResourceEffects(node.resourceEffects, trigger, context);
            ApplyEnemyElementModifiers(node.enemyElementModifiers, trigger, context);
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

            ApplyRuntimeTagEffect(effect);
        }
    }

    private void ApplyRuntimeEffects(
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
                || effect.effect == null
                || effect.trigger != trigger
                || effect.filter == null
                || !effect.filter.Matches(context))
            {
                continue;
            }

            ApplyRuntimeEffect(effect, context);
        }
    }

    private void ApplyRuntimeEffect(PassiveRuntimeEffectDefinition effect, PassiveEventContext context)
    {
        if (effect == null || effect.effect == null || playerCharacter == null)
        {
            return;
        }

        CharacterObject sourceCharacter = playerCharacter.characterObject;
        CharacterStats sourceStats = playerCharacter.Stats != null ? playerCharacter.Stats.Clone() : null;

        if (effect.scope == PassiveEffectScope.Enemy)
        {
            RuntimeEffectReceiver enemyReceiver = context.enemy != null ? context.enemy.RuntimeEffects : null;
            enemyReceiver?.ApplyEffect(effect.effect, sourceCharacter, sourceStats, effect.durationOverride);
            return;
        }

        TeamLoader teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
        Project.ShopSystem.PlayerRunInventory inventory = teamLoader != null ? teamLoader.RunInventory : null;
        if (inventory == null)
        {
            if (effect.scope == PassiveEffectScope.Self)
            {
                playerCharacter.RuntimeEffects?.ApplyEffect(effect.effect, sourceCharacter, sourceStats, effect.durationOverride);
            }

            return;
        }

        CharacterObject[] team = teamLoader.GetTeamSnapshot();
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null)
            {
                continue;
            }

            bool shouldApply = effect.scope switch
            {
                PassiveEffectScope.Self => target == sourceCharacter,
                PassiveEffectScope.Allies => target != sourceCharacter,
                PassiveEffectScope.Team => true,
                _ => false
            };

            if (shouldApply)
            {
                inventory.ApplyRuntimeEffect(target, effect.effect, sourceCharacter, sourceStats, effect.durationOverride);
            }
        }
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
            if (effect == null || effect.trigger != trigger || !effect.Matches(context))
            {
                continue;
            }

            for (int tagIndex = 0; tagIndex < effect.runtimeTagEffects.Count; tagIndex++)
            {
                if (effect.runtimeTagEffects[tagIndex] != null)
                {
                    ApplyRuntimeTagEffect(effect.runtimeTagEffects[tagIndex]);
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
            if (effect == null || effect.trigger != trigger || effect.filter == null || !effect.filter.Matches(context))
            {
                continue;
            }

            ApplyResourceEffect(effect);
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

    private void ApplyResourceEffect(PassiveResourceEffectDefinition effect)
    {
        if (effect == null || playerCharacter == null)
        {
            return;
        }

        float value = Mathf.Max(0f, effect.flatValue + ResolveResourceValue(effect.valueSource) * Mathf.Max(0f, effect.valueRatio));

        switch (effect.effectType)
        {
            case PassiveResourceEffectType.Heal:
                ApplyHeal(effect.scope, value);
                break;
            case PassiveResourceEffectType.RefillUltimateAuraEnergy:
                ApplyAuraEnergy(effect.scope, value);
                break;
            case PassiveResourceEffectType.WaiveUltimateAuraEnergyCost:
                runtimeState.SetTag(
                    string.IsNullOrWhiteSpace(effect.runtimeTag) ? "waive_next_ultimate_cost" : effect.runtimeTag,
                    effect.runtimeTagDuration);
                break;
            case PassiveResourceEffectType.ResetSkillCooldown:
                playerCharacter.Combat?.ResetActionCooldown(AttackCategory.Skill, null);
                break;
            case PassiveResourceEffectType.ResetUltimateCooldown:
                playerCharacter.Combat?.ResetActionCooldown(AttackCategory.Ultimate, null);
                break;
        }
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

        TeamLoader teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
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

        TeamLoader teamLoader = UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
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
        CharacterStats stats = playerCharacter != null ? playerCharacter.Stats : null;
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

    private void ApplyRuntimeTagEffect(PassiveRuntimeTagEffectDefinition effect)
    {
        if (effect.addTag)
        {
            runtimeState.SetTag(effect.tag, effect.duration);
        }
        else
        {
            runtimeState.RemoveTag(effect.tag);
        }
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
}
