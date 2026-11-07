using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MainSkillPotentialAuthoring
{
    private const string Folder = "Assets/Project/Character/Hero/test_0_Main/";

    [MenuItem("Tools/Project/Combat/Apply Main Skill Potential Revision", true)]
    private static bool CanApply()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_main.asset");
        return character?.kitDefinition != null && character.kitDefinition.GetNodeById("main.memory.skill.lv5") == null;
    }

    [MenuItem("Tools/Project/Combat/Apply Main Skill Potential Revision")]
    public static void Apply()
    {
        if (!CanApply()) return;
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_main.asset");
        var projectile = AssetDatabase.LoadAssetAtPath<ProjectileData>(Folder + "ProjectileData_main_skill_lv2.asset");
        var kit = character.kitDefinition;
        Undo.RecordObjects(new Object[] { character, projectile }, "Revise Main Skill Potential");
        GameObject marker = kit.baseSkillNode.runtimeEffects.Concat(kit.GetNodeById("main.skill.lv1").runtimeEffects)
            .Select(e => e.recipe?.presentation?.fieldVisualPrefab).FirstOrDefault(p => p != null);
        var damageDefinition = AssetDatabase.LoadAssetAtPath<DamageModifierRuntimeEffectDefinition>(
            "Assets/Project/Character/Hero/RuntimeEffect/RuntimeEffect_DamageModifier.asset");
        var statDefinition = AssetDatabase.LoadAssetAtPath<StatRuntimeEffectDefinition>(
            "Assets/Project/Character/Hero/RuntimeEffect/RuntimeEffect_Stat.asset");
        kit.baseSkillNode.runtimeEffects.Clear();
        for (int level = 1; level <= 4; level++)
        {
            var node = kit.GetNodeById("main.skill.lv" + level);
            node.runtimeEffects.Clear();
            node.actionModifiers.Clear();
            if (level == 1 || level == 3)
            {
                var recipe = Recipe(damageDefinition, "main.skill.damage_taken", "Damage Taken Increase",
                    RuntimeEffectApplyTarget.Enemy);
                recipe.presentation.fieldVisualPrefab = marker;
                recipe.policy.kind = RuntimeEffectKind.Debuff;
                recipe.parameters.overrideDamageModifier = true;
                recipe.parameters.damageModifier.incomingDamageTakenPercentAdd = level == 1 ? 20f : 40f;
                node.runtimeEffects.Add(OnSkillHit(recipe));
            }
            if (level == 1)
                node.actionModifiers.Add(new PassiveActionModifierDefinition
                { category = AttackCategory.Skill, actionId = "main.skill.dark_chain", auraEnergyRewardAdd = 5f });
            if (level == 2)
                node.actionModifiers.Add(new PassiveActionModifierDefinition
                { category = AttackCategory.Skill, actionId = "main.skill.dark_chain", replacementProjectileData = projectile });
            if (level == 4)
            {
                var recipe = Recipe(statDefinition, "main.skill.team_mastery", "DOT Mastery and Additional Damage Increase",
                    RuntimeEffectApplyTarget.Allies);
                recipe.parameters.overrideStatModifiers = true;
                foreach (var target in new[] { RuntimeEffectStatTarget.DotMastery, RuntimeEffectStatTarget.AdditionalDamageIncrease })
                    recipe.parameters.statModifiers.Add(new RuntimeEffectStatModifier
                    {
                        target = target, operation = RuntimeEffectStatOperation.AddSourceStatRatio,
                        sourceStat = RuntimeEffectStatTarget.Attack, value = .125f
                    });
                node.runtimeEffects.Add(OnSkillHit(recipe));
            }
            node.nodeName = new[] { "Chain Reward", "Magnetic Chain", "Exposed Dimension", "Shared Dimension" }[level - 1];
            node.description = new[]
            {
                "Increase enemy damage taken by 20% for 17 seconds, and increase AE Reward by 5.",
                "Dark Chain constantly pulls surrounding enemies gently toward its center.",
                "Increase enemy damage taken by 40% for 17 seconds.",
                "Increase all allies' DM and ADI by 1% per 8 of Main's ATK for 17 seconds."
            }[level - 1];
            node.skillPanelDescriptionOverride = "Dark Chain hits 5 times at 350% of ATK. Enemies take "
                + (level >= 3 ? "40" : "20") + "% increased damage for 17 seconds. AE Reward +5."
                + (level >= 2 ? " Constantly pulls surrounding enemies gently toward its center." : "")
                + (level >= 4 ? " All allies gain +1% DM and ADI per 8 of Main's ATK for 17 seconds." : "");
        }
        projectile.maxHits = 5;
        projectile.magnetismSpeed = 1.5f;
        EditorUtility.SetDirty(character);
        EditorUtility.SetDirty(projectile);
        AssetDatabase.SaveAssetIfDirty(character);
        AssetDatabase.SaveAssetIfDirty(projectile);
        Debug.Log("Main Skill Potential revision applied.");
    }

    private static RuntimeEffectRecipe Recipe(RuntimeEffectDefinition definition, string id, string name, RuntimeEffectApplyTarget target)
        => new RuntimeEffectRecipe
        {
            recipeId = id, definition = definition,
            presentation = new RuntimeEffectPresentation { effectId = id, displayName = name },
            policy = new RuntimeEffectApplicationPolicy
            {
                appliesOn = target, durationSeconds = 17f, stackKey = id,
                stackMode = RuntimeEffectStackMode.Override, stackLimit = 1, refreshDurationOnReapply = true
            }
        };

    private static PassiveRuntimeEffectDefinition OnSkillHit(RuntimeEffectRecipe recipe)
        => new PassiveRuntimeEffectDefinition
        {
            trigger = PassiveTriggerType.OnHitDealt,
            filter = new PassiveTriggerFilter { filterByCategory = true, category = AttackCategory.Skill },
            recipe = recipe
        };
}
