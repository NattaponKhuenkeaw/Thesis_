#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Progression;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class MainDimensionSlashTests
{
    private sealed class AttackProbe : PlayerAttackState
    {
        public AttackProbe(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }
        public void Recovery() => currentPhase = ActionPhase.Recovery;
    }
    private CharacterObject _character;
    private ActionResolverTable _table;
    private PlayerMetaProgression _progression;
    private GameObject _owner;
    private PlayerCharacter _player;
    private PlayerCombat _combat;
    private CharacterPassiveController _passive;
    private SkillAction Slash => _table.skillActions.Single(a => a.actionId == MainDimensionSlashAuthoring.ActionId);

    [SetUp]
    public void Setup()
    {
        _character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset"));
        _table = Object.Instantiate(_character.actionResolverTable); _character.actionResolverTable = _table;
        MainDimensionSlashAuthoring.Configure(_character);
        _progression = ScriptableObject.CreateInstance<PlayerMetaProgression>();
        _owner = new GameObject("Dimension Slash Test"); _owner.SetActive(false);
        _player = _owner.AddComponent<PlayerCharacter>(); _player.characterObject = _character;
        _combat = _owner.AddComponent<PlayerCombat>(); _combat.playerCharacter = _player; _player.Combat = _combat;
        _passive = _owner.AddComponent<CharacterPassiveController>(); _passive.playerCharacter = _player;
        _player.PassiveController = _passive; _passive.SetMetaProgression(_progression);
        _player.InitializeHP();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_owner); Object.DestroyImmediate(_character);
        Object.DestroyImmediate(_table); Object.DestroyImmediate(_progression);
    }

    private void Level(int level) => _progression.SetPotentialLevel(_character, PotentialTrack.Passive, level);
    private void Cut(bool qte = false, string action = MainDimensionSlashAuthoring.CutId, string step = "DimensionCut", bool provenance = true)
    {
        var execution = provenance ? new CombatExecutionContext(_combat, _character, _combat.FieldSession, qte) : null;
        _passive.NotifyActionStarted(AttackCategory.Normal, action, "Dimension Cut", new AttackStepData { stepName = step }, execution);
    }

    private string Resolve()
    {
        var resolver = new ActionResolver();
        resolver.ResolveInitial(new InputContext { isGrounded = true, passiveController = _passive,
            runtimeState = _passive.RuntimeState, actionAvailabilityProvider = _player }, _table, AttackCategory.Skill);
        return resolver.ActiveCombatAction?.actionId;
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void LockedLevelsNeverCountOrSelectFollowUp(int level)
    {
        Level(level); for (int i = 0; i < 8; i++) Cut();
        Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
        _passive.RuntimeState.SetTag(MainDimensionSlashAuthoring.ReadyTag, 1.5f);
        Assert.That(Resolve(), Is.EqualTo("main.skill.dark_chain"));
    }

    [Test]
    public void FifthPaidManualRootArmsOneWindowAndResetsCount()
    {
        Level(4);
        for (int i = 1; i < 5; i++)
        {
            Cut(); Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.EqualTo(i));
            Assert.That(Resolve(), Is.EqualTo("main.skill.dark_chain"));
        }
        Cut(); Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
        Assert.That(Resolve(), Is.EqualTo(MainDimensionSlashAuthoring.ActionId));
        Assert.That(_passive.TryGetActionAvailabilityWindow(AttackCategory.Skill, MainDimensionSlashAuthoring.ActionId,
            "Dimension Slash", out float remaining, out float duration), Is.True);
        Assert.That(duration, Is.EqualTo(1.5f).Within(0.01f)); Assert.That(remaining, Is.GreaterThan(0f));
    }

    [TestCase(true, MainDimensionSlashAuthoring.CutId, "DimensionCut", true)]
    [TestCase(false, MainDimensionSlashAuthoring.CutId, "DimensionCut", false)]
    [TestCase(false, "other.action", "DimensionCut", true)]
    [TestCase(false, MainDimensionSlashAuthoring.CutId, "OtherStep", true)]
    public void QteUnknownOtherActionOrStepCannotCount(bool qte, string action, string step, bool provenance)
    {
        Level(4); for (int i = 0; i < 5; i++) Cut(qte, action, step, provenance);
        Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
        Assert.That(Resolve(), Is.EqualTo("main.skill.dark_chain"));
    }

    [Test]
    public void StaleExecutionAndDeadOwnerCannotCount()
    {
        Level(4);
        var stale = new CombatExecutionContext(_combat, _character, _combat.FieldSession, false);
        _combat.InvalidateFieldExecutions();
        _passive.NotifyActionStarted(AttackCategory.Normal, MainDimensionSlashAuthoring.CutId, "Dimension Cut",
            new AttackStepData { stepName = "DimensionCut" }, stale);
        Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
        _player.SetCurrentHP(0f);
        Cut(); Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
    }

    [Test]
    public void ExtraCutsCannotBankOrRefreshAndStartConsumesExactlyOnce()
    {
        Level(4); for (int i = 0; i < 5; i++) Cut();
        for (int i = 0; i < 10; i++) Cut();
        Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
        for (int i = 0; i < 10; i++) Assert.That(Resolve(), Is.EqualTo(MainDimensionSlashAuthoring.ActionId));
        _passive.NotifyActionStarted(AttackCategory.Skill, MainDimensionSlashAuthoring.ActionId, "Dimension Slash", Slash.comboTree.rootNode.stepData);
        Assert.That(Resolve(), Is.EqualTo("main.skill.dark_chain"));
        for (int i = 0; i < 4; i++) Cut();
        Assert.That(Resolve(), Is.EqualTo("main.skill.dark_chain"));
        Cut(); Assert.That(Resolve(), Is.EqualTo(MainDimensionSlashAuthoring.ActionId));
    }

    [Test]
    public void RealFsmSpendsFiveCachedCutsThenSkillConsumesOneOpportunity()
    {
        Level(4);
        var fsm = _owner.AddComponent<PlayerStateMachine>();
        fsm.playerCharacter = _player;
        fsm.showCombatLogs = fsm.showFsmLogs = fsm.showFrameDataLogs = false;
        _player.StateMachine = fsm;
        typeof(PlayerStateMachine).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fsm, null);
        _combat.passiveController = _passive;
        _table.standaloneActions.Single(a => a.actionId == MainDimensionSlashAuthoring.CutId).requiresEnemyTarget = false;
        var attack = new AttackProbe(_player, fsm);
        _owner.SetActive(true);
        _player.InitializeHP();
        _passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 5);
        fsm.ChangeState(attack);
        for (int i = 0; i < 5; i++)
        {
            if (i > 0) attack.Recovery();
            attack.OnAttackInput();
            Assert.That(_combat.CurrentActionId, Is.EqualTo(MainDimensionSlashAuthoring.CutId));
            Assert.That(_passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(4 - i));
        }
        Assert.That(_passive.RuntimeState.HasTag(MainDimensionSlashAuthoring.ReadyTag), Is.True);
        attack.Recovery();
        fsm.RouteSkillInput();
        Assert.That(_combat.CurrentActionId, Is.EqualTo(MainDimensionSlashAuthoring.ActionId));
        Assert.That(_passive.RuntimeState.HasTag(MainDimensionSlashAuthoring.ReadyTag), Is.False);
        Assert.That(_passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.Zero);
    }

    [Test]
    public void ExistingCooldownDoesNotBlockOrChangeReadyFollowUp()
    {
        Level(4); _combat.StartActionCooldown(AttackCategory.Skill, "Dark Chain", "main.skill.dark_chain");
        SkillAction normal = _table.skillActions.Single(a => a.actionId == "main.skill.dark_chain");
        Assert.That(_player.IsActionAvailable(normal), Is.False);
        for (int i = 0; i < 5; i++) Cut();
        Assert.That(Resolve(), Is.EqualTo(MainDimensionSlashAuthoring.ActionId));
        Assert.That(_player.IsActionAvailable(Slash), Is.True);
        Assert.That(Slash.resourceCost, Is.Zero); Assert.That(Slash.cooldown, Is.Zero);
        _passive.NotifyActionStarted(AttackCategory.Skill, MainDimensionSlashAuthoring.ActionId, "Dimension Slash", Slash.comboTree.rootNode.stepData);
        Assert.That(_player.IsActionAvailable(normal), Is.False);
    }

    [Test]
    public void FiniteTimelineHasSevenNonOverlappingBudgetedWindows()
    {
        AttackStepData step = Slash.comboTree.rootNode.stepData;
        var hits = step.hitData.hitboxes.Cast<NormalHitbox>().ToArray();
        Assert.That(hits.All(h => h.elementType == ElementType.Darkness), Is.True, "Match Main's existing Dimension Cut damage type.");
        Assert.That(hits.Select(h => h.startFrameOffset + step.ActivePhaseStart), Is.EqualTo(new[] { 12, 20, 27, 33, 38, 42, 54 }));
        Assert.That(hits.Sum(h => h.damageRatio), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(hits.Last().damageRatio, Is.GreaterThan(hits.First().damageRatio));
        for (int i = 1; i < hits.Length; i++) Assert.That(hits[i].startFrameOffset, Is.GreaterThanOrEqualTo(hits[i - 1].startFrameOffset + hits[i - 1].activeFrames));
        Assert.That(step.GetPhase(11), Is.EqualTo(ActionPhase.Startup)); Assert.That(step.GetPhase(12), Is.EqualTo(ActionPhase.Active));
        Assert.That(step.GetPhase(60), Is.EqualTo(ActionPhase.Recovery)); Assert.That(step.GetPhase(66), Is.EqualTo(ActionPhase.Cancel));
        Assert.That(step.GetPhase(84), Is.EqualTo(ActionPhase.Cancel)); Assert.That(step.GetPhase(85), Is.EqualTo(ActionPhase.Done));
    }

    [Test]
    public void CameraEnvelopeRescalesAndCanBeSampledOutOfOrder()
    {
        var entry = Slash.comboTree.rootNode.stepData.eventData.entries.Single(e => e.eventType == ActionEventType.ActionCameraMotion);
        entry.cameraMotionSettings.Sample(54, out float size, out float offset);
        Assert.That(size, Is.EqualTo(0.96f).Within(0.0001f)); Assert.That(offset, Is.Negative);
        entry.cameraMotionSettings.Sample(0, out size, out offset); Assert.That(size, Is.EqualTo(1f));
        var track = new ActionEventFrameData(); track.entries.Add(entry); track.Rescale(2f);
        entry.cameraMotionSettings.Sample(108, out size, out offset); Assert.That(size, Is.EqualTo(0.96f).Within(0.0001f));
        Assert.That(entry.GetTimelineDurationFrames(), Is.EqualTo(133));
    }

    [Test]
    public void FinalCameraViewportIsClampedIncludingExistingCorrection()
    {
        CameraState state = CameraState.Default;
        state.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        state.Lens.OrthographicSize = 3f;
        state.RawPosition = new Vector3(9f, 4f, -10f); state.PositionCorrection = new Vector3(2f, 1f, 0f);
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state, bounds, 0.96f, 0.0035f), Is.True);
        Assert.That(state.GetCorrectedPosition().x + state.Lens.OrthographicSize * state.Lens.Aspect, Is.LessThanOrEqualTo(bounds.max.x + 0.001f));
        Assert.That(state.GetCorrectedPosition().y + state.Lens.OrthographicSize, Is.LessThanOrEqualTo(bounds.max.y + 0.001f));
    }

    [Test]
    public void UnsupportedCameraOrSmallRoomRetainsBaseComposition()
    {
        CameraState state = CameraState.Default; state.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        state.Lens.OrthographicSize = 6f;
        Vector3 before = state.GetCorrectedPosition();
        Assert.That(ActionCameraMotionExtension.ApplyBounded(ref state, new Bounds(Vector3.zero, Vector3.one), 0.96f, 0.01f), Is.False);
        Assert.That(state.Lens.OrthographicSize, Is.EqualTo(6f)); Assert.That(state.GetCorrectedPosition(), Is.EqualTo(before));
    }

    [Test]
    public void OlderCameraOwnerCannotSampleOrReleaseReplacement()
    {
        var host = new GameObject("Dimension Slash Camera Test");
        try
        {
            var manager = host.AddComponent<CameraManager>();
            _combat.ExecuteStep(Slash.comboTree.rootNode.stepData, 1, false, AttackCategory.Skill, "Dimension Slash", MainDimensionSlashAuthoring.ActionId);
            var settings = Slash.comboTree.rootNode.stepData.eventData.entries.Single().cameraMotionSettings;
            var first = manager.BeginActionCamera(_combat.CurrentExecution, settings, 0, 1);
            var second = manager.BeginActionCamera(_combat.CurrentExecution, settings, 0, -1);
            manager.SampleActionCamera(second, 54);
            manager.SampleActionCamera(first, 0); manager.ReleaseActionCamera(first);
            Assert.That(manager.TrySampleActionCamera(out float size, out float offset), Is.True);
            Assert.That(size, Is.EqualTo(0.96f).Within(0.0001f)); Assert.That(offset, Is.Positive);
            manager.ReleaseActionCamera(second);
            Assert.That(manager.TrySampleActionCamera(out _, out _), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void QteAndInvalidatedFieldExecutionCannotOwnActionCamera()
    {
        var host = new GameObject("Dimension Slash Camera Exclusions");
        try
        {
            var manager = host.AddComponent<CameraManager>();
            var settings = Slash.comboTree.rootNode.stepData.eventData.entries.Single().cameraMotionSettings;
            _combat.ExecuteStep(Slash.comboTree.rootNode.stepData, 1, false, AttackCategory.Skill,
                "Dimension Slash", MainDimensionSlashAuthoring.ActionId, false, true);
            var qte = manager.BeginActionCamera(_combat.CurrentExecution, settings, 0, 1);
            Assert.That(qte.Generation, Is.Zero);
            _combat.ExecuteStep(Slash.comboTree.rootNode.stepData, 1, false, AttackCategory.Skill, "Dimension Slash", MainDimensionSlashAuthoring.ActionId);
            var token = manager.BeginActionCamera(_combat.CurrentExecution, settings, 0, 1);
            Assert.That(manager.HasActionCamera, Is.True);
            _combat.InvalidateFieldExecutions(); manager.SampleActionCamera(token, 54);
            Assert.That(manager.TrySampleActionCamera(out _, out _), Is.False);
        }
        finally { Object.DestroyImmediate(host); }
    }
}
#endif
