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
        _passive.RuntimeState.SetTag(MainDimensionSlashAuthoring.ReadyTag, 5f);
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
        Assert.That(duration, Is.EqualTo(5f).Within(0.01f)); Assert.That(remaining, Is.GreaterThan(0f));
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
    public void BarrageAndExistingCutProvideTwentyLowImpactBudgetedWindows()
    {
        AttackStepData step = Slash.comboTree.rootNode.stepData;
        var hits = step.hitData.hitboxes.Cast<NormalHitbox>().ToArray();
        Assert.That(hits.All(h => h.elementType == ElementType.Darkness), Is.True, "Match Main's existing Dimension Cut damage type.");
        Assert.That(hits.Select(h => h.startFrameOffset + step.ActivePhaseStart), Is.EqualTo(Enumerable.Range(0, 19).Select(i => 24 + i * 3)));
        var final = Slash.comboTree.rootNode.defaultNext.stepData;
        var last = (NormalHitbox)final.hitData.hitboxes.Single();
        Assert.That(hits.Sum(h => h.damageRatio) + last.damageRatio, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(hits.All(h => h.suppressImpactFeedback && h.impactTier == DamageImpactTier.Low), Is.True);
        Assert.That(last.suppressImpactFeedback && last.impactTier == DamageImpactTier.Low, Is.True);
        var existing = _table.standaloneActions.Single(a => a.actionId == MainDimensionSlashAuthoring.CutId).comboTree.rootNode.stepData;
        Assert.That(final.animationClip, Is.EqualTo(existing.animationClip));
        Assert.That(JsonUtility.ToJson(final.motionData), Is.EqualTo(JsonUtility.ToJson(existing.motionData)));
        for (int i = 1; i < hits.Length; i++) Assert.That(hits[i].startFrameOffset, Is.GreaterThanOrEqualTo(hits[i - 1].startFrameOffset + hits[i - 1].activeFrames));
        Assert.That(step.GetPhase(18), Is.EqualTo(ActionPhase.Startup)); Assert.That(step.GetPhase(19), Is.EqualTo(ActionPhase.Active));
        Assert.That(step.GetPhase(103), Is.EqualTo(ActionPhase.Active)); Assert.That(step.GetPhase(104), Is.EqualTo(ActionPhase.Cancel));
        Assert.That(step.autoChainToNext, Is.True); Assert.That(step.GetPhase(105), Is.EqualTo(ActionPhase.Done));
    }

    [Test]
    public void CameraEnvelopeRescalesAndCanBeSampledOutOfOrder()
    {
        var entry = new ActionEventEntry { eventType = ActionEventType.ActionCameraMotion, cameraMotionSettings = CameraEnvelope() };
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
            var settings = CameraEnvelope();
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
    public void TimedZoomSurvivesNormalActionEndAndReturnsAfterAuthoredHold()
    {
        var cameraHost = new GameObject("Timed Zoom Output");
        var managerHost = new GameObject("Timed Zoom Manager");
        try
        {
            cameraHost.tag = "MainCamera";
            cameraHost.AddComponent<Camera>();
            var manager = managerHost.AddComponent<CameraManager>();
            typeof(CameraManager).GetField("_ultimateCam", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, null);
            typeof(CameraManager).GetField("_brain", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, null);
            _combat.ExecuteStep(Slash.comboTree.rootNode.stepData, 1, false, AttackCategory.Skill,
                "Dimension Slash", MainDimensionSlashAuthoring.ActionId);
            var settings = new ScreenImpulseCameraSettings
                { enabled = true, zoomMultiplier = 0.8f, holdFrames = 1000, returnFrames = 100 };
            var token = manager.BeginScreenImpulseCamera(_combat.CurrentExecution, settings, 20);
            Assert.That(token.Generation, Is.Not.Zero);
            manager.ReleaseActionCamera(token, true);
            _combat.ResetCombo();
            manager.SampleActionCamera(token, 2000);
            manager.AdvanceTimedCamera(500f / CombatCore.FrameDataConstants.LOGIC_FPS);
            Assert.That(manager.TrySampleScreenImpulseCamera(out float zoom, out _, out _), Is.True);
            Assert.That(zoom, Is.EqualTo(0.8f).Within(0.001f));
            manager.AdvanceTimedCamera(550f / CombatCore.FrameDataConstants.LOGIC_FPS);
            manager.TrySampleScreenImpulseCamera(out zoom, out _, out _);
            Assert.That(zoom, Is.EqualTo(0.9f).Within(0.001f));
            manager.AdvanceTimedCamera(52f / CombatCore.FrameDataConstants.LOGIC_FPS);
            Assert.That(manager.HasActionCamera, Is.False);

            var execution = new CombatExecutionContext(_combat, _character, _combat.FieldSession, false);
            token = manager.BeginScreenImpulseCamera(execution, settings, 0);
            Assert.That(token.Generation, Is.Not.Zero);
            _combat.InvalidateFieldExecutions();
            manager.AdvanceTimedCamera(0f);
            Assert.That(manager.HasActionCamera, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(managerHost);
            Object.DestroyImmediate(cameraHost);
        }
    }

    [Test]
    public void CameraSequenceSurvivesNaturalCompletionAndStaleReleaseCannotCancelReplacement()
    {
        var cameraHost = new GameObject("Camera Sequence Output");
        var managerHost = new GameObject("Camera Sequence Manager");
        try
        {
            cameraHost.tag = "MainCamera";
            var output = cameraHost.AddComponent<Camera>(); output.orthographic = true; output.orthographicSize = 10f;
            var manager = managerHost.AddComponent<CameraManager>();
            typeof(CameraManager).GetField("_ultimateCam", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, null);
            typeof(CameraManager).GetField("_brain", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, null);
            _combat.ExecuteStep(Slash.comboTree.rootNode.stepData, 1, false, AttackCategory.Skill, "Sequence", MainDimensionSlashAuthoring.ActionId);
            var data = new CameraAnimationFrameData();
            data.segments.Add(new CameraAnimationSegment { task = CameraAnimationTask.Zoom, zoomMultiplier = 0.5f });
            data.segments.Add(new CameraAnimationSegment { task = CameraAnimationTask.Hold, channels = CameraAnimationChannels.Zoom, endFrame = 1000 });
            data.segments.Add(new CameraAnimationSegment { task = CameraAnimationTask.Return, channels = CameraAnimationChannels.Zoom,
                startFrame = 1000, endFrame = 1100, easing = CameraAnimationEasing.Linear });
            var execution = _combat.CurrentExecution;
            var first = manager.BeginCameraAnimation(execution, data, 20, 1);
            Assert.That(first.Generation, Is.Not.Zero);
            manager.ReleaseActionCamera(first, true); _combat.ResetCombo();
            manager.SampleActionCamera(first, 5000);
            manager.AdvanceTimedCamera(1000f / CombatCore.FrameDataConstants.LOGIC_FPS);
            var normal = new CameraAnimationPose(Vector2.zero, 10f);
            Assert.That(manager.TrySampleCameraAnimation(normal, out var pose), Is.True);
            Assert.That(pose.Size, Is.EqualTo(5f).Within(0.001f));
            manager.AdvanceTimedCamera(50f / CombatCore.FrameDataConstants.LOGIC_FPS);
            manager.TrySampleCameraAnimation(normal, out pose);
            Assert.That(pose.Size, Is.EqualTo(7.5f).Within(0.001f));
            var replacement = manager.BeginCameraAnimation(execution, data, 0, 1);
            manager.ReleaseActionCamera(first); manager.SampleActionCamera(first, 9000);
            Assert.That(manager.TrySampleCameraAnimation(normal, out _), Is.True);
            manager.ReleaseActionCamera(replacement);
            Assert.That(manager.HasActionCamera, Is.False);
            manager.BeginCameraAnimation(execution, data, 0, 1);
            _combat.InvalidateFieldExecutions(); manager.AdvanceTimedCamera(0f);
            Assert.That(manager.HasActionCamera, Is.False);
        }
        finally { Object.DestroyImmediate(managerHost); Object.DestroyImmediate(cameraHost); }
    }

    [Test]
    public void QteAndInvalidatedFieldExecutionCannotOwnActionCamera()
    {
        var host = new GameObject("Dimension Slash Camera Exclusions");
        try
        {
            var manager = host.AddComponent<CameraManager>();
            var settings = CameraEnvelope();
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

    private static ActionCameraMotionSettings CameraEnvelope() => new ActionCameraMotionSettings
    {
        keys = new System.Collections.Generic.List<ActionCameraMotionSettings.Key>
        {
            new ActionCameraMotionSettings.Key { frame = 0 },
            new ActionCameraMotionSettings.Key { frame = 54, sizeMultiplier = 0.96f, horizontalViewportOffset = -0.0035f },
            new ActionCameraMotionSettings.Key { frame = 66 }
        }
    };

    [Test]
    public void CriticalBarrageDamageRetainsCritWithoutTriggeringMinimumHitstop()
    {
        var damageObject = new GameObject("HitData Crit Test"); damageObject.SetActive(false);
        var enemyObject = new GameObject("Crit Target"); enemyObject.SetActive(false);
        try
        {
            var target = enemyObject.AddComponent<EnemyController>(); target.currentHP = target.maxHP = 1000;
            var damage = damageObject.AddComponent<HitBoxDamage>();
            var hit = new NormalHitbox { suppressImpactFeedback = true };
            damage.SetHitData(100f, false, false, 0f, 1f, hit, AttackCategory.Skill, "Crit Test", new AttackStepData(), null);
            damage.ConfigurePerEnemyDamageCalculation(new CharacterStats { critRate = 100f, critDamage = 50f }, 100f, true,
                null, null, null, null);
            typeof(HitBoxDamage).GetMethod("ResolveDamageAgainstEnemy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(damage, new object[] { target });
            var result = (DamageResult)typeof(HitBoxDamage).GetField("currentDamageResult", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(damage);
            Assert.That(result.IsCriticalHit, Is.True);
            Assert.That((int)typeof(HitBoxDamage).GetMethod("GetConfiguredHitstopFrames", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(damage, null), Is.Zero);
        }
        finally { Object.DestroyImmediate(damageObject); Object.DestroyImmediate(enemyObject); }
    }

    [Test]
    public void ProceduralPreviewRestoresHiddenActorAndSeedIsRepeatable()
    {
        var host = new GameObject("Slash Field Test"); var actor = new GameObject("Actor").AddComponent<SpriteRenderer>();
        try
        {
            var field = host.AddComponent<ProceduralSlashField>();
            var settings = Slash.comboTree.rootNode.stepData.eventData.entries.Single().slashFieldSettings;
            settings.seed = 1234;
            field.Begin(null, settings, Vector3.zero, 1, actor, true);
            Assert.That(field.IsPlaying, Is.True);
            Assert.That(field.TotalSlashCount, Is.EqualTo(31));
            Assert.That(field.ForegroundSlashCount, Is.EqualTo(6));
            var layers = field.VisualRoot.GetComponentsInChildren<MeshRenderer>();
            Assert.That(layers.Length, Is.EqualTo(2));
            Assert.That(layers[0].sortingLayerName, Is.EqualTo("WorldUI"));
            Assert.That(layers[0].sortingOrder, Is.EqualTo(999));
            Assert.That(layers[1].sortingOrder, Is.EqualTo(12000));
            Assert.That(layers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length, Is.EqualTo(25 * 6));
            Assert.That(layers[1].GetComponent<MeshFilter>().sharedMesh.triangles.Length, Is.EqualTo(6 * 6));
            field.Sample(30f); Assert.That(actor.forceRenderingOff, Is.True);
            int seed = field.ActiveSeed; field.Sample(73f); Assert.That(actor.forceRenderingOff, Is.False);
            field.Begin(null, settings, Vector3.zero, 1, actor, true); Assert.That(field.ActiveSeed, Is.EqualTo(seed));
            field.Sample(30f); field.Stop(); Assert.That(actor.forceRenderingOff, Is.False);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(actor.gameObject); }
    }

    [Test]
    public void ViewportFittedCutsReachBeyondVisibleWidthAndHeight()
    {
        var cameraObject = new GameObject("Slash Viewport Camera", typeof(Camera));
        var host = new GameObject("Slash Viewport Field");
        try
        {
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
            camera.orthographicSize = 6.8f; camera.aspect = 16f / 9f;
            var field = host.AddComponent<ProceduralSlashField>();
            var settings = Slash.comboTree.rootNode.stepData.eventData.entries.Single().slashFieldSettings;
            settings.seed = 1234; settings.fitViewport = true;
            field.Begin(null, settings, Vector3.zero, 1, null, true);
            Bounds bounds = field.VisualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            Assert.That(bounds.size.x, Is.GreaterThan(camera.orthographicSize * camera.aspect * 2f));
            Assert.That(bounds.size.y, Is.GreaterThan(camera.orthographicSize * 2f));
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void JudgmentFieldIsDetachedAndUsesEnlargedWidthAndHeight()
    {
        var host = new GameObject("Anchored judgment field");
        try
        {
            var field = host.AddComponent<ProceduralSlashField>();
            var settings = Slash.comboTree.rootNode.stepData.eventData.entries.Single().slashFieldSettings;
            Assert.That(settings.overallScale, Is.EqualTo(1.95f).Within(0.001f));
            settings.seed = 1234; settings.fitViewport = false; settings.overallScale = 1f;
            field.Begin(null, settings, new Vector3(3,2,0), 1, null, true);
            var size = field.VisualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size;
            settings.overallScale = 1.95f; field.Begin(null, settings, new Vector3(3,2,0), 1, null, true);
            var enlargedSize = field.VisualRoot.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size;
            Assert.That(enlargedSize.x, Is.EqualTo(size.x*1.95f).Within(0.001f));
            Assert.That(enlargedSize.y, Is.EqualTo(size.y*1.95f).Within(0.001f));
            Assert.That(field.VisualRoot.parent, Is.Null);
            Vector3 anchored = field.VisualRoot.position;
            host.transform.position += Vector3.right * 20f; host.transform.rotation = Quaternion.Euler(0,180,0);
            Assert.That(field.VisualRoot.position, Is.EqualTo(anchored));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void QteComboUsesRecoveryFramesAndLeavesManualCutTerminal()
    {
        Assert.That(_character.qteSwap.TryResolveIntro(_table, out var qte, out var intro), Is.True);
        var cut = _table.standaloneActions.Single(a => a.actionId == MainDimensionSlashAuthoring.CutId);
        var manual = cut.GetEntryStep();
        Assert.That(qte.actionId, Is.EqualTo(MainLightningPlungeAuthoring.ActionId));
        Assert.That(_table.qteActions, Does.Contain(qte));
        Assert.That(intro, Is.Not.SameAs(manual));
        Assert.That(intro.animationClip.name, Is.EqualTo("main_qte_lightning_plunge_actor"));
        Assert.That(intro.motionData.keyframes.Single().taskType, Is.EqualTo(MotionTaskType.CapturedWorldPoint));
        Assert.That(intro.recoveryFrames, Is.EqualTo(120));
        Assert.That(intro.GetPhase(intro.ActivePhaseStart), Is.EqualTo(ActionPhase.Active));
        Assert.That(intro.GetPhase(intro.RecoveryPhaseStart), Is.EqualTo(ActionPhase.Recovery));
        Assert.That(intro.GetPhase(intro.TotalFrames + 1), Is.EqualTo(ActionPhase.Done));
        var resolver = new ActionResolver();
        resolver.BeginExplicitStep(cut, manual, true);
        Assert.That(resolver.IsLastStep(), Is.True);
        var combo = _character.qteSwap.GetIntroComboTree(_table);
        resolver.BeginExplicitStep(qte, intro, true, combo);
        Assert.That(resolver.ResolveNext(new InputContext()), Is.SameAs(combo.rootNode.defaultNext.stepData));
        Assert.That(resolver.ResolveNext(new InputContext()), Is.Null);
        Assert.That(_table.standaloneActions.Any(a => a.actionId == "main.qte.execution_breakdown"), Is.False);
    }

    [Test]
    public void ExecutionKeepsOriginalContactsAndUsesFullscreenGlowSlash()
    {
        var step = _character.qteSwap.GetIntroComboTree(_table).rootNode.defaultNext.stepData;
        Assert.That(step.hitData.hitboxes.Select(h => h.startFrameOffset + step.ActivePhaseStart), Is.EqualTo(new[] {12,20,27,33,38,42,54}));
        Assert.That(step.hitData.hitboxes.Cast<NormalHitbox>().Sum(h => h.damageRatio), Is.EqualTo(1f).Within(0.001f));
        Assert.That(step.hitData.hitboxes.Cast<NormalHitbox>().Last().impactTier, Is.EqualTo(DamageImpactTier.Heavy));
        Assert.That(step.vfxData.entries.Count, Is.EqualTo(14));
        var mask = step.eventData.entries.Single(e => e.eventType == ActionEventType.ProceduralSlashField);
        Assert.That(mask.triggerFrame, Is.EqualTo(54)); Assert.That(mask.slashFieldSettings.fullScreenSlash, Is.True);
        Assert.That(mask.slashFieldSettings.foregroundFraction, Is.EqualTo(1f));
        Assert.That(step.animationClip.name, Is.EqualTo("main_dimension_slash_actor"));
    }

    [TestCase(27)] [TestCase(124)]
    public void QteRecoveryNaAdvancesComboWithoutCachedCostOrGateCount(int inputFrame)
    {
        Level(4);
        var fsm = _owner.AddComponent<PlayerStateMachine>(); fsm.playerCharacter = _player; _player.StateMachine = fsm;
        fsm.showCombatLogs = fsm.showFsmLogs = fsm.showFrameDataLogs = false;
        typeof(PlayerStateMachine).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fsm, null);
        _combat.passiveController = _passive; _owner.SetActive(true); _player.InitializeHP();
        _character.qteSwap.TryResolveIntro(_table, out var cut, out var intro);
        _passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 2);
        fsm.QteIntroState.Prepare(cut, intro, null); fsm.ChangeState(fsm.QteIntroState);
        for (int i = 0; i < 150 && fsm.QteIntroState.CurrentFrame < inputFrame; i++) fsm.QteIntroState.FixedUpdate();
        Assert.That(_combat.GetCurrentStepData(), Is.SameAs(intro));
        fsm.RouteAttackInput();
        Assert.That(fsm.CurrentState, Is.SameAs(fsm.QteIntroState));
        Assert.That(_combat.GetCurrentStepData().stepName, Is.EqualTo(ExecutionBreakdownAuthoring.StepName));
        Assert.That(_combat.GetComboStep(), Is.EqualTo(2));
        Assert.That(_combat.CurrentExecution.isQteIntro, Is.False);
        Assert.That(_passive.RuntimeState.GetCounter("main.shadow.cached_cuts"), Is.EqualTo(2));
        Assert.That(_passive.RuntimeState.GetCounter(MainDimensionSlashAuthoring.CountId), Is.Zero);
    }

    [Test]
    public void SkippingQteRecoveryEndsComboWithoutAutomaticallyExecutingFollowUp()
    {
        var fsm = _owner.AddComponent<PlayerStateMachine>(); fsm.playerCharacter = _player; _player.StateMachine = fsm;
        fsm.showCombatLogs = fsm.showFsmLogs = fsm.showFrameDataLogs = false;
        typeof(PlayerStateMachine).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fsm, null);
        _combat.passiveController = _passive; _owner.SetActive(true); _player.InitializeHP();
        _character.qteSwap.TryResolveIntro(_table, out var cut, out var intro);
        fsm.QteIntroState.Prepare(cut, intro, null); fsm.ChangeState(fsm.QteIntroState);
        for (int i = 0; i < 150 && fsm.CurrentState == fsm.QteIntroState; i++) fsm.QteIntroState.FixedUpdate();
        Assert.That(fsm.CurrentState, Is.Not.SameAs(fsm.QteIntroState));
        Assert.That(_combat.IsAttacking(), Is.False);
        Assert.That(_combat.GetComboStep(), Is.Zero);
    }
}
#endif
