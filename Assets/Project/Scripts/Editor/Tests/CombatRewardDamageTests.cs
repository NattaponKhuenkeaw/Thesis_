using NUnit.Framework;
using Project.Progression;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

public class CombatRewardDamageTests
{
    [Test]
    public void CombatAction_DefaultsGuaranteedAuraRewardToZero()
    {
        ComboAction action = new ComboAction();

        Assert.That(action.auraEnergyReward, Is.EqualTo(0f));
    }

    [Test]
    public void AuraEnergyRewardClaim_CanOnlyBeClaimedOnce()
    {
        AuraEnergyRewardClaim claim = new AuraEnergyRewardClaim(25f);

        Assert.That(claim.TryClaim(), Is.True);
        Assert.That(claim.TryClaim(), Is.False);
        Assert.That(claim.Amount, Is.EqualTo(25f));
    }

    [Test]
    public void PlayerCombat_ReusesRewardClaimAcrossStepsOfSameAction()
    {
        GameObject playerObject = new GameObject("Reward Claim Test Player");
        PlayerCombat combat = playerObject.AddComponent<PlayerCombat>();
        ComboAction action = new ComboAction { auraEnergyReward = 25f };

        combat.BeginResolvedAction(true, false, false, action);
        AuraEnergyRewardClaim firstClaim = combat.CurrentAuraEnergyRewardClaim;
        combat.BeginResolvedAction(true, false, false, action);

        Assert.That(combat.CurrentAuraEnergyRewardClaim, Is.SameAs(firstClaim));

        Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void AuraEnergyChanceGainGate_LimitsGrantsWithinWindow()
    {
        GameObject playerObject = new GameObject("Chance Gate Test Player");
        PlayerCombat combat = playerObject.AddComponent<PlayerCombat>();
        MethodInfo consumeSlot = typeof(PlayerCombat).GetMethod(
            "TryConsumeAuraEnergyChanceGrantSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(consumeSlot, Is.Not.Null);
        for (int i = 0; i < 5; i++)
        {
            Assert.That((bool)consumeSlot.Invoke(combat, null), Is.True);
        }

        Assert.That((bool)consumeSlot.Invoke(combat, null), Is.False);

        Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void EnemyTakeHit_ReturnsFinalDamageInsteadOfRemainingHpLoss()
    {
        GameObject enemyObject = new GameObject("Damage Test Enemy");
        enemyObject.AddComponent<Rigidbody2D>();
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();
        enemy.maxHP = 64f;
        enemy.currentHP = 64f;

        float eventDamage = 0f;
        enemy.OnDamaged += data => eventDamage = data.damage;

        HitReactionData hit = HitReactionData.Default(150f, Vector2.right, 0f);
        hit.damageIsFinal = true;
        hit.flashFrames = 0;

        LogAssert.Expect(
            LogType.Error,
            "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
        float finalDamage = enemy.TakeHit(hit, Vector2.zero);

        Assert.That(finalDamage, Is.EqualTo(150f));
        Assert.That(eventDamage, Is.EqualTo(150f));
        Assert.That(enemy.currentHP, Is.EqualTo(0f));

        if (enemyObject != null)
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void PassiveController_UsesUnlockedPotentialActionModifier()
    {
        GameObject playerObject = new GameObject("Potential Effect Test Player");
        PlayerMetaProgression metaProgression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "effect-character";
            character.kitDefinition.potentialNodes.Add(new PotentialNodeDefinition
            {
                nodeId = "effect-character.skill.lv1",
                track = PotentialTrack.Skill,
                level = 1,
                actionModifiers =
                {
                    new PassiveActionModifierDefinition
                    {
                        category = AttackCategory.Skill,
                        actionId = "effect.skill",
                        damageMultiplierOperation = PassiveValueModifierOperation.Override,
                        damageMultiplierValue = 6.5f
                    }
                }
            });
            metaProgression.SetPotentialLevel(character, PotentialTrack.Skill, 1);

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            player.characterObject = character;
            player.PassiveController = passiveController;
            passiveController.playerCharacter = player;
            passiveController.SetMetaProgression(metaProgression);
            passiveController.InitializeFromCharacterData();

            float resolved = passiveController.ModifyActionDamageMultiplier(
                AttackCategory.Skill,
                "effect.skill",
                "Skill",
                5f);

            Assert.That(resolved, Is.EqualTo(6.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(metaProgression);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void PassiveController_UsesHighestEvasionChance()
    {
        GameObject playerObject = new GameObject("Potential Evasion Test Player");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "evasion-character";
            character.kitDefinition.basePassiveNode.defensiveEffects.Add(new PassiveDefensiveEffectDefinition
            {
                evasionChance = 1f
            });

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            player.characterObject = character;
            player.PassiveController = passiveController;
            passiveController.playerCharacter = player;
            passiveController.InitializeFromCharacterData();

            Assert.That(passiveController.TryEvadeIncomingDamage(100f), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void PassiveController_AppliesStatModifierOnlyWhenRuntimeRequirementMatches()
    {
        GameObject playerObject = new GameObject("Potential Timed Stat Test Player");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "timed-stat-character";
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.CritRate,
                operation = PassiveNumericOperation.Add,
                value = 17f,
                scope = PassiveEffectScope.Self,
                requiredTags =
                {
                    new RuntimeTagRequirement
                    {
                        tag = "crit_window",
                        expectedValue = true
                    }
                }
            });

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            player.characterObject = character;
            player.PassiveController = passiveController;
            passiveController.playerCharacter = player;
            passiveController.InitializeFromCharacterData();

            Assert.That(passiveController.ModifyValue(PassiveModifierTarget.CritRate, 5f), Is.EqualTo(5f).Within(0.001f));

            passiveController.RuntimeState.SetTag("crit_window", 5f);

            Assert.That(passiveController.ModifyValue(PassiveModifierTarget.CritRate, 5f), Is.EqualTo(22f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void AdditionalDamage_CalculatesAttackAndTargetMaxHpDamage()
    {
        AdditionalDamageEffectDefinition effect = new AdditionalDamageEffectDefinition
        {
            attackRatio = 0.2f,
            targetMaxHpRatio = 0.04f
        };
        PassiveEventContext context = new PassiveEventContext
        {
            enemyMaxHp = 1000f
        };

        Assert.That(effect.CalculateBaseDamage(500f, context), Is.EqualTo(140f).Within(0.001f));
    }

    [Test]
    public void EnemyElementalState_RuntimeEchoResistanceModifierAffectsOnlyRuntimeElement()
    {
        GameObject enemyObject = new GameObject("Runtime Element Test Enemy");
        EnemyElementalState elementalState = enemyObject.AddComponent<EnemyElementalState>();

        try
        {
            elementalState.ApplyRuntimeElementModifier(new PassiveEnemyElementModifierDefinition
            {
                elementType = ElementType.Echo,
                resistancePercentAdd = -15f,
                permanent = true
            });

            Assert.That(elementalState.ModifyIncomingDamage(ElementType.Echo, 100f), Is.EqualTo(115f).Within(0.001f));
            Assert.That(elementalState.ModifyIncomingDamage(ElementType.Ash, 100f), Is.EqualTo(100f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(enemyObject);
        }
    }
}
