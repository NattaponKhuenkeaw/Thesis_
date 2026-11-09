#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Progression;
using Project.ShopSystem;
using Project.InventorySystem.Items;
using UnityEditor;
using UnityEngine;

public class ShadowMeterTests
{
    private class ComboProbe : PlayerAttackState
    {
        public ComboProbe(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }
        public void OpenRecovery() => currentPhase = ActionPhase.Recovery;
    }
    private CharacterObject character;
    private PlayerMetaProgression progression;
    private ActionResolverTable table;
    private GameObject owner;
    private CharacterPassiveController passive;
    private PlayerRunInventory testInventory;
    private GameObject teamHost;
    private CharacterObject testAlly;
    private CharacterItem testMainItem;
    private CharacterItem testAllyItem;

    [SetUp]
    public void SetUp()
    {
        character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset"));
        table = Object.Instantiate(character.actionResolverTable);
        character.actionResolverTable = table;
        progression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        owner = new GameObject("ShadowMeterTest");
        owner.SetActive(false);
        PlayerCharacter player = owner.AddComponent<PlayerCharacter>();
        player.characterObject = character;
        passive = owner.AddComponent<CharacterPassiveController>();
        passive.playerCharacter = player;
        player.PassiveController = passive;
        passive.SetMetaProgression(progression);
    }

    [TearDown]
    public void TearDown()
    {
        if (teamHost != null) Object.DestroyImmediate(teamHost);
        if (testInventory != null) Object.DestroyImmediate(testInventory);
        if (testAlly != null) Object.DestroyImmediate(testAlly);
        if (testMainItem != null) Object.DestroyImmediate(testMainItem);
        if (testAllyItem != null) Object.DestroyImmediate(testAllyItem);
        Object.DestroyImmediate(owner);
        Object.DestroyImmediate(character);
        Object.DestroyImmediate(table);
        Object.DestroyImmediate(progression);
    }

    private PlayerRunInventory CreateIsolatedTeamInventory()
    {
        testInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
        testInventory.ownedItems.Clear();
        testMainItem = Object.Instantiate(Resources.Load<CharacterItem>("ItemSO/char_0_Main"));
        testMainItem.characterObjectData = character;
        testInventory.ownedItems.Add(testMainItem);
        testAlly = ScriptableObject.CreateInstance<CharacterObject>();
        testAlly.characterId = "shadow-potential-test-ally";
        testAlly.stats = new CharacterStats { maxHP = 100f, critRate = 5f };
        testAllyItem = ScriptableObject.CreateInstance<CharacterItem>();
        testAllyItem.id = "shadow-potential-test-ally-item";
        testAllyItem.characterObjectData = testAlly;
        testInventory.ownedItems.Add(testAllyItem);
        testInventory.DeployTeam(new[] { character, testAlly }, 0);
        Assert.That(testInventory.GetTeamSnapshot(), Does.Contain(character));
        teamHost = new GameObject("Shadow Potential Isolated Team");
        TeamLoader team = teamHost.AddComponent<TeamLoader>();
        team.playerCharacter = owner.GetComponent<PlayerCharacter>();
        typeof(TeamLoader).GetField("runInventory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(team, testInventory);
        return testInventory;
    }

    [Test]
    public void WhiffsAndIncomingDamageDoNotBuildMeter()
    {
        passive.NotifyActionStarted(AttackCategory.Normal, "Ground", new AttackStepData { stepName = "Attack3" });
        passive.NotifyDamageTaken(10f);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.Zero);
        Assert.That(character.kitDefinition.basePassiveNode.resourceEffects, Is.Empty,
            "The old third-hit heal and AE effects must be removed.");
    }

    [TestCase(AttackCategory.Normal, "Attack1", 2)]
    [TestCase(AttackCategory.Normal, "Attack3", 4)]
    [TestCase(AttackCategory.Normal, "Attack5", 1)]
    [TestCase(AttackCategory.Skill, "Dark Chain", 4)]
    [TestCase(AttackCategory.Ultimate, "Ult", 0)]
    [TestCase(AttackCategory.Normal, "DimensionCut", 0)]
    public void HitGainComesFromAuthoredRules(AttackCategory category, string step, int expected)
    {
        string actionId = step == "DimensionCut" ? "main.dash.dimension_cut"
            : category == AttackCategory.Skill ? "main.skill.dark_chain"
            : category == AttackCategory.Ultimate ? "test.unknown_ultimate"
            : step == "Attack5" ? "char-0-main.basic.0" : "char-0-main.basic.1";
        passive.NotifyHitDealt(category, actionId, "Ground", new AttackStepData { stepName = step }, null, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(expected));
    }

    [Test]
    public void MeterCapsWithoutBankingOverflowAndSpendsWholeBox()
    {
        for (int i = 0; i < 20; i++)
            passive.NotifyHitDealt(AttackCategory.Normal, "char-0-main.basic.1", "Ground", new AttackStepData { stepName = "Attack3" }, null, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(40));
        Assert.That(passive.RuntimeState.ConsumeCounter("main.shadow", 20), Is.True);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(20));
        passive.RuntimeState.SetCounter("main.shadow", 19);
        Assert.That(passive.RuntimeState.ConsumeCounter("main.shadow", 20), Is.False);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(19));
    }

    [TestCase(0, 2)]
    [TestCase(1, 3)]
    [TestCase(2, 4)]
    [TestCase(3, 4)]
    [TestCase(4, 5)]
    public void CapacityUsesUnlockedPotentialNodes(int level, int expected)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: level);
        Assert.That(passive.GetMeterBoxCount(), Is.EqualTo(expected));
    }

    [TestCase(0, "main.dash.dimension_cut", 0.1f, false, 50f)]
    [TestCase(1, "main.dash.dimension_cut", 0.1f, false, 50f)]
    [TestCase(2, "main.dash.dimension_cut", 0.1f, true, 150f)]
    [TestCase(2, "main.dash.dimension_cut", 0.2f, false, 50f)]
    [TestCase(2, "other.normal.action", 0.1f, false, 50f)]
    public void CutCriticalBonusAndDefenseBypassAreScopedPerHit(int level, string actionId,
        float roll, bool critical, float damage)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: level);
        var stats = new CharacterStats { critRate = 0f, critDamage = 50f };
        var request = new DamageRequest
        {
            sourceCharacter = character, sourcePassiveController = passive, sourceStats = stats,
            category = AttackCategory.Normal, actionId = actionId,
            descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            baseDamage = 100f, defenderDefense = 100f, hasCriticalRoll = true, criticalRoll01 = roll
        };
        DamageResult result = CombatDamageProcessor.Resolve(request);
        Assert.That(result.IsCriticalHit, Is.EqualTo(critical));
        Assert.That(result.Damage, Is.EqualTo(damage).Within(0.01f));
        Assert.That(stats.critRate, Is.Zero, "The action bonus must not mutate shared character stats.");
        request.actionId = "ordinary.attack";
        Assert.That(CombatDamageProcessor.Resolve(request).Damage, Is.EqualTo(50f));
    }

    [Test]
    public void CriticalCutStillRespectsResistanceAndTypeReduction()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: 2);
        DamageResult result = CombatDamageProcessor.Resolve(new DamageRequest
        {
            sourceCharacter = character, sourcePassiveController = passive,
            sourceStats = new CharacterStats { critRate = 0f, critDamage = 50f },
            category = AttackCategory.Normal, actionId = "main.dash.dimension_cut",
            descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            baseDamage = 100f, defenderDefense = 100f, defenderDamageResistance = 20f,
            typeReductionPercent = 25f, hasCriticalRoll = true, criticalRoll01 = 0.1f
        });
        Assert.That(result.Damage, Is.EqualTo(90f).Within(0.01f));
    }

    [TestCase(2, "main.ultimate.execution_breakdown", false)]
    [TestCase(3, "main.ultimate.execution_breakdown", true)]
    [TestCase(3, "main.ultimate.jailbreak_punisher", false)]
    public void ExecutionRestoresMaxHpAtPassiveThree(int level, string actionId, bool heals)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: level);
        var player = owner.GetComponent<PlayerCharacter>();
        player.InitializeHP(20f);
        float previous = player.CurrentHP;
        passive.NotifyActionEnded(AttackCategory.Ultimate, actionId, "Execution Breakdown", new AttackStepData());
        Assert.That(player.CurrentHP, Is.EqualTo(previous + (heals ? .08f * player.MaxHP : 0f)).Within(.01f));
    }

    [TestCase("ProjectileData_main_void_area")]
    [TestCase("ProjectileData_main_void_area_lv3")]
    [TestCase("ProjectileData_main_void_area_lv4")]
    public void VoidOccupancyDoublesGainAndExpiresWithoutChangingDashCost(string asset)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: 4);
        PlayerRunInventory inventory = CreateIsolatedTeamInventory();
        ProjectileData area = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            "Assets/Project/Character/Hero/test_0_Main/" + asset + ".asset");
        RuntimeAreaEffectApplication application = area.areaEffects.Single();
        Assert.That(application.requiredNodeId, Is.EqualTo("main.passive.lv4"));
        passive.NotifyHitDealt(AttackCategory.Normal, "char-0-main.basic.1", "Ground", new AttackStepData { stepName = "Attack3" }, null, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(4));
        inventory.ApplyRuntimeEffect(character, application.recipe, character, character.stats);
        passive.NotifyHitDealt(AttackCategory.Normal, "char-0-main.basic.1", "Ground", new AttackStepData { stepName = "Attack3" }, null, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(12));
        passive.RuntimeState.SetCounter("main.shadow", 20);
        passive.NotifyAfterDash();
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.Zero);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(1));
        inventory.TickRuntimeEffects(0.31f);
        passive.NotifyHitDealt(AttackCategory.Normal, "char-0-main.basic.1", "Ground", new AttackStepData { stepName = "Attack3" }, null, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(4));
    }

    [Test]
    public void SkillThreeVulnerabilityReplacesTwentyInsteadOfStackingToSixty()
    {
        var receiver = owner.AddComponent<RuntimeEffectReceiver>();
        CharacterMemoryTests.SetMemoryRanks(progression, character, skill: 1);
        var baseRecipe = ResolveMemoryProjectile("skill", "main.skill.dark_chain", "Dark Chain").projectileData.onHitEffects[0].recipe;
        CharacterMemoryTests.SetMemoryRanks(progression, character, skill: 3);
        var enhanced = ResolveMemoryProjectile("skill", "main.skill.dark_chain", "Dark Chain").projectileData.onHitEffects[0].recipe;
        Assert.That(baseRecipe.presentation.fieldVisualPrefab, Is.Not.Null);
        receiver.ApplyEffect(baseRecipe, character, character.stats);
        Assert.That(receiver.ModifyIncomingDamage(new RuntimeEffectDamageContext(), 100f), Is.EqualTo(120f).Within(.01f));
        receiver.ApplyEffect(enhanced, character, character.stats);
        receiver.ApplyEffect(enhanced, character, character.stats);
        Assert.That(receiver.ModifyIncomingDamage(new RuntimeEffectDamageContext(), 100f), Is.EqualTo(140f).Within(.01f));
        Assert.That(receiver.ActiveEffects.Count, Is.EqualTo(1));
        Assert.That(receiver.ActiveEffects.Single().remainingSeconds, Is.EqualTo(10f));
    }

    [Test]
    public void SkillFiveBuffSnapshotsMaxHpForAllThreeAllyStats()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, skill: 5);
        RuntimeEffectRecipe recipe = passive.GetActiveNodes().Single(n => n.nodeId == "main.memory.skill.lv5").runtimeEffects.Single().recipe;
        Assert.That(recipe.policy.appliesOn, Is.EqualTo(RuntimeEffectApplyTarget.Allies));
        Assert.That(recipe.policy.durationSeconds, Is.EqualTo(10f));
        var receiver = owner.AddComponent<RuntimeEffectReceiver>();
        receiver.ApplyEffect(recipe, character, new CharacterStats { maxHP = 600f });
        receiver.ApplyEffect(recipe, character, new CharacterStats { maxHP = 1200f });
        var instance = receiver.ActiveEffects.Single();
        var stats = new CharacterStats();
        instance.definition.ModifyStats(new RuntimeEffectContext { instance = instance, sourceStats = instance.sourceStatsSnapshot }, stats);
        Assert.That(stats.dotMastery, Is.EqualTo(10f).Within(.01f));
        Assert.That(stats.additionalDamageIncrease, Is.EqualTo(10f).Within(.01f));
        Assert.That(stats.damageIncrease, Is.EqualTo(10f).Within(.01f));
    }

    [TestCase(0, 0f, 16f)]
    [TestCase(1, 0f, 16f)]
    [TestCase(2, 1.5f, 16f)]
    [TestCase(3, 3f, 16f)]
    [TestCase(6, 3f, 10f)]
    public void SkillMemoryAppliesMagnetismAndCooldown(int level, float pull, float cooldown)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, skill: level);
        ProjectileSpawnContext chain = ResolveMemoryProjectile("skill", "main.skill.dark_chain", "Dark Chain");
        Assert.That(chain.projectileData.magnetismSpeed, Is.EqualTo(pull));
        Assert.That(passive.ModifyCooldown(AttackCategory.Skill, "main.skill.dark_chain", "Dark Chain", 16f), Is.EqualTo(cooldown));
    }

    [TestCase(0, 0)] [TestCase(4, 0)] [TestCase(4, 1)] [TestCase(6, 6)]
    public void MemoryUpgradesKeepGroundAndAirNormalAttacksAvailable(int skill, int passiveRank)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, skill: skill, passive: passiveRank);
        foreach (var action in table.comboActions)
        {
            Assert.That(passive.IsActionUnlocked(AttackCategory.Normal, action.actionId, action.actionName), Is.True,
                $"{action.actionName}, Skill {skill}, Passive {passiveRank}");
            var resolver = new ActionResolver();
            var ctx = new InputContext { isGrounded = action.context == CombatActionContext.Ground, passiveController = passive };
            AttackStepData step = resolver.ResolveInitial(ctx, table);
            Assert.That(step, Is.Not.Null);
            if (action.context == CombatActionContext.Ground)
            {
                int stages = 1;
                while (stages < 8 && resolver.ResolveNext(ctx) != null) stages++;
                Assert.That(stages, Is.EqualTo(passiveRank >= 1 ? 4 : 3),
                    "Passive Memory Lv1 should add stage 4 without locking base attacks.");
                if (passiveRank >= 1)
                {
                    Assert.That(resolver.ResolveTriggers(ctx, 0), Is.Null, "Stage 5 requires a captured enemy.");
                    ctx.hasGrabbedEnemy = true;
                    Assert.That(resolver.ResolveTriggers(ctx, 0)?.stepName, Is.EqualTo("Attack5"));
                }
            }
        }
    }

    [TestCase(1, 20f, 2f, 0f, 0f)]
    [TestCase(2, 20f, 2f, 0f, 0f)]
    [TestCase(3, 20f, 2f, 0f, 5f)]
    [TestCase(4, 20f, 5f, 0f, 5f)]
    [TestCase(5, 40f, 5f, 0f, 5f)]
    [TestCase(6, 40f, 10f, 0f, 5f)]
    public void VoidMemoryResolvesEachRank(int rank, float duration, float attack, float hp, float energy)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, ultimate: rank);
        ProjectileSpawnContext area = ResolveMemoryProjectile("void_area", "main.ultimate.jailbreak_punisher", "Void Area");
        Assert.That(area.projectileData.lifetime, Is.EqualTo(duration));
        Assert.That(area.projectileData.hitIntervalSeconds, Is.EqualTo(5f));
        Assert.That(area.projectileData.ResolveRoutineHitbox().elementType,
            Is.EqualTo(rank >= 4 ? ElementType.None : ElementType.Void));
        var scaling = ((NormalHitbox)area.projectileData.ResolveRoutineHitbox()).damageScaling;
        Assert.That(scaling.Single(t => t.stat == CombatStatId.Attack).ratio, Is.EqualTo(attack));
        Assert.That(scaling.FirstOrDefault(t => t.stat == CombatStatId.MaxHP)?.ratio ?? 0f, Is.EqualTo(hp));
        if (rank >= 4)
        {
            RuntimeAreaEffectApplication damageApplication = area.projectileData.onHitEffects
                .Single(application => application.triggerOnHit);
            RuntimeEffectRecipe damageRecipe = damageApplication.ResolvedRecipe;
            Assert.That(damageRecipe.parameters.additionalDamage.sourceStatRatio, Is.EqualTo(rank >= 6 ? .12f : .06f));
            Assert.That(damageRecipe.parameters.additionalDamage.elementType, Is.EqualTo(ElementType.Darkness));
            Assert.That(damageRecipe.parameters.additionalDamage.filter.requiredDamageTags, Is.EqualTo(CombatDamageTag.VoidArea));
            PotentialNodeDefinition damageNode = character.kitDefinition.potentialNodes
                .Single(node => node.nodeId == damageApplication.requiredNodeId);
            Assert.That(damageNode.track, Is.EqualTo(PotentialTrack.Ultimate));
            Assert.That(damageNode.level, Is.EqualTo(rank >= 6 ? 6 : 4));
        }
        Assert.That(passive.IsNodeUnlocked(area.projectileData.auraEnergyRequiredNodeId) ? area.projectileData.auraEnergyPerDamageBatch : 0f, Is.EqualTo(energy));
        var hit = area.projectileData.onHitEffects.Single(application => !application.triggerOnHit);
        var vulnerability = hit.recipe;
        if (rank == 1) Assert.That(passive.IsNodeUnlocked(hit.requiredNodeId), Is.False);
        else
        {
            Assert.That(vulnerability.policy.durationSeconds, Is.EqualTo(7f));
            Assert.That(vulnerability.parameters.damageModifier.incomingDamageTakenPercentAdd, Is.EqualTo(15f));
        }
    }

    [Test]
    public void PassiveFiveAndUltimateFiveUpgradeJudgmentAndAddOneUltimateHealthBuff()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: 5);
        Assert.That(passive.ModifyActionDamageMultiplier(AttackCategory.Skill,
            "main.skill.dimension_slash", "Judgment Cut", 6.5f), Is.EqualTo(8.5f));
        CharacterMemoryTests.SetMemoryRanks(progression, character, ultimate: 5);
        var effect = passive.GetActiveNodes().Single(n => n.track == PotentialTrack.Ultimate && n.level == 5).runtimeEffects.Single();
        Assert.That(effect.filter.Matches(new PassiveEventContext { category = AttackCategory.Ultimate }), Is.True);
        Assert.That(effect.filter.Matches(new PassiveEventContext { category = AttackCategory.Ultimate, isAutoChainStep = true }), Is.False);
        var receiver = owner.AddComponent<RuntimeEffectReceiver>();
        receiver.ApplyEffect(effect.recipe, character, character.stats);
        receiver.ApplyEffect(effect.recipe, character, character.stats);
        var instance = receiver.ActiveEffects.Single();
        Assert.That(instance.remainingSeconds, Is.EqualTo(12f));
        var stats = new CharacterStats { maxHP = 1000f };
        instance.definition.ModifyStats(new RuntimeEffectContext { instance = instance }, stats);
        Assert.That(stats.maxHP, Is.EqualTo(1400f).Within(.01f));
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: 6);
        Assert.That(passive.GetActiveNodes().Single(n => n.track == PotentialTrack.Passive && n.level == 6)
            .runtimeEffects.Any(e => e.recipe.recipeId == "main.memory.ultimate_hp"), Is.False);
    }

    [Test]
    public void PassiveFiveAddsDarknessOnlyToTheAuthoredUltimateFinishers()
    {
        var recipe = character.kitDefinition.potentialNodes.Single(n => n.track == PotentialTrack.Passive && n.level == 5)
            .runtimeEffects.Single().recipe;
        Assert.That(recipe.definition, Is.TypeOf<AdditionalDamageOnHitRuntimeEffectDefinition>());
        Assert.That(recipe.parameters.additionalDamage.sourceStatDamageSource, Is.EqualTo(RuntimeEffectStatTarget.MaxHP));
        Assert.That(recipe.parameters.additionalDamage.sourceStatRatio, Is.EqualTo(.2f));
        Assert.That(recipe.parameters.additionalDamage.elementType, Is.EqualTo(ElementType.Darkness));
        foreach (var ultimate in table.ultimateActions.Where(a => a.actionId == "main.ultimate.jailbreak_punisher" || a.actionId == "main.ultimate.execution_breakdown"))
        {
            var step = ultimate.comboTree.rootNode.stepData;
            if (ultimate.actionId == "main.ultimate.jailbreak_punisher") step = step.triggerWindows[0].targetNode.stepData;
            var hits = step.hitData.hitboxes.OfType<NormalHitbox>().OrderBy(h => h.startFrameOffset).ToArray();
            foreach (var hit in hits)
            {
                var context = new RuntimeEffectDamageContext { category = AttackCategory.Ultimate,
                    damageDelivery = DamageDelivery.Primary, actionId = ultimate.actionId,
                    provenance = new DamageProvenance(0, 0, 0, 0, null, hit.damageTags) };
                Assert.That(recipe.parameters.additionalDamage.filter.Matches(context), Is.EqualTo(hit == hits.Last()));
            }
        }
    }

    [Test]
    public void SummonedChainUsesAuthoredGroundProjectionAndTemplate()
    {
        var skillNode = character.kitDefinition.GetNodeById("main.skill.lv4");
        var recipe = skillNode.runtimeEffects.Single(effect => effect.recipe?.recipeId == "main.memory.chain_summon").recipe;
        Assert.That(recipe.definition, Is.TypeOf<PeriodicProjectileRuntimeEffectDefinition>());
        Assert.That(recipe.policy.durationSeconds, Is.EqualTo(15f));
        Assert.That(recipe.parameters.projectile.tickInterval, Is.EqualTo(2.5f));
        var projectile = recipe.parameters.projectile.projectile;
        Assert.That(AssetDatabase.Contains(projectile), Is.True);
        Assert.That(projectile.spawnReferencePointMode, Is.EqualTo(ProjectileReferencePointMode.GroundProjection));
        Assert.That(projectile.spawnPositionMode, Is.EqualTo(SpawnPositionMode.Target));
        Assert.That(projectile.magnetismSpeed, Is.Zero);
        Assert.That(projectile.limitHitsToTarget, Is.True);
        Assert.That(projectile.maxHits, Is.EqualTo(1));
        var hit = (NormalHitbox)projectile.ResolveRoutineHitbox();
        Assert.That(hit.elementType, Is.EqualTo(ElementType.None));
        Assert.That(hit.ResolveBaseDamage(new CharacterStats { attack = 100f, maxHP = 1000f }, 0f), Is.EqualTo(200f));
        var damageApplication = projectile.onHitEffects.Single(application => application.triggerOnHit);
        Assert.That(damageApplication.requiredNodeId, Is.EqualTo(skillNode.nodeId));
        Assert.That(damageApplication.ResolvedRecipe.parameters.additionalDamage.sourceStatRatio, Is.EqualTo(.08f));
        Assert.That(damageApplication.ResolvedRecipe.parameters.additionalDamage.elementType, Is.EqualTo(ElementType.Darkness));
        var visual = recipe.presentation.fieldVisualPrefab;
        Assert.That(visual, Is.Not.Null);
        Assert.That(recipe.presentation.followActiveCharacter, Is.True);
        Assert.That(visual.GetComponent<FieldEffectVisual>().style, Is.EqualTo(FieldEffectVisual.VisualStyle.FloatingIcon));
    }

    private ProjectileSpawnContext ResolveMemoryProjectile(string asset, string action, string step)
    {
        var context = new ProjectileSpawnContext
        {
            sourceCharacter = character, sourceActionId = action, sourceStep = new AttackStepData { stepName = step },
            projectileData = AssetDatabase.LoadAssetAtPath<ProjectileData>(
                "Assets/Project/Character/Hero/test_0_Main/ProjectileData_main_" + asset + ".asset")
        };
        if (passive.TryGetProjectileReplacement(asset == "skill" ? AttackCategory.Skill : AttackCategory.Ultimate,
            action, null, step, out var type, out var replacement) && replacement != null)
            context.projectileData = replacement;
        return context;
    }

    [Test]
    public void LockingPotentialClampsExistingMeter()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: 4);
        passive.RuntimeState.SetCounter("main.shadow", 100);
        CharacterMemoryTests.SetMemoryRanks(progression, character, passive: 0);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(40));
    }

    [Test]
    public void DashCachesBoxesWithoutTargetAndNormalConsumesOneCutPerInput()
    {
        StandaloneAction action = table.standaloneActions.Single(a => a.actionId == "main.dash.dimension_cut");
        Assert.That(action.Category, Is.EqualTo(AttackCategory.Normal));
        Assert.That(action.resourceCounterId, Is.EqualTo(character.kitDefinition.combatMeter.cachedCounterId));
        Assert.That(action.resourceCost, Is.EqualTo(1));
        Assert.That(action.commitsDash, Is.False, "Cooldown belongs to the dash that cached the charge.");
        Assert.That(action.requiresEnemyTarget && action.reselectOnAttackInput, Is.True);
        Assert.That(action.outputContext, Is.EqualTo(CombatActionOutputContext.MatchInput));
        passive.RuntimeState.SetCounter("main.shadow", 40);
        passive.NotifyAfterDash();
        passive.NotifyAfterDash();
        passive.NotifyAfterDash();
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.Zero);
        Assert.That(passive.RuntimeState.GetCounter(action.resourceCounterId), Is.EqualTo(2));
        // Target selection has its own test; isolate funding here without creating scene enemies.
        action.requiresEnemyTarget = false;
        PlayerCharacter player = owner.GetComponent<PlayerCharacter>();
        foreach (bool grounded in new[] { true, false })
        {
            InputContext context = new InputContext { isGrounded = grounded, passiveController = passive, actionAvailabilityProvider = player };
            var resolver = new ActionResolver();
            Assert.That(resolver.ResolveInitial(context, table, AttackCategory.Normal), Is.Not.Null);
            Assert.That(resolver.ActiveCombatAction, Is.SameAs(action));
            Assert.That(player.TryCommitActionCounterCost(action), Is.True);
        }
        Assert.That(passive.RuntimeState.GetCounter(action.resourceCounterId), Is.Zero);
        Assert.That(player.TryCommitActionCounterCost(action), Is.False);
        var fallback = new ActionResolver();
        fallback.ResolveInitial(new InputContext { isGrounded = true, passiveController = passive }, table);
        Assert.That(fallback.ActiveCombatAction, Is.Not.SameAs(action));
    }

    [Test]
    public void LessThanOneBoxDoesNotCacheAndNoTargetDoesNotSpendCache()
    {
        passive.RuntimeState.SetCounter("main.shadow", 19);
        passive.NotifyAfterDash();
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(19));
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.Zero);
        passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 2);
        var resolver = new ActionResolver();
        resolver.ResolveInitial(new InputContext { isGrounded = true, passiveController = passive,
            actionAvailabilityProvider = owner.GetComponent<PlayerCharacter>() }, table);
        Assert.That(resolver.ActiveCombatAction.actionName, Is.Not.EqualTo("Dimension Cut"));
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(2));
    }

    [Test]
    public void FullMeterWithoutDashingCannotTriggerDimensionCut()
    {
        passive.RuntimeState.SetCounter("main.shadow", 40);
        var resolver = new ActionResolver();
        resolver.ResolveInitial(new InputContext { isGrounded = true, passiveController = passive }, table);
        Assert.That(resolver.ActiveCombatAction.actionName, Is.Not.EqualTo("Dimension Cut"));
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(40));
    }

    [Test]
    public void NormalComboSpendsCachedCutsThenResumesBasicAttackWithoutAirTransition()
    {
        var player = owner.GetComponent<PlayerCharacter>();
        var fsm = owner.AddComponent<PlayerStateMachine>();
        fsm.playerCharacter = player;
        fsm.showCombatLogs = fsm.showFsmLogs = fsm.showFrameDataLogs = false;
        player.StateMachine = fsm;
        var combat = owner.AddComponent<PlayerCombat>();
        combat.playerCharacter = player;
        player.Combat = combat;
        table.standaloneActions.Single(a => a.actionId == "main.dash.dimension_cut").requiresEnemyTarget = false;
        var state = new ComboProbe(player, fsm);
        owner.SetActive(true);
        passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 2);
        fsm.ChangeState(state);
        state.OnAttackInput();
        Assert.That(combat.CurrentActionId, Is.EqualTo("main.dash.dimension_cut"));
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(1));
        state.OpenRecovery();
        state.OnAttackInput();
        Assert.That(combat.CurrentActionId, Is.EqualTo("main.dash.dimension_cut"));
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.Zero);
        state.OpenRecovery();
        state.OnAttackInput();
        Assert.That(combat.CurrentActionName, Is.EqualTo("Ground"));
        Assert.That(fsm.IsGroundedContext, Is.True);
        Assert.That(fsm.CurrentStateId, Is.EqualTo(PlayerStateId.Attacking));
    }

    [Test]
    public void CombatDashLocksDashUntilExitThenStartsNormalCooldown()
    {
        var movement = owner.AddComponent<PlayerMovement>();
        movement.profile = character.movementProfile;
        movement.dashData = new DashData { cooldownFrames = 24 };
        Assert.That(movement.TryCommitCombatDash(), Is.True);
        Assert.That(movement.CanDash(), Is.False);
        Assert.That(movement.TryCommitCombatDash(), Is.False);
        movement.FinishDash();
        Assert.That(movement.DashCooldownRemaining, Is.EqualTo(24 * Time.fixedDeltaTime).Within(0.001f));
        Assert.That(movement.CanDash(), Is.False);
    }

    [Test]
    public void RejectedDashDoesNotCacheAnotherCharge()
    {
        var movement = owner.AddComponent<PlayerMovement>();
        movement.profile = character.movementProfile;
        movement.dashData = new DashData { cooldownFrames = 24 };
        movement.OnDashStart += passive.NotifyAfterDash;
        passive.RuntimeState.SetCounter("main.shadow", 40);
        Assert.That(movement.TryCommitCombatDash(), Is.True);
        Assert.That(movement.TryCommitCombatDash(), Is.False);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(20));
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(1));
        movement.FinishDash();
        Assert.That(movement.TryCommitCombatDash(), Is.False);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(1));
    }

    [TestCase(2.1f, 99f, 1, 5f)]
    [TestCase(1.9f, -99f, 1, 5f)]
    [TestCase(2f, 99f, -1, -1f)]
    [TestCase(10f, 99f, 1, 11.2f)]
    public void HorizontalTargetDashPreservesYAndMinimumTravel(float targetX, float targetY, int facing, float expectedX)
    {
        var motion = new MotionKeyframe { horizontalOnly = true, minimumTravelDistance = 3f, passThroughDistance = 1.2f };
        Vector2 destination = motion.ResolveTargetDashDestination(new Vector2(2f, 5f), new Vector2(targetX, targetY), facing);
        Assert.That(destination.x, Is.EqualTo(expectedX).Within(0.001f));
        Assert.That(destination.y, Is.EqualTo(5f));
    }

    [TestCase(-3f, 0f)]
    [TestCase(3f, 0f)]
    [TestCase(0f, 3f)]
    [TestCase(0f, -3f)]
    public void TargetAcquisitionIncludesEveryDirection(float x, float y)
    {
        var targetObject = new GameObject("OmnidirectionalTarget", typeof(BoxCollider2D), typeof(EnemyController));
        try
        {
            owner.transform.position = new Vector2(10000f, 10000f);
            targetObject.transform.position = owner.transform.position + new Vector3(x, y, 0f);
            var enemy = targetObject.GetComponent<EnemyController>();
            typeof(EnemyController).GetField("lifecycleState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, EnemyLifecycleStateId.Active);
            var combat = owner.AddComponent<PlayerCombat>();
            combat.enemyLayerMask = 1 << targetObject.layer;
            Physics2D.SyncTransforms();
            Assert.That(combat.FindNearestEnemyTarget(8f), Is.SameAs(enemy));
        }
        finally { Object.DestroyImmediate(targetObject); }
    }

    [TestCase(1000f, 100f, true)]
    [TestCase(1000f, -100f, true)]
    [TestCase(1000f, 999f, true)]
    [TestCase(1000f, 1001f, false)]
    [TestCase(2f, 3f, false)]
    [TestCase(2f, 1f, true)]
    public void MotionRadiusGatesActionAvailabilityAndCommit(float radius, float distance, bool available)
    {
        var targetObject = new GameObject("RadiusRegressionTarget", typeof(BoxCollider2D), typeof(EnemyController));
        try
        {
            var player = owner.GetComponent<PlayerCharacter>();
            owner.transform.position = new Vector2(10000f, 10000f);
            targetObject.transform.position = owner.transform.position + Vector3.right * distance;
            var enemy = targetObject.GetComponent<EnemyController>();
            typeof(EnemyController).GetField("lifecycleState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, EnemyLifecycleStateId.Active);
            var combat = owner.AddComponent<PlayerCombat>();
            combat.enemyLayerMask = 1 << targetObject.layer;
            player.Combat = combat;
            StandaloneAction cut = table.standaloneActions.Single(a => a.actionId == "main.dash.dimension_cut");
            cut.targetRadius = 8f;
            cut.GetEntryTargetMotion().targetRadius = radius;
            passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 1);
            Physics2D.SyncTransforms();
            Assert.That(cut.GetTargetSearchRadius(), Is.EqualTo(radius));
            Assert.That(player.IsActionAvailable(cut), Is.EqualTo(available));
            var resolver = new ActionResolver();
            resolver.ResolveInitial(new InputContext { isGrounded = true, passiveController = passive,
                actionAvailabilityProvider = player }, table);
            Assert.That(resolver.ActiveCombatAction == cut, Is.EqualTo(available));
            Assert.That(combat.FindNearestEnemyTarget(cut.GetTargetSearchRadius()) == enemy, Is.EqualTo(available));
            Assert.That(player.TryCommitActionCounterCost(cut), Is.EqualTo(available));
            Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(available ? 0 : 1));
        }
        finally { Object.DestroyImmediate(targetObject); }
    }

    [Test]
    public void ActionRadiusControlAndMotionRadiusUseTheSameSource()
    {
        StandaloneAction cut = table.standaloneActions.Single(a => a.actionId == "main.dash.dimension_cut");
        MotionKeyframe motion = cut.GetEntryTargetMotion();
        cut.SetTargetSearchRadius(1000f);
        Assert.That(cut.targetRadius, Is.EqualTo(1000f));
        Assert.That(motion.targetRadius, Is.EqualTo(1000f));
        motion.targetRadius = 2f;
        Assert.That(cut.GetTargetSearchRadius(), Is.EqualTo(2f), "A smaller motion radius must not retain a hidden larger gate.");
        cut.GetEntryStep().motionData.keyframes.Insert(0, new MotionKeyframe
        {
            startFrame = motion.startFrame + 10, taskType = MotionTaskType.DashThroughTarget, targetRadius = 500f
        });
        Assert.That(cut.GetEntryTargetMotion(), Is.SameAs(motion), "Activation follows the earliest targeted keyframe, not list order.");
    }

    [Test]
    public void ActionsWithoutTargetMotionKeepAuthoredFallbackRadius()
    {
        var action = new ComboAction { targetRadius = 24f, comboTree = new ComboTree
        {
            rootNode = new ComboNode { stepData = new AttackStepData { motionData = new MotionFrameData() } }
        } };
        action.comboTree.rootNode.stepData.motionData.keyframes.Add(new MotionKeyframe
        {
            taskType = MotionTaskType.Relative, targetRadius = 1f
        });
        Assert.That(action.GetEntryTargetMotion(), Is.Null);
        Assert.That(action.GetTargetSearchRadius(), Is.EqualTo(24f));
        action.SetTargetSearchRadius(12f);
        Assert.That(action.GetTargetSearchRadius(), Is.EqualTo(12f));
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void CapturedTargetFacesEitherSideIndependentlyOfInput(int side)
    {
        var targetObject = new GameObject("CapturedTarget", typeof(EnemyController));
        try
        {
            var enemy = targetObject.GetComponent<EnemyController>();
            typeof(EnemyController).GetField("lifecycleState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, EnemyLifecycleStateId.Active);
            targetObject.transform.position = new Vector2(side * 3f, 0f);
            var movement = owner.AddComponent<PlayerMovement>();
            movement.ForceFaceDirectionFromInput(new Vector2(-side, 0f));
            movement.SetActionMotionTarget(enemy);
            Assert.That(movement.FacingDirection, Is.EqualTo(side));
            Assert.That(typeof(PlayerMovement).GetMethod("GetAttackFacingLockOnTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(movement, null), Is.SameAs(enemy));
        }
        finally { Object.DestroyImmediate(targetObject); }
    }

    [TestCase(AttackCategory.Normal, 1, false, 0f, -1)]
    [TestCase(AttackCategory.Normal, -1, false, 0f, 1)]
    [TestCase(AttackCategory.Skill, 1, false, 0f, -1)]
    [TestCase(AttackCategory.Skill, -1, false, 0f, 1)]
    [TestCase(AttackCategory.Ultimate, 1, false, 0f, -1)]
    [TestCase(AttackCategory.Ultimate, -1, false, 0f, 1)]
    [TestCase(AttackCategory.Normal, 1, true, 0f, 1)]
    [TestCase(AttackCategory.Skill, 1, false, 1f, 1)]
    public void CommittedStepRetargetsAfterCutWithoutAnIdleMovementTick(
        AttackCategory category, int travelDirection, bool autoChain, float inputX, int expectedFacing)
    {
        var targetObject = new GameObject("BufferedCutFacingTarget", typeof(BoxCollider2D), typeof(EnemyController));
        try
        {
            owner.transform.position = new Vector2(10000f, 10000f);
            targetObject.layer = LayerMask.NameToLayer("Enemy");
            targetObject.transform.position = owner.transform.position + Vector3.right * (3f * travelDirection);
            var enemy = targetObject.GetComponent<EnemyController>();
            typeof(EnemyController).GetField("lifecycleState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(enemy, EnemyLifecycleStateId.Active);
            var movement = owner.AddComponent<PlayerMovement>();
            var combat = owner.AddComponent<PlayerCombat>();
            combat.playerCharacter = owner.GetComponent<PlayerCharacter>();
            combat.movement = movement;
            typeof(PlayerMovement).GetField("combat", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, combat);
            typeof(PlayerMovement).GetField("enemyLayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(movement, targetObject.layer);
            var step = new AttackStepData { stepName = "FacingRegression", startupFrames = 5, activeFrames = 5, recoveryFrames = 5 };
            Physics2D.SyncTransforms();
            combat.ExecuteStep(step, 1, false, AttackCategory.Normal, "Dimension Cut", "main.dash.dimension_cut");
            movement.SetActionMotionTarget(enemy);
            Assert.That(movement.FacingDirection, Is.EqualTo(travelDirection));

            // Reproduce Face Target On Arrival disabled: pass the enemy while retaining travel facing.
            owner.transform.position += Vector3.right * (6f * travelDirection);
            Physics2D.SyncTransforms();
            typeof(PlayerMovement).GetField("horizontalInput", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, inputX);
            combat.ExecuteStep(step, 1, false, category, "Buffered Followup", "test.buffered.followup", autoChain);
            Assert.That(movement.FacingDirection, Is.EqualTo(expectedFacing),
                "The next committed step must retarget even when combo depth is unchanged and no Update ran between steps.");
        }
        finally { Object.DestroyImmediate(targetObject); }
    }

    [TestCase(-5f)]
    [TestCase(5f)]
    public void HeldVerticalTargetAlignsToHorizontalCutWithoutMovingPlayer(float targetY)
    {
        var motion = new MotionKeyframe { horizontalOnly = true, holdTargetAtAnchor = true,
            alignTargetToDashPlane = true, minimumTravelDistance = 3f };
        Vector2 start = new Vector2(0f, 2f);
        Vector2 anchor = motion.ResolveTargetDashAnchor(start, new Vector2(1f, targetY), 1);
        Assert.That(anchor.y, Is.EqualTo(start.y));
        Assert.That(motion.ResolveTargetDashDestination(start, anchor, 1).y, Is.EqualTo(start.y));
    }

    [Test]
    public void TargetDashHoldsEachHitEnemyAtItsOwnAnchor()
    {
        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var movement = owner.AddComponent<PlayerMovement>();
        typeof(PlayerMovement).GetField("rb", privateInstance).SetValue(movement, owner.GetComponent<Rigidbody2D>());
        var combat = owner.AddComponent<PlayerCombat>();
        combat.movement = movement;
        var hitboxObject = new GameObject("Dash Hitbox");
        hitboxObject.transform.SetParent(owner.transform, false);
        var hitbox = hitboxObject.AddComponent<HitBoxDamage>();
        // The fixture's inactive player is excluded from the runtime parent lookup.
        typeof(HitBoxDamage).GetField("playerCombat", privateInstance).SetValue(hitbox, combat);
        hitbox.movement = movement;
        combat.hitBoxDamage = hitbox;
        typeof(PlayerCombat).GetField("currentAttackFrame", privateInstance).SetValue(combat, 5);
        hitbox.SetHitData(10f, false, false, 0f, 1f, new NormalHitbox { flashFrames = 0 },
            AttackCategory.Normal, "Dimension Cut", null, null);

        var enemies = new EnemyController[4];
        var anchors = (System.Collections.Generic.Dictionary<EnemyController, Transform>)typeof(PlayerMovement)
            .GetField("dashHitAnchors", privateInstance).GetValue(movement);
        try
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemyObject = new GameObject($"Dash Target {i}");
                enemyObject.transform.position = new Vector2(i + 1f, i);
                var enemy = enemies[i] = enemyObject.AddComponent<EnemyController>();
                enemy.hitReactionHandler = enemyObject.AddComponent<EnemyHitReactionHandler>();
                typeof(EnemyHitReactionHandler).GetMethod("Awake", privateInstance).Invoke(enemy.hitReactionHandler, null);
                typeof(EnemyController).GetField("lifecycleState", privateInstance).SetValue(enemy, EnemyLifecycleStateId.Active);
            }
            enemies[3].ignoreHitReactions = true;

            var motion = new MotionKeyframe { taskType = MotionTaskType.DashThroughTarget, startFrame = 3, endFrame = 11,
                horizontalOnly = true, holdTargetAtAnchor = true, alignTargetToDashPlane = true,
                targetAnchorOffset = new Vector2(-0.5f, 0f) };
            movement.SetActionMotionTarget(enemies[0]);
            var dash = (System.Collections.IEnumerator)typeof(PlayerMovement).GetMethod("AttackMovementCoroutine", privateInstance)
                .Invoke(movement, new object[] { motion, true });
            Assert.That(dash.MoveNext(), Is.True);
            Assert.That(movement.TryGetResolvedTargetMotion(out _, out Vector2 initialTravel), Is.True);
            Assert.That(anchors, Is.Empty);

            hitbox.ProcessHit(enemies[1]);
            hitbox.ProcessHit(enemies[2]);
            hitbox.ProcessHit(enemies[3]);

            Assert.That(enemies[0].IsGrabbed, Is.False);
            Assert.That(enemies[1].currentHP, Is.EqualTo(90f));
            Assert.That(enemies[2].currentHP, Is.EqualTo(90f));
            Assert.That(enemies[1].CurrentReactionState, Is.EqualTo(EnemyReactionState.Grabbed));
            Assert.That(enemies[2].CurrentReactionState, Is.EqualTo(EnemyReactionState.Grabbed));
            Assert.That(enemies[3].IsGrabbed, Is.False);
            Assert.That((Vector2)anchors[enemies[1]].position, Is.EqualTo(new Vector2(1.5f, 0f)));
            Assert.That((Vector2)anchors[enemies[2]].position, Is.EqualTo(new Vector2(2.5f, 0f)));
            movement.TryGetResolvedTargetMotion(out _, out Vector2 travelAfterHits);
            Assert.That(travelAfterHits, Is.EqualTo(initialTravel));
        }
        finally
        {
            foreach (Transform anchor in anchors.Values)
                if (anchor != null) Object.DestroyImmediate(anchor.gameObject);
            foreach (EnemyController enemy in enemies)
                if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
        }
    }

    [Test]
    public void PrefabMeterLivesBetweenStatusAndStats()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/CombatUI/Player_UI.prefab");
        CombatMeterUI meter = prefab.GetComponentInChildren<CombatMeterUI>(true);
        Assert.That(meter, Is.Not.Null);
        Transform row = meter.transform.parent;
        Transform parent = row.parent;
        Assert.That(parent.GetChild(row.GetSiblingIndex() - 1).name, Is.EqualTo("RuntimeStatusPanel"));
        Assert.That(parent.GetChild(row.GetSiblingIndex() + 1).name, Is.EqualTo("PlayerPanel"));
        Assert.That(row.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout, Is.False);
    }

    [Test]
    public void CombatAirDashConsumesNormalAirDashAllowance()
    {
        var player = owner.GetComponent<PlayerCharacter>();
        var fsm = owner.AddComponent<PlayerStateMachine>();
        fsm.playerCharacter = player;
        typeof(PlayerStateMachine).GetProperty("EnvironmentContext").SetValue(fsm, PlayerEnvironmentContext.Airborne);
        var movement = owner.AddComponent<PlayerMovement>();
        movement.profile = character.movementProfile;
        movement.maxAirDashOverride = 1;
        movement.dashData = new DashData { cooldownFrames = 0 };
        typeof(PlayerMovement).GetField("fsm", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, fsm);
        Assert.That(movement.TryCommitCombatDash(), Is.True);
        movement.FinishDash();
        Assert.That(movement.DashCooldownRemaining, Is.Zero);
        Assert.That(movement.CanDash(), Is.False, "A second air dash still needs the normal landing reset.");
    }

    [Test]
    public void MeterDisplaysPartialFillAndPulsesOnlyNewReadyBox()
    {
        var meterObject = new GameObject("MeterUI", typeof(RectTransform), typeof(CombatMeterUI));
        meterObject.transform.SetParent(owner.transform);
        CombatMeterUI meter = meterObject.GetComponent<CombatMeterUI>();
        meter.Bind(owner.GetComponent<PlayerCharacter>());
        MethodInfo refresh = typeof(CombatMeterUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        passive.RuntimeState.SetCounter("main.shadow", 10);
        refresh.Invoke(meter, null);
        Assert.That(meter.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty, "The meter must not create a CUT label.");
        var fill = meter.transform.GetChild(0).Find("Fill").GetComponent<UnityEngine.UI.Image>();
        var second = meter.transform.GetChild(1).Find("Fill").GetComponent<UnityEngine.UI.Image>();
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.5f));
        Assert.That(second.enabled, Is.False);
        passive.RuntimeState.SetCounter("main.shadow", 20);
        refresh.Invoke(meter, null);
        var overlay = meter.transform.GetChild(0).Find("Ready Overlay").GetComponent<UnityEngine.UI.Image>();
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(1f));
        Assert.That(overlay.color.a, Is.GreaterThan(0.9f));
        var starts = (System.Collections.Generic.List<float>)typeof(CombatMeterUI)
            .GetField("pulseStarts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(meter);
        starts[0] = Time.unscaledTime - 0.2f;
        refresh.Invoke(meter, null);
        Assert.That(overlay.rectTransform.localScale.x, Is.GreaterThan(1f));
        Assert.That(overlay.color.a, Is.LessThan(0.5f));
    }

    [TestCase(0f, 1f)]
    [TestCase(1f, 1f)]
    [TestCase(-1f, 1f)]
    [TestCase(0f, -1f)]
    public void TargetMotionCastsBodyAgainstSolidsInBothAxes(float x, float y)
    {
        var movement = owner.AddComponent<PlayerMovement>();
        var bodyObject = new GameObject("TargetMotionBody", typeof(Rigidbody2D), typeof(BoxCollider2D));
        var obstacle = new GameObject("TargetMotionSolid", typeof(BoxCollider2D));
        try
        {
            Vector2 start = new Vector2(10000f, 10000f);
            Vector2 direction = new Vector2(x, y).normalized;
            bodyObject.transform.position = start;
            obstacle.transform.position = start + direction * 4f;
            Rigidbody2D body = bodyObject.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            typeof(PlayerMovement).GetField("rb", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, body);
            typeof(PlayerMovement).GetField("enemyLayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(movement, LayerMask.NameToLayer("Enemy"));
            typeof(PlayerMovement).GetField("solidColliders", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(movement, new Collider2D[] { bodyObject.GetComponent<BoxCollider2D>() });
            Physics2D.SyncTransforms();
            Vector2 clipped = (Vector2)typeof(PlayerMovement).GetMethod("ClipTargetMotionVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(movement, new object[] { direction * 1000f });
            Assert.That(clipped.magnitude, Is.GreaterThan(0f).And.LessThan(1000f));
            Assert.That(clipped.magnitude * Time.fixedDeltaTime, Is.LessThan(4f), "The body must stop before the solid.");
        }
        finally
        {
            Object.DestroyImmediate(bodyObject);
            Object.DestroyImmediate(obstacle);
        }
    }
}
#endif
