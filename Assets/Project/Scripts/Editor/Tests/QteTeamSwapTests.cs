#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class QteTeamSwapTests
{
    private readonly List<Object> _objects = new List<Object>();
    private GameObject _owner;
    private PlayerCharacter _player;
    private PlayerCombat _combat;
    private PlayerMovement _movement;
    private PlayerStateMachine _fsm;
    private TeamLoader _team;
    private CharacterObject _main;
    private CharacterObject _cg;
    private EnemyController _enemy;
    private QteTeamSwapController Qte => _team.QteSwaps;

    private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string method)
        => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    [SetUp]
    public void Setup()
    {
        _main = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset")));
        _cg = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset")));
        _main.actionResolverTable = Keep(Object.Instantiate(_main.actionResolverTable));
        _cg.actionResolverTable = Keep(Object.Instantiate(_cg.actionResolverTable));
        _owner = Keep(new GameObject("QTE Test Player"));
        _owner.SetActive(false);
        _owner.transform.position = new Vector3(20000f, 20000f, 0f);
        _player = _owner.AddComponent<PlayerCharacter>();
        _player.characterObject = _main;
        _combat = _owner.AddComponent<PlayerCombat>();
        _movement = _owner.AddComponent<PlayerMovement>();
        _movement.profile = _main.movementProfile;
        _movement.dashData = _main.DashData;
        _fsm = _owner.AddComponent<PlayerStateMachine>();
        _owner.AddComponent<BoxCollider2D>().size = new Vector2(1f, 2f);
        Transform foot = new GameObject("GroundCheck").transform;
        foot.SetParent(_owner.transform, false);
        foot.localPosition = Vector3.down;
        Set(_movement, "groundCheck", foot);
        _player.Combat = _combat;
        _player.Movement = _movement;
        _player.StateMachine = _fsm;
        _combat.playerCharacter = _player;
        _combat.movement = _movement;
        _combat.enemyLayerMask = LayerMask.GetMask("Enemy");
        _fsm.playerCharacter = _player;
        _fsm.showCombatLogs = _fsm.showFrameDataLogs = _fsm.showFsmLogs = false;
        _owner.SetActive(true);
        // EditMode fixtures do not receive the normal player-loop lifecycle.
        Call(_combat, "Awake");
        Call(_movement, "Awake");
        Call(_fsm, "Awake");
        _player.InitializeHP();
        _fsm.ChangeState(_fsm.IdleState);

        var inventory = Keep(Object.Instantiate(Resources.Load<PlayerRunInventory>("InventorySO/PlayerRunInventory")));
        inventory.ownedItems.Clear();
        foreach (CharacterObject character in new[] { _main, _cg })
        {
            var item = Keep(ScriptableObject.CreateInstance<CharacterItem>());
            item.id = "qte-test-" + character.characterId;
            item.characterObjectData = character;
            inventory.ownedItems.Add(item);
        }
        inventory.DeployTeam(new[] { _main, _cg }, 0);
        var host = Keep(new GameObject("QTE Test Team"));
        host.SetActive(false);
        _team = host.AddComponent<TeamLoader>();
        _team.playerCharacter = _player;
        Set(_team, "runInventory", inventory);
        host.SetActive(true);
        Call(_team, "Awake");
        Call(Qte, "Awake");
        Call(Qte, "OnEnable");
        _fsm.teamLoader = _team;
        _team.switchCooldownDuration = 0f;
        _team.GetTeamSnapshot();

        var floor = Keep(new GameObject("QTE Test Floor", typeof(BoxCollider2D)));
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = new Vector3(20000f, 19998.5f, 0f);
        floor.GetComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
        var enemyGo = Keep(new GameObject("QTE Test Enemy", typeof(BoxCollider2D), typeof(EnemyController)));
        enemyGo.layer = LayerMask.NameToLayer("Enemy");
        enemyGo.transform.position = _owner.transform.position + Vector3.right * 3f;
        _enemy = enemyGo.GetComponent<EnemyController>();
        Set(_enemy, "lifecycleState", EnemyLifecycleStateId.Active);
        Set(_enemy, "spriteRenderers", Array.Empty<SpriteRenderer>());
        Set(_enemy, "originalColors", Array.Empty<Color>());
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f;
        for (int i = _objects.Count - 1; i >= 0; i--)
            if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear();
        foreach (var clone in Object.FindObjectsByType<PlayerTeamSwitchState>(FindObjectsSortMode.None))
            if (clone != null && clone.transform.position.x > 19000f) Object.DestroyImmediate(clone.gameObject);
    }

    private CombatExecutionContext Execute(bool intro = false)
    {
        Assert.That(_main.qteSwap.TryResolveIntro(_main.actionResolverTable, out CombatAction action, out AttackStepData step), Is.True);
        _combat.BeginResolvedAction(false, action);
        _combat.ExecuteStep(step, 1, false, AttackCategory.Normal, action.actionName, action.actionId, false, intro);
        return _combat.CurrentExecution;
    }

    private void Publish(CombatExecutionContext execution, float damage = 10f,
        DamageDelivery delivery = DamageDelivery.Primary, string action = "main.dash.dimension_cut",
        CombatDamageTag tags = CombatDamageTag.None)
    {
        CombatEventHub.Shared.Publish(new DamageResolvedEvent(
            new DamageProvenance(0, 0, 0, 0, null, tags), new DamageDescriptor(delivery, ElementType.None),
            DamageProcPolicy.PublishResolvedEvent, _player.characterObject, null, _enemy,
            AttackCategory.Normal, action, 10f, damage, false, false, _enemy.transform.position, execution));
    }

    [Test]
    public void AuthoredProfilesReferenceActualAttacksWithoutMutatingTheirNextNode()
    {
        Assert.That(_main.qteSwap.TryResolveIntro(_main.actionResolverTable, out CombatAction cut, out AttackStepData mainStep), Is.True);
        Assert.That(cut.actionId, Is.EqualTo(MainLightningPlungeAuthoring.ActionId));
        Assert.That(mainStep.startupFrames, Is.EqualTo(2));
        Assert.That(mainStep.animationClip.name, Is.EqualTo("main_qte_lightning_plunge_actor"));
        Assert.That(_cg.qteSwap.TryResolveIntro(_cg.actionResolverTable, out CombatAction normal, out AttackStepData cgStep), Is.True);
        Assert.That(cgStep.animationClip.name, Is.EqualTo("cg_attack4"));
        Assert.That(cgStep.hitData.hitboxes.Count, Is.EqualTo(6));
        var resolver = new ActionResolver();
        resolver.BeginExplicitStep(normal, cgStep, true);
        Assert.That(resolver.GetCurrentStepData(), Is.SameAs(cgStep));
        Assert.That(resolver.IsLastStep(), Is.True);
        Assert.That(_cg.qteSwap.MatchesTrigger("cg.skill.ex"), Is.True);
    }

    [Test]
    public void SuccessfulHitRefreshesOneFiveSecondWindow()
    {
        var execution = Execute();
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.False);
        Publish(execution);
        Assert.That(execution.HasConfirmedHit, Is.True);
        Assert.That(Qte.TryGetWindow(_cg, out float remaining, out float total), Is.True);
        Assert.That(remaining, Is.EqualTo(5f).Within(0.02f));
        Set(Qte, "_expiresAt", Time.time + 1f);
        Publish(execution);
        Assert.That(Qte.TryGetWindow(_cg, out remaining, out total), Is.True);
        Assert.That(remaining, Is.EqualTo(5f).Within(0.02f));
        Assert.That(total, Is.EqualTo(5f));
        Set(Qte, "_expiresAt", Time.time - 1f);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.False);
    }

    [TestCase(DamageDelivery.Primary, 0f, CombatDamageTag.None)]
    [TestCase(DamageDelivery.Dot, 10f, CombatDamageTag.None)]
    [TestCase(DamageDelivery.Additional, 10f, CombatDamageTag.None)]
    [TestCase(DamageDelivery.Primary, 10f, CombatDamageTag.Artifact)]
    public void UnqualifiedDamageCannotArm(DamageDelivery delivery, float damage, CombatDamageTag tags)
    {
        Publish(Execute(), damage, delivery, tags: tags);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.False);
    }

    [Test]
    public void CloneIntroUnrelatedActionAndPreviousFieldSessionCannotArm()
    {
        Publish(null);
        Publish(Execute(true));
        Publish(Execute(), action: "main.skill.dark_chain");
        var previous = Execute();
        _combat.InvalidateFieldExecutions();
        Publish(previous);
        Assert.That(previous.IsOnField, Is.False);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.False);
    }

    [Test]
    public void GroundedIntroPlacementUsesOutgoingOffsetAndMirrors()
    {
        Publish(Execute());
        Assert.That(Qte.TryPrepare(1, out _, out _, out _, out Vector2 position, out int facing), Is.True);
        Assert.That(position.x, Is.EqualTo(_owner.transform.position.x - _cg.qteSwap.entryOffset).Within(0.03f));
        Assert.That(facing, Is.EqualTo(1));
        _enemy.transform.position = _owner.transform.position - Vector3.right * 3f;
        _movement.SnapFacingDirection(-1);
        Physics2D.SyncTransforms();
        Assert.That(Qte.TryPrepare(1, out _, out _, out _, out position, out facing), Is.True);
        Assert.That(position.x, Is.EqualTo(_owner.transform.position.x + _cg.qteSwap.entryOffset).Within(0.03f));
        Assert.That(facing, Is.EqualTo(-1));
    }

    [Test]
    public void EntryUsesOutgoingFacingEvenWhenEnemyIsBehindAfterDashThrough()
    {
        _movement.SnapFacingDirection(1);
        _enemy.transform.position = _owner.transform.position - Vector3.right * 3f;
        Physics2D.SyncTransforms();
        Publish(Execute());
        _movement.SnapFacingDirection(1);
        Assert.That(Qte.TryPrepare(1, out _, out _, out _, out Vector2 position, out int facing), Is.True);
        Assert.That(position.x, Is.EqualTo(_owner.transform.position.x - _cg.qteSwap.entryOffset).Within(0.03f));
        Assert.That(facing, Is.EqualTo(-1));
    }

    [Test]
    public void AcceptedDamagePipelineCarriesExecutionAndArmsWindow()
    {
        CombatExecutionContext execution = Execute();
        _enemy.currentHP = _enemy.maxHP = 100f;
        var request = new DamageRequest
        {
            target = _enemy,
            sourceCharacter = _main,
            sourceCombat = _combat,
            sourceStats = _player.Stats,
            execution = execution,
            actionId = "main.dash.dimension_cut",
            baseDamage = 10f,
            attackerPosition = _owner.transform.position,
            hitDirection = Vector2.right,
            category = AttackCategory.Normal,
            descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None, DamageCriticalPolicy.Prevent),
            hasExplicitProcPolicy = true,
            procPolicy = DamageProcPolicy.PublishResolvedEvent
        };
        DamageResult result = CombatDamageProcessor.Apply(request);
        Assert.That(result.Damage, Is.GreaterThan(0));
        Assert.That(execution.HasConfirmedHit, Is.True);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.True);
    }

    [Test]
    public void MainIntroCompletionReturnsToLocomotionWithoutAttack5OrResourceCost()
    {
        _main.qteSwap.TryResolveIntro(_main.actionResolverTable, out CombatAction action, out AttackStepData step);
        _fsm.QteIntroState.Prepare(action, step, _enemy);
        _fsm.ChangeState(_fsm.QteIntroState);
        typeof(PlayerAttackState).GetMethod("OnActionComplete", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_fsm.QteIntroState, null);
        Assert.That(_fsm.CurrentStateId, Is.EqualTo(PlayerStateId.Idle));
        Assert.That(_combat.IsAttacking(), Is.False);
    }

    [Test]
    public void PreparedEntryBypassesSwapCooldownAndRestartsIt()
    {
        Publish(Execute());
        _team.switchCooldownDuration = 1f;
        Set(_team, "lastSwitchTime", Time.time);
        Assert.That(_team.CanSwitchToIndex(1), Is.False);
        Assert.That(_fsm.CanSwitchCharacter(), Is.False);
        Assert.That(_fsm.CanSwitchCharacterTo(1), Is.True);
        Assert.That(_team.SwitchToIndex(1), Is.True);
        Assert.That(_fsm.CurrentStateId, Is.EqualTo(PlayerStateId.QteIntro));
        Assert.That(_combat.GetCurrentStepData().stepName, Is.EqualTo("Attack4"));
        Assert.That(_combat.CurrentExecution.isQteIntro, Is.True);
        Assert.That(_team.RemainingCooldown, Is.GreaterThan(0.9f));
        Assert.That(Qte.TryGetWindow(_main, out _, out _), Is.False);
    }

    [Test]
    public void OrdinarySwapStillRequiresCooldown()
    {
        _team.switchCooldownDuration = 1f;
        Set(_team, "lastSwitchTime", Time.time);
        Assert.That(_fsm.CanSwitchCharacterTo(1), Is.False);
        Assert.That(_team.SwitchToIndex(1), Is.False);
        Assert.That(_team.CurrentCharacter, Is.SameAs(_main));
    }

    [Test]
    public void MainIntroDoesNotRequireCachedCutAndHasOriginalDamageBudget()
    {
        Assert.That(_main.qteSwap.TryResolveIntro(_main.actionResolverTable, out CombatAction action, out AttackStepData step), Is.True);
        _combat.ResetCombo();
        _fsm.QteIntroState.Prepare(action, step, _enemy);
        _fsm.ChangeState(_fsm.QteIntroState);
        Assert.That(_combat.CurrentActionId, Is.EqualTo(MainLightningPlungeAuthoring.ActionId));
        Assert.That(_combat.CurrentActionDamageMultiplier, Is.EqualTo(action.actionDamageMultiplier));
        Assert.That(_combat.CurrentExecution.isQteIntro, Is.True);
        Assert.That(_combat.RuntimeState == null || _combat.RuntimeState.GetCounter("main.shadow.cached_cuts") == 0, Is.True);
    }

    [Test]
    public void MissingGroundRejectsPreparedEntry()
    {
        foreach (Object candidate in _objects)
            if (candidate is GameObject go && go.name == "QTE Test Floor") go.SetActive(false);
        Physics2D.SyncTransforms();
        Publish(Execute());
        Assert.That(Qte.TryPrepare(1, out _, out _, out _, out _, out _), Is.False);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.True);
    }

    [Test]
    public void PlungeChecksDestinationInsteadOfOutgoingGroundCorridor()
    {
        foreach (Object candidate in _objects)
            if (candidate is GameObject go && go.name == "QTE Test Floor")
                go.GetComponent<BoxCollider2D>().size = new Vector2(2f, 1f);
        var platform = Keep(new GameObject("QTE destination platform", typeof(BoxCollider2D)));
        platform.layer = LayerMask.NameToLayer("Ground");
        platform.transform.position = _owner.transform.position + new Vector3(6f, -2.5f, 0f);
        platform.GetComponent<BoxCollider2D>().size = new Vector2(4f, 1f);
        Vector2 target = (Vector2)_owner.transform.position + new Vector2(6f, -1f);
        Physics2D.SyncTransforms();
        Assert.That(_movement.TryFindQteGroundPosition(target.x, out _), Is.False);
        Assert.That(_movement.TryFindQtePlungePlacement(target, new Vector2(0f, 6f), 1, out var entry), Is.True);
        Assert.That(entry.support, Is.SameAs(platform.GetComponent<BoxCollider2D>()));
        Assert.That(entry.landingBodyPosition.x, Is.EqualTo(target.x));
        platform.SetActive(false);
        Physics2D.SyncTransforms();
        Assert.That(_movement.TryFindQtePlungePlacement(target, new Vector2(0f, 6f), 1, out _), Is.False);
    }

    [Test]
    public void HitstunAndPauseCannotUsePreparedEntry()
    {
        Publish(Execute());
        Time.timeScale = 0f;
        Assert.That(Qte.CanHandoffTo(1), Is.False);
        Time.timeScale = 1f;
        _fsm.ChangeState(_fsm.HitstunState);
        Assert.That(Qte.CanHandoffTo(1), Is.False);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.True);
    }

    [Test]
    public void IntroSlowMotionHasEditableStrongHandoffDefaults()
    {
        Assert.That(Qte.introTimeScale, Is.EqualTo(0.08f));
        Assert.That(Qte.introSlowMotionSeconds, Is.EqualTo(0.24f));
        Qte.introSlowMotionSeconds = 0f;
        Qte.PlayIntroHandoff();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [Test]
    public void PreparedQteUsesTheOrdinaryTeamSwapPolicy()
    {
        _main.qteSwap.TryResolveIntro(_main.actionResolverTable, out CombatAction action, out _);
        var step = new AttackStepData { startupFrames = 10, activeFrames = 20, recoveryFrames = 10 };
        _fsm.ChangeState(_fsm.AttackState);
        _combat.BeginResolvedAction(false, action);
        _combat.ExecuteStep(step, 1, false, AttackCategory.Normal, action.actionName, action.actionId);
        typeof(FrameDataPlayerState).GetMethod("StartAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_fsm.AttackState, null);
        var phase = typeof(FrameDataPlayerState).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);
        var frame = typeof(FrameDataPlayerState).GetField("currentFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        phase.SetValue(_fsm.AttackState, ActionPhase.Active); frame.SetValue(_fsm.AttackState, 15);
        Publish(_combat.CurrentExecution);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.True);
        Assert.That(_fsm.CanSwitchCharacter(), Is.False);
        Assert.That(Qte.CanUsePreparedTransition(), Is.False);
        Assert.That(_fsm.CanSwitchCharacterTo(1), Is.False);
        phase.SetValue(_fsm.AttackState, ActionPhase.Recovery); frame.SetValue(_fsm.AttackState, 31);
        Assert.That(_fsm.CanSwitchCharacter(), Is.True);
        Assert.That(Qte.CanUsePreparedTransition(), Is.True);
        Assert.That(_fsm.CanSwitchCharacterTo(1), Is.True);
        step.autoChainToNext = true;
        Assert.That(_fsm.CanSwitchCharacter(), Is.False);
        Assert.That(Qte.CanUsePreparedTransition(), Is.False);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.True);
    }

    [Test]
    public void HealthAndStatRefreshDoNotConsumeOrRestartPreparedWindow()
    {
        Publish(Execute());
        float expiry = (float)typeof(QteTeamSwapController).GetField("_expiresAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Qte);
        _team.SetCharacterCurrentHP(_main, _team.GetCharacterMaxHP(_main) * 0.9f);
        typeof(TeamLoader).GetMethod("RefreshRuntimeStatsOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_team, new object[] { _main });
        Call(_team, "NotifyCurrentCharacterChanged");
        Assert.That(Qte.TryGetWindow(_cg, out float remaining, out float total), Is.True);
        Assert.That(total, Is.EqualTo(5f));
        Assert.That(remaining, Is.GreaterThan(4.9f));
        Assert.That((float)typeof(QteTeamSwapController).GetField("_expiresAt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Qte), Is.EqualTo(expiry));
        Set(Qte, "_expiresAt", Time.time - 0.01f);
        Assert.That(Qte.TryGetWindow(_cg, out _, out _), Is.False);
    }

    [TestCase("cg.skill")]
    [TestCase("cg.skill.ex")]
    public void CameraGirlAcceptedSkillHitRunsMainIntroDuringSwapCooldown(string actionId)
    {
        var visual = new GameObject("QTE Test Actor", typeof(SpriteRenderer));
        visual.transform.SetParent(_owner.transform, false);
        var animator = visual.AddComponent<PlayerAnimator>();
        animator.anim = visual.GetComponent<Animator>();
        animator.anim.runtimeAnimatorController = _main.metaData.animatorController;
        animator.movement = _movement;
        animator.combat = _combat;
        Call(animator, "Awake");
        _player.Animator = animator;
        Assert.That(_team.SwitchToIndex(1), Is.True);
        SkillAction skill = _cg.actionResolverTable.skillActions.Find(a => a.actionId == actionId);
        var step = skill.comboTree.rootNode.stepData;
        _combat.BeginResolvedAction(false, skill);
        _combat.ExecuteStep(step, 1, false, AttackCategory.Skill, skill.actionName, skill.actionId);
        _enemy.currentHP = _enemy.maxHP = 1000f;
        var request = new DamageRequest { target = _enemy, sourceCharacter = _cg, sourceCombat = _combat,
            sourceStats = _player.Stats, execution = _combat.CurrentExecution, actionId = skill.actionId,
            category = AttackCategory.Skill, baseDamage = 10f, attackerPosition = _owner.transform.position,
            hitDirection = Vector2.right, descriptor = new DamageDescriptor(DamageDelivery.Primary, ElementType.None, DamageCriticalPolicy.Prevent),
            hasExplicitProcPolicy = true, procPolicy = DamageProcPolicy.PublishResolvedEvent };
        Assert.That(CombatDamageProcessor.Apply(request).Damage, Is.GreaterThan(0f));
        Assert.That(Qte.TryGetWindow(_main, out _, out float total), Is.True);
        typeof(TeamLoader).GetMethod("RefreshRuntimeStatsOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_team, new object[] { _cg });
        Assert.That(Qte.TryGetWindow(_main, out _, out _), Is.True);
        Assert.That(total, Is.EqualTo(5f));
        _fsm.ChangeState(_fsm.IdleState);
        _team.switchCooldownDuration = 10f;
        Set(_team, "lastSwitchTime", Time.time);
        Assert.That(_fsm.CanSwitchCharacterTo(0), Is.True);
        Assert.That(_team.SwitchToIndex(0), Is.True);
        Assert.That(_fsm.CurrentStateId, Is.EqualTo(PlayerStateId.QteIntro));
        Assert.That(_combat.CurrentActionId, Is.EqualTo(MainLightningPlungeAuthoring.ActionId));
        Assert.That(_combat.GetCurrentStepData().stepName, Is.EqualTo("LightningPlunge"));
        Assert.That(_movement.HasCapturedWorldMotion, Is.True);
        for (int i = 0; i < 8; i++) _fsm.QteIntroState.FixedUpdate();
        Assert.That(_fsm.CurrentStateId, Is.EqualTo(PlayerStateId.QteIntro));
        Assert.That(_movement.IsPhysicallyGrounded(), Is.True);
        Assert.That(visual.GetComponent<SpriteRenderer>().forceRenderingOff, Is.False);
    }

    [Test]
    public void IntroDoesNotAcceptCombatCancelBeforeItsAuthoredBoundary()
    {
        _main.qteSwap.TryResolveIntro(_main.actionResolverTable, out CombatAction action, out AttackStepData step);
        _fsm.QteIntroState.Prepare(action, step, _enemy);
        _fsm.ChangeState(_fsm.QteIntroState);
        var phase = typeof(FrameDataPlayerState).GetField("currentPhase", BindingFlags.Instance | BindingFlags.NonPublic);
        phase.SetValue(_fsm.QteIntroState, ActionPhase.Active);
        _fsm.QteIntroState.HandleAttackInput();
        Assert.That(_fsm.CurrentState, Is.SameAs(_fsm.QteIntroState));
        Assert.That(_fsm.QteIntroState.CanBeInterruptedBy(ActionCategory.Dash), Is.False);
        phase.SetValue(_fsm.QteIntroState, ActionPhase.Cancel);
        Assert.That(_fsm.QteIntroState.CanBeInterruptedBy(ActionCategory.BasicAttack), Is.True);
    }

    [Test]
    public void WallClampsEntryAndGapRejectsIt()
    {
        var wall = Keep(new GameObject("QTE Entry Wall", typeof(BoxCollider2D)));
        wall.layer = LayerMask.NameToLayer("Ground");
        wall.transform.position = _owner.transform.position + Vector3.left * 1.5f;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 6f);
        Physics2D.SyncTransforms();
        Assert.That(_movement.TryFindQteGroundPosition(_owner.transform.position.x - 3f, out Vector2 clamped), Is.True);
        Assert.That(clamped.x, Is.GreaterThan(_owner.transform.position.x - 1f));
        wall.SetActive(false);
        foreach (Object candidate in _objects)
            if (candidate is GameObject go && go.name == "QTE Test Floor") go.GetComponent<BoxCollider2D>().size = new Vector2(1f, 1f);
        Physics2D.SyncTransforms();
        Assert.That(_movement.TryFindQteGroundPosition(_owner.transform.position.x - 3f, out _), Is.False);
    }

    [Test]
    public void HitHistoryTransferPreventsCloneReplayingAcceptedTarget()
    {
        var originalObject = Keep(new GameObject("Original hitbox", typeof(HitBoxDamage)));
        var cloneObject = Keep(new GameObject("Clone hitbox", typeof(HitBoxDamage)));
        HitBoxDamage original = originalObject.GetComponent<HitBoxDamage>();
        HitBoxDamage clone = cloneObject.GetComponent<HitBoxDamage>();
        var history = (HashSet<EnemyController>)typeof(HitBoxDamage).GetField("hitEnemiesThisActivation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(original);
        history.Add(_enemy);
        original.CopyHitHistoryTo(clone);
        var clonedHistory = (HashSet<EnemyController>)typeof(HitBoxDamage).GetField("hitEnemiesThisActivation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clone);
        Assert.That(clonedHistory.Contains(_enemy), Is.True);
        clone.ProcessHit(_enemy);
        Assert.That(clonedHistory.Count, Is.EqualTo(1));
    }

    [Test]
    public void OutgoingClonePreservesResolvedTargetMotionAndLongActionLifetime()
    {
        var motion = new MotionKeyframe { taskType = MotionTaskType.DashThroughTarget, startFrame = 3, endFrame = 11, position = Vector3.right * 6f };
        Set(_movement, "resolvedTargetMotion", motion);
        Set(_movement, "resolvedTargetMotionDelta", new Vector2(10f, 0f));
        var cloneGo = Keep(new GameObject("QTE Long Action Clone", typeof(PlayerTeamSwitchState)));
        var clone = cloneGo.GetComponent<PlayerTeamSwitchState>();
        var step = new AttackStepData { startupFrames = 3, activeFrames = 300, recoveryFrames = 6,
            motionData = new MotionFrameData { keyframes = new List<MotionKeyframe> { motion } } };
        clone.Initialize(step, 5, 1, _player.Stats, 1f, 1f, 1f, ActionDamageSourceStat.Attack, 0f, null,
            null, _combat, _main, "main.dash.dimension_cut", "Dimension Cut", null, null, AttackCategory.Normal);
        float offset = (float)typeof(PlayerTeamSwitchState).GetMethod("EvaluateAuthoredMotionOffsetX", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(clone, new object[] { 11 });
        float lifetime = (float)typeof(PlayerTeamSwitchState).GetField("maximumCloneLifetime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clone);
        Assert.That(offset, Is.EqualTo(10f).Within(0.001f));
        Assert.That(lifetime, Is.GreaterThan(5f));
    }

    [Test]
    public void PortraitShowsReadyPulseAndClearsAfterEntry()
    {
        Publish(Execute());
        var canvasGo = Keep(new GameObject("QTE Portrait Preview", typeof(Canvas)));
        var cameraGo = Keep(new GameObject("QTE Preview Camera", typeof(Camera)));
        Camera camera = cameraGo.GetComponent<Camera>();
        cameraGo.transform.position = new Vector3(25000f, 25000f, -10f);
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        var hudGo = Keep(new GameObject("QTE Preview HUD", typeof(TeamSwitchHUD)));
        var hud = hudGo.GetComponent<TeamSwitchHUD>();
        hud.teamLoader = _team;
        hud.stateMachine = _fsm;
        GameObject slotGo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/Hero_TeamSwitchPanel.prefab"), canvasGo.transform);
        var slot = slotGo.GetComponent<CharacterSwitchSlot>();
        Assert.That(slot, Is.Not.Null);
        foreach (Animator animator in slotGo.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        var rect = slotGo.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        slot.Setup(_cg, 1, hud);
        Call(slot, "Update");
        Assert.That(slot.qteReadyOverlay.gameObject.activeSelf, Is.True);
        Assert.That(slot.qteWindowFill.gameObject.activeSelf, Is.True);
        Assert.That(slot.qteReadyOverlay.raycastTarget, Is.False);
        _team.switchCooldownDuration = 1f;
        Set(_team, "lastSwitchTime", Time.time);
        Call(slot, "Update");
        Assert.That(_fsm.CanSwitchCharacterTo(1), Is.True);
        Assert.That(slot.qteReadyOverlay.gameObject.activeSelf, Is.True);
        Assert.That(slot.qteWindowFill.gameObject.activeSelf, Is.True);
        _team.switchCooldownDuration = 0f;
        var target = Keep(new RenderTexture(520, 220, 24));
        var capture = Keep(new Texture2D(520, 220, TextureFormat.RGB24, false));
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, 520, 220), 0, 0);
            capture.Apply();
            System.IO.File.WriteAllBytes("Temp/qte-ready-portrait.png", capture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
        }
        Assert.That(_team.SwitchToIndex(1), Is.True);
        Call(slot, "Update");
        Assert.That(slot.qteReadyOverlay.gameObject.activeSelf, Is.False);
        Assert.That(slot.qteWindowFill.gameObject.activeSelf, Is.False);
    }
}
#endif
