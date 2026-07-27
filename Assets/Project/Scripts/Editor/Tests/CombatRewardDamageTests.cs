using NUnit.Framework;
using Project.Progression;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

public class CombatRewardDamageTests
{
    [Test]
    public void AdditionalDamageFormulaSourceUsesCurrentEffectiveStats()
    {
        GameObject playerObject = new GameObject("Additional Damage Live Source Test");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "additional-live-source-character";
            CharacterStats snapshot = new CharacterStats { attack = 100f };
            CharacterStats currentStats = new CharacterStats { attack = 25f };
            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            player.characterObject = character;

            FieldInfo runtimeStatsField = typeof(PlayerCharacter).GetField(
                "runtimeStats",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(runtimeStatsField, Is.Not.Null);
            runtimeStatsField.SetValue(player, currentStats);

            CharacterStats resolved = RuntimeEffectRuntimeUtility.ResolveCurrentFormulaSourceStats(
                new RuntimeEffectDamageContext
                {
                    sourceCharacter = character,
                    sourceStats = snapshot
                });

            Assert.That(resolved.attack, Is.EqualTo(25f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void CombatFormula_ElementalAdditionalDamageUsesMasteryWithoutCritOrImpact()
    {
        CharacterStats stats = new CharacterStats
        {
            elementalMastery = 50f,
            critRate = 100f,
            critDamage = 300f,
            impact = 100f
        };

        float damage = CombatFormula.CalculateResolvedDamage(
            100f,
            DamageRoutine.Additional,
            ElementType.Ash,
            stats,
            0f,
            AttackCategory.Ultimate,
            out bool isCrit);

        Assert.That(damage, Is.EqualTo(150f).Within(0.001f));
        Assert.That(isCrit, Is.False);
    }

    [Test]
    public void PassiveController_OutgoingDamageUsesCategoryFilterAndLegacyTargets()
    {
        GameObject playerObject = new GameObject("Outgoing Damage Filter Test Player");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "outgoing-damage-filter-character";
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.OutgoingDamage,
                operation = PassiveNumericOperation.Multiply,
                value = 1.5f,
                filterOutgoingDamageByCategory = true,
                outgoingDamageCategory = AttackCategory.Skill
            });
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.UltimateDamage,
                operation = PassiveNumericOperation.Multiply,
                value = 2f
            });

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            player.characterObject = character;
            player.PassiveController = passiveController;
            passiveController.playerCharacter = player;
            passiveController.InitializeFromCharacterData();

            float skillDamage = passiveController.ModifyOutgoingDamage(
                new RuntimeEffectDamageContext { category = AttackCategory.Skill },
                100f);
            float ultimateDamage = passiveController.ModifyOutgoingDamage(
                new RuntimeEffectDamageContext { category = AttackCategory.Ultimate },
                100f);

            Assert.That(skillDamage, Is.EqualTo(150f).Within(0.001f));
            Assert.That(ultimateDamage, Is.EqualTo(200f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(playerObject);
        }
    }

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
    public void PlayerCombat_DoesNotReapplyStaticAttackStatModifiersToResolvedStats()
    {
        GameObject playerObject = new GameObject("Static Attack Stat Test Player");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "static-attack-stat-character";
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Multiply,
                value = 1.2f,
                scope = PassiveEffectScope.Self
            });
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Add,
                value = 10f,
                scope = PassiveEffectScope.Self,
                requiredTags =
                {
                    new RuntimeTagRequirement
                    {
                        tag = "attack_window",
                        expectedValue = true
                    }
                }
            });

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            PlayerCombat combat = playerObject.AddComponent<PlayerCombat>();
            player.PassiveController = passiveController;
            passiveController.playerCharacter = player;
            combat.playerCharacter = player;
            combat.passiveController = passiveController;
            player.SetCharacter(character, 100f, new CharacterStats
            {
                maxHP = 100f,
                attack = 120f,
                defense = 0f
            });

            Assert.That(combat.CurrentSourceAttack, Is.EqualTo(120f).Within(0.001f));

            passiveController.RuntimeState.SetTag("attack_window", 5f);

            Assert.That(combat.CurrentSourceAttack, Is.EqualTo(130f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void PassiveController_FinalDamageModifierAppliesAfterCriticalDamage()
    {
        GameObject playerObject = new GameObject("Final Damage Test Player");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "final-damage-character";
            character.kitDefinition.basePassiveNode.actionModifiers.Add(new PassiveActionModifierDefinition
            {
                category = AttackCategory.Skill,
                actionId = "goenitz.skill.yonokaze",
                finalDamageOperation = PassiveValueModifierOperation.Multiply,
                finalDamageValue = 2.5f,
                finalDamageRequiresCriticalHit = true
            });

            PlayerCharacter player = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            player.characterObject = character;
            player.PassiveController = passiveController;
            passiveController.playerCharacter = player;
            passiveController.InitializeFromCharacterData();

            float criticalDamage = passiveController.ModifyActionFinalDamage(
                AttackCategory.Skill,
                "goenitz.skill.yonokaze",
                "Yonokaze",
                112.5f,
                true);

            float normalDamage = passiveController.ModifyActionFinalDamage(
                AttackCategory.Skill,
                "goenitz.skill.yonokaze",
                "Yonokaze",
                50f,
                false);

            Assert.That(criticalDamage, Is.EqualTo(281.25f).Within(0.001f));
            Assert.That(normalDamage, Is.EqualTo(50f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
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
    public void AdditionalDamage_CapsTargetMaxHpDamageBySelfValue()
    {
        AdditionalDamageEffectDefinition effect = new AdditionalDamageEffectDefinition
        {
            attackRatio = 0.2f,
            targetMaxHpRatio = 0.2f,
            capTargetMaxHpDamage = true,
            targetMaxHpDamageCapSource = PassiveModifierTarget.AttackStat,
            targetMaxHpDamageCapRatio = 0.5f
        };
        PassiveEventContext context = new PassiveEventContext
        {
            enemyMaxHp = 1000f
        };
        CharacterStats sourceStats = new CharacterStats
        {
            attack = 100f
        };

        Assert.That(effect.CalculateBaseDamage(100f, context, sourceStats), Is.EqualTo(70f).Within(0.001f));
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

    [Test]
    public void EnemyElementalProfile_LegacyStrengthMapsToResistanceAndMastery()
    {
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();

        try
        {
            profile.resistances.Add(new ElementResistance
            {
                elementType = ElementType.Echo,
                strength = ElementStrength.Strong
            });
            profile.resistances.Add(new ElementResistance
            {
                elementType = ElementType.Ash,
                strength = ElementStrength.Weak
            });

            Assert.That(profile.GetResistancePercent(ElementType.Echo), Is.EqualTo(30f).Within(0.001f));
            Assert.That(profile.GetMasteryPercent(ElementType.Echo), Is.EqualTo(30f).Within(0.001f));
            Assert.That(profile.GetResistancePercent(ElementType.Ash), Is.EqualTo(-30f).Within(0.001f));
            Assert.That(profile.GetMasteryPercent(ElementType.Ash), Is.EqualTo(-30f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void EnemyElementalState_UsesProfileResistanceAndMastery()
    {
        GameObject enemyObject = new GameObject("Profile Element Test Enemy");
        EnemyElementalState elementalState = enemyObject.AddComponent<EnemyElementalState>();
        EnemyElementalProfile profile = ScriptableObject.CreateInstance<EnemyElementalProfile>();

        try
        {
            profile.resistances.Add(new ElementResistance
            {
                elementType = ElementType.Echo,
                useLegacyStrengthForResistance = false,
                resistancePercent = 25f,
                useLegacyStrengthForMastery = false,
                masteryPercent = 40f
            });

            elementalState.Initialize(profile);

            Assert.That(elementalState.ModifyIncomingDamage(ElementType.Echo, 100f), Is.EqualTo(75f).Within(0.001f));
            Assert.That(elementalState.ModifyOutgoingDamageAgainst(ElementType.Echo, 100f), Is.EqualTo(140f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void CombatFormula_CritDamageAppliesBonusPercentage()
    {
        CharacterStats stats = new CharacterStats
        {
            critRate = 100f,
            critDamage = 50f // 50% extra damage -> 1.5x total multiplier
        };

        float damage = CombatFormula.CalculateResolvedDamage(
            100f,
            DamageRoutine.Normal,
            ElementType.None,
            stats,
            0f,
            AttackCategory.Normal,
            out bool isCrit);

        Assert.That(isCrit, Is.True);
        Assert.That(damage, Is.EqualTo(150f).Within(0.001f));
    }
}
