#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MainAuthoredEffectsTests
{
    private const string Folder = "Assets/Project/Character/Hero/test_0_Main/";
    private readonly List<Object> cleanup = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    private EnemyController Enemy(string name)
    {
        var host = new GameObject(name);
        cleanup.Add(host);
        var enemy = EnemyLifecycleTestUtility.AddActive(host);
        enemy.maxHP = enemy.currentHP = 10000f;
        enemy.defense = enemy.damageRes = 0f;
        return enemy;
    }

    [TestCase("ProjectileData_main_chain_strike", 200f, ElementType.None, CombatDamageTag.None, .08f, "main.skill.dark_chain.summon", AttackCategory.Skill)]
    [TestCase("ProjectileData_main_void_area", 200f, ElementType.Void, CombatDamageTag.VoidArea, 0f, "main.ultimate.jailbreak_punisher", AttackCategory.Ultimate)]
    [TestCase("ProjectileData_main_void_area_lv3", 500f, ElementType.None, CombatDamageTag.VoidArea, .06f, "main.ultimate.jailbreak_punisher", AttackCategory.Ultimate)]
    [TestCase("ProjectileData_main_void_area_long", 500f, ElementType.None, CombatDamageTag.VoidArea, .06f, "main.ultimate.jailbreak_punisher", AttackCategory.Ultimate)]
    [TestCase("ProjectileData_main_void_area_lv4", 1000f, ElementType.None, CombatDamageTag.VoidArea, .12f, "main.ultimate.jailbreak_punisher", AttackCategory.Ultimate)]
    public void AuthoredPrimaryAndAdditionalHitsUseSeparateDamageTypes(
        string asset, float expectedPrimary, ElementType primaryElement, CombatDamageTag requiredTag,
        float additionalRatio, string actionId, AttackCategory category)
    {
        var data = AssetDatabase.LoadAssetAtPath<ProjectileData>(Folder + asset + ".asset");
        var hit = (NormalHitbox)data.ResolveRoutineHitbox();
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_main.asset");
        CharacterStats stats = new() { attack = 100f, maxHP = 1000f, critRate = 0f };
        var host = new GameObject("Separate primary and additional damage test");
        cleanup.Add(host);
        var damage = host.AddComponent<HitBoxDamage>();
        damage.SetHitData(1f, false, false, 0f, 0f, hit, AttackCategory.Skill, "Test", null, null);
        damage.ConfigurePerEnemyDamageCalculation(stats, 100f, true, null, null, null, null);
        var enemy = Enemy("Separate damage target");
        var resolvedHits = new List<DamageResolvedEvent>();
        System.Action<DamageResolvedEvent> capture = resolvedHits.Add;
        CombatEventHub.Shared.DamageResolved += capture;
        try
        {
            damage.ProcessHit(enemy);
            Assert.That(10000f - enemy.currentHP, Is.EqualTo(expectedPrimary).Within(.01f));
            Assert.That(damage.DamageValue, Is.EqualTo(expectedPrimary).Within(.01f));
            Assert.That(hit.elementType, Is.EqualTo(primaryElement));
            Assert.That(hit.damageScaling.Any(scaling => scaling.stat == CombatStatId.MaxHP), Is.False);
            Assert.That((hit.damageTags & requiredTag), Is.EqualTo(requiredTag));

            if (additionalRatio > 0f)
            {
                var recipe = data.onHitEffects.Single(application => application.triggerOnHit).ResolvedRecipe;
                var instance = new RuntimeEffectInstance();
                instance.Initialize(recipe, character, stats);
                recipe.definition.OnHitDealt(new RuntimeEffectDamageContext
                {
                    instance = instance,
                    sourceCharacter = character,
                    sourceStats = stats,
                    targetEnemy = enemy,
                    category = category,
                    actionId = actionId,
                    damageDelivery = DamageDelivery.Primary,
                    damage = expectedPrimary,
                    damageDealt = expectedPrimary,
                    provenance = new DamageProvenance(0, 0, 0, 0, null, requiredTag)
                });
                Assert.That(10000f - enemy.currentHP, Is.EqualTo(expectedPrimary + stats.maxHP * additionalRatio).Within(.01f));
                Assert.That(resolvedHits, Has.Count.EqualTo(2));
                Assert.That(resolvedHits[1].descriptor.delivery, Is.EqualTo(DamageDelivery.Additional));
                Assert.That(resolvedHits[1].descriptor.elementType, Is.EqualTo(ElementType.Darkness));
                Assert.That(resolvedHits[1].damageDealt, Is.EqualTo(stats.maxHP * additionalRatio).Within(.01f));
            }
            else
            {
                Assert.That(resolvedHits, Has.Count.EqualTo(1));
                Assert.That(resolvedHits[0].descriptor.delivery, Is.EqualTo(DamageDelivery.Primary));
                Assert.That(resolvedHits[0].descriptor.elementType, Is.EqualTo(primaryElement));
            }
        }
        finally
        {
            CombatEventHub.Shared.DamageResolved -= capture;
        }
    }

    [Test]
    public void PureMaxHpFormulaWorksWithZeroAttackAndEmptyFormulaPreservesActionBudget()
    {
        var hit = new NormalHitbox { damageScaling = new List<HitDamageStatScaling>
            { new() { stat = CombatStatId.MaxHP, ratio = .2f } } };
        var host = new GameObject("Pure HP damage test");
        cleanup.Add(host);
        var damage = host.AddComponent<HitBoxDamage>();
        var stats = new CharacterStats { attack = 0f, maxHP = 1000f, critRate = 0f };
        damage.SetHitData(0f, false, false, 0f, 0f, hit, AttackCategory.Normal, "Test", null, null);
        damage.ConfigurePerEnemyDamageCalculation(stats, 0f, false, null, null, null, null);
        var enemy = Enemy("HP formula target");
        damage.ProcessHit(enemy);
        Assert.That(enemy.currentHP, Is.EqualTo(9800f));
        Assert.That(new NormalHitbox().ResolveBaseDamage(stats, 37f), Is.EqualTo(37f));
    }

    [Test]
    public void FinisherRuntimeEffectDealsOwnHpDamageAndFiltersEarlierHits()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_main.asset");
        var recipe = character.kitDefinition.potentialNodes.Single(n => n.track == PotentialTrack.Passive && n.level == 5)
            .runtimeEffects.Single().recipe;
        var stats = new CharacterStats { attack = 0f, maxHP = 1000f, additionalDamageIncrease = 25f, dotMastery = 20f };
        var instance = new RuntimeEffectInstance();
        instance.Initialize(recipe, null, stats);
        var enemy = Enemy("Finisher target");
        var context = new RuntimeEffectDamageContext { instance = instance, sourceStats = stats,
            targetEnemy = enemy, category = AttackCategory.Ultimate, damageDelivery = DamageDelivery.Primary };
        recipe.definition.OnHitDealt(context);
        Assert.That(enemy.currentHP, Is.EqualTo(10000f));
        context.provenance = new DamageProvenance(0, 0, 0, 0, null, CombatDamageTag.Finisher);
        recipe.definition.OnHitDealt(context);
        // 20% own HP, then ADI and DM. Target HP, ATK, DI and defense do not supply the formula.
        Assert.That(enemy.currentHP, Is.EqualTo(9700f));
    }

    [Test]
    public void JudgmentRoomPulseRunsOncePerExecutionAndIncludesTargetsOutsideTheHitbox()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_main.asset");
        var recipe = character.kitDefinition.potentialNodes.Single(n => n.track == PotentialTrack.Passive && n.level == 6)
            .runtimeEffects.Single().recipe;
        var stats = new CharacterStats { maxHP = 1000f };
        var instance = new RuntimeEffectInstance();
        instance.Initialize(recipe, null, stats);
        var first = Enemy("Judgment primary target");
        var distant = Enemy("Judgment distant target");
        distant.transform.position = Vector3.right * 500f;
        RoomEnemyRegistry.Register(first);
        RoomEnemyRegistry.Register(distant);
        var context = new RuntimeEffectDamageContext
        {
            instance = instance, sourceStats = stats, targetEnemy = first,
            execution = new CombatExecutionContext(null, character, 0, false),
            category = AttackCategory.Skill, actionId = "main.skill.dimension_slash", damageDelivery = DamageDelivery.Primary,
            provenance = new DamageProvenance(0, 0, 0, 0, null, CombatDamageTag.Finisher)
        };
        recipe.definition.OnHitDealt(context);
        context.targetEnemy = distant;
        recipe.definition.OnHitDealt(context);
        Assert.That(first.currentHP, Is.EqualTo(9860f));
        Assert.That(distant.currentHP, Is.EqualTo(9860f));
        context.execution = new CombatExecutionContext(null, character, 0, false);
        recipe.definition.OnHitDealt(context);
        Assert.That(distant.currentHP, Is.EqualTo(9720f));
    }

    [Test]
    public void PercentageCooldownReductionHalvesEachRemainingTimerAndLeavesReadyActionsReady()
    {
        var character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "CharacterObject_main.asset"));
        cleanup.Add(character);
        var host = new GameObject("Cooldown ratio test");
        host.SetActive(false);
        cleanup.Add(host);
        var player = host.AddComponent<PlayerCharacter>();
        var combat = host.AddComponent<PlayerCombat>();
        player.characterObject = character;
        player.Combat = combat;
        combat.playerCharacter = player;
        var skill = character.actionResolverTable.skillActions.First(a => a.cooldown > 0f);
        var ult = character.actionResolverTable.ultimateActions.First(a => a.cooldown > 0f);
        combat.StartActionCooldown(AttackCategory.Skill, skill.actionName, skill.actionId);
        combat.StartActionCooldown(AttackCategory.Ultimate, ult.actionName, ult.actionId);
        combat.ReduceActionCooldown(AttackCategory.Skill, skill.actionName, skill.actionId, 3f);
        float skillBefore = combat.GetRemainingCooldown(AttackCategory.Skill, skill.cooldown, skill.actionName, skill.actionId);
        float ultBefore = combat.GetRemainingCooldown(AttackCategory.Ultimate, ult.cooldown, ult.actionName, ult.actionId);
        combat.ReduceActionCooldownByRatio(AttackCategory.Skill, null, null, .5f);
        combat.ReduceActionCooldownByRatio(AttackCategory.Ultimate, null, null, .5f);
        Assert.That(combat.GetRemainingCooldown(AttackCategory.Skill, skill.cooldown, skill.actionName, skill.actionId), Is.EqualTo(skillBefore / 2f).Within(.01f));
        Assert.That(combat.GetRemainingCooldown(AttackCategory.Ultimate, ult.cooldown, ult.actionName, ult.actionId), Is.EqualTo(ultBefore / 2f).Within(.01f));
        combat.ResetActionCooldown(AttackCategory.Skill);
        combat.ReduceActionCooldownByRatio(AttackCategory.Skill, null, null, .5f);
        combat.StartActionCooldown(AttackCategory.Skill, skill.actionName, skill.actionId);
        Assert.That(combat.GetRemainingCooldown(AttackCategory.Skill, skill.cooldown, skill.actionName, skill.actionId),
            Is.EqualTo(combat.GetFinalCooldown(AttackCategory.Skill, skill.cooldown, skill.actionName, skill.actionId)).Within(.01f));
    }
}
#endif
