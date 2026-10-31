#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Combat;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ActionVfxLifetimeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> _objects = new List<Object>();
    private CharacterObject _character;
    private GameObject _owner;
    private PlayerCharacter _player;
    private PlayerCombat _combat;
    private VFXManager _oldManager;
    private bool _changedManager;
    private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

    [SetUp]
    public void Setup()
    {
        _objects.Clear(); _changedManager = false;
        _character = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset")));
        _owner = Keep(new GameObject("VFX lifetime owner")); _owner.SetActive(false);
        _player = _owner.AddComponent<PlayerCharacter>(); _player.characterObject = _character;
        _combat = _owner.AddComponent<PlayerCombat>(); _combat.playerCharacter = _player; _player.Combat = _combat;
        _player.InitializeHP();
        _owner.SetActive(true);
        _combat.ExecuteStep(new AttackStepData { stepName = "source" }, 1, false, AttackCategory.Normal);
    }

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f;
        for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        if (_changedManager) typeof(VFXManager).GetProperty("Instance").SetValue(null, _oldManager);
    }

    private ProceduralSlashField Field()
    {
        var template = _character.actionResolverTable.skillActions.Single(a => a.actionId == MainDimensionSlashAuthoring.ActionId)
            .comboTree.rootNode.stepData.eventData.entries.First(e => e.eventType == ActionEventType.ProceduralSlashField).slashFieldSettings;
        var settings = new ProceduralSlashFieldSettings { material = template.material, fitViewport = false,
            firstFrame = 0, endFrame = 60, hideActor = true, hideFromFrame = 0, showAtFrame = 60,
            slashCount = 1, seamCount = 0, revealFrames = Vector2.one * 2, decayFrames = Vector2.one * 50,
            impactDistortion = new CutImpactDistortionSettings { enabled = true, startFrame = 0, durationFrames = 40 } };
        var actor = _owner.AddComponent<SpriteRenderer>();
        var host = Keep(new GameObject("Lifetime slash")); host.transform.SetParent(_owner.transform, false);
        var field = host.AddComponent<ProceduralSlashField>();
        field.Begin(_combat.CurrentExecution, settings, new Vector3(1, 2, 0), 1, actor);
        field.SetLogicFrame(5);
        return field;
    }

    [TestCase(false)] [TestCase(true)]
    public void ReleasedFieldKeepsItsLifetimeAndRestoresActorImmediately(bool swap)
    {
        var field = Field();
        Assert.That(_owner.GetComponent<SpriteRenderer>().forceRenderingOff, Is.True);
        Vector3 position = field.VisualRoot.position;
        field.ReleaseToLifetime();
        Assert.That(_owner.GetComponent<SpriteRenderer>().forceRenderingOff, Is.False);
        if (swap) { _combat.InvalidateFieldExecutions(); _owner.SetActive(false); }
        else _combat.ResetCombo();
        Assert.That(field.IsPlaying, Is.True);
        Assert.That(field.transform.parent, Is.Null);
        field.SetLogicFrame(0);
        field.AdvanceLifetime(0.25f);
        Assert.That(field.SampledFrame, Is.EqualTo(20f).Within(0.001f));
        Assert.That(field.VisualRoot.position, Is.EqualTo(position));
        field.AdvanceLifetime(1f);
        Assert.That(field.IsPlaying, Is.False);
        Assert.That(field.VisualRoot.GetComponentsInChildren<MeshRenderer>().All(r => !r.enabled), Is.True);
    }

    [Test]
    public void LostExecutionAutomaticallyHandsOffWithoutRehidingNewAction()
    {
        var field = Field(); _combat.InvalidateFieldExecutions();
        typeof(ProceduralSlashField).GetMethod("LateUpdate", Private).Invoke(field, null);
        Assert.That(field.IsPlaying, Is.True);
        Assert.That(_owner.GetComponent<SpriteRenderer>().forceRenderingOff, Is.False);
        _owner.GetComponent<SpriteRenderer>().forceRenderingOff = true;
        field.AdvanceLifetime(0.1f);
        Assert.That(_owner.GetComponent<SpriteRenderer>().forceRenderingOff, Is.True, "The tail no longer owns actor rendering.");
    }

    [Test]
    public void ReleasedCutRetainsRefractionThroughSwapAndPauseUntilExpiry()
    {
        var cameraObject = Keep(new GameObject("Lifetime refraction camera", typeof(Camera)));
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        var field = Field();
        typeof(ProceduralSlashField).GetField("_camera", Private).SetValue(field, camera);
        field.ReleaseToLifetime(); _combat.InvalidateFieldExecutions();
        Assert.That(ProceduralSlashField.TryGetCutImpact(camera, out _), Is.True);
        Time.timeScale = 0f; float frame = field.SampledFrame;
        field.AdvanceLifetime(1f);
        Assert.That(field.SampledFrame, Is.EqualTo(frame));
        Time.timeScale = 1f; field.AdvanceLifetime(2f);
        Assert.That(ProceduralSlashField.TryGetCutImpact(camera, out _), Is.False);
    }

    private VFXManager Manager()
    {
        _oldManager = VFXManager.Instance; _changedManager = true;
        typeof(VFXManager).GetProperty("Instance").SetValue(null, null);
        var host = Keep(new GameObject("Lifetime VFX pool")); host.SetActive(false);
        var manager = host.AddComponent<VFXManager>();
        typeof(VFXManager).GetField("initialPoolSize", Private).SetValue(manager, 0);
        typeof(VFXManager).GetField("vfxPrefab", Private).SetValue(manager,
            AssetDatabase.LoadAssetAtPath<GenericVFXPlayer>("Assets/Project/Character/Hero/Material/TestingHeroData/VFXGenericPrefab.prefab"));
        host.SetActive(true); typeof(VFXManager).GetMethod("Awake", Private).Invoke(manager, null);
        var pooled = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GenericVFXPlayer>(
            "Assets/Project/Character/Hero/Material/TestingHeroData/VFXGenericPrefab.prefab"), host.transform);
        typeof(GenericVFXPlayer).GetMethod("Awake", Private).Invoke(pooled, null);
        manager.ReturnToPool(pooled);
        return manager;
    }

    private AnimationClip Clip => AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Project/Character/Hero/Material/TestingHeroData/VFX_Default.anim");

    [Test]
    public void CleanupDoesNotReleaseAnInstanceAlreadyReusedByAnotherPlayback()
    {
        var manager = Manager();
        _combat.vfxSpawner = _owner.AddComponent<VFXSpawner>();
        var step = new AttackStepData { stepName = "old", vfxData = new VFXFrameData() };
        step.vfxData.entries.Add(new VFXEntry { vfxClip = Clip, triggerFrame = 0, duration = 180 });
        _combat.ExecuteStep(step, 1, false, AttackCategory.Normal);
        typeof(PlayerCombat).GetMethod("ProcessVFX", Private).Invoke(_combat, new object[] { 0 });
        var first = manager.GetComponentInChildren<GenericVFXPlayer>(true);
        first.OnAnimationComplete();
        var replacement = manager.SpawnResult(VFXType.None, Vector3.zero, clipOverride: Clip, lifespan: 3f);
        Assert.That(replacement, Is.SameAs(first));
        _combat.ResetCombo();
        Assert.That(replacement.gameObject.activeSelf, Is.True);
        Assert.That(replacement.transform.parent, Is.SameAs(manager.transform));
    }

    [TestCase("reset")] [TestCase("step")] [TestCase("cancel")] [TestCase("disable")]
    public void CombatCleanupReleasesTimelineVisualWithoutStoppingItsPlayback(string change)
    {
        var manager = Manager();
        var spawner = _owner.AddComponent<VFXSpawner>(); _combat.vfxSpawner = spawner;
        var step = new AttackStepData { stepName = "visual", vfxData = new VFXFrameData() };
        step.vfxData.entries.Add(new VFXEntry { vfxClip = Clip, triggerFrame = 0, duration = 180 });
        _combat.ExecuteStep(step, 1, false, AttackCategory.Normal);
        typeof(PlayerCombat).GetMethod("ProcessVFX", Private).Invoke(_combat, new object[] { 0 });
        var vfx = manager.GetComponentInChildren<GenericVFXPlayer>(true);
        Keep(vfx.gameObject);
        // Edit-mode instantiation does not invoke the player's Awake automatically.
        Assert.That(vfx.gameObject.activeSelf, Is.True);
        if (change == "reset") _combat.ResetCombo();
        else if (change == "step") _combat.ExecuteStep(new AttackStepData { stepName = "next" }, 2, false, AttackCategory.Normal);
        else if (change == "disable")
        {
            typeof(PlayerCombat).GetMethod("OnDisable", Private).Invoke(_combat, null);
            _owner.SetActive(false);
        }
        else _combat.CancelCurrentAttackButKeepComboProgress();
        Assert.That(vfx.gameObject.activeSelf, Is.True);
        Assert.That(vfx.transform.parent, Is.Null);
        var completed = (IEnumerator)typeof(GenericVFXPlayer).GetMethod("PlaybackRoutine", Private).Invoke(vfx, new object[] { 0f });
        completed.MoveNext();
        Assert.That(vfx.gameObject.activeSelf, Is.False, "Its own completion still returns the visual to the pool.");
    }

    [Test]
    public void LocalVisualDetachesWithCapturedTrajectorySpace()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GenericVFXPlayer>("Assets/Project/Character/Hero/Material/TestingHeroData/VFXGenericPrefab.prefab");
        var vfx = Keep(Object.Instantiate(prefab, _owner.transform));
        Keep(vfx.gameObject);
        typeof(GenericVFXPlayer).GetMethod("Awake", Private).Invoke(vfx, null);
        _owner.transform.SetPositionAndRotation(new Vector3(3, 4, 0), Quaternion.Euler(0, 180, 0));
        _owner.transform.localScale = Vector3.one * 2f;
        vfx.transform.localPosition = Vector3.right;
        vfx.Play(new VFXDefinition { clip = Clip, isLocalSpace = true }, false,
            new List<VFXTransformKey> { new VFXTransformKey { frame = 0, position = Vector3.right } }, 3f);
        vfx.ReleaseToLifetime();
        Matrix4x4 space = _owner.transform.localToWorldMatrix;
        _owner.transform.position += Vector3.one * 20f; _owner.SetActive(false);
        typeof(GenericVFXPlayer).GetMethod("UpdateTrajectory", Private).Invoke(vfx, new object[] { 0f });
        Assert.That(Vector3.Distance(vfx.transform.position, space.MultiplyPoint3x4(Vector3.right * 2f)), Is.LessThan(0.001f));
        Assert.That(vfx.gameObject.activeInHierarchy, Is.True);
    }

    [TestCase(false)] [TestCase(true)]
    public void QteTailSurvivesOwnerDisableAndExpiresWithoutInventingContact(bool hasContact)
    {
        var settings = _character.qteSwap.plunge;
        var vfx = Object.Instantiate(settings.prefab, _owner.transform); Keep(vfx.gameObject);
        vfx.Begin(settings.visuals, Vector3.up * 8f, Vector3.zero, Vector2.up, seed: 1234, execution: _combat.CurrentExecution);
        int frame = hasContact ? 20 : 5;
        vfx.Sample(frame, 20, 40);
        vfx.FinishTail(20, _combat, hasContact);
        _combat.InvalidateFieldExecutions(); _owner.SetActive(false);
        Assert.That(vfx.IsPlaying, Is.True); Assert.That(vfx.transform.parent, Is.Null);
        vfx.AdvanceTail(0.1f);
        Assert.That(vfx.IsPlaying, Is.True);
        Assert.That((bool)typeof(LightningPlungeVFX).GetField("_emitted", Private).GetValue(vfx), Is.EqualTo(hasContact));
        vfx.AdvanceTail(10f);
        Assert.That(vfx.IsPlaying, Is.False);
    }

    [Test]
    public void MaxCancelPixelSequenceSlidesGlowsDriftsExitsAndRestarts()
    {
        MaxCancelFeedback.Show(_player);
        var feedback = _owner.GetComponent<MaxCancelFeedback>();
        var root = (GameObject)typeof(MaxCancelFeedback).GetField("_root", Private).GetValue(feedback);
        var content = root.transform.Find("Animated Content").GetComponent<RectTransform>();
        var aura = content.Find("White Growing Aura").GetComponent<RectTransform>();
        var label = content.Find("Label").GetComponent<TextMeshProUGUI>();
        Assert.That(label.font, Is.EqualTo(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Project/Fonts/GNF TTF SDF.asset")));
        Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
        feedback.Sample(0.08f); float enterX = content.anchoredPosition.x;
        Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.InRange(0f, 1f));
        Assert.That(aura.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
        feedback.Sample(0.18f); float firstScale = aura.localScale.x;
        Assert.That(content.anchoredPosition.x, Is.GreaterThan(enterX));
        feedback.Sample(0.30f); float firstHoldX = content.anchoredPosition.x;
        Assert.That(aura.localScale.x, Is.GreaterThan(firstScale));
        feedback.Sample(0.55f);
        Assert.That(content.anchoredPosition.x, Is.GreaterThan(firstHoldX));
        feedback.Sample(0.75f);
        Assert.That(content.anchoredPosition.x, Is.GreaterThan(94f));
        Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.LessThan(1f));
        feedback.Sample(MaxCancelFeedback.Duration);
        Assert.That(feedback.IsVisible, Is.False);
        MaxCancelFeedback.Show(_player);
        Assert.That(feedback.IsVisible, Is.True); Assert.That(content.anchoredPosition.x, Is.EqualTo(-300f));
    }
}
#endif
