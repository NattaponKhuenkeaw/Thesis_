using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using UnityEditor;
using UnityEngine;

public static class ArtifactPhase1AssetCreator
{
    private const string RootFolder = "Assets/Project/Resources/ItemSO/Artifacts/Phase1";

    private readonly struct ArtifactContent
    {
        public readonly string id;
        public readonly string fileName;
        public readonly string itemName;
        public readonly string description;
        public readonly string passiveDescription;

        public ArtifactContent(string id, string fileName, string itemName, string description,
            string passiveDescription)
        {
            this.id = id;
            this.fileName = fileName;
            this.itemName = itemName;
            this.description = description;
            this.passiveDescription = passiveDescription;
        }
    }

    private static readonly ArtifactContent[] Content =
    {
        new ArtifactContent("ART-P1-001", "P1_01_BlindsBoundlessRibbon", "Blind's Boundless Ribbon",
            "A strip of black silk from Blind's blindfold. \"Follow my thread, or lose yourself to the fog.\"",
            "Ultimate: grant other deployed allies Toxic for 10s. Toxic converts their qualifying damage to Darkness. 20s cooldown."),
        new ArtifactContent("ART-P1-002", "P1_02_FiendFleshMask", "Fiend-Flesh Mask",
            "Carved from a freshly slain Fiend, who was once one of the 50 players left behind in the death game.",
            "When this wearer's Particle pulse deals damage, allies gain +3% ATK for 3s, up to 10 stacks. One stack per pulse."),
        new ArtifactContent("ART-P1-003", "P1_03_SoulMerchantsScales", "Soul-Merchant's Scales",
            "Used by the opportunist devil of the Soul Shop. \"Everything has a price here, kid... even memories.\"",
            "Skill: summon a 10s Throne Barrier that follows the wearer. It mitigates damage, pulses 50% ATK damage every 2s, and heals allies for 50% of confirmed pulse damage. 20s cooldown."),
        new ArtifactContent("ART-P1-004", "P1_04_BrokenChronoRelic", "Broken Chrono-Relic",
            "In the Cursed Realm, time is your ultimate executioner. Run faster, kill faster, or forget your name.",
            "Skill damage marks one enemy. The next eligible hit consumes the mark, creates Explosion Splash and Bleed, then grants Claw. 14s mark cooldown."),
        new ArtifactContent("ART-P1-005", "P1_05_AuraResonatingShard", "Aura-Resonating Shard",
            "Clashing Auras from player swaps release shockwaves of raw fighting spirit that Fiends fear.",
            "Switching into the wearer applies Affliction to every living room enemy: -3% DEF and +2% DOT damage taken per stack, up to 10."),
        new ArtifactContent("ART-P1-006", "P1_06_RivalsBrokenCrest", "Rival's Broken Crest",
            "Only 1 of 50 players can return to the living world. If you do not break their crest, they will shatter yours.",
            "Skill damage summons a 5s enemy-following Wrath of Demon. Enemies inside take +10% damage and 200% ATK Additional damage every 2s. 10s cooldown."),
        new ArtifactContent("ART-P1-007", "P1_07_ShroudOfDeprivation", "Shroud of Deprivation",
            "A dark wrap that blocks out sight. \"The horrors of this place are easier to fight when you shut out the terror of seeing them.\"",
            "Switching away from the wearer grants the incoming character +10% ATK, Crit Rate, and Crit Damage for 10s. 20s cooldown."),
        new ArtifactContent("ART-P1-008", "P1_08_MemoryDrainingUrn", "Memory-Draining Urn",
            "An urn catching fragments of forgotten pasts leaked during death and respawn, hardening them into armor.",
            "HP thresholds grant +17% Crit Rate and +26% Crit Damage at 90% HP or below, while a -10% eligible-stat curse remains only at 66% HP or above."),
        new ArtifactContent("ART-P1-009", "P1_09_ShinsRustyDagger", "Shin's Rusty Dagger",
            "Shin's starting blade. Useless against giant Fiends, yet his refusal to let go gave birth to his Aura.",
            "Critical Primary hits build up to 6 Focus (+5% Crit Damage each), then arm a Thunder Strike for the next confirmed Primary hit. 2s cooldown after consumption.")
    };

    [MenuItem("Tools/Project/Artifacts/Create or Update Phase 1 GDD Assets")]
    public static void CreateOrUpdateApprovedAssets()
    {
        EnsureFolder(RootFolder);

        StatRuntimeEffectDefinition statFormula = GetOrCreate<StatRuntimeEffectDefinition>("P1_StatFormula.asset");
        ConfigureFormula(statFormula, "FORMULA-P1-STAT");
        DamageModifierRuntimeEffectDefinition damageFormula =
            GetOrCreate<DamageModifierRuntimeEffectDefinition>("P1_DamageModifierFormula.asset");
        ConfigureFormula(damageFormula, "FORMULA-P1-DAMAGE");

        RuntimeEffectRecipe particleSupport = StatRecipe(statFormula, "P1-A02-ATK", 3f, true, 10,
            Stat(RuntimeEffectStatTarget.Attack, RuntimeEffectStatOperation.Multiply, 1.03f));
        RuntimeEffectRecipe throneMitigation = DamageRecipe(damageFormula, "P1-A03-MITIGATION", 10f, -10f);
        RuntimeEffectRecipe affliction = StatRecipe(statFormula, "P1-A05-AFFLICTION", -1f, true, 10,
            Stat(RuntimeEffectStatTarget.Defense, RuntimeEffectStatOperation.Multiply, 0.97f));
        affliction.childRecipes.Add(DamageRecipe(damageFormula, "P1-A05-DOT-TAKEN", -1f, 2f, DamageDelivery.Dot));
        RuntimeEffectRecipe wrathDamageTaken = DamageRecipe(damageFormula, "P1-A06-DAMAGE-TAKEN", 5f, 10f);
        RuntimeEffectRecipe switchSupport = StatRecipe(statFormula, "P1-A07-SUPPORT", 10f, false, 1,
            Stat(RuntimeEffectStatTarget.Attack, RuntimeEffectStatOperation.Multiply, 1.1f),
            Stat(RuntimeEffectStatTarget.CritRate, RuntimeEffectStatOperation.Add, 10f),
            Stat(RuntimeEffectStatTarget.CritDamage, RuntimeEffectStatOperation.Add, 10f));
        RuntimeEffectRecipe thresholdCurse = StatRecipe(statFormula, "P1-A08-CURSE", -1f, false, 1,
            Stat(RuntimeEffectStatTarget.Attack, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.Defense, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.Speed, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.CritRate, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.CritDamage, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.CooldownMastery, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.AuraEnergyRecharge, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.DotMastery, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.DamageIncrease, RuntimeEffectStatOperation.Multiply, 0.9f),
            Stat(RuntimeEffectStatTarget.AdditionalDamageIncrease, RuntimeEffectStatOperation.Multiply, 0.9f));
        RuntimeEffectRecipe thresholdBenefit = StatRecipe(statFormula, "P1-A08-BENEFIT", -1f, false, 1,
            Stat(RuntimeEffectStatTarget.CritRate, RuntimeEffectStatOperation.Add, 17f),
            Stat(RuntimeEffectStatTarget.CritDamage, RuntimeEffectStatOperation.Add, 26f));
        thresholdBenefit.policy.kind = RuntimeEffectKind.Effect;
        RuntimeEffectRecipe focus = StatRecipe(statFormula, "P1-A09-FOCUS", -1f, true, 5,
            Stat(RuntimeEffectStatTarget.CritDamage, RuntimeEffectStatOperation.Add, 5f));

        RuntimeEffectRecipe markInjuryDebuff = StatRecipe(statFormula, "P1-A04-MARK-INJURY", 10f, false, 1);
        markInjuryDebuff.policy.kind = RuntimeEffectKind.Debuff;
        RuntimeEffectRecipe clawBuff = StatRecipe(statFormula, "P1-A04-CLAW", 5f, false, 1);
        ArtifactAdvancedVisualAssetCreator.Configure(markInjuryDebuff);
        ArtifactAdvancedVisualAssetCreator.Configure(clawBuff);
        DotEffectDefinition markBleed = GetOrCreate<DotEffectDefinition>("P1_Bleed_Mark.asset");
        ConfigureDot(markBleed, "DOT-P1-A04-MARK", 3f);
        DotEffectDefinition clawBleed = GetOrCreate<DotEffectDefinition>("P1_Bleed_Claw.asset");
        ConfigureDot(clawBleed, "DOT-P1-A04-CLAW", 5f);

        InfusionRuntimeEffectDefinition toxicFormula = GetOrCreate<InfusionRuntimeEffectDefinition>("P1_ToxicInfusionFormula.asset");
        toxicFormula.effectId = "P1-A01-TOXIC";
        toxicFormula.displayName = "Toxic Infusion";
        toxicFormula.infusedElementType = ElementType.Darkness;
        RuntimeEffectRecipe toxicInfusion = InfusionRecipe(toxicFormula, "P1-A01-TOXIC", 10f);
        FieldEffectVisualAssetCreator.ConfigureRecipeVisuals(toxicInfusion);
        FieldEffectVisualAssetCreator.ConfigureRecipeVisuals(particleSupport);
        FieldEffectVisualAssetCreator.ConfigureRecipeVisuals(affliction);
        FieldEffectVisualAssetCreator.ConfigureRecipeVisuals(switchSupport);

        ExplosionSplashDefinition markExplosion = GetOrCreate<ExplosionSplashDefinition>("P1_A04_MarkExplosion.asset");
        markExplosion.stableId = "SPLASH-P1-A04";
        markExplosion.radius = 2f;
        markExplosion.verticalRadiusMultiplier = 1f;
        markExplosion.centerOffset = Vector2.zero;
        markExplosion.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldEffectVisualAssetCreator.Root + "/ChronoBloodExplosion.prefab");

        ParticleBehaviorDefinition throne = GetOrCreate<ParticleBehaviorDefinition>("P1_A03_ThroneBarrier.asset");
        ConfigureParticle(throne, "PARTICLE-P1-A03", ParticleAnchorPolicy.FollowWearer, 10f, 2f,
            new ParticleRuntimeEffectDefinition { target = ArtifactResponseTarget.Wearer, recipe = throneMitigation },
            new ParticleDamageEffectDefinition { damageBasis = ArtifactDamageBasis.SourceAttack, ratio = 0.5f },
            new ParticleHealFromDamageEffectDefinition { target = ArtifactResponseTarget.Allies, confirmedDamageRatio = 0.5f });
        throne.runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldEffectVisualAssetCreator.Root + "/ScalesHexShield.prefab");
        throne.volume.radius = 2f;
        throne.volume.verticalRadiusMultiplier = 1f;
        throne.volume.centerOffset = Vector2.zero;
        throne.volume.anchorToGround = false;

        ParticleBehaviorDefinition wrath = GetOrCreate<ParticleBehaviorDefinition>("P1_A06_WrathOfDemon.asset");
        ConfigureParticle(wrath, "PARTICLE-P1-A06", ParticleAnchorPolicy.FollowTarget, 5f, 2f,
            new ParticleRuntimeEffectDefinition { target = ArtifactResponseTarget.ParticleTargets, recipe = wrathDamageTaken },
            new ParticleDamageEffectDefinition { damageBasis = ArtifactDamageBasis.SourceAttack, ratio = 2f });
        wrath.runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldEffectVisualAssetCreator.Root + "/CrestDarkFlameMark.prefab");

        ParticleBehaviorDefinition thunder = GetOrCreate<ParticleBehaviorDefinition>("P1_A09_ThunderStrike.asset");
        ConfigureParticle(thunder, "PARTICLE-P1-A09", ParticleAnchorPolicy.WorldPosition, 0.05f, 0.05f,
            new ParticleDamageEffectDefinition
            {
                damageBasis = ArtifactDamageBasis.SourceAttack,
                ratio = 2.5f,
                critDamageScalingRatio = 0.3f,
                descriptor = new DamageDescriptor(DamageDelivery.Additional, ElementType.Lightning,
                    DamageCriticalPolicy.Prevent)
            });

        thunder.runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FieldEffectVisualAssetCreator.Root + "/DaggerThunderStrike.prefab");
        ArtifactAdvancedVisualAssetCreator.ConfigureGroundAnchor(thunder);

        for (int index = 0; index < Content.Length; index++)
        {
            ArtifactContent content = Content[index];
            ArtifactPassiveSetDefinition passiveSet = GetOrCreate<ArtifactPassiveSetDefinition>(
                content.fileName + "_PassiveSet.asset");
            passiveSet.stableId = content.id + "-PASSIVES";
            passiveSet.rules = BuildRules(index, markBleed, clawBleed, markExplosion, throne, wrath, thunder,
                particleSupport, affliction, switchSupport, thresholdCurse, thresholdBenefit, focus, clawBuff, markInjuryDebuff, toxicInfusion);

            ArtifactDefinition artifact = GetOrCreate<ArtifactDefinition>(content.fileName + ".asset");
            artifact.id = content.id;
            artifact.itemName = content.itemName;
            artifact.description = content.description;
            artifact.passiveDescription = content.passiveDescription;
            artifact.isEquippable = true;
            artifact.passiveSet = passiveSet;
            EditorUtility.SetDirty(passiveSet);
            EditorUtility.SetDirty(artifact);
        }

        EditorUtility.SetDirty(toxicFormula);
        EditorUtility.SetDirty(markBleed);
        EditorUtility.SetDirty(clawBleed);
        EditorUtility.SetDirty(markExplosion);
        EditorUtility.SetDirty(throne);
        EditorUtility.SetDirty(wrath);
        EditorUtility.SetDirty(thunder);
        EditorUtility.SetDirty(statFormula);
        EditorUtility.SetDirty(damageFormula);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Artifact Phase 1 assets created/updated successfully.");
    }

    private static List<ArtifactPassiveRuleDefinition> BuildRules(int index,
        DotEffectDefinition markBleed, DotEffectDefinition clawBleed,
        ExplosionSplashDefinition markExplosion, ParticleBehaviorDefinition throne,
        ParticleBehaviorDefinition wrath, ParticleBehaviorDefinition thunder,
        RuntimeEffectRecipe particleSupport, RuntimeEffectRecipe affliction,
        RuntimeEffectRecipe switchSupport, RuntimeEffectRecipe thresholdCurse,
        RuntimeEffectRecipe thresholdBenefit, RuntimeEffectRecipe focus,
        RuntimeEffectRecipe clawBuff, RuntimeEffectRecipe markInjuryDebuff,
        RuntimeEffectRecipe toxicInfusion)
    {
        switch (index)
        {
            case 0:
                return Rules(Rule("toxic-on-ultimate", ArtifactPassiveTrigger.ActionStarted,
                    Filter(AttackCategory.Ultimate), Conditions(20f, 10f),
                    new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.Teammates, recipe = toxicInfusion },
                    Cooldown()));
            case 1:
                return Rules(Rule("particle-support-stack", ArtifactPassiveTrigger.DamageResolved,
                    DamageFilter(CombatDamageTag.Particle), OncePerRootConditions(3f, 10),
                    new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.Allies, recipe = particleSupport },
                    new ModifyArtifactStateResponseDefinition { target = ArtifactResponseTarget.Allies, operation = ArtifactStateOperation.AddStacks }));
            case 2:
                return Rules(Rule("throne-on-skill", ArtifactPassiveTrigger.ActionStarted,
                    Filter(AttackCategory.Skill), Conditions(20f, 10f, 2f),
                    new SpawnParticleResponseDefinition { target = ArtifactResponseTarget.Wearer, definition = throne }, Cooldown()));
            case 3:
                return Rules(
                    Rule("mark-on-skill-hit", ArtifactPassiveTrigger.DamageResolved,
                        DamageFilter(CombatDamageTag.Particle | CombatDamageTag.Explosion | CombatDamageTag.DotTick, AttackCategory.Skill, forbidden: true), Conditions(14f, 10f),
                        new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, recipe = markInjuryDebuff },
                        new ModifyArtifactStateResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, operation = ArtifactStateOperation.Arm }, Cooldown()),
                    Rule("consume-mark", ArtifactPassiveTrigger.DamageResolved,
                        DamageFilter(CombatDamageTag.Artifact | CombatDamageTag.Particle | CombatDamageTag.Explosion | CombatDamageTag.Splash | CombatDamageTag.DotApplication | CombatDamageTag.DotTick, forbidden: true, requireWearer: false), StoredTargetArmedConditions(),
                        new ExplosionSplashResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, definition = markExplosion, damageBasis = ArtifactDamageBasis.ConfirmedDamage, damageRatio = 0.5f, elementType = ElementType.Bleed, preserveParentElement = false },
                        new ApplyDotResponseDefinition { target = ArtifactResponseTarget.SplashTargets, definition = markBleed, damageBasis = ArtifactDamageBasis.SourceAttack, ratio = 0.3f },
                        new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.TriggerSource, recipe = clawBuff },
                        new ModifyArtifactStateResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, operation = ArtifactStateOperation.ConsumeMark }),
                    Rule("claw-bleed", ArtifactPassiveTrigger.DamageResolved,
                        DamageFilter(CombatDamageTag.Artifact | CombatDamageTag.Particle | CombatDamageTag.Explosion | CombatDamageTag.Splash | CombatDamageTag.DotApplication | CombatDamageTag.DotTick, forbidden: true, requireWearer: false), Conditions(),
                        new ApplyDotResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, definition = clawBleed, ratio = 0.3f }));
            case 4:
                return Rules(Rule("affliction-on-switch-in", ArtifactPassiveTrigger.CharacterSwitchCompleted,
                    new CombatEventFilter { switchRelation = ArtifactSwitchRelation.IncomingIsWearer }, Conditions(maximumStacks: 10),
                    new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.RoomEnemies, recipe = affliction, applications = 1 }));
            case 5:
                return Rules(Rule("wrath-on-skill-hit", ArtifactPassiveTrigger.DamageResolved,
                    DamageFilter(CombatDamageTag.Particle | CombatDamageTag.Explosion | CombatDamageTag.DotTick, AttackCategory.Skill, forbidden: true), Conditions(10f, 5f, 2f),
                    new SpawnParticleResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, definition = wrath }, Cooldown()));
            case 6:
                return Rules(Rule("support-on-switch-out", ArtifactPassiveTrigger.CharacterSwitchCompleted,
                    new CombatEventFilter { switchRelation = ArtifactSwitchRelation.OutgoingIsWearer }, Conditions(20f, 10f),
                    new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.IncomingCharacter, recipe = switchSupport }, Cooldown()));
            case 7:
                return Rules(
                    HealthRule("curse-above-90", 0.9f, 1f, thresholdCurse),
                    HealthRule("benefit-and-curse-66-to-90", 0.66f, 0.9f, thresholdBenefit, thresholdCurse),
                    HealthRule("benefit-below-66", 0f, 0.66f, thresholdBenefit));
            default:
                return Rules(
                    Rule("focus-on-critical", ArtifactPassiveTrigger.CriticalHitResolved,
                        CriticalPrimaryFilter(), DisarmedStackConditions(5),
                        new ApplyRuntimeEffectResponseDefinition { target = ArtifactResponseTarget.Wearer, recipe = focus },
                        new ModifyArtifactStateResponseDefinition { operation = ArtifactStateOperation.AddStacks }),
                    Rule("thunder-on-primary", ArtifactPassiveTrigger.DamageResolved,
                        PrimaryFilter(), ArmedConditions(0f),
                        new SpawnParticleResponseDefinition { target = ArtifactResponseTarget.TriggerTarget, definition = thunder },
                        new ModifyArtifactStateResponseDefinition { operation = ArtifactStateOperation.Disarm }));
        }
    }

    private static ArtifactPassiveRuleDefinition HealthRule(string id, float minimum, float maximum,
        params RuntimeEffectRecipe[] recipes)
    {
        ArtifactPassiveConditionSet conditions = Conditions();
        conditions.minimumHealthRatio = minimum;
        conditions.maximumHealthRatio = maximum;
        conditions.minimumHealthExclusive = minimum >= 0.9f;
        ArtifactPassiveResponseDefinition[] responses = new ArtifactPassiveResponseDefinition[recipes.Length];
        for (int i = 0; i < recipes.Length; i++)
        {
            responses[i] = new ApplyRuntimeEffectResponseDefinition
            {
                target = ArtifactResponseTarget.Wearer,
                recipe = recipes[i]
            };
        }
        return Rule(id, ArtifactPassiveTrigger.CharacterHealthChanged, new CombatEventFilter(), conditions, responses);
    }

    private static ArtifactPassiveRuleDefinition Rule(string id, ArtifactPassiveTrigger trigger,
        CombatEventFilter filter, ArtifactPassiveConditionSet conditions,
        params ArtifactPassiveResponseDefinition[] responses)
    {
        return new ArtifactPassiveRuleDefinition
        {
            ruleId = id,
            stateKey = ResolveStateKey(id),
            trigger = trigger,
            filter = filter,
            conditions = conditions,
            responses = new List<ArtifactPassiveResponseDefinition>(responses),
            feedback = new ArtifactPassiveFeedbackDefinition { feedbackId = id }
        };
    }

    private static List<ArtifactPassiveRuleDefinition> Rules(params ArtifactPassiveRuleDefinition[] rules)
        => new List<ArtifactPassiveRuleDefinition>(rules);

    private static string ResolveStateKey(string ruleId)
    {
        if (ruleId == "focus-on-critical" || ruleId == "thunder-on-primary") return "focus-thunder";
        if (ruleId == "mark-on-skill-hit" || ruleId == "consume-mark") return "mark-injury";
        return ruleId;
    }

    private static CombatEventFilter Filter(AttackCategory category) => new CombatEventFilter
    {
        filterByCategory = true,
        category = category,
        requirePositiveDamage = false
    };

    private static CombatEventFilter DamageFilter(CombatDamageTag tags,
        AttackCategory? category = null, bool forbidden = false, bool requireWearer = true)
    {
        return new CombatEventFilter
        {
            filterByCategory = category.HasValue,
            category = category ?? AttackCategory.Normal,
            requiredDamageTags = forbidden ? CombatDamageTag.None : tags,
            forbiddenDamageTags = forbidden ? tags : CombatDamageTag.None,
            requirePositiveDamage = true,
            requireWearerAsSource = requireWearer
        };
    }

    private static CombatEventFilter PrimaryFilter() => new CombatEventFilter
    {
        filterByDelivery = true,
        delivery = DamageDelivery.Primary,
        forbiddenDamageTags = CombatDamageTag.DotTick | CombatDamageTag.Explosion,
        requirePositiveDamage = true
    };

    private static CombatEventFilter CriticalPrimaryFilter()
    {
        CombatEventFilter filter = PrimaryFilter();
        filter.requireCriticalHit = true;
        return filter;
    }

    private static ArtifactPassiveConditionSet Conditions(float cooldown = 0f, float duration = 0f,
        float interval = 0f, float chance = 1f, int maximumStacks = 1)
    {
        return new ArtifactPassiveConditionSet
        {
            cooldownSeconds = cooldown,
            durationSeconds = duration,
            intervalSeconds = interval,
            chance = chance,
            maximumStacks = maximumStacks
        };
    }

    private static ArtifactPassiveConditionSet ArmedConditions(float cooldown)
    {
        ArtifactPassiveConditionSet conditions = Conditions(cooldown);
        conditions.requireArmed = true;
        return conditions;
    }

    private static ArtifactPassiveConditionSet DisarmedStackConditions(int maximumStacks)
    {
        ArtifactPassiveConditionSet conditions = Conditions(maximumStacks: maximumStacks);
        conditions.requireDisarmed = true;
        return conditions;
    }

    private static ArtifactPassiveConditionSet OncePerRootConditions(float duration, int maximumStacks)
    {
        ArtifactPassiveConditionSet conditions = Conditions(duration: duration, maximumStacks: maximumStacks);
        conditions.oncePerRootDamageEvent = true;
        return conditions;
    }

    private static ArtifactPassiveConditionSet StoredTargetArmedConditions()
    {
        ArtifactPassiveConditionSet conditions = ArmedConditions(0f);
        conditions.requireStoredTarget = true;
        return conditions;
    }

    private static ModifyArtifactStateResponseDefinition Cooldown() =>
        new ModifyArtifactStateResponseDefinition { operation = ArtifactStateOperation.StartCooldown };

    private static void ConfigureDot(DotEffectDefinition definition, string id, float duration)
    {
        definition.stableId = id;
        definition.displayName = "Bleed";
        definition.elementType = ElementType.Bleed;
        definition.criticalPolicy = DamageCriticalPolicy.Prevent;
        definition.durationSeconds = duration;
        definition.tickIntervalSeconds = 1f;
        definition.stackingPolicy = DotStackingPolicy.IndependentBySource;
    }

    private static void ConfigureParticle(ParticleBehaviorDefinition definition, string id,
        ParticleAnchorPolicy anchor, float lifetime, float interval,
        params ParticleEffectDefinition[] effects)
    {
        definition.stableId = id;
        definition.anchorPolicy = anchor;
        definition.lifetimeSeconds = lifetime;
        definition.pulseIntervalSeconds = interval;
        definition.effects = new List<ParticleEffectDefinition>(effects);
    }

    private static void ConfigureFormula(RuntimeEffectDefinition definition, string id)
    {
        definition.effectId = id;
        definition.displayName = id;
        definition.durationType = RuntimeEffectDurationType.Seconds;
        definition.durationSeconds = 10f;
        definition.refreshDurationOnReapply = true;
    }

    private static RuntimeEffectRecipe StatRecipe(StatRuntimeEffectDefinition definition, string id,
        float duration, bool stackable, int stackLimit, params RuntimeEffectStatModifier[] modifiers)
    {
        return new RuntimeEffectRecipe
        {
            recipeId = id,
            definition = definition,
            presentation = new RuntimeEffectPresentation { effectId = id, displayName = id },
            policy = new RuntimeEffectApplicationPolicy
            {
                durationType = duration < 0f ? RuntimeEffectDurationType.Forever : RuntimeEffectDurationType.Seconds,
                durationSeconds = Mathf.Max(0f, duration),
                stackable = stackable,
                stackMode = stackable ? RuntimeEffectStackMode.Stack : RuntimeEffectStackMode.Override,
                stackLimit = stackLimit,
                refreshDurationOnReapply = true,
                stackKey = id
            },
            parameters = new RuntimeEffectParameterSet
            {
                overrideStatModifiers = true,
                statModifiers = new List<RuntimeEffectStatModifier>(modifiers)
            }
        };
    }

    private static RuntimeEffectRecipe DamageRecipe(DamageModifierRuntimeEffectDefinition definition,
        string id, float duration, float incomingDamagePercentAdd, DamageDelivery? delivery = null)
    {
        RuntimeEffectDamageFilter filter = new RuntimeEffectDamageFilter();
        if (delivery.HasValue)
        {
            filter.filterByDelivery = true;
            filter.delivery = delivery.Value;
        }

        return new RuntimeEffectRecipe
        {
            recipeId = id,
            definition = definition,
            presentation = new RuntimeEffectPresentation { effectId = id, displayName = id },
            policy = new RuntimeEffectApplicationPolicy
            {
                durationType = duration < 0f ? RuntimeEffectDurationType.Forever : RuntimeEffectDurationType.Seconds,
                durationSeconds = Mathf.Max(0f, duration),
                stackKey = id
            },
            parameters = new RuntimeEffectParameterSet
            {
                overrideDamageModifier = true,
                damageModifier = new RuntimeEffectDamageModifierParameters
                {
                    filter = filter,
                    incomingDamageTakenPercentAdd = incomingDamagePercentAdd
                }
            }
        };
    }

    private static RuntimeEffectRecipe InfusionRecipe(InfusionRuntimeEffectDefinition definition,
        string id, float duration)
    {
        return new RuntimeEffectRecipe
        {
            recipeId = id,
            definition = definition,
            presentation = new RuntimeEffectPresentation { effectId = id, displayName = "Toxic Infusion" },
            policy = new RuntimeEffectApplicationPolicy
            {
                durationType = duration < 0f ? RuntimeEffectDurationType.Forever : RuntimeEffectDurationType.Seconds,
                durationSeconds = Mathf.Max(0f, duration),
                stackable = false,
                stackMode = RuntimeEffectStackMode.Override,
                stackLimit = 1,
                refreshDurationOnReapply = true,
                stackKey = id
            },
            parameters = new RuntimeEffectParameterSet()
        };
    }

    private static RuntimeEffectStatModifier Stat(RuntimeEffectStatTarget target,
        RuntimeEffectStatOperation operation, float value)
    {
        return new RuntimeEffectStatModifier { target = target, operation = operation, value = value };
    }

    private static T GetOrCreate<T>(string fileName) where T : ScriptableObject
    {
        string path = RootFolder + "/" + fileName;
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string fullPath)
    {
        string[] segments = fullPath.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }
            current = next;
        }
    }
}
