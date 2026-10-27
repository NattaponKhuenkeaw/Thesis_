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
    [TestCase(AttackCategory.Normal, "Attack6", 6)]
    [TestCase(AttackCategory.Skill, "Skill", 8)]
    [TestCase(AttackCategory.Ultimate, "Ult", 10)]
    [TestCase(AttackCategory.Normal, "DimensionCut", 0)]
    public void HitGainComesFromAuthoredRules(AttackCategory category, string step, int expected)
    {
        passive.NotifyHitDealt(category, step == "DimensionCut" ? "Dimension Cut" : "Ground", new AttackStepData { stepName = step }, null);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(expected));
    }

    [Test]
    public void MeterCapsWithoutBankingOverflowAndSpendsWholeBox()
    {
        for (int i = 0; i < 20; i++)
            passive.NotifyHitDealt(AttackCategory.Normal, "Ground", new AttackStepData { stepName = "Attack3" }, null);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(40));
        Assert.That(passive.RuntimeState.ConsumeCounter("main.shadow", 20), Is.True);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(20));
        passive.RuntimeState.SetCounter("main.shadow", 19);
        Assert.That(passive.RuntimeState.ConsumeCounter("main.shadow", 20), Is.False);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(19));
    }

    [TestCase(0, 2)]
    [TestCase(1, 3)]
    [TestCase(2, 3)]
    [TestCase(3, 3)]
    [TestCase(4, 5)]
    public void CapacityUsesUnlockedPotentialNodes(int level, int expected)
    {
        progression.SetPotentialLevel(character, PotentialTrack.Passive, level);
        Assert.That(passive.GetMeterBoxCount(), Is.EqualTo(expected));
    }

    [TestCase(0, "main.dash.dimension_cut", 0.1f, false, 50f)]
    [TestCase(1, "main.dash.dimension_cut", 0.1f, true, 150f)]
    [TestCase(1, "main.dash.dimension_cut", 0.2f, false, 50f)]
    [TestCase(1, "other.normal.action", 0.1f, false, 50f)]
    public void CutCriticalBonusAndDefenseBypassAreScopedPerHit(int level, string actionId,
        float roll, bool critical, float damage)
    {
        progression.SetPotentialLevel(character, PotentialTrack.Passive, level);
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
        progression.SetPotentialLevel(character, PotentialTrack.Passive, 1);
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

    [TestCase(2, "main.dash.dimension_cut", 100f, 100f)]
    [TestCase(3, "main.dash.dimension_cut", 100f, 108f)]
    [TestCase(3, "main.dash.dimension_cut", 0f, 100f)]
    [TestCase(3, "ordinary.attack", 100f, 100f)]
    public void CutHealingUsesResolvedDamageAtPassiveThree(int level, string actionId, float dealt, float expectedHP)
    {
        progression.SetPotentialLevel(character, PotentialTrack.Passive, level);
        var player = owner.GetComponent<PlayerCharacter>();
        player.InitializeHP(100f);
        passive.NotifyHitDealt(AttackCategory.Normal, actionId, "Dimension Cut",
            new AttackStepData { stepName = "DimensionCut" }, null, ElementType.None, false, dealt, false);
        Assert.That(player.CurrentHP, Is.EqualTo(expectedHP).Within(0.01f));
    }

    [TestCase("ProjectileData_main_void_area")]
    [TestCase("ProjectileData_main_void_area_lv3")]
    [TestCase("ProjectileData_main_void_area_lv4")]
    public void VoidOccupancyDoublesGainAndExpiresWithoutChangingDashCost(string asset)
    {
        progression.SetPotentialLevel(character, PotentialTrack.Passive, 4);
        PlayerRunInventory inventory = CreateIsolatedTeamInventory();
        ProjectileData area = AssetDatabase.LoadAssetAtPath<ProjectileData>(
            "Assets/Project/Character/Hero/test_0_Main/" + asset + ".asset");
        RuntimeAreaEffectApplication application = area.areaEffects.Single();
        Assert.That(application.requiredNodeId, Is.EqualTo("main.passive.lv4"));
        passive.NotifyHitDealt(AttackCategory.Normal, "Ground", new AttackStepData { stepName = "Attack3" }, null);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(4));
        inventory.ApplyRuntimeEffect(character, application.recipe, character, character.stats);
        passive.NotifyHitDealt(AttackCategory.Normal, "Ground", new AttackStepData { stepName = "Attack3" }, null);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(12));
        passive.RuntimeState.SetCounter("main.shadow", 20);
        passive.NotifyAfterDash();
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.Zero);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(1));
        inventory.TickRuntimeEffects(0.31f);
        passive.NotifyHitDealt(AttackCategory.Normal, "Ground", new AttackStepData { stepName = "Attack3" }, null);
        Assert.That(passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(4));
    }

    [Test]
    public void SkillFourVulnerabilityReplacesBaseInsteadOfStackingToFiftyFive()
    {
        var receiver = owner.AddComponent<RuntimeEffectReceiver>();
        RuntimeEffectRecipe baseRecipe = character.kitDefinition.baseSkillNode.runtimeEffects.Single().recipe;
        RuntimeEffectRecipe enhanced = character.kitDefinition.GetNodeById("main.skill.lv4").runtimeEffects
            .Single(e => e.recipe.policy.appliesOn == RuntimeEffectApplyTarget.Enemy).recipe;
        for (int hit = 0; hit < 3; hit++)
        {
            receiver.ApplyEffect(baseRecipe, character, character.stats);
            receiver.ApplyEffect(enhanced, character, character.stats);
            Assert.That(receiver.ModifyIncomingDamage(new RuntimeEffectDamageContext(), 100f), Is.EqualTo(140f).Within(0.01f));
            Assert.That(receiver.ActiveEffects.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void SkillHitsRefreshAlliesCritForSevenSecondsWithoutStacking()
    {
        progression.SetPotentialLevel(character, PotentialTrack.Skill, 4);
        PlayerRunInventory inventory = CreateIsolatedTeamInventory();
        for (int hit = 0; hit < 3; hit++)
            passive.NotifyHitDealt(AttackCategory.Skill, "main.skill.dark_chain", "Dark Chain",
                new AttackStepData { stepName = "Skill" }, null, ElementType.None, false, 100f, false);
        foreach (CharacterObject member in new[] { character, testAlly })
        {
            RuntimeEffectInstance instance = inventory.GetActiveRuntimeEffects(member).Single();
            Assert.That(instance.recipe.policy.appliesOn, Is.EqualTo(RuntimeEffectApplyTarget.Allies));
            var stats = new CharacterStats { critRate = 5f };
            instance.definition.ModifyStats(new RuntimeEffectContext { instance = instance }, stats);
            Assert.That(stats.critRate, Is.EqualTo(20f));
            Assert.That(instance.remainingSeconds, Is.EqualTo(7f));
        }
        inventory.TickRuntimeEffects(7.01f);
        Assert.That(inventory.GetActiveRuntimeEffects(character), Is.Empty);
        Assert.That(inventory.GetActiveRuntimeEffects(testAlly), Is.Empty);
    }

    [Test]
    public void LockingPotentialClampsExistingMeter()
    {
        progression.SetPotentialLevel(character, PotentialTrack.Passive, 4);
        passive.RuntimeState.SetCounter("main.shadow", 100);
        progression.SetPotentialLevel(character, PotentialTrack.Passive, 0);
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
    [TestCase(1.9f, -99f, 1, -1f)]
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
