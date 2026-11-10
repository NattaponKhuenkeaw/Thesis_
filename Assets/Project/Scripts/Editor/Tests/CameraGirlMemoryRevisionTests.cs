#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Combat;
using Project.ShopSystem;
using Project.Progression;
using Project.InventorySystem.Items;
using UnityEditor;
using UnityEngine;

public class CameraGirlMemoryRevisionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> cleanup = new();
    private CharacterObject cg, ally;
    private PlayerCharacter player;
    private PlayerCombat combat;
    private CharacterPassiveController passive;
    private PlayerRunInventory inventory;
    private CharacterProgressionState progression;
    private TeamLoader team, oldTeam;
    private PlayerCharacter oldPlayer;

    [SetUp]
    public void Setup()
    {
        oldTeam = TeamLoader.Instance; oldPlayer = PlayerCharacter.Instance;
        cg = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset")));
        cg.characterId = "cg-memory-test";
        cg.stats = new CharacterStats { attack = 50f, defense = 100f, maxHP = 1000f };
        ally = Keep(ScriptableObject.CreateInstance<CharacterObject>());
        ally.characterId = "cg-memory-ally";
        ally.stats = new CharacterStats { attack = 100f, defense = 100f, maxHP = 1000f };
        var host = Keep(new GameObject("CG recipe test")); host.SetActive(false);
        player = host.AddComponent<PlayerCharacter>(); player.characterObject = cg; player.InitializeHP(1000f);
        combat = host.AddComponent<PlayerCombat>(); combat.playerCharacter = player; player.Combat = combat;
        passive = host.AddComponent<CharacterPassiveController>(); passive.playerCharacter = player; player.PassiveController = passive;
        progression = Keep(ScriptableObject.CreateInstance<PlayerMetaProgression>());
        passive.SetMetaProgression(progression);
        inventory = Keep(ScriptableObject.CreateInstance<PlayerRunInventory>());
        inventory.ownedItems.Clear();
        foreach (var character in new[] { cg, ally })
        {
            var item = Keep(ScriptableObject.CreateInstance<CharacterItem>());
            item.id = character.characterId; item.characterObjectData = character; inventory.ownedItems.Add(item);
        }
        inventory.DeployTeam(new[] { cg, ally }, 0);
        var teamHost = Keep(new GameObject("CG recipe team")); teamHost.SetActive(false);
        team = teamHost.AddComponent<TeamLoader>(); team.playerCharacter = player;
        typeof(TeamLoader).GetField("runInventory", Private).SetValue(team, inventory);
        typeof(TeamLoader).GetField("metaProgression", Private).SetValue(team, progression);
        SetInstance(typeof(TeamLoader), team); SetInstance(typeof(PlayerCharacter), player);
        teamHost.SetActive(true);
        team.RefreshRuntimeStatsAndResources();
    }

    [TearDown]
    public void Teardown()
    {
        SetInstance(typeof(TeamLoader), oldTeam); SetInstance(typeof(PlayerCharacter), oldPlayer);
        for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }
    private T Keep<T>(T value) where T : Object { cleanup.Add(value); return value; }
    private static void SetInstance(System.Type type, Object value) => type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    private PotentialNodeDefinition Node(PotentialTrack track, int level) => cg.kitDefinition.potentialNodes.Single(n => n.track == track && n.level == level);

    [TestCase(1, 17f, 33f, false)]
    [TestCase(2, 31f, 64f, false)]
    [TestCase(3, 31f, 64f, true)]
    public void CaptureUsesOnlyTheCurrentRankAndCorrectRecipients(int rank, float cr, float cd, bool self)
    {
        CharacterMemoryTests.SetMemoryRanks(progression, cg, skill: rank);
        passive.NotifyActionStarted(AttackCategory.Skill, "cg.skill", "Capture Da Soul", null);
        var effects = inventory.GetActiveRuntimeEffects(ally);
        Assert.That(effects.Count(e => e.recipe.recipeId == "cg.capture.cr"), Is.EqualTo(1));
        Assert.That(effects.Single(e => e.recipe.recipeId == "cg.capture.cr").recipe.parameters.statModifiers.Single().value, Is.EqualTo(cr));
        Assert.That(effects.Single(e => e.recipe.recipeId == "cg.capture.cd").recipe.parameters.statModifiers.Single().value, Is.EqualTo(cd));
        Assert.That(inventory.GetActiveRuntimeEffects(cg).Any(e => e.recipe.recipeId == "cg.capture.cr"), Is.EqualTo(self));
        if (self) Assert.That(inventory.GetActiveRuntimeEffects(cg).Any(e => e.recipe.recipeId == "cg.capture.defense"), Is.True);
    }

    [Test]
    public void BaseCaptureSelectsExactlyOneBuffAndAllSixMemoryRanksAreReachable()
    {
        progression.SetPotentialLevel(cg, PotentialTrack.Skill, 4);
        Assert.That(progression.GetPotentialLevel(cg, PotentialTrack.Skill), Is.EqualTo(4));
        Assert.That(progression.GetCombatPotentialLevel(cg, PotentialTrack.Skill), Is.Zero);
        Assert.That(progression.GetNextPotentialNode(cg, PotentialTrack.Skill), Is.Null);
        passive.NotifyActionStarted(AttackCategory.Skill, "cg.skill", "Capture Da Soul", null);
        var buffs = inventory.GetActiveRuntimeEffects(ally).Where(e => e.recipe.recipeId == "cg.capture.cr" || e.recipe.recipeId == "cg.capture.cd").ToArray();
        Assert.That(buffs.Length, Is.EqualTo(1));
        Assert.That(inventory.GetActiveRuntimeEffects(cg).Any(e => e.recipe.recipeId == "cg.capture.cr" || e.recipe.recipeId == "cg.capture.cd"), Is.False);
        CharacterMemoryTests.SetMemoryRanks(progression, cg, skill: 6, ultimate: 6, passive: 6);
        foreach (var track in new[] { PotentialTrack.Skill, PotentialTrack.Ultimate, PotentialTrack.Passive })
            Assert.That(progression.GetCombatPotentialLevel(cg, track), Is.EqualTo(6));
        Assert.That(progression.GetUnlockedPotentialNodes(cg).Count(n => n.level > 0), Is.EqualTo(18));
    }

    [TestCase(100f, 600f)]
    [TestCase(300f, 1000f)]
    public void UltimateAttackBuffCapsOnlyItsOwnContribution(float defense, float expectedAttack)
    {
        var recipe = Node(PotentialTrack.Ultimate, 4).runtimeEffects.Single().recipe;
        var instance = new RuntimeEffectInstance(); instance.Initialize(recipe, cg, new CharacterStats { defense = defense });
        var stats = new CharacterStats { attack = 100f };
        recipe.definition.ModifyStats(new RuntimeEffectContext { instance = instance, sourceStats = new CharacterStats { defense = defense } }, stats);
        Assert.That(stats.attack, Is.EqualTo(expectedAttack));
    }

    [Test]
    public void FocusRefreshesOneSourceInstanceAndBuffsBothTeamMembers()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, cg, skill: 5);
        passive.NotifyActionStarted(AttackCategory.Skill, "cg.skill", "Capture Da Soul", null);
        inventory.TickRuntimeEffects(2f);
        passive.NotifyActionStarted(AttackCategory.Skill, "cg.skill", "Capture Da Soul", null);
        Assert.That(inventory.GetActiveRuntimeEffects(cg).Count(e => e.recipe.recipeId == "cg.lens_focus"), Is.EqualTo(1));
        Assert.That(inventory.GetActiveRuntimeEffects(cg).Single(e => e.recipe.recipeId == "cg.lens_focus").remainingSeconds, Is.EqualTo(10f));
        foreach (var target in new[] { cg, ally })
            Assert.That(inventory.GetActiveRuntimeEffects(target).Count(e => e.recipe.recipeId == "cg.lens_focus.aura"), Is.EqualTo(1));
        var focus = inventory.GetActiveRuntimeEffects(cg).Single(e => e.recipe.recipeId == "cg.lens_focus");
        inventory.RemoveRuntimeEffect(cg, focus.definition, focus.recipe as RuntimeEffectRecipe);
        inventory.TickRuntimeEffects(.01f);
        foreach (var target in new[] { cg, ally })
            Assert.That(inventory.GetActiveRuntimeEffects(target).Any(e => e.recipe.recipeId == "cg.lens_focus.aura"), Is.False);
    }

    [Test]
    public void LensFocusPlaysUltimateProjectileAnimationAfterSpendingItsSingleHit()
    {
        var data = Node(PotentialTrack.Skill, 5).runtimeEffects.Single(e => e.recipe.parameters.overrideProjectile).recipe.parameters.projectile.projectile;
        var ultimate = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            "Assets/Project/Character/Hero/test_1_CameraGirl/ProjectileData_cg_ult.asset");
        var target = Keep(new GameObject("Len Focus target", typeof(BoxCollider2D)));
        var enemy = EnemyLifecycleTestUtility.AddActive(target);
        enemy.maxHP = enemy.currentHP = 1000f;
        enemy.defense = enemy.damageRes = 0f;
        var host = Keep(Object.Instantiate(data.projectilePrefab));
        host.SetActive(true);
        var projectile = host.GetComponent<GenericProjectilePlayer>();
        var animator = host.GetComponentInChildren<Animator>();
        var renderer = host.GetComponentInChildren<SpriteRenderer>();
        typeof(GenericProjectilePlayer).GetMethod("Awake", Private).Invoke(projectile, null);
        animator.Rebind();
        animator.Update(0f);
        projectile.Play(null, data.projectileType, data, new ProjectileSpawnContext
        {
            targetTransform = target.transform,
            direction = Vector2.right,
            sourceCharacter = cg,
            sourceCombat = combat,
            attackerStats = cg.stats,
            sourceActionId = "cg.lens_focus",
            category = AttackCategory.Skill,
            remainingHits = data.maxHits
        });

        animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorClipInfo(0).Single().clip, Is.SameAs(ultimate.spawnClip));
        var openingSprite = renderer.sprite;
        Physics2D.SyncTransforms();
        var runtime = projectile.Runtime;
        typeof(ProjectileRuntime).GetMethod("OnTriggerEnter2D", Private).Invoke(runtime, new object[] { enemy.bodyCollider });
        Assert.That(enemy.currentHP, Is.EqualTo(900f));
        Assert.That(((ProjectileSpawnContext)typeof(ProjectileRuntime).GetField("spawnContext", Private).GetValue(runtime)).remainingHits, Is.Zero);

        AdvanceFrames(12);
        Assert.That(renderer.sprite, Is.Not.SameAs(openingSprite), "The strike should continue animating after the hit.");
        AdvanceFrames(15);
        Assert.That(animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip), Does.Contain(ultimate.loopClip));
        Assert.That(host.GetComponent<HitBoxDamage>().enabled, Is.False);
        Tick(.11f);
        typeof(ProjectileRuntime).GetMethod("Update", Private).Invoke(runtime, null);
        Assert.That(host.activeSelf, Is.False);

        void AdvanceFrames(int frames)
        {
            const float frameSeconds = 1f / 60f;
            for (int frame = 0; frame < frames; frame++)
            {
                animator.Update(frameSeconds);
                Tick(frameSeconds);
            }
        }

        void Tick(float seconds)
        {
            var state = typeof(ProjectileRuntime).GetField("currentState", Private).GetValue(runtime);
            state.GetType().GetMethod("Tick").Invoke(state, new object[] { seconds });
        }
    }

    [Test]
    public void RoundRobinVisitsEverySceneTargetWhileNearestModeStaysUnchanged()
    {
        var near = Enemy("RoundRobin near"); var far = Enemy("RoundRobin far"); var middle = Enemy("RoundRobin middle");
        near.transform.position = Vector3.right; middle.transform.position = Vector3.right * 10f; far.transform.position = Vector3.right * 1000f;
        var targets = new List<EnemyController> { far, near, middle };
        var method = typeof(PeriodicProjectileRuntimeEffectDefinition).GetMethod("SelectTarget", BindingFlags.Static | BindingFlags.NonPublic);
        var visited = new HashSet<EnemyController>(); object[] args = { targets, Vector2.zero, true, 0 };
        for (int i = 0; i < 3; i++) visited.Add((EnemyController)method.Invoke(null, args));
        Assert.That(visited.Count, Is.EqualTo(3));
        Assert.That((EnemyController)method.Invoke(null, new object[] { targets, Vector2.zero, false, 0 }), Is.SameAs(near));
    }

    [Test]
    public void HigherAssistRankDoesNotRerollThroughLowerRanksOnFailure()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, cg, ultimate: 2);
        Node(PotentialTrack.Ultimate, 1).coordinatedActionEffects[0].triggerChance = 1f;
        Node(PotentialTrack.Ultimate, 2).coordinatedActionEffects[0].triggerChance = 0f;
        team.GetCharacterCombatRuntimeState(cg).SetTag("cg.ult.shadow_assist", 15f);
        var enemy = Enemy("Assist roll");
        var host = Keep(new GameObject("Assist selector")); host.SetActive(false);
        var system = host.AddComponent<CharacterCoordinatedActionSystem>();
        typeof(CharacterCoordinatedActionSystem).GetMethod("HandleDamageResolved", Private).Invoke(system,
            new object[] { new DamageResolvedEvent(default, new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
                DamageProcPolicy.TriggerOrdinaryOnHit, ally, null, enemy, AttackCategory.Normal, "ally.attack", 1f, 1f, false, false, Vector2.zero) });
        Assert.That(ShadowActionExecutor.GetActiveCount(cg), Is.Zero);
    }

    [Test]
    public void CapturedModifiesDefenseResistanceAndOnlyDotAndAdditionalDamage()
    {
        var enemy = Enemy("Captured input");
        var recipe = Node(PotentialTrack.Passive, 2).runtimeEffects[0].recipe;
        enemy.RuntimeEffects.ApplyEffect(recipe, cg, cg.stats);
        foreach (var delivery in new[] { DamageDelivery.Primary, DamageDelivery.Dot, DamageDelivery.Additional })
        {
            var context = new RuntimeEffectDamageContext { targetEnemy = enemy, damageDelivery = delivery };
            var input = new RuntimeEffectDamageInput { context = context, defenderDefense = 100f, defenderDamageResistance = 20f };
            enemy.RuntimeEffects.ModifyDamageInput(input);
            Assert.That(input.defenderDefense, Is.EqualTo(60f).Within(.01f));
            Assert.That(input.defenderDamageResistance, Is.EqualTo(-20f).Within(.01f));
            Assert.That(enemy.RuntimeEffects.ModifyIncomingDamage(context, 100f), Is.EqualTo(delivery == DamageDelivery.Primary ? 100f : 140f).Within(.01f));
        }
    }

    [Test]
    public void CapturedDefenseAndResistanceReachTheUnifiedDamageResolver()
    {
        var enemy = Enemy("Captured mitigation"); enemy.defense = 100f; enemy.damageRes = 20f;
        enemy.RuntimeEffects.ApplyEffect(Node(PotentialTrack.Passive, 2).runtimeEffects[0].recipe, cg, cg.stats);
        var request = new DamageRequest { target = enemy, baseDamage = 100f,
            sourceStats = new CharacterStats(), descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None),
            defenderDefense = 100f, defenderDamageResistance = 20f, hasCriticalRoll = true, criticalRoll01 = 1f };
        Assert.That(CombatDamageProcessor.Resolve(request).Damage,
            Is.EqualTo(CombatFormula.CalculateIncomingDamage(100f, 60f, -20f)).Within(1f));
    }

    [Test]
    public void AlreadyCapturedHitOpensMasteryButInitialMarkingHitDoesNot()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, cg, passive: 3);
        var enemy = Enemy("Captured followup");
        passive.NotifyHitDealt(AttackCategory.Normal, "basic", "Ground", new AttackStepData { stepName = "Attack3" }, enemy, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.HasTag("cg.skill.followup_window"), Is.False);
        Assert.That(enemy.RuntimeEffects.GetStackCount("cg.captured"), Is.EqualTo(1));
        passive.NotifyHitDealt(AttackCategory.Skill, "cg.skill", "Capture Da Soul", null, enemy, ElementType.None, false, 1f, false);
        Assert.That(passive.RuntimeState.HasTag("cg.skill.followup_window"), Is.True);
    }

    [Test]
    public void TenStacksGiveThirtyPercentThenExpireAndMovementBuffHalvesDashCooldown()
    {
        var recipe = Node(PotentialTrack.Passive, 6).runtimeEffects.Single().recipe.parameters.reaction.effects.Single().recipe;
        for (int i = 0; i < 11; i++) inventory.ApplyRuntimeEffect(cg, recipe, cg, cg.stats);
        Assert.That(inventory.GetActiveRuntimeEffects(cg).Single().stackCount, Is.EqualTo(10));
        var stats = new CharacterStats { attack = 100f, defense = 100f };
        inventory.ApplyRuntimeEffectsToStats(cg, stats);
        Assert.That(stats.attack, Is.EqualTo(130f).Within(.01f)); Assert.That(stats.defense, Is.EqualTo(130f).Within(.01f));
        inventory.TickRuntimeEffects(3.01f); Assert.That(inventory.GetActiveRuntimeEffects(cg), Is.Empty);
        var aura = Node(PotentialTrack.Skill, 5).runtimeEffects.Single(e => e.recipe.parameters.overrideMovement).recipe;
        inventory.ApplyRuntimeEffect(cg, Node(PotentialTrack.Skill, 5).runtimeEffects.Single(e => e.recipe.parameters.overrideProjectile).recipe, cg, cg.stats);
        inventory.ApplyRuntimeEffect(cg, aura, cg, cg.stats);
        Assert.That(RuntimeEffectRuntimeUtility.ModifyDashCooldown(player, 2f), Is.EqualTo(1f));
        inventory.TickRuntimeEffects(10.01f);
        Assert.That(RuntimeEffectRuntimeUtility.ModifyDashCooldown(player, 2f), Is.EqualTo(2f));
    }

    [Test]
    public void SourceOwnedReactionWorksOffFieldWithOneCooldownAndNoRecursiveAdditionalHit()
    {
        var enemy = Enemy("Captured source proc");
        enemy.RuntimeEffects.ApplyEffect(Node(PotentialTrack.Passive, 1).runtimeEffects[0].recipe, cg, cg.stats);
        combat.StartActionCooldown(AttackCategory.Skill, "Capture Da Soul", "cg.skill");
        float before = combat.GetRemainingCooldownForCharacter(cg, AttackCategory.Skill, 7f, "Capture Da Soul", "cg.skill");
        inventory.SetActiveTeamSlotIndex(1); player.characterObject = ally;
        var recipe = Node(PotentialTrack.Passive, 5).runtimeEffects.Single().recipe;
        inventory.ApplyRuntimeEffect(cg, recipe, cg, cg.stats);
        var context = new RuntimeEffectDamageContext { sourceCharacter = cg, sourceStats = cg.stats,
            targetEnemy = enemy, damageDelivery = DamageDelivery.Primary, actionId = "basic" };
        combat.NotifyRuntimeHitDealt(cg, context); float hp = enemy.currentHP;
        Assert.That(hp, Is.LessThan(10000f));
        combat.NotifyRuntimeHitDealt(cg, context); Assert.That(enemy.currentHP, Is.EqualTo(hp));
        Assert.That(combat.GetRemainingCooldownForCharacter(cg, AttackCategory.Skill, 7f, "Capture Da Soul", "cg.skill"), Is.EqualTo(before - .5f).Within(.01f));
    }

    [Test]
    public void CoordinatedRewardCanSuppressOrdinaryAuraRollsAndRequiresConfirmedDamage()
    {
        var enemy = Enemy("Assist confirmed reward");
        var host = Keep(new GameObject("Assist hit")); var hit = host.AddComponent<HitBoxDamage>();
        hit.SetHitData(10f, false, false, 0f, 0f, new NormalHitbox(), AttackCategory.Skill, "Shadow Attack", null, null, "cg.skill.ex");
        hit.ConfigurePerEnemyDamageCalculation(cg.stats, 50f, true, null, combat, cg, null, suppressNativeAuraEnergy: true);
        var events = new List<DamageResolvedEvent>(); System.Action<DamageResolvedEvent> capture = events.Add;
        CombatEventHub.Shared.DamageResolved += capture;
        try
        {
            hit.ProcessHit(enemy);
            Assert.That(hit.LastDamageDealt, Is.GreaterThan(0f));
            Assert.That(events.Single().acceptedPolicy & DamageProcPolicy.GrantAuraEnergy, Is.EqualTo(DamageProcPolicy.None));
            hit.ProcessHit(enemy);
            Assert.That(hit.LastDamageDealt, Is.Zero, "An ignored duplicate collider must not count as a confirmed hit.");
        }
        finally { CombatEventHub.Shared.DamageResolved -= capture; }
    }

    [TestCase(1, 113.75f)]
    [TestCase(5, 650f)]
    public void ShadowAttackPreservesBaseDamageAndUsesTheFullDefenseOverride(int rank, float expected)
    {
        var enemy = Enemy("Shadow damage budget");
        var definition = Node(PotentialTrack.Ultimate, rank).coordinatedActionEffects.Single();
        var executor = ShadowActionExecutor.Spawn(cg, enemy, definition);
        Assert.That(executor, Is.Not.Null);
        Keep(executor.gameObject);
        ShadowActionExecutor.TryResolveAction(cg, "cg.skill.ex", out var step, out _, out _, out _);
        typeof(ShadowActionExecutor).GetMethod("TriggerHitbox", Private).Invoke(executor, new object[] { step.hitData.hitboxes.OfType<NormalHitbox>().Single() });
        var hit = (HitBoxDamage)typeof(ShadowActionExecutor).GetField("cloneHitboxDamage", Private).GetValue(executor);
        var events = new List<DamageResolvedEvent>(); System.Action<DamageResolvedEvent> capture = events.Add;
        CombatEventHub.Shared.DamageResolved += capture;
        try
        {
            hit.ProcessHit(enemy);
            Assert.That(events.Single().baseDamage, Is.EqualTo(expected).Within(.01f));
        }
        finally { CombatEventHub.Shared.DamageResolved -= capture; }
    }

    [Test]
    public void MasteryDeliveryConversionUsesAdditionalScalingInTheNativeHitPath()
    {
        var recipe = Node(PotentialTrack.Passive, 4).runtimeEffects.Single().recipe;
        inventory.ApplyRuntimeEffect(cg, recipe, cg, cg.stats);
        var enemy = Enemy("Mastery conversion");
        var host = Keep(new GameObject("Mastery hit"));
        var hit = host.AddComponent<HitBoxDamage>();
        var stats = new CharacterStats { attack = 100f, maxHP = 1000f, damageIncrease = 100f, additionalDamageIncrease = 0f };
        hit.SetHitData(100f, false, false, 0f, 0f, new NormalHitbox(), AttackCategory.Skill, "Mastery EndField", null, null, "Skill.super.ex");
        hit.ConfigurePerEnemyDamageCalculation(stats, 100f, true, null, combat, cg, null);
        var events = new List<DamageResolvedEvent>(); System.Action<DamageResolvedEvent> capture = events.Add;
        CombatEventHub.Shared.DamageResolved += capture;
        try
        {
            hit.ProcessHit(enemy);
            Assert.That(events.Single().descriptor.delivery, Is.EqualTo(DamageDelivery.Additional));
            Assert.That(events.Single().damageDealt, Is.EqualTo(100f).Within(.01f));
        }
        finally { CombatEventHub.Shared.DamageResolved -= capture; }
    }

    [Test]
    public void SkillSixSplashRequiresAnExistingMarkAndUsesSourceDefenseWithinItsRadius()
    {
        CharacterMemoryTests.SetMemoryRanks(progression, cg, skill: 6, passive: 1);
        var target = Enemy("Splash center"); var near = Enemy("Splash neighbor"); var far = Enemy("Splash distant");
        near.transform.position = Vector3.right * 2.9f;
        far.transform.position = Vector3.right * 3.1f;
        RoomEnemyRegistry.Register(target); RoomEnemyRegistry.Register(near); RoomEnemyRegistry.Register(far);
        var recipe = Node(PotentialTrack.Skill, 6).runtimeEffects.Single().recipe;
        inventory.ApplyRuntimeEffect(cg, recipe, cg, cg.stats);
        float sourceDefense = team.GetCharacterRuntimeStats(cg).defense;
        var request = new DamageRequest
        {
            sourceCharacter = cg, sourceStats = cg.stats, sourceCombat = combat,
            sourcePassiveController = passive, target = target, baseDamage = 100f,
            actionId = "cg.skill", category = AttackCategory.Skill,
            descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None, DamageCriticalPolicy.Prevent)
        };
        var events = new List<DamageResolvedEvent>();
        void CaptureAdditional(DamageResolvedEvent damage)
        {
            if (damage.descriptor.delivery == DamageDelivery.Additional) events.Add(damage);
        }
        System.Action<DamageResolvedEvent> capture = CaptureAdditional;
        CombatEventHub.Shared.DamageResolved += capture;
        try
        {
            CombatDamageProcessor.Apply(request);
            Assert.That(target.RuntimeEffects.GetStackCount("cg.captured"), Is.EqualTo(1));
            Assert.That(near.currentHP, Is.EqualTo(10000f));
            Assert.That(events.Select(e => e.baseDamage), Is.EqualTo(new[] { sourceDefense * 3.5f }).Within(.01f));

            events.Clear();
            CombatDamageProcessor.Apply(request);
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events.Count(e => Mathf.Approximately(e.baseDamage, sourceDefense * 10f)), Is.EqualTo(2));
            Assert.That(events.Count(e => Mathf.Approximately(e.baseDamage, sourceDefense * 3.5f)), Is.EqualTo(1));
            Assert.That(far.currentHP, Is.EqualTo(10000f));

            target.RuntimeEffects.RemoveEffectStacks("cg.captured", int.MaxValue);
            events.Clear();
            CombatDamageProcessor.Apply(request);
            Assert.That(events.Select(e => e.baseDamage), Is.EqualTo(new[] { sourceDefense * 3.5f }).Within(.01f));
        }
        finally { CombatEventHub.Shared.DamageResolved -= capture; }
    }

    private EnemyController Enemy(string name)
    {
        var host = Keep(new GameObject(name)); var enemy = EnemyLifecycleTestUtility.AddActive(host);
        enemy.maxHP = enemy.currentHP = 10000f; enemy.defense = enemy.damageRes = 0f; return enemy;
    }
}
#endif
