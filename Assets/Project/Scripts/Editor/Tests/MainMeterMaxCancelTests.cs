#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public class MainMeterMaxCancelTests
{
    private GameObject _owner;
    private CharacterObject _character;
    private ActionResolverTable _table;
    private PlayerCharacter _player;
    private PlayerCombat _combat;
    private CharacterPassiveController _passive;
    private PlayerStateMachine _fsm;
    private GameObject _inputOwner;
    private InputActionAsset _inputActions;
    private Keyboard _keyboard;
    private bool _previousEditInputUpdates;
    private bool _changedEditInputUpdates;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void Setup()
    {
        _inputOwner = null; _inputActions = null; _keyboard = null; _changedEditInputUpdates = false;
        _character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset"));
        _table = Object.Instantiate(_character.actionResolverTable);
        _character.actionResolverTable = _table;
        _owner = new GameObject("Main meter/max cancel fixture");
        _owner.SetActive(false);
        _player = _owner.AddComponent<PlayerCharacter>();
        _player.characterObject = _character;
        _combat = _owner.AddComponent<PlayerCombat>(); _combat.playerCharacter = _player; _player.Combat = _combat;
        _passive = _owner.AddComponent<CharacterPassiveController>(); _passive.playerCharacter = _player;
        _player.PassiveController = _passive; _combat.passiveController = _passive;
        _fsm = _owner.AddComponent<PlayerStateMachine>(); _fsm.playerCharacter = _player; _player.StateMachine = _fsm;
        _fsm.showCombatLogs = _fsm.showFsmLogs = _fsm.showFrameDataLogs = false;
        typeof(PlayerStateMachine).GetMethod("Awake", Private).Invoke(_fsm, null);
        _player.InitializeHP();
    }

    [TearDown]
    public void Teardown()
    {
        if (_inputOwner != null) Object.DestroyImmediate(_inputOwner);
        if (_inputActions != null) { _inputActions.Disable(); Object.DestroyImmediate(_inputActions); }
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        if (_changedEditInputUpdates)
            InputSystem.settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", _previousEditInputUpdates);
        Object.DestroyImmediate(_owner); Object.DestroyImmediate(_table); Object.DestroyImmediate(_character);
    }

    [TestCase("main.ultimate.execution_breakdown", AttackCategory.Ultimate, 5)]
    [TestCase("main.ultimate.jailbreak_punisher", AttackCategory.Ultimate, 1)]
    [TestCase("main.dash.dimension_cut", AttackCategory.Normal, 1)]
    [TestCase("main.skill.dimension_slash", AttackCategory.Skill, 1)]
    [TestCase("another.ultimate", AttackCategory.Ultimate, 0)]
    public void CharacterRulesOverrideBroadCategoryAndAwardOnlyAcceptedDamage(string actionId, AttackCategory category, int expected)
    {
        var step = new AttackStepData { stepName = "AuthoredStep" };
        _passive.NotifyHitDealt(category, actionId, actionId, step, null, ElementType.None, false, 0f, false);
        Assert.That(_passive.RuntimeState.GetCounter("main.shadow"), Is.Zero);
        _passive.NotifyHitDealt(category, actionId, actionId, step, null, ElementType.None, false, 10f, false);
        Assert.That(_passive.RuntimeState.GetCounter("main.shadow"), Is.EqualTo(expected));
        Assert.That(_character.kitDefinition.basePassiveNode.counterEffects.Any(e => e.trigger == PassiveTriggerType.OnHitDealt && e.counterId == "main.shadow"), Is.False);
    }

    [Test]
    public void StableActionIdTakesPrecedenceOverCollidingDisplayName()
    {
        Assert.That(_character.combatRules.GetMeterGain(AttackCategory.Ultimate, "other.ultimate", "Ground", null), Is.Zero);
        Assert.That(_character.combatRules.GetMeterGain(AttackCategory.Skill, "main.skill.dark_chain", "Ground", null), Is.EqualTo(8));
    }

    private UltimateAction Ultimate(string id, UltimateInputTrigger trigger) => new UltimateAction
    {
        actionId = id, actionName = id, context = CombatActionContext.Both, energyCost = 0f,
        inputTrigger = trigger, cooldown = 15f, allowUltimateInterrupt = true,
        comboTree = new ComboTree { rootNode = new ComboNode { stepData = new AttackStepData
        { stepName = id, startupFrames = 2, activeFrames = 3, recoveryFrames = 20, cancelWindowStartFrame = 24 } } }
    };

    [Test]
    public void CommittedQteAddsCachedCutAndItsManualSpendUnlocksSkillWindow()
    {
        var progression = ScriptableObject.CreateInstance<Project.Progression.PlayerMetaProgression>();
        try
        {
            progression.SetPotentialLevel(_character, PotentialTrack.Passive, 4);
            _passive.SetMetaProgression(progression);
            _owner.SetActive(true); _player.InitializeHP();
            _passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 2);
            _passive.RuntimeState.SetCounter(MainDimensionSlashAuthoring.CountId, 4);
            _character.qteSwap.TryResolveIntro(_table, out CombatAction action, out AttackStepData step);
            _fsm.QteIntroState.Prepare(action, step, null);
            _fsm.ChangeState(_fsm.QteIntroState);
            Assert.That(_passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(3));
            Assert.That(_passive.RuntimeState.HasTag("main.qte.dimension_cut.ready"), Is.True);
            Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.EqualTo(4));

            _table.standaloneActions.Single(a => a.actionId == MainDimensionSlashAuthoring.CutId).requiresEnemyTarget = false;
            _fsm.ChangeState(_fsm.AttackState);
            _fsm.AttackState.OnAttackInput();
            Assert.That(_combat.CurrentActionId, Is.EqualTo(MainDimensionSlashAuthoring.CutId));
            Assert.That(_passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(2));
            Assert.That(_passive.RuntimeState.HasTag("main.qte.dimension_cut.ready"), Is.False);
            Assert.That(_passive.RuntimeState.HasTag(MainDimensionSlashAuthoring.ReadyTag), Is.True);
            Assert.That(_passive.TryGetActionAvailabilityWindow(AttackCategory.Skill, MainDimensionSlashAuthoring.ActionId,
                "Judgment Cut", out _, out float duration), Is.True);
            Assert.That(duration, Is.EqualTo(5f).Within(0.001f));
        }
        finally { Object.DestroyImmediate(progression); }
    }

    private UltimateAction StartSource()
    {
        _table.ultimateActions = new List<UltimateAction> { Ultimate("source", UltimateInputTrigger.Press), Ultimate("destination", UltimateInputTrigger.Hold) };
        _owner.SetActive(true); _player.InitializeHP();
        _fsm.ChangeState(_fsm.UltimateState);
        _fsm.UltimateState.OnUltimateInput();
        Assert.That(_combat.CurrentActionId, Is.EqualTo("source"));
        Assert.That(_combat.IsOnCooldown(AttackCategory.Ultimate, 15f, "source", "source"), Is.True);
        return _table.ultimateActions[0];
    }

    private InputHandler_Player BeginPhysicalUltimateHold()
    {
        _previousEditInputUpdates = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", Private)
            .Invoke(InputSystem.settings, new object[] { "RUN_PLAYER_UPDATES_IN_EDIT_MODE" });
        _changedEditInputUpdates = true;
        InputSystem.settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        _keyboard = InputSystem.AddDevice<Keyboard>();
        _inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = new InputActionMap("Player");
        map.AddAction("Ultimate", InputActionType.Button, "<Keyboard>/r");
        _inputActions.AddActionMap(map);
        _inputOwner = new GameObject("Buffered hold input fixture");
        _inputOwner.SetActive(false);
        var input = _inputOwner.AddComponent<PlayerInput>();
        input.actions = _inputActions;
        input.defaultActionMap = "Player";
        input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        var handler = _inputOwner.AddComponent<InputHandler_Player>();
        handler.playerInput = input; handler.stateMachine = _fsm;
        _inputOwner.SetActive(true);
        // Edit-mode fixtures do not run MonoBehaviour Start/OnEnable.
        input.currentActionMap = input.actions.FindActionMap("Player");
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.R));
        InputSystem.Update();
        Assert.That(input.actions.FindAction("Ultimate").IsPressed(), Is.True);
        typeof(InputHandler_Player).GetMethod("BeginUltimateGesture", Private).Invoke(handler, null);
        return handler;
    }

    private void ChargeHold(InputHandler_Player handler)
    {
        Assert.That((bool)typeof(InputHandler_Player).GetField("ultimatePending", Private).GetValue(handler), Is.True);
        typeof(InputHandler_Player).GetField("ultimatePressedAt", Private).SetValue(handler, Time.unscaledTime - 10f);
        typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        Assert.That(_fsm.InputBuffer.UltimateInputHeld, Is.True);
    }

    private void ExpireUltimateTimer() => typeof(ActionInputBuffer).GetField("ultimateBufferedUntil", Private)
        .SetValue(_fsm.InputBuffer, Time.time - 1f);

    [Test]
    public void PhysicalHoldChargedDuringActiveSurvivesBufferExpiryAndMaxCancelsOnceInRecovery()
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Active);
        var source = _combat.CurrentExecution;
        InputHandler_Player handler = BeginPhysicalUltimateHold();
        ChargeHold(handler);
        Assert.That(_combat.CurrentExecution, Is.SameAs(source));
        ExpireUltimateTimer();
        Assert.That(_fsm.InputBuffer.UltimateInputHeld, Is.True, "Still holding must outlive the ordinary tap buffer.");
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
        typeof(FrameDataPlayerState).GetMethod("TryConsumeAllowedBuffers", Private).Invoke(_fsm.UltimateState, null);
        Assert.That(_combat.CurrentActionId, Is.EqualTo("destination"));
        Assert.That(_owner.GetComponent<MaxCancelFeedback>().IsVisible, Is.True);
        var destination = _combat.CurrentExecution;
        typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        Assert.That(_fsm.InputBuffer.HasUltimateBuffer, Is.False, "One physical press cannot queue again after consumption.");
        Assert.That(_combat.CurrentExecution, Is.SameAs(destination));
    }

    [Test]
    public void ReleasedChargedHoldKeepsGestureOnlyForNormalBufferDuration()
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Active);
        InputHandler_Player handler = BeginPhysicalUltimateHold(); ChargeHold(handler);
        ExpireUltimateTimer();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); InputSystem.Update();
        typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        Assert.That(_fsm.InputBuffer.UltimateInputHeld, Is.True, "Release retains the charged hold briefly.");
        ExpireUltimateTimer();
        Assert.That(_fsm.InputBuffer.HasUltimateBuffer, Is.False);
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
        typeof(FrameDataPlayerState).GetMethod("TryConsumeAllowedBuffers", Private).Invoke(_fsm.UltimateState, null);
        Assert.That(_combat.CurrentActionId, Is.EqualTo("source"));
    }

    [Test]
    public void ShortReleaseBuffersTapWithoutSustainingIt()
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Active);
        InputHandler_Player handler = BeginPhysicalUltimateHold();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); InputSystem.Update();
        typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        Assert.That(_fsm.InputBuffer.HasUltimateBuffer, Is.True);
        Assert.That(_fsm.InputBuffer.UltimateInputHeld, Is.False);
        ExpireUltimateTimer();
        Assert.That(_fsm.InputBuffer.HasUltimateBuffer, Is.False);
    }

    [TestCase("focus")]
    [TestCase("disable")]
    [TestCase("character")]
    [TestCase("actionMap")]
    public void InvalidGestureClearsSustainedHold(string reason)
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Active);
        InputHandler_Player handler = BeginPhysicalUltimateHold(); ChargeHold(handler);
        if (reason == "focus") typeof(InputHandler_Player).GetMethod("OnApplicationFocus", Private).Invoke(handler, new object[] { false });
        else if (reason == "disable") typeof(InputHandler_Player).GetMethod("OnDisable", Private).Invoke(handler, null);
        else if (reason == "actionMap")
        {
            handler.playerInput.currentActionMap.Disable();
            typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        }
        else
        {
            _player.characterObject = null;
            typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        }
        Assert.That(_fsm.InputBuffer.HasUltimateBuffer, Is.False);
    }

    [Test]
    public void ConsumingAnotherInputClearsHoldWithoutRecreatingItWhileButtonRemainsDown()
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Active);
        InputHandler_Player handler = BeginPhysicalUltimateHold(); ChargeHold(handler);
        _fsm.InputBuffer.BufferDash(1f);
        Assert.That(_fsm.InputBuffer.ConsumeDash(out _), Is.True);
        typeof(InputHandler_Player).GetMethod("UpdateUltimateGesture", Private).Invoke(handler, null);
        Assert.That(_fsm.InputBuffer.HasUltimateBuffer, Is.False);
    }

    private SkillAction ExSkill() => new SkillAction
    {
        actionId = "test.ex_skill", actionName = "EX Skill", actionType = CombatActionType.Ultimate,
        context = CombatActionContext.Both, cooldown = 0f,
        comboTree = new ComboTree { rootNode = new ComboNode { stepData = new AttackStepData
        { stepName = "EX", startupFrames = 2, activeFrames = 3, recoveryFrames = 20 } } }
    };

    [Test]
    public void SkillBoundUltimateTypeKeepsSkillCategoryAndNeedsNoAuraEnergy()
    {
        SkillAction ex = ExSkill(); _table.skillActions = new List<SkillAction> { ex };
        _owner.SetActive(true); _player.InitializeHP();
        Assert.That(_player.IsActionAvailable(ex), Is.True);
        _fsm.ChangeState(_fsm.SkillState); _fsm.SkillState.OnSkillInput();
        Assert.That(_fsm.CurrentStateId, Is.EqualTo(PlayerStateId.Skill));
        Assert.That(ex.Category, Is.EqualTo(AttackCategory.Skill));
        Assert.That(_combat.CurrentExecution.actionType, Is.EqualTo(CombatActionType.Ultimate));
        Assert.That(_combat.CurrentExecution.UsesUltimateHitFlash(AttackCategory.Skill), Is.True);
        Assert.That(_owner.GetComponent<MaxCancelFeedback>(), Is.Null);
    }

    [Test]
    public void UltimateRecoveryMaxCancelsIntoSkillBoundUltimateType()
    {
        StartSource();
        _table.skillActions = new List<SkillAction> { ExSkill() };
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
        _fsm.RouteSkillInput();
        Assert.That(_fsm.CurrentStateId, Is.EqualTo(PlayerStateId.Skill));
        Assert.That(_combat.CurrentActionId, Is.EqualTo("test.ex_skill"));
        Assert.That(_owner.GetComponent<MaxCancelFeedback>().IsVisible, Is.True);
    }

    [Test]
    public void SkillBoundUltimateRecoveryMaxCancelsIntoUltimateInput()
    {
        _table.skillActions = new List<SkillAction> { ExSkill() };
        _table.ultimateActions = new List<UltimateAction> { Ultimate("destination", UltimateInputTrigger.Press) };
        _owner.SetActive(true); _player.InitializeHP();
        _fsm.ChangeState(_fsm.SkillState); _fsm.SkillState.OnSkillInput();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.SkillState, ActionPhase.Recovery);
        _fsm.RouteUltimateInput();
        Assert.That(_combat.CurrentActionId, Is.EqualTo("destination"));
        Assert.That(_owner.GetComponent<MaxCancelFeedback>().IsVisible, Is.True);
    }

    [TestCase(ActionPhase.Startup)]
    [TestCase(ActionPhase.Active)]
    public void SkillBoundUltimateTypeKeepsUltimateCommitment(ActionPhase phase)
    {
        _table.skillActions = new List<SkillAction> { ExSkill() };
        _owner.SetActive(true); _player.InitializeHP();
        _fsm.ChangeState(_fsm.SkillState); _fsm.SkillState.OnSkillInput();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.SkillState, phase);
        Assert.That(_fsm.SkillState.CanBeInterruptedBy(ActionCategory.Dash), Is.False);
        Assert.That(_fsm.SkillState.CanBeInterruptedBy(ActionCategory.Jump), Is.False);
        Assert.That(_fsm.SkillState.CanBeInterruptedBy(ActionCategory.Ultimate), Is.False);
    }

    [Test]
    public void UltimateOptOutAlsoBlocksUltimateTypeOnSkillInput()
    {
        StartSource().allowUltimateInterrupt = false;
        _table.skillActions = new List<SkillAction> { ExSkill() };
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
        var execution = _combat.CurrentExecution;
        _fsm.RouteSkillInput();
        Assert.That(_combat.CurrentExecution, Is.SameAs(execution));
        Assert.That(_owner.GetComponent<MaxCancelFeedback>(), Is.Null);
    }

    [Test]
    public void HitFlashSnapshotsTypeWithoutReclassifyingSkillDamage()
    {
        SkillAction ex = ExSkill();
        var execution = new CombatExecutionContext(_combat, _character, _combat.FieldSession, false, ex);
        ex.actionType = CombatActionType.Normal;
        Assert.That(execution.UsesUltimateHitFlash(AttackCategory.Skill), Is.True);
        Assert.That(ex.Category, Is.EqualTo(AttackCategory.Skill));
        Assert.That(new CombatExecutionContext(_combat, _character, 0, false, ex).UsesUltimateHitFlash(AttackCategory.Ultimate), Is.False);
        var hitbox = _owner.AddComponent<HitBoxDamage>();
        hitbox.ConfigurePerEnemyDamageCalculation(null, 0f, true, null, null, null, null,
            presentationType: CombatActionType.Ultimate);
        Assert.That(hitbox.UsesUltimateHitFlash, Is.True, "Off-field clones carry presentation type without on-field QTE provenance.");
    }

    [Test]
    public void MaxCancelCalloutRendersWithoutInheritingFighterFacing()
    {
        _owner.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        MaxCancelFeedback.Show(_player);
        var feedback = _owner.GetComponent<MaxCancelFeedback>();
        feedback.Sample(0.3f);
        var root = (GameObject)typeof(MaxCancelFeedback).GetField("_root", Private).GetValue(feedback);
        Assert.That(root.transform.parent, Is.Null);
        var cameraObject = new GameObject("MAX CANCEL preview", typeof(Camera));
        var target = new RenderTexture(1280, 720, 24);
        var capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .018f, .04f);
            camera.transform.position = new Vector3(50000f, 50000f, -10f); camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            capture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0); capture.Apply();
            System.IO.File.WriteAllBytes("Temp/main-max-cancel-preview.png", capture.EncodeToPNG());
            Assert.That(feedback.DisplayedText, Is.EqualTo("MAX CANCEL"));
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }
        finally
        {
            RenderTexture.active = previous; Object.DestroyImmediate(capture);
            cameraObject.GetComponent<Camera>().targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
        }
    }

    [Test]
    public void RecoveryMaxCancelStartsDifferentUltimateDespiteSourceCooldownAndShowsLabel()
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
        Assert.That(_player.IsActionAvailable(_table.ultimateActions[1]), Is.False);
        _fsm.RouteUltimateInput(held: true);
        Assert.That(_combat.CurrentActionId, Is.EqualTo("destination"));
        Assert.That(_fsm.UltimateState.CurrentPhase, Is.EqualTo(ActionPhase.None));
        Assert.That(_owner.GetComponent<MaxCancelFeedback>().IsVisible, Is.True);
        Assert.That(_owner.GetComponent<MaxCancelFeedback>().DisplayedText, Is.EqualTo("MAX CANCEL"));
    }

    [TestCase(true)] [TestCase(false)]
    public void ActionExitHandsProceduralVisualToItsOwnLifetime(bool maxCancel)
    {
        StartSource();
        var settings = _table.skillActions.Single(a => a.actionId == MainDimensionSlashAuthoring.ActionId)
            .comboTree.rootNode.stepData.eventData.entries.First(e => e.eventType == ActionEventType.ProceduralSlashField).slashFieldSettings;
        typeof(CombatPlayerState).GetMethod("ExecuteActionEvent", Private).Invoke(_fsm.UltimateState,
            new object[] { new ActionEventEntry { eventType = ActionEventType.ProceduralSlashField, slashFieldSettings = settings } });
        var field = (ProceduralSlashField)typeof(CombatPlayerState).GetField("_slashField", Private).GetValue(_fsm.UltimateState);
        try
        {
            field.SetLogicFrame(30f);
            if (maxCancel)
            {
                typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
                _fsm.RouteUltimateInput(held: true);
                Assert.That(_combat.CurrentActionId, Is.EqualTo("destination"));
            }
            else _fsm.ChangeState(_fsm.IdleState);
            Assert.That(field.IsPlaying, Is.True);
            Assert.That(typeof(CombatPlayerState).GetField("_slashField", Private).GetValue(_fsm.UltimateState), Is.Null);
            field.AdvanceLifetime(10f);
            Assert.That(field.IsPlaying, Is.False);
        }
        finally { if (field != null) Object.DestroyImmediate(field.gameObject); }
    }

    [TestCase(ActionPhase.Startup)]
    [TestCase(ActionPhase.Active)]
    public void StartupAndActiveCannotMaxCancel(ActionPhase phase)
    {
        StartSource();
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, phase);
        var execution = _combat.CurrentExecution;
        _fsm.RouteUltimateInput(held: true);
        Assert.That(_combat.CurrentExecution, Is.SameAs(execution));
        Assert.That(_owner.GetComponentInChildren<MaxCancelFeedback>(true), Is.Null);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void OptOutDestinationCooldownAndUnaffordableCostPreserveSource(bool optOut, bool cooldown)
    {
        UltimateAction source = StartSource();
        if (optOut) source.allowUltimateInterrupt = false;
        else if (cooldown) _combat.StartActionCooldown(AttackCategory.Ultimate, "destination", "destination");
        else _table.ultimateActions[1].energyCost = 100f;
        typeof(FrameDataPlayerState).GetField("currentPhase", Private).SetValue(_fsm.UltimateState, ActionPhase.Recovery);
        var execution = _combat.CurrentExecution;
        _fsm.RouteUltimateInput(held: true);
        Assert.That(_combat.CurrentExecution, Is.SameAs(execution));
        Assert.That(_combat.CurrentActionId, Is.EqualTo("source"));
        Assert.That(_owner.GetComponentInChildren<MaxCancelFeedback>(true), Is.Null);
    }
}
#endif
