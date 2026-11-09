using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Explicit authoring command only. Runtime consumes the saved assets and recipes.
public static class MainMemoryEffectsAuthoring
{
    private const string Folder = "Assets/Project/Character/Hero/test_0_Main/";
    private const string Formulas = "Assets/Project/Character/Hero/RuntimeEffect/";

    [MenuItem("Tools/Project/Combat/Apply Main Authored Memory Effects")]
    public static void Apply()
    {
        var character = Load<CharacterObject>("CharacterObject_main");
        var table = character.actionResolverTable;
        var kit = character.kitDefinition;
        Undo.RecordObjects(new Object[] { character, table }, "Main authored Memory effects");
        PotentialNodeDefinition Node(PotentialTrack track, int level) => kit.potentialNodes.Single(n => n.track == track && n.level == level);

        var skill1 = Node(PotentialTrack.Skill, 1);
        var skill3 = Node(PotentialTrack.Skill, 3);
        var exposure20 = Recipe<DamageModifierRuntimeEffectDefinition>("RuntimeEffect_DamageModifier", "main.memory.chain_exposure", 10f);
        exposure20.presentation.effectId = "main.memory.chain_exposure";
        exposure20.presentation.displayName = "Dark Chain - Damage Taken";
        exposure20.presentation.fieldVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            AssetDatabase.GUIDToAssetPath("1870efd1a41bdc846a9773df31d2827f"));
        exposure20.policy.appliesOn = RuntimeEffectApplyTarget.Enemy;
        exposure20.policy.kind = RuntimeEffectKind.Debuff;
        exposure20.parameters.overrideDamageModifier = true;
        exposure20.parameters.damageModifier.incomingDamageTakenPercentAdd = 20f;
        var exposure40 = JsonUtility.FromJson<RuntimeEffectRecipe>(JsonUtility.ToJson(exposure20));
        exposure40.parameters.damageModifier.incomingDamageTakenPercentAdd = 40f;
        var stun = Recipe<StatusRuntimeEffectDefinition>("RuntimeEffect_Status", "main.memory.chain_stun", .5f);
        stun.policy.kind = RuntimeEffectKind.Debuff;
        stun.policy.appliesOn = RuntimeEffectApplyTarget.Enemy;
        stun.parameters.overrideStatus = true;
        stun.parameters.status.status = StatusEffectType.Stun;

        var chain = Load<ProjectileData>("ProjectileData_main_skill");
        var weak = Load<ProjectileData>("ProjectileData_main_skill_lv2");
        var strong = AssetCopy(weak, "ProjectileData_main_skill_lv3");
        strong.magnetismSpeed = weak.magnetismSpeed * 2f;
        foreach (var data in new[] { chain, weak })
        {
            data.onHitEffects = new List<RuntimeAreaEffectApplication> { Hit(exposure20, skill1.nodeId) };
            Save(data);
        }
        strong.onHitEffects = new List<RuntimeAreaEffectApplication> { Hit(exposure40, skill3.nodeId), Hit(stun, skill3.nodeId) };
        Save(strong);
        skill3.actionModifiers.RemoveAll(m => m.replacementProjectileData != null);
        skill3.actionModifiers.Add(Replacement(AttackCategory.Skill, "main.skill.dark_chain", null, strong));

        var skill4 = Node(PotentialTrack.Skill, 4);
        var chainDamage = SourceHpDarkness("main.memory.chain_summon_darkness", .08f,
            AttackCategory.Skill, "main.skill.dark_chain.summon");
        var strike = AssetCopy(strong, "ProjectileData_main_chain_strike");
        strike.spawnPositionMode = SpawnPositionMode.Target;
        strike.spawnReferencePointMode = ProjectileReferencePointMode.GroundProjection;
        strike.applyOffsetToTarget = false;
        strike.traversalMode = ProjectileTraversalMode.Linear;
        strike.speed = 0f;
        strike.lifetime = Mathf.Max(.1f, 2f - (strike.spawnClip != null ? strike.spawnClip.length : 0f));
        strike.magnetismSpeed = 0f;
        strike.maxHits = 1;
        strike.hitCountMode = ProjectileHitCountMode.Shared;
        strike.limitHitsToTarget = true;
        strike.destroyWhenHitsExhausted = false;
        strike.spawningHitbox.enabled = false;
        var strikeHit = (NormalHitbox)strike.ResolveRoutineHitbox();
        strikeHit.offset = Vector2.zero;
        strikeHit.elementType = ElementType.None;
        Scaling(strikeHit, 2f, 0f);
        strike.onHitEffects ??= new List<RuntimeAreaEffectApplication>();
        strike.onHitEffects.RemoveAll(application => application?.recipe?.recipeId == chainDamage.recipeId);
        strike.onHitEffects.Add(Hit(chainDamage, skill4.nodeId, true));
        Save(strike);

        GameObject fieldVisual = MainDarkChainVisualAuthoring.CreateOrUpdate();

        var formula = AssetDatabase.LoadAssetAtPath<PeriodicProjectileRuntimeEffectDefinition>(Formulas + "RuntimeEffect_PeriodicProjectile.asset");
        if (formula == null)
        {
            formula = ScriptableObject.CreateInstance<PeriodicProjectileRuntimeEffectDefinition>();
            AssetDatabase.CreateAsset(formula, Formulas + "RuntimeEffect_PeriodicProjectile.asset");
        }
        var summon = new RuntimeEffectRecipe
        {
            recipeId = "main.memory.chain_summon", definition = formula,
            presentation = new RuntimeEffectPresentation { displayName = "Dark Chain", showRemainingTime = true,
                fieldVisualPrefab = fieldVisual, followActiveCharacter = true },
            policy = new RuntimeEffectApplicationPolicy
            { durationSeconds = 15f, stackMode = RuntimeEffectStackMode.Override, stackKey = "main.memory.chain_summon" },
            parameters = new RuntimeEffectParameterSet
            {
                overrideProjectile = true,
                projectile = new RuntimeEffectProjectileParameters
                { projectile = strike, tickInterval = 2.5f, useActivePlayerOrigin = true, category = AttackCategory.Skill,
                    actionId = "main.skill.dark_chain.summon", actionName = "Dark Chain" }
            }
        };
        skill4.runtimeEffects = new List<PassiveRuntimeEffectDefinition>
        { OnStart(summon, AttackCategory.Skill, "main.skill.dark_chain") };
        Describe(skill4, "Summon a Dark Chain for 15 seconds. Every 2.5 seconds it attacks the enemy nearest the active character for 200% of Main's ATK as Normal DMG, followed by Additional Darkness DMG equal to 8% of Main's Max HP. Continues after switching characters.");
        Describe(skill3, "Using Dark Chain increases enemy damage taken by 40% for 10 seconds. Dark Chain stuns enemies for 0.5 seconds and has stronger Magnetism.");

        var voidExposure = Recipe<DamageModifierRuntimeEffectDefinition>("RuntimeEffect_DamageModifier", "main.memory.void_exposure", 7f);
        voidExposure.presentation.effectId = "main.memory.void_exposure";
        voidExposure.presentation.displayName = "Void Area - Damage Taken";
        voidExposure.policy.appliesOn = RuntimeEffectApplyTarget.Enemy;
        voidExposure.policy.kind = RuntimeEffectKind.Debuff;
        voidExposure.parameters.overrideDamageModifier = true;
        voidExposure.parameters.damageModifier.incomingDamageTakenPercentAdd = 15f;
        var area = Load<ProjectileData>("ProjectileData_main_void_area");
        var middle = Load<ProjectileData>("ProjectileData_main_void_area_lv3");
        var high = Load<ProjectileData>("ProjectileData_main_void_area_lv4");
        string voidAreaActionId = "main.ultimate.jailbreak_punisher";
        var voidHpDamage6 = SourceHpDarkness("main.memory.void_area_hp_darkness", .06f,
            AttackCategory.Ultimate, voidAreaActionId, CombatDamageTag.VoidArea);
        var voidHpDamage12 = SourceHpDarkness("main.memory.void_area_hp_darkness", .12f,
            AttackCategory.Ultimate, voidAreaActionId, CombatDamageTag.VoidArea);
        string voidExposureNode = Node(PotentialTrack.Ultimate, 2).nodeId;
        string voidEnergyNode = Node(PotentialTrack.Ultimate, 3).nodeId;
        string voidHpNode6 = Node(PotentialTrack.Ultimate, 4).nodeId;
        string voidHpNode12 = Node(PotentialTrack.Ultimate, 6).nodeId;
        ConfigureVoid(area, 20f, 2f, ElementType.Void, voidExposure, voidExposureNode, voidEnergyNode, null, null);
        ConfigureVoid(middle, 20f, 5f, ElementType.None, voidExposure, voidExposureNode, voidEnergyNode, voidHpDamage6, voidHpNode6);
        ConfigureVoid(high, 40f, 10f, ElementType.None, voidExposure, voidExposureNode, voidEnergyNode, voidHpDamage12, voidHpNode12);
        var longArea = AssetCopy(middle, "ProjectileData_main_void_area_long");
        ConfigureVoid(longArea, 40f, 5f, ElementType.None, voidExposure, voidExposureNode, voidEnergyNode, voidHpDamage6, voidHpNode6);
        // Only the Void Area step changes; Jailbreak's other projectile remains authored by its action.
        foreach (int rank in new[] { 4, 5, 6 })
        {
            var node = Node(PotentialTrack.Ultimate, rank);
            node.actionModifiers.RemoveAll(m => m.stepName == "Void Area" && m.replacementProjectileData != null);
            node.actionModifiers.Add(Replacement(AttackCategory.Ultimate, "main.ultimate.jailbreak_punisher", "Void Area",
                rank == 4 ? middle : rank == 5 ? longArea : high));
        }

        RemoveRuntimeEffect(Node(PotentialTrack.Ultimate, 4), "main.memory.void_area_hp_darkness");
        RemoveRuntimeEffect(Node(PotentialTrack.Ultimate, 6), "main.memory.void_area_hp_darkness");
        Describe(Node(PotentialTrack.Ultimate, 4), "Void Area's first hit deals 500% ATK as Normal DMG. It then deals Additional Darkness DMG equal to 6% of Main's Max HP.");
        Describe(Node(PotentialTrack.Ultimate, 6), "Void Area's first hit deals 1000% ATK as Normal DMG. It then deals Additional Darkness DMG equal to 12% of Main's Max HP.");

        var passive6 = Node(PotentialTrack.Passive, 6);
        var ultimate5 = Node(PotentialTrack.Ultimate, 5);
        var hpBuff = passive6.runtimeEffects.FirstOrDefault(e => e.recipe?.recipeId == "main.memory.ultimate_hp");
        if (hpBuff != null)
        {
            passive6.runtimeEffects.Remove(hpBuff);
            ultimate5.runtimeEffects.Add(hpBuff);
        }
        Describe(ultimate5, "Void Area lasts 40 seconds. Using any Ultimate increases Main's Max HP by 40% for 12 seconds. Recasting refreshes the effect without stacking.");

        var finisher = Recipe<AdditionalDamageOnHitRuntimeEffectDefinition>("RuntimeEffect_AdditionalDamage", "main.memory.ultimate_finisher", 0f);
        finisher.policy.durationType = RuntimeEffectDurationType.Forever;
        finisher.parameters.overrideAdditionalDamage = true;
        finisher.parameters.additionalDamage.attackRatio = 0f;
        finisher.parameters.additionalDamage.sourceStatDamageSource = RuntimeEffectStatTarget.MaxHP;
        finisher.parameters.additionalDamage.sourceStatRatio = .2f;
        finisher.parameters.additionalDamage.elementType = ElementType.Darkness;
        finisher.parameters.additionalDamage.filter = new RuntimeEffectDamageFilter
        { filterByDelivery = true, delivery = DamageDelivery.Primary, filterByCategory = true,
            category = AttackCategory.Ultimate, requiredDamageTags = CombatDamageTag.Finisher };
        Node(PotentialTrack.Passive, 5).runtimeEffects = new List<PassiveRuntimeEffectDefinition>
        { OnStart(finisher, AttackCategory.Ultimate, null) };
        foreach (string id in new[] { "main.ultimate.jailbreak_punisher", "main.ultimate.execution_breakdown" })
        {
            var step = table.ultimateActions.Single(a => a.actionId == id).comboTree.rootNode.stepData;
            if (id == "main.ultimate.jailbreak_punisher") step = step.triggerWindows[0].targetNode.stepData;
            step.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(h => h.startFrameOffset).Last().damageTags |= CombatDamageTag.Finisher;
        }
        var judgment = table.skillActions.Single(a => a.actionId == "main.skill.dimension_slash");
        var judgmentStep = judgment.comboTree.rootNode.stepData;
        judgmentStep.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(h => h.startFrameOffset).Last().damageTags |= CombatDamageTag.Finisher;
        var roomPulse = JsonUtility.FromJson<RuntimeEffectRecipe>(JsonUtility.ToJson(finisher));
        roomPulse.recipeId = roomPulse.policy.stackKey = "main.memory.judgment_room_finisher";
        roomPulse.parameters.additionalDamage.sourceStatRatio = .14f;
        roomPulse.parameters.additionalDamage.targetAllRoomEnemies = true;
        roomPulse.parameters.additionalDamage.oncePerExecution = true;
        roomPulse.parameters.additionalDamage.filter.category = AttackCategory.Skill;
        roomPulse.parameters.additionalDamage.filter.actionId = judgment.actionId;
        passive6.runtimeEffects = new List<PassiveRuntimeEffectDefinition> { OnStart(roomPulse, AttackCategory.Skill, judgment.actionId) };
        passive6.resourceEffects = new List<PassiveResourceEffectDefinition>
        {
            Completion(judgment.actionId, PassiveResourceEffectType.RefillUltimateAuraEnergy, 20f, 0f),
            Completion(judgment.actionId, PassiveResourceEffectType.ReduceSkillCooldown, 0f, .5f),
            Completion(judgment.actionId, PassiveResourceEffectType.ReduceUltimateCooldown, 0f, .5f)
        };
        Describe(passive6, "Judgment Cut's last hit deals additional Darkness DMG equal to 14% of Main's Max HP to every enemy in the room. After Judgment Cut, restore 20 AE and reduce current Skill and Ultimate cooldowns by 50% of their remaining time.");
        Save(character);
        Save(table);
        Debug.Log("Main Memory now uses saved projectile assets, RuntimeEffect recipes and authored finisher tags.");
    }

    private static void ConfigureVoid(ProjectileData data, float duration, float attack, ElementType elementType,
        RuntimeEffectRecipe exposure, string exposureNode, string aeNode,
        RuntimeEffectRecipe additionalDamage, string additionalDamageNode)
    {
        data.lifetime = duration;
        data.hitIntervalSeconds = 5f;
        data.fixedHitCadence = true;
        data.auraEnergyPerDamageBatch = 5f;
        data.auraEnergyRequiredNodeId = aeNode;
        data.onHitEffects = new List<RuntimeAreaEffectApplication> { Hit(exposure, exposureNode) };
        if (additionalDamage != null)
            data.onHitEffects.Add(Hit(additionalDamage, additionalDamageNode, true));
        var hit = (NormalHitbox)data.ResolveRoutineHitbox();
        hit.elementType = elementType;
        hit.damageTags |= CombatDamageTag.VoidArea;
        Scaling(hit, attack, 0f);
        Save(data);
    }

    private static RuntimeEffectRecipe SourceHpDarkness(string recipeId, float ratio,
        AttackCategory category, string actionId, CombatDamageTag requiredDamageTags = CombatDamageTag.None)
    {
        var recipe = Recipe<AdditionalDamageOnHitRuntimeEffectDefinition>(
            "RuntimeEffect_AdditionalDamage", recipeId, 0f);
        recipe.policy.durationType = RuntimeEffectDurationType.Instant;
        recipe.parameters.overrideAdditionalDamage = true;
        recipe.parameters.additionalDamage.attackRatio = 0f;
        recipe.parameters.additionalDamage.sourceStatDamageSource = RuntimeEffectStatTarget.MaxHP;
        recipe.parameters.additionalDamage.sourceStatRatio = ratio;
        recipe.parameters.additionalDamage.elementType = ElementType.Darkness;
        recipe.parameters.additionalDamage.filter = new RuntimeEffectDamageFilter
        {
            filterByDelivery = true,
            delivery = DamageDelivery.Primary,
            filterByCategory = true,
            category = category,
            actionId = actionId,
            requiredDamageTags = requiredDamageTags
        };
        return recipe;
    }

    private static void RemoveRuntimeEffect(PotentialNodeDefinition node, string recipeId)
    {
        node.runtimeEffects?.RemoveAll(existing => existing?.recipe?.recipeId == recipeId);
    }

    private static void Scaling(NormalHitbox hit, float attack, float hp)
    {
        hit.damageScaling = new List<HitDamageStatScaling> { new() { stat = CombatStatId.Attack, ratio = attack } };
        if (hp > 0f) hit.damageScaling.Add(new HitDamageStatScaling { stat = CombatStatId.MaxHP, ratio = hp });
    }

    private static PassiveResourceEffectDefinition Completion(string id, PassiveResourceEffectType type, float flat, float ratio) => new()
    {
        trigger = PassiveTriggerType.OnActionEnded, filter = new PassiveTriggerFilter { filterByActionId = true, actionId = id },
        effectType = type, flatValue = flat, valueRatio = ratio,
        valueMode = ratio > 0f ? PassiveResourceValueMode.RemainingCooldown : PassiveResourceValueMode.CharacterStat
    };

    private static RuntimeAreaEffectApplication Hit(RuntimeEffectRecipe recipe, string node, bool triggerOnHit = false) => new()
    { recipe = recipe, requiredNodeId = node, triggerOnHit = triggerOnHit };
    private static PassiveRuntimeEffectDefinition OnStart(RuntimeEffectRecipe recipe, AttackCategory category, string id) => new()
    {
        trigger = PassiveTriggerType.OnActionStarted,
        filter = new PassiveTriggerFilter { filterByCategory = true, category = category,
            filterByActionId = !string.IsNullOrWhiteSpace(id), actionId = id, requireInitialActionStep = true }, recipe = recipe
    };
    private static PassiveActionModifierDefinition Replacement(AttackCategory category, string id, string step, ProjectileData projectile) => new()
    { category = category, actionId = id, stepName = step, replacementProjectileData = projectile };

    private static RuntimeEffectRecipe Recipe<T>(string asset, string id, float duration) where T : RuntimeEffectDefinition => new()
    {
        recipeId = id, definition = AssetDatabase.LoadAssetAtPath<T>(Formulas + asset + ".asset"),
        policy = new RuntimeEffectApplicationPolicy { durationSeconds = duration, stackMode = RuntimeEffectStackMode.Override, stackKey = id }
    };
    private static T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Folder + name + ".asset");
    private static ProjectileData AssetCopy(ProjectileData source, string name)
    {
        var existing = Load<ProjectileData>(name);
        if (existing != null) return existing;
        var data = Object.Instantiate(source);
        data.name = name;
        AssetDatabase.CreateAsset(data, Folder + name + ".asset");
        return data;
    }
    private static void Describe(PotentialNodeDefinition node, string copy)
    {
        node.description = copy;
        foreach (var patch in node.kitInfoPatches) if (patch.noteId == node.nodeId) patch.description = copy;
    }
    private static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
}
