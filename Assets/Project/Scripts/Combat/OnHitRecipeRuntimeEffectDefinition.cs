using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RuntimeEffectReactionParameters
{
    public RuntimeEffectDamageFilter filter = new RuntimeEffectDamageFilter();
    [Min(0f)] public float internalCooldown;
    public List<RuntimeAreaEffectApplication> effects = new List<RuntimeAreaEffectApplication>();
    [Min(0f)] public float skillCooldownReduction;
}

[CreateAssetMenu(fileName = "RuntimeEffect_OnHitRecipe", menuName = "Game/Runtime Effects/On Hit Recipe")]
public sealed class OnHitRecipeRuntimeEffectDefinition : RuntimeEffectDefinition
{
    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        var instance = context?.instance;
        var parameters = RuntimeEffectRuntimeUtility.GetParameters(instance);
        var reaction = parameters?.overrideReaction == true ? parameters.reaction : null;
        if (reaction == null || !reaction.filter.Matches(context) ||
            Time.time < instance.lastTriggeredAt + reaction.internalCooldown) return;
        instance.lastTriggeredAt = Time.time;
        var inventory = TeamLoader.Instance?.RunInventory;
        foreach (var application in reaction.effects)
        {
            var recipe = application?.ResolvedRecipe;
            if (recipe == null) continue;
            if (!string.IsNullOrWhiteSpace(application.requiredNodeId))
            {
                bool unlocked = false;
                var nodes = TeamLoader.Instance?.GetUnlockedPotentialNodes(context.sourceCharacter);
                if (nodes != null)
                    foreach (var node in nodes) if (node?.nodeId == application.requiredNodeId) { unlocked = true; break; }
                if (!unlocked) continue;
            }
            if (application.triggerOnHit)
            {
                var previous = context.instance;
                context.instance = instance.GetChildInstance(recipe);
                recipe.definition.OnHitDealt(context);
                context.instance = previous;
            }
            else if (recipe.policy.appliesOn == RuntimeEffectApplyTarget.Enemy)
                context.targetEnemy?.RuntimeEffects?.ApplyEffect(recipe, context.sourceCharacter, context.sourceStats);
            else if (inventory != null)
            {
                var stats = RuntimeEffectRuntimeUtility.ResolveCurrentFormulaSourceStats(context);
                if (recipe.policy.appliesOn == RuntimeEffectApplyTarget.Self)
                    inventory.ApplyRuntimeEffect(context.sourceCharacter, recipe, context.sourceCharacter, stats);
                else
                    foreach (var target in inventory.GetTeamSnapshot())
                        if (target != null && (recipe.policy.appliesOn == RuntimeEffectApplyTarget.Allies || target != context.sourceCharacter))
                            inventory.ApplyRuntimeEffect(target, recipe, context.sourceCharacter, stats);
            }
            else if (PlayerCharacter.Instance?.characterObject == context.sourceCharacter)
                PlayerCharacter.Instance.RuntimeEffects?.ApplyEffect(recipe, context.sourceCharacter, context.sourceStats);
        }
        if (reaction.skillCooldownReduction > 0f)
        {
            var combat = TeamLoader.Instance?.playerCharacter?.Combat ?? PlayerCharacter.Instance?.Combat;
            var skills = context.sourceCharacter?.actionResolverTable?.skillActions;
            if (skills != null)
                foreach (var skill in skills)
                    combat?.ReduceActionCooldownForCharacter(context.sourceCharacter, AttackCategory.Skill,
                        skill.actionName, skill.actionId, reaction.skillCooldownReduction);
        }
    }
}
