#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CameraGirlPotentialTests
{
    private CharacterObject cg;

    [SetUp]
    public void SetUp()
    {
        cg = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
        Assert.That(cg, Is.Not.Null, "CharacterObject_cg asset should exist and load cleanly.");
    }

    [Test]
    public void CameraGirl_HasAll18PotentialNodesConfigured()
    {
        var nodes = cg.kitDefinition.potentialNodes;
        Assert.That(nodes.Count, Is.EqualTo(18));
        foreach (var track in new[] { PotentialTrack.Skill, PotentialTrack.Ultimate, PotentialTrack.Passive })
            CollectionAssert.AreEqual(Enumerable.Range(1, 6), nodes.Where(n => n.track == track).Select(n => n.level).OrderBy(n => n));
    }

    [Test]
    public void CameraGirl_SkillTrack_ConfiguredCorrectly()
    {
        var nodes = cg.kitDefinition.potentialNodes.Where(n => n.track == PotentialTrack.Skill).OrderBy(n => n.level).ToArray();
        Assert.That(nodes[0].runtimeEffects[0].recipe.parameters.statModifiers[0].value, Is.EqualTo(17f));
        Assert.That(nodes[0].runtimeEffects[1].recipe.parameters.statModifiers[0].value, Is.EqualTo(33f));
        Assert.That(nodes[0].runtimeEffects[0].recipe.policy.appliesOn, Is.EqualTo(RuntimeEffectApplyTarget.Teammates));
        Assert.That(nodes[1].runtimeEffects[0].recipe.parameters.statModifiers[0].value, Is.EqualTo(31f));
        Assert.That(nodes[1].runtimeEffects[1].recipe.parameters.statModifiers[0].value, Is.EqualTo(64f));
        Assert.That(nodes[2].runtimeEffects.Take(2).All(e => e.recipe.policy.appliesOn == RuntimeEffectApplyTarget.Allies), Is.True);
        Assert.That(nodes[2].runtimeEffects[2].recipe.parameters.statModifiers[0].value, Is.EqualTo(1.4f));
        Assert.That(nodes[2].runtimeEffects[2].recipe.policy.durationSeconds, Is.EqualTo(15f));
        Assert.That(nodes[3].onHitStatusEffects.Single().chance, Is.EqualTo(.4f));
        Assert.That(nodes[3].onHitStatusEffects.Single().durationSeconds, Is.EqualTo(2.5f));
        var focus = nodes[4].runtimeEffects.Single(e => e.recipe.parameters.overrideProjectile).recipe;
        Assert.That(focus.policy.durationSeconds, Is.EqualTo(10f));
        Assert.That(focus.parameters.projectile.tickInterval, Is.EqualTo(1f));
        Assert.That(focus.parameters.projectile.roundRobinTargets, Is.True);
        Assert.That(AssetDatabase.Contains(focus.parameters.projectile.projectile), Is.True);
        Assert.That(nodes[5].runtimeEffects.Single().recipe.definition, Is.TypeOf<OnHitRecipeRuntimeEffectDefinition>());
    }

    [Test]
    public void CameraGirl_UltimateTrack_ConfiguredCorrectly()
    {
        var nodes = cg.kitDefinition.potentialNodes.Where(n => n.track == PotentialTrack.Ultimate).OrderBy(n => n.level).ToArray();
        Assert.That(nodes[0].runtimeTagEffects.Single().duration, Is.EqualTo(15f));
        Assert.That(nodes[0].coordinatedActionEffects.Single().triggerChance, Is.EqualTo(.5f));
        Assert.That(nodes[0].coordinatedActionEffects.Single().canTriggerOffField, Is.True);
        Assert.That(nodes[1].coordinatedActionEffects.Single().auraEnergyRewardOnHit, Is.EqualTo(3f));
        Assert.That(nodes[1].coordinatedActionEffects.Single().triggerChance, Is.EqualTo(.5f));
        Assert.That(nodes[2].runtimeTagEffects.Single().duration, Is.EqualTo(30f));
        var attack = nodes[3].runtimeEffects.Single().recipe;
        Assert.That(attack.policy.durationSeconds, Is.EqualTo(20f));
        Assert.That(attack.parameters.statModifiers.Single().value, Is.EqualTo(5f));
        Assert.That(attack.parameters.statModifiers.Single().maxIncrease, Is.EqualTo(900f));
        var shadow = nodes[4].coordinatedActionEffects.Single();
        Assert.That(shadow.triggerChance, Is.EqualTo(1f));
        Assert.That(shadow.damageMultiplierOverride, Is.EqualTo(6.5f));
        Assert.That(shadow.overrideDamageSource, Is.True);
        Assert.That(shadow.damageSourceStat, Is.EqualTo(ActionDamageSourceStat.Defense));
        Assert.That(shadow.maxSimultaneousActive, Is.EqualTo(1));
        Assert.That(nodes[5].runtimeEffects.Single().recipe.parameters.statModifiers.Single().value, Is.EqualTo(100f));
    }

    [Test]
    public void CameraGirl_PassiveTrack_ConfiguredCorrectly()
    {
        var nodes = cg.kitDefinition.potentialNodes.Where(n => n.track == PotentialTrack.Passive).OrderBy(n => n.level).ToArray();
        foreach (var node in nodes.Take(2))
        {
            Assert.That(node.runtimeEffects.Count, Is.EqualTo(2));
            Assert.That(node.runtimeEffects[0].recipe.policy.durationSeconds, Is.EqualTo(10f));
            Assert.That(node.runtimeEffects[0].recipe.recipeId, Is.EqualTo("cg.captured"));
            Assert.That(node.runtimeEffects[0].recipe.childRecipes[0].parameters.damageInput.defenderDefensePercentAdd, Is.EqualTo(-40f));
        }
        Assert.That(nodes[1].runtimeEffects[0].recipe.childRecipes.Count, Is.EqualTo(5));
        Assert.That(nodes[1].runtimeEffects[0].recipe.childRecipes[0].parameters.damageInput.defenderDamageResistancePercentAdd, Is.EqualTo(-40f));
        Assert.That(nodes[2].runtimeTagEffects.Count, Is.EqualTo(2));
        Assert.That(nodes[2].runtimeTagEffects.All(e => e.duration == 10f && e.filter.requiredEnemyEffectKey == "cg.captured"), Is.True);
        var mastery = cg.actionResolverTable.skillActions.Single(a => a.actionId == "Skill.super.ex");
        Assert.That(mastery.comboTree.rootNode.stepData.animationClip.name, Is.EqualTo("cg_attack6"));
        Assert.That(nodes[3].resourceEffects.Single().flatValue, Is.EqualTo(5f));
        Assert.That(nodes[3].runtimeEffects.Single().recipe.parameters.damageInput.delivery, Is.EqualTo(DamageDelivery.Additional));
        var proc = nodes[4].runtimeEffects.Single().recipe.parameters.reaction;
        Assert.That(proc.internalCooldown, Is.EqualTo(1.5f));
        Assert.That(proc.skillCooldownReduction, Is.EqualTo(.5f));
        Assert.That(proc.effects.Single().recipe.parameters.additionalDamage.sourceStatRatio, Is.EqualTo(.7f));
        var stacks = nodes[5].runtimeEffects.Single().recipe.parameters.reaction.effects.Single().recipe;
        Assert.That(stacks.policy.stackLimit, Is.EqualTo(10));
        Assert.That(stacks.policy.durationSeconds, Is.EqualTo(3f));
    }

    [Test]
    public void MasteryRetainsAttackSixTimingAndBothAuthoredHitboxes()
    {
        var mastery = cg.actionResolverTable.skillActions.Single(a => a.actionId == "Skill.super.ex");
        var step = mastery.comboTree.rootNode.stepData;
        var pending = new Stack<ComboNode>(cg.actionResolverTable.comboActions.Select(a => a.comboTree.rootNode));
        var visited = new HashSet<ComboNode>();
        AttackStepData original = null;
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node == null || !visited.Add(node)) continue;
            if (node.stepData?.animationClip == step.animationClip) { original = node.stepData; break; }
            pending.Push(node.defaultNext);
            pending.Push(node.onGrabSuccessNode);
            foreach (var route in node.routes) pending.Push(route.targetNode);
        }
        Assert.That(original, Is.Not.Null);
        Assert.That(step.TotalFrames, Is.EqualTo(original.TotalFrames));
        Assert.That(step.hitData.hitboxes.Count, Is.EqualTo(2));
        Assert.That(step.hitData.hitboxes.All(hit => hit is NormalHitbox), Is.True);
        Assert.That(JsonUtility.ToJson(step.hitData), Is.EqualTo(JsonUtility.ToJson(original.hitData)));
        Assert.That(JsonUtility.ToJson(step.motionData), Is.EqualTo(JsonUtility.ToJson(original.motionData)));
        Assert.That(JsonUtility.ToJson(step.eventData), Is.EqualTo(JsonUtility.ToJson(original.eventData)));
    }

    [Test]
    public void ShadowActionExecutor_ResolvesSkillExToActionResolver()
    {
        // Both "cg.skill.ex" and "skill.ex" should resolve directly to "Shadow Attack"
        bool successExact = ShadowActionExecutor.TryResolveAction(
            cg,
            "cg.skill.ex",
            out AttackStepData stepExact,
            out float multExact,
            out string actNameExact,
            out string actIdExact);

        Assert.That(successExact, Is.True);
        Assert.That(stepExact, Is.Not.Null);
        Assert.That(actNameExact, Is.EqualTo("Shadow Attack"));
        Assert.That(actIdExact, Is.EqualTo("cg.skill.ex"));

        bool successSuffix = ShadowActionExecutor.TryResolveAction(
            cg,
            "skill.ex",
            out AttackStepData stepSuffix,
            out float multSuffix,
            out string actNameSuffix,
            out string actIdSuffix);

        Assert.That(successSuffix, Is.True);
        Assert.That(stepSuffix, Is.Not.Null);
        Assert.That(actNameSuffix, Is.EqualTo("Shadow Attack"));
        Assert.That(actIdSuffix, Is.EqualTo("cg.skill.ex"));
    }

    [Test]
    public void ShadowActionExecutor_ResolvesGracefullyWhenActionMissing()
    {
        bool success = ShadowActionExecutor.TryResolveAction(
            cg,
            "nonexistent.action.id",
            out AttackStepData step,
            out float mult,
            out string actName,
            out string actId);

        Assert.That(success, Is.True, "Should resolve fallback without throwing exceptions.");
        Assert.That(step, Is.Not.Null, "Fallback stepData should be populated.");
        Assert.That(actName, Is.EqualTo("Capture Da Soul"), "Fallback action name should match CameraGirl's available skill.");
    }

    [Test]
    public void CharacterCoordinatedActionSystem_InternalCooldownTracksAndBlocksCorrectly()
    {
        GameObject go = new GameObject("CoordinatedActionSystemTest");
        try
        {
            var system = go.AddComponent<CharacterCoordinatedActionSystem>();
            var coordDef = new PassiveCoordinatedActionDefinition
            {
                actionId = "cg.skill.ex",
                internalCooldown = 2.0f
            };

            Assert.That(system.IsOnCooldown(cg, coordDef), Is.False, "Should not be on cooldown initially.");
            Assert.That(system.GetRemainingCooldown(cg, coordDef), Is.Zero);

            system.TriggerCooldown(cg, coordDef);
            Assert.That(system.IsOnCooldown(cg, coordDef), Is.True, "Should be on cooldown immediately after trigger.");
            Assert.That(system.GetRemainingCooldown(cg, coordDef), Is.GreaterThan(0f));

            system.ClearCooldowns();
            Assert.That(system.IsOnCooldown(cg, coordDef), Is.False, "Should be cleared after ClearCooldowns.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
#endif
