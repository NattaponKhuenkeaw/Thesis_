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
    public void CameraGirl_HasAll12PotentialNodesConfigured()
    {
        Assert.That(cg.kitDefinition, Is.Not.Null);
        List<PotentialNodeDefinition> allNodes = cg.kitDefinition.EnumerateAllNodes().ToList();

        // 3 base nodes + 12 potential nodes = 15 total nodes
        var potentialNodes = allNodes.Where(n => n.level > 0).ToList();
        Assert.That(potentialNodes.Count, Is.EqualTo(12), "Should have exactly 12 potential nodes authored.");

        var skillNodes = potentialNodes.Where(n => n.track == PotentialTrack.Skill).OrderBy(n => n.level).ToList();
        var ultNodes = potentialNodes.Where(n => n.track == PotentialTrack.Ultimate).OrderBy(n => n.level).ToList();
        var passiveNodes = potentialNodes.Where(n => n.track == PotentialTrack.Passive).OrderBy(n => n.level).ToList();

        Assert.That(skillNodes.Count, Is.EqualTo(4), "Skill track should have 4 nodes (Lv.1 to Lv.4).");
        Assert.That(ultNodes.Count, Is.EqualTo(4), "Ultimate track should have 4 nodes (Lv.1 to Lv.4).");
        Assert.That(passiveNodes.Count, Is.EqualTo(4), "Passive track should have 4 nodes (Lv.1 to Lv.4).");
    }

    [Test]
    public void CameraGirl_SkillTrack_ConfiguredCorrectly()
    {
        var skillNodes = cg.kitDefinition.EnumerateAllNodes()
            .Where(n => n.track == PotentialTrack.Skill && n.level > 0)
            .OrderBy(n => n.level)
            .ToList();

        // Skill Lv.1: Deterministic buffs for CR (13%) & CD (23%) to Teammates (3)
        var lv1 = skillNodes[0];
        Assert.That(lv1.nodeId, Is.EqualTo("cg.skill.lv1"));
        Assert.That(lv1.runtimeEffects.Count, Is.EqualTo(2));
        Assert.That(lv1.runtimeEffects[0].recipe.policy.appliesOn, Is.EqualTo(RuntimeEffectApplyTarget.Teammates));
        Assert.That(lv1.runtimeEffects[0].recipe.parameters.statModifiers[0].value, Is.EqualTo(13f));
        Assert.That(lv1.runtimeEffects[1].recipe.parameters.statModifiers[0].value, Is.EqualTo(23f));

        // Skill Lv.2: Increased values to CR 17% and CD 33%
        var lv2 = skillNodes[1];
        Assert.That(lv2.nodeId, Is.EqualTo("cg.skill.lv2"));
        Assert.That(lv2.runtimeEffects[0].recipe.parameters.statModifiers[0].value, Is.EqualTo(17f));
        Assert.That(lv2.runtimeEffects[1].recipe.parameters.statModifiers[0].value, Is.EqualTo(33f));

        // Skill Lv.3: Scope changed to Allies (2)
        var lv3 = skillNodes[2];
        Assert.That(lv3.nodeId, Is.EqualTo("cg.skill.lv3"));
        Assert.That(lv3.runtimeEffects[0].recipe.policy.appliesOn, Is.EqualTo(RuntimeEffectApplyTarget.Allies));
        Assert.That(lv3.runtimeEffects[1].recipe.policy.appliesOn, Is.EqualTo(RuntimeEffectApplyTarget.Allies));

        // Skill Lv.4: 40% chance Stun for 2.5s on hit
        var lv4 = skillNodes[3];
        Assert.That(lv4.nodeId, Is.EqualTo("cg.skill.lv4"));
        Assert.That(lv4.onHitStatusEffects.Count, Is.EqualTo(1));
        Assert.That(lv4.onHitStatusEffects[0].status, Is.EqualTo(StatusEffectType.Stun));
        Assert.That(lv4.onHitStatusEffects[0].chance, Is.EqualTo(0.4f).Within(0.01f));
        Assert.That(lv4.onHitStatusEffects[0].durationSeconds, Is.EqualTo(2.5f).Within(0.01f));
    }

    [Test]
    public void CameraGirl_UltimateTrack_ConfiguredCorrectly()
    {
        var ultNodes = cg.kitDefinition.EnumerateAllNodes()
            .Where(n => n.track == PotentialTrack.Ultimate && n.level > 0)
            .OrderBy(n => n.level)
            .ToList();

        // Ult Lv.1: 15s assist tag, 50% shadow attack trigger, off-field supported, max 1 active
        var lv1 = ultNodes[0];
        Assert.That(lv1.nodeId, Is.EqualTo("cg.ultimate.lv1"));
        Assert.That(lv1.runtimeTagEffects.Count, Is.EqualTo(1));
        Assert.That(lv1.runtimeTagEffects[0].tag, Is.EqualTo("cg.ult.shadow_assist"));
        Assert.That(lv1.runtimeTagEffects[0].duration, Is.EqualTo(15f));
        Assert.That(lv1.coordinatedActionEffects.Count, Is.EqualTo(1));
        var coord1 = lv1.coordinatedActionEffects[0];
        Assert.That(coord1.actionId, Is.EqualTo("cg.skill.ex"));
        Assert.That(coord1.triggerChance, Is.EqualTo(0.5f).Within(0.01f));
        Assert.That(coord1.canTriggerOffField, Is.True);
        Assert.That(coord1.maxSimultaneousActive, Is.EqualTo(1));
        Assert.That(coord1.internalCooldown, Is.EqualTo(1.5f).Within(0.01f));

        // Ult Lv.2: 100% chance, 400% ATK damage override
        var lv2 = ultNodes[1];
        Assert.That(lv2.nodeId, Is.EqualTo("cg.ultimate.lv2"));
        var coord2 = lv2.coordinatedActionEffects[0];
        Assert.That(coord2.actionId, Is.EqualTo("cg.skill.ex"));
        Assert.That(coord2.triggerChance, Is.EqualTo(1.0f).Within(0.01f));
        Assert.That(coord2.damageMultiplierOverride, Is.EqualTo(4.0f).Within(0.01f));
        Assert.That(coord2.internalCooldown, Is.EqualTo(1.5f).Within(0.01f));

        // Ult Lv.3: Bigger Radius Projectile (1.5x)
        var lv3 = ultNodes[2];
        Assert.That(lv3.nodeId, Is.EqualTo("cg.ultimate.lv3"));
        Assert.That(lv3.actionModifiers.Count, Is.EqualTo(1));
        Assert.That(lv3.actionModifiers[0].projectileScaleMultiplier, Is.EqualTo(1.5f).Within(0.01f));
        Assert.That(lv3.actionModifiers[0].hitboxScaleMultiplier, Is.EqualTo(1.5f).Within(0.01f));

        // Ult Lv.4: 1.5x hitbox scale, +3 AE reward on hit
        var lv4 = ultNodes[3];
        Assert.That(lv4.nodeId, Is.EqualTo("cg.ultimate.lv4"));
        var coord4 = lv4.coordinatedActionEffects[0];
        Assert.That(coord4.actionId, Is.EqualTo("cg.skill.ex"));
        Assert.That(coord4.hitboxScaleMultiplier, Is.EqualTo(1.5f).Within(0.01f));
        Assert.That(coord4.auraEnergyRewardOnHit, Is.EqualTo(3.0f).Within(0.01f));
        Assert.That(coord4.internalCooldown, Is.EqualTo(1.5f).Within(0.01f));
    }

    [Test]
    public void CameraGirl_PassiveTrack_ConfiguredCorrectly()
    {
        var passiveNodes = cg.kitDefinition.EnumerateAllNodes()
            .Where(n => n.track == PotentialTrack.Passive && n.level > 0)
            .OrderBy(n => n.level)
            .ToList();

        // Passive Lv.1: On Attack3 hit, reduce DEF by -20% for 10s
        var lv1 = passiveNodes[0];
        Assert.That(lv1.nodeId, Is.EqualTo("cg.passive.lv1"));
        Assert.That(lv1.runtimeEffects.Count, Is.EqualTo(1));
        var effect1 = lv1.runtimeEffects[0];
        Assert.That(effect1.filter.stepName, Is.EqualTo("Attack3"));
        Assert.That(effect1.recipe.parameters.damageInput.defenderDefensePercentAdd, Is.EqualTo(-20f));
        Assert.That(effect1.recipe.policy.durationSeconds, Is.EqualTo(10f));

        // Passive Lv.2: On Attack3 hit, reduce DEF by -40% for 10s
        var lv2 = passiveNodes[1];
        Assert.That(lv2.nodeId, Is.EqualTo("cg.passive.lv2"));
        var effect2 = lv2.runtimeEffects[0];
        Assert.That(effect2.filter.stepName, Is.EqualTo("Attack3"));
        Assert.That(effect2.recipe.parameters.damageInput.defenderDefensePercentAdd, Is.EqualTo(-40f));
        Assert.That(effect2.recipe.policy.durationSeconds, Is.EqualTo(10f));

        // Passive Lv.3: Bigger 3rd basic (Attack3) hitbox collider (1.5x scale)
        var lv3 = passiveNodes[2];
        Assert.That(lv3.nodeId, Is.EqualTo("cg.passive.lv3"));
        Assert.That(lv3.actionModifiers.Count, Is.EqualTo(1));
        Assert.That(lv3.actionModifiers[0].stepName, Is.EqualTo("Attack3"));
        Assert.That(lv3.actionModifiers[0].hitboxScaleMultiplier, Is.EqualTo(1.5f).Within(0.01f));

        // Passive Lv.4: 10s follow-up window unlocking Skill.super.ex
        var lv4 = passiveNodes[3];
        Assert.That(lv4.nodeId, Is.EqualTo("cg.passive.lv4"));
        Assert.That(lv4.runtimeTagEffects.Count, Is.EqualTo(1));
        Assert.That(lv4.runtimeTagEffects[0].tag, Is.EqualTo("cg.skill.followup_window"));
        Assert.That(lv4.runtimeTagEffects[0].duration, Is.EqualTo(10f));
        Assert.That(lv4.actionModifiers.Count, Is.EqualTo(1));
        Assert.That(lv4.actionModifiers[0].actionId, Is.EqualTo("Skill.super.ex"));
        Assert.That(lv4.actionModifiers[0].unlocked, Is.True);
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
