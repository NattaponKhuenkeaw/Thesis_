using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using Project.TeamBuilder.UI;
using Project.Core.ZoneState;
using Project.Progression;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TeamRuntimeStatFeedbackTests
{
    [Test]
    public void ShermieSelfStatBuffs_DoNotModifyTeammateStats()
    {
        CharacterObject shermieAsset = AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_03_shermie/CharacterObject_Shermie.asset");
        Assert.That(shermieAsset, Is.Not.Null);

        GameObject playerObject = new GameObject("Shermie Self Buff Test Player");
        GameObject loaderObject = new GameObject("Shermie Self Buff Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        PlayerMetaProgression progression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        CharacterObject shermie = Object.Instantiate(shermieAsset);
        CharacterObject teammate = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            teammate.characterId = "shermie-self-buff-test-teammate";
            teammate.stats.maxHP = 1000f;
            teammate.stats.defense = 100f;

            progression.SetPotentialLevel(shermie, PotentialTrack.Skill, 2);

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            typeof(TeamLoader)
                .GetField("metaProgression", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(teamLoader, progression);
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(new[] { shermie, teammate }, 0);

            passiveController.SetMetaProgression(progression);
            passiveController.InitializeFromCharacterData();

            float teammateDefenseBefore = teamLoader.GetCharacterRuntimeStats(teammate).defense;
            float shermieDefenseBefore = teamLoader.GetCharacterRuntimeStats(shermie).defense;

            passiveController.NotifyActionStarted(
                AttackCategory.Skill,
                "shermie.skill",
                "Diamod Dust",
                new AttackStepData { stepName = "Grab Dash" });

            Assert.That(
                teamLoader.GetCharacterRuntimeStats(shermie).defense,
                Is.EqualTo(shermieDefenseBefore * 1.5f).Within(0.001f));
            Assert.That(
                teamLoader.GetCharacterRuntimeStats(teammate).defense,
                Is.EqualTo(teammateDefenseBefore).Within(0.001f));

            runInventory.ClearRuntimeEffects(shermie);
            runInventory.ClearRuntimeEffects(teammate);

            passiveController.NotifyActionStarted(
                AttackCategory.Skill,
                "shermie.skill",
                "Diamod Dust",
                new AttackStepData { stepName = "Grab Dash" });

            float teammateMaxHpBefore = teamLoader.GetCharacterRuntimeStats(teammate).maxHP;
            float shermieMaxHpBefore = teamLoader.GetCharacterRuntimeStats(shermie).maxHP;

            passiveController.NotifyActionStarted(
                AttackCategory.Ultimate,
                "shermie.ultimate",
                "Shermie Carnival",
                null);

            Assert.That(
                teamLoader.GetCharacterRuntimeStats(shermie).maxHP,
                Is.EqualTo(shermieMaxHpBefore * 1.5f).Within(0.001f));
            Assert.That(
                teamLoader.GetCharacterRuntimeStats(teammate).maxHP,
                Is.EqualTo(teammateMaxHpBefore).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(teammate);
            Object.DestroyImmediate(shermie);
            Object.DestroyImmediate(progression);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_StoresArtifactsAndRefreshesRuntimeStats()
    {
        GameObject playerObject = new GameObject("Test Player");
        GameObject loaderObject = new GameObject("Test TeamLoader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        Artifact artifact = ScriptableObject.CreateInstance<Artifact>();

        try
        {
            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);

            artifact.bonusStats = CreateZeroBonusStats();
            artifact.bonusStats.maxHP = 50f;
            artifact.bonusStats.cooldownRate = 50f;

            teamLoader.DeployLoadout(
                new[] { character },
                0,
                new[] { new[] { artifact } });

            Assert.That(teamLoader.GetEquippedArtifactsAt(0)[0], Is.SameAs(artifact));
            Assert.That(playerCharacter.MaxHP, Is.EqualTo(150f).Within(0.001f));
            Assert.That(playerCharacter.Stats.cooldownRate, Is.EqualTo(50f).Within(0.001f));
            Assert.That(runInventory.GetTeamRunCharacterAt(0).character, Is.SameAs(character));
            playerCharacter.SetCurrentHP(140f);
            artifact.bonusStats.maxHP = 0f;

            teamLoader.RefreshRuntimeState();

            Assert.That(playerCharacter.MaxHP, Is.EqualTo(100f).Within(0.001f));
            Assert.That(playerCharacter.CurrentHP, Is.EqualTo(100f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(artifact);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamSlotTrend_ClassifiesCdrOnlyEquipAndRemove()
    {
        MethodInfo calculateTrend = typeof(TeamSlotUI).GetMethod(
            "CalculateStatTrend",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(calculateTrend, Is.Not.Null);

        CharacterStats baseline = new CharacterStats();
        CharacterStats upgraded = baseline.Clone();
        upgraded.cooldownRate += 50f;

        float equipTrend = (float)calculateTrend.Invoke(null, new object[] { baseline, upgraded });
        float removeTrend = (float)calculateTrend.Invoke(null, new object[] { upgraded, baseline });

        Assert.That(equipTrend, Is.GreaterThan(0f));
        Assert.That(removeTrend, Is.LessThan(0f));
    }

    [Test]
    public void TeamLoader_AppliesAlliesScopePotentialStatModifiersToEntireTeam()
    {
        GameObject playerObject = new GameObject("Potential Stat Test Player");
        GameObject loaderObject = new GameObject("Potential Stat Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject leader = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject ally = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            leader.characterId = "leader";
            leader.stats.attack = 100f;
            ally.characterId = "ally";
            ally.stats.attack = 100f;
            leader.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Add,
                value = 10f,
                scope = PassiveEffectScope.Allies
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);

            teamLoader.DeployLoadout(new[] { leader, ally }, 0);

            Assert.That(teamLoader.GetCharacterRuntimeStats(leader).attack, Is.EqualTo(110f).Within(0.001f));
            Assert.That(teamLoader.GetCharacterRuntimeStats(ally).attack, Is.EqualTo(110f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(leader);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_AppliesSelfPotentialStatSourceRatio()
    {
        GameObject playerObject = new GameObject("Potential Source Ratio Test Player");
        GameObject loaderObject = new GameObject("Potential Source Ratio Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "source-ratio-character";
            character.stats.attack = 100f;
            character.stats.defense = 40f;
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Add,
                valueSource = PassiveModifierValueSource.SourceStatRatio,
                value = 0.5f,
                sourceStat = PassiveModifierTarget.Defense,
                scope = PassiveEffectScope.Self
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);

            teamLoader.DeployLoadout(new[] { character }, 0);

            Assert.That(teamLoader.GetCharacterRuntimeStats(character).attack, Is.EqualTo(120f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_AppliesSourceRatioWithSelectedOperation()
    {
        GameObject playerObject = new GameObject("Potential Source Ratio Operation Test Player");
        GameObject loaderObject = new GameObject("Potential Source Ratio Operation Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "source-ratio-operation-character";
            character.stats.attack = 10f;
            character.stats.defense = 4f;
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Multiply,
                valueSource = PassiveModifierValueSource.SourceStatRatio,
                value = 0.5f,
                sourceStat = PassiveModifierTarget.Defense,
                scope = PassiveEffectScope.Self
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);

            teamLoader.DeployLoadout(new[] { character }, 0);

            Assert.That(teamLoader.GetCharacterRuntimeStats(character).attack, Is.EqualTo(20f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_PermanentStatsExcludeRuntimeEffects()
    {
        GameObject playerObject = new GameObject("Permanent Stat Test Player");
        GameObject loaderObject = new GameObject("Permanent Stat Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        Artifact artifact = ScriptableObject.CreateInstance<Artifact>();
        StatRuntimeEffectDefinition runtimeEffect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            character.characterId = "permanent-stat-character";
            character.stats.attack = 100f;
            artifact.bonusStats = CreateZeroBonusStats();
            artifact.bonusStats.attack = 50f;
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AttackStat,
                operation = PassiveNumericOperation.Multiply,
                value = 1.2f,
                scope = PassiveEffectScope.Self
            });

            runtimeEffect.kind = RuntimeEffectKind.Effect;
            runtimeEffect.durationType = RuntimeEffectDurationType.Forever;
            runtimeEffect.statModifiers.Add(new RuntimeEffectStatModifier
            {
                target = RuntimeEffectStatTarget.Attack,
                operation = RuntimeEffectStatOperation.Add,
                value = 30f
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(
                new[] { character },
                0,
                new[] { new[] { artifact } });

            Assert.That(teamLoader.GetCharacterPermanentStats(character).attack, Is.EqualTo(120f).Within(0.001f));
            Assert.That(teamLoader.GetCharacterRuntimeStats(character).attack, Is.EqualTo(180f).Within(0.001f));

            runInventory.ApplyRuntimeEffect(character, runtimeEffect, character, character.stats);

            Assert.That(teamLoader.GetCharacterPermanentStats(character).attack, Is.EqualTo(120f).Within(0.001f));
            Assert.That(teamLoader.GetCharacterRuntimeStats(character).attack, Is.EqualTo(210f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(runtimeEffect);
            Object.DestroyImmediate(artifact);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_AppliesAlwaysForeverRuntimeRecipeToAllies()
    {
        GameObject playerObject = new GameObject("Always Runtime Effect Test Player");
        GameObject loaderObject = new GameObject("Always Runtime Effect Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject source = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject ally = ScriptableObject.CreateInstance<CharacterObject>();
        DamageModifierRuntimeEffectDefinition damageEffect = ScriptableObject.CreateInstance<DamageModifierRuntimeEffectDefinition>();

        try
        {
            source.characterId = "always-source";
            source.elementType = ElementType.Ash;
            ally.characterId = "always-ally";
            ally.elementType = ElementType.Ash;
            damageEffect.effectId = "test.always_ash_damage";

            source.kitDefinition.basePassiveNode.runtimeEffects.Add(new PassiveRuntimeEffectDefinition
            {
                trigger = PassiveTriggerType.Always,
                recipe = new RuntimeEffectRecipe
                {
                    recipeId = "test.always_ash_damage.recipe",
                    definition = damageEffect,
                    policy = new RuntimeEffectApplicationPolicy
                    {
                        kind = RuntimeEffectKind.Effect,
                        appliesOn = RuntimeEffectApplyTarget.Allies,
                        durationType = RuntimeEffectDurationType.Forever,
                        stackMode = RuntimeEffectStackMode.Override
                    },
                    parameters = new RuntimeEffectParameterSet
                    {
                        overrideDamageModifier = true,
                        damageModifier = new RuntimeEffectDamageModifierParameters
                        {
                            filter = new RuntimeEffectDamageFilter
                            {
                                filterByElement = true,
                                elementType = ElementType.Ash
                            },
                            outgoingDamagePercentAdd = 15f
                        }
                    }
                }
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(new[] { source, ally }, 0);

            RuntimeEffectDamageContext context = new RuntimeEffectDamageContext
            {
                sourceCharacter = ally,
                elementType = ElementType.Ash,
                category = AttackCategory.Normal
            };

            Assert.That(runInventory.GetActiveRuntimeEffects(source).Count, Is.EqualTo(1));
            Assert.That(runInventory.GetActiveRuntimeEffects(ally).Count, Is.EqualTo(1));
            Assert.That(runInventory.ModifyOutgoingDamage(ally, context, 100f), Is.EqualTo(115f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(damageEffect);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void PassiveController_ActionEndedRuntimeRecipeAppliesToAllies()
    {
        GameObject playerObject = new GameObject("Triggered Runtime Effect Test Player");
        GameObject loaderObject = new GameObject("Triggered Runtime Effect Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject source = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterObject ally = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition statEffect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            source.characterId = "trigger-source";
            source.stats.attack = 100f;
            ally.characterId = "trigger-ally";
            ally.stats.attack = 100f;
            statEffect.effectId = "test.triggered_allies_attack";

            source.kitDefinition.baseUltimateNode.runtimeEffects.Add(new PassiveRuntimeEffectDefinition
            {
                trigger = PassiveTriggerType.OnActionEnded,
                filter = new PassiveTriggerFilter
                {
                    filterByCategory = true,
                    category = AttackCategory.Ultimate
                },
                recipe = new RuntimeEffectRecipe
                {
                    recipeId = "test.triggered_allies_attack.recipe",
                    definition = statEffect,
                    policy = new RuntimeEffectApplicationPolicy
                    {
                        kind = RuntimeEffectKind.Buff,
                        appliesOn = RuntimeEffectApplyTarget.Allies,
                        durationType = RuntimeEffectDurationType.Seconds,
                        durationSeconds = 20f,
                        stackMode = RuntimeEffectStackMode.Override
                    },
                    parameters = new RuntimeEffectParameterSet
                    {
                        overrideStatModifiers = true,
                        statModifiers = new System.Collections.Generic.List<RuntimeEffectStatModifier>
                        {
                            new RuntimeEffectStatModifier
                            {
                                target = RuntimeEffectStatTarget.Attack,
                                operation = RuntimeEffectStatOperation.Add,
                                value = 25f
                            }
                        }
                    }
                }
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;
            playerCharacter.characterObject = source;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(new[] { source, ally }, 0);

            passiveController.InitializeFromCharacterData();
            passiveController.NotifyActionEnded(AttackCategory.Ultimate, "test.ultimate", "Test Ultimate", null);

            Assert.That(runInventory.GetActiveRuntimeEffects(source).Count, Is.EqualTo(1));
            Assert.That(runInventory.GetActiveRuntimeEffects(ally).Count, Is.EqualTo(1));
            Assert.That(teamLoader.GetCharacterRuntimeStats(ally).attack, Is.EqualTo(125f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(statEffect);
            Object.DestroyImmediate(ally);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_ConditionalHpTagGatesPotentialStatModifier()
    {
        GameObject playerObject = new GameObject("HP Tag Runtime Stat Test Player");
        GameObject loaderObject = new GameObject("HP Tag Runtime Stat Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            const string highHpTag = "kyo_high_hp_ae_recharge";

            character.characterId = "hp-tag-character";
            character.stats.maxHP = 100f;
            character.stats.aeRecharge = 1f;
            character.kitDefinition.basePassiveNode.statModifiers.Add(new PassiveStatModifierDefinition
            {
                target = PassiveModifierTarget.AuraEnergyRecharge,
                operation = PassiveNumericOperation.Add,
                value = 0.25f,
                scope = PassiveEffectScope.Self,
                requiredTags = new System.Collections.Generic.List<RuntimeTagRequirement>
                {
                    new RuntimeTagRequirement { tag = highHpTag, expectedValue = true }
                }
            });
            character.kitDefinition.basePassiveNode.conditionalStateEffects.Add(new PassiveConditionalStateEffectDefinition
            {
                trigger = PassiveTriggerType.Always,
                requiredValues = new System.Collections.Generic.List<PassiveRuntimeValueRequirement>
                {
                    new PassiveRuntimeValueRequirement
                    {
                        metric = PassiveRuntimeValueMetric.CurrentHpRatio,
                        comparison = CounterComparison.GreaterThanOrEqual,
                        value = 0.7f
                    }
                },
                runtimeTagEffects = new System.Collections.Generic.List<PassiveRuntimeTagEffectDefinition>
                {
                    new PassiveRuntimeTagEffectDefinition { tag = highHpTag, addTag = true }
                }
            });
            character.kitDefinition.basePassiveNode.conditionalStateEffects.Add(new PassiveConditionalStateEffectDefinition
            {
                trigger = PassiveTriggerType.Always,
                requiredValues = new System.Collections.Generic.List<PassiveRuntimeValueRequirement>
                {
                    new PassiveRuntimeValueRequirement
                    {
                        metric = PassiveRuntimeValueMetric.CurrentHpRatio,
                        comparison = CounterComparison.LessThan,
                        value = 0.7f
                    }
                },
                runtimeTagEffects = new System.Collections.Generic.List<PassiveRuntimeTagEffectDefinition>
                {
                    new PassiveRuntimeTagEffectDefinition { tag = highHpTag, addTag = false }
                }
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(new[] { character }, 0);

            passiveController.RefreshConditionalStateEffects();
            teamLoader.RefreshRuntimeStatsAndResources();

            Assert.That(passiveController.RuntimeState.HasTag(highHpTag), Is.True);
            Assert.That(teamLoader.GetCharacterRuntimeStats(character).aeRecharge, Is.EqualTo(1.25f).Within(0.001f));

            teamLoader.SetCharacterCurrentHP(character, 60f);

            Assert.That(passiveController.RuntimeState.HasTag(highHpTag), Is.False);
            Assert.That(teamLoader.GetCharacterRuntimeStats(character).aeRecharge, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_AlwaysRuntimeRecipeFilterRemovesEffectWhenTagMissing()
    {
        GameObject playerObject = new GameObject("Always Filter Runtime Effect Test Player");
        GameObject loaderObject = new GameObject("Always Filter Runtime Effect Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        StatRuntimeEffectDefinition statEffect = ScriptableObject.CreateInstance<StatRuntimeEffectDefinition>();

        try
        {
            const string tag = "test.hp.above.70";

            character.characterId = "always-filter-character";
            character.stats.aeRecharge = 1f;
            statEffect.effectId = "test.always_filtered_ae_recharge";

            character.kitDefinition.basePassiveNode.runtimeEffects.Add(new PassiveRuntimeEffectDefinition
            {
                trigger = PassiveTriggerType.Always,
                filter = new PassiveTriggerFilter
                {
                    requireRuntimeTag = true,
                    runtimeTag = tag
                },
                recipe = new RuntimeEffectRecipe
                {
                    recipeId = "test.always_filtered_ae_recharge.recipe",
                    definition = statEffect,
                    policy = new RuntimeEffectApplicationPolicy
                    {
                        kind = RuntimeEffectKind.Effect,
                        appliesOn = RuntimeEffectApplyTarget.Self,
                        durationType = RuntimeEffectDurationType.Forever,
                        stackMode = RuntimeEffectStackMode.Override
                    },
                    parameters = new RuntimeEffectParameterSet
                    {
                        overrideStatModifiers = true,
                        statModifiers = new System.Collections.Generic.List<RuntimeEffectStatModifier>
                        {
                            new RuntimeEffectStatModifier
                            {
                                target = RuntimeEffectStatTarget.AuraEnergyRecharge,
                                operation = RuntimeEffectStatOperation.Add,
                                value = 0.25f
                            }
                        }
                    }
                }
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(new[] { character }, 0);

            passiveController.RuntimeState.SetTag(tag);
            teamLoader.RefreshRuntimeStatsAndResources();

            Assert.That(runInventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(1));
            Assert.That(teamLoader.GetCharacterRuntimeStats(character).aeRecharge, Is.EqualTo(1.25f).Within(0.001f));

            passiveController.RuntimeState.RemoveTag(tag);
            teamLoader.RefreshRuntimeStatsAndResources();

            Assert.That(runInventory.GetActiveRuntimeEffects(character).Count, Is.EqualTo(0));
            Assert.That(teamLoader.GetCharacterRuntimeStats(character).aeRecharge, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(statEffect);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void ActionResolver_TaggedUltimateFollowUpSelectsExActionOnlyWhenTagExists()
    {
        GameObject playerObject = new GameObject("Tagged Ultimate Follow Up Test Player");
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        ActionResolverTable table = ScriptableObject.CreateInstance<ActionResolverTable>();

        try
        {
            const string followUpTag = "kyo.ultimate.follow_up";

            character.characterId = "tagged-ultimate-follow-up-character";
            character.kitDefinition.basePassiveNode.actionModifiers.Add(new PassiveActionModifierDefinition
            {
                category = AttackCategory.Ultimate,
                actionId = "kyo.ultimate.ex",
                unlocked = true,
                priorityBonus = 100,
                requiredTags = new System.Collections.Generic.List<RuntimeTagRequirement>
                {
                    new RuntimeTagRequirement { tag = followUpTag, expectedValue = true }
                }
            });

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            playerCharacter.PassiveController = passiveController;
            passiveController.playerCharacter = playerCharacter;
            playerCharacter.characterObject = character;
            passiveController.InitializeFromCharacterData();

            table.ultimateActions.Add(CreateTestUltimateAction("kyo.ultimate", "Base Ultimate", 1, null));
            table.ultimateActions.Add(CreateTestUltimateAction(
                "kyo.ultimate.ex",
                "Follow Up",
                0,
                new RuntimeTagActionCondition { tag = followUpTag, shouldExist = true }));

            InputContext context = new InputContext
            {
                isGrounded = true,
                facingDirection = 1,
                passiveController = passiveController,
                runtimeState = passiveController.RuntimeState
            };

            ActionResolver resolver = new ActionResolver();
            resolver.ResolveInitial(context, table, AttackCategory.Ultimate);
            Assert.That(resolver.ActiveCombatAction?.actionId, Is.EqualTo("kyo.ultimate"));

            passiveController.RuntimeState.SetTag(followUpTag, 20f);
            resolver.ResolveInitial(context, table, AttackCategory.Ultimate);
            Assert.That(resolver.ActiveCombatAction?.actionId, Is.EqualTo("kyo.ultimate.ex"));
        }
        finally
        {
            Object.DestroyImmediate(table);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void TeamLoader_StatsResourceRefreshPreservesPassiveRuntimeState()
    {
        GameObject playerObject = new GameObject("Passive Runtime State Test Player");
        GameObject loaderObject = new GameObject("Passive Runtime State Test Loader");
        PlayerRunInventory runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();

        try
        {
            character.characterId = "passive-runtime-state-character";
            character.stats.attack = 100f;

            PlayerCharacter playerCharacter = playerObject.AddComponent<PlayerCharacter>();
            CharacterPassiveController passiveController = playerObject.AddComponent<CharacterPassiveController>();
            passiveController.playerCharacter = playerCharacter;
            playerCharacter.PassiveController = passiveController;

            TeamLoader teamLoader = loaderObject.AddComponent<TeamLoader>();
            teamLoader.playerCharacter = playerCharacter;
            teamLoader.SetRunInventory(runInventory);
            teamLoader.DeployLoadout(new[] { character }, 0);

            passiveController.RuntimeState.SetTag("follow_up_window", 5f);

            teamLoader.RefreshRuntimeStatsAndResources();

            Assert.That(passiveController.RuntimeState.HasTag("follow_up_window"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(runInventory);
            Object.DestroyImmediate(loaderObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [UnityTest]
    public IEnumerator TeamSlot_CdrOnlyArtifactFlashesGreenThenRed()
    {
        GameObject builderObject = new GameObject("Test TeamBuilder");
        GameObject slotObject = new GameObject("Test TeamSlot", typeof(RectTransform));
        GameObject portraitObject = new GameObject(
            "Test Portrait",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        GameObject artifactSlotObject = new GameObject(
            "Test ArtifactSlot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ArtifactSlotUI));
        CharacterObject character = ScriptableObject.CreateInstance<CharacterObject>();
        CharacterItem characterItem = ScriptableObject.CreateInstance<CharacterItem>();
        Artifact artifact = ScriptableObject.CreateInstance<Artifact>();

        try
        {
            portraitObject.transform.SetParent(slotObject.transform, false);
            artifactSlotObject.transform.SetParent(slotObject.transform, false);

            TeamBuilderUI builder = builderObject.AddComponent<TeamBuilderUI>();
            SetPrivateField(builder, "currentMode", ZoneMode.SafeZone);

            TeamSlotUI teamSlot = slotObject.AddComponent<TeamSlotUI>();
            ArtifactSlotUI artifactSlot = artifactSlotObject.GetComponent<ArtifactSlotUI>();
            Image portrait = portraitObject.GetComponent<Image>();
            SetPrivateField(teamSlot, "profileImage", portrait);
            SetPrivateField(teamSlot, "artifactSlots", new[] { artifactSlot });

            characterItem.characterObjectData = character;
            artifact.bonusStats = CreateZeroBonusStats();
            artifact.bonusStats.cooldownRate = 50f;

            teamSlot.Init(builder, 0);
            teamSlot.SetCharacter(characterItem);

            artifactSlot.SetArtifact(artifact);
            teamSlot.RecalculateStats();
            yield return null;

            Assert.That(portrait.color.g, Is.GreaterThan(portrait.color.r));

            yield return new WaitForSecondsRealtime(0.35f);

            artifactSlot.ClearArtifact();
            teamSlot.RecalculateStats();
            yield return null;

            Assert.That(portrait.color.r, Is.GreaterThan(portrait.color.g));
        }
        finally
        {
            Object.DestroyImmediate(artifact);
            Object.DestroyImmediate(characterItem);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(slotObject);
            Object.DestroyImmediate(builderObject);
        }
    }

    private static CharacterStats CreateZeroBonusStats()
    {
        return new CharacterStats
        {
            maxHP = 0f,
            attack = 0f,
            defense = 0f,
            speed = 0f,
            elementalMastery = 0f,
            critRate = 0f,
            critDamage = 0f,
            cooldownRate = 0f,
            impact = 0f,
            aeRecharge = 0f
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static UltimateAction CreateTestUltimateAction(
        string actionId,
        string actionName,
        int priority,
        ActionConditionBase condition)
    {
        UltimateAction action = new UltimateAction
        {
            actionId = actionId,
            actionName = actionName,
            context = CombatActionContext.Ground,
            priority = priority,
            comboTree = new ComboTree
            {
                rootNode = new ComboNode
                {
                    stepData = new AttackStepData { stepName = actionName }
                }
            }
        };

        if (condition != null)
        {
            action.conditions.Add(condition);
        }

        return action;
    }
}
