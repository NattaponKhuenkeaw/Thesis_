#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class ActionWindowIconTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _host;
    private CharacterObject _character;
    private ActionResolverTable _table;
    private PlayerCharacter _player;
    private CharacterPassiveController _passive;
    private AbilityUIManager _ui;
    private Image _image;
    private Image _overlay;
    private Texture2D _texture;
    private Sprite _original;
    private Sprite _ordinary;
    private Sprite _special;

    [SetUp]
    public void SetUp()
    {
        _host = new GameObject("Action icon tests");
        _host.SetActive(false);
        _character = ScriptableObject.CreateInstance<CharacterObject>();
        _table = ScriptableObject.CreateInstance<ActionResolverTable>();
        _character.actionResolverTable = _table;
        _player = _host.AddComponent<PlayerCharacter>();
        _player.characterObject = _character;
        _passive = _host.AddComponent<CharacterPassiveController>();
        _passive.playerCharacter = _player;
        _player.PassiveController = _passive;
        _ui = _host.AddComponent<AbilityUIManager>();
        Set(_ui, "playerCharacter", _player);
        Set(_ui, "teamLoader", _host.AddComponent<TeamLoader>());
        _texture = new Texture2D(4, 4);
        _original = Sprite.Create(_texture, new Rect(0, 0, 1, 1), Vector2.zero);
        _ordinary = Sprite.Create(_texture, new Rect(1, 0, 1, 1), Vector2.zero);
        _special = Sprite.Create(_texture, new Rect(2, 0, 1, 1), Vector2.zero);
        _image = Image("ActionIcon"); _image.sprite = _original;
        _overlay = Image("Window");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_host);
        Object.DestroyImmediate(_character); Object.DestroyImmediate(_table);
        Object.DestroyImmediate(_original); Object.DestroyImmediate(_ordinary);
        Object.DestroyImmediate(_special); Object.DestroyImmediate(_texture);
    }

    [TestCase(AttackCategory.Normal)]
    [TestCase(AttackCategory.Skill)]
    [TestCase(AttackCategory.Ultimate)]
    public void SelectedWindowReplacesIconAndRestoresItWithoutConsumingGameplay(AttackCategory category)
    {
        Bind(category);
        CombatAction ordinary = Action(category, "ordinary", _ordinary);
        CombatAction special = Action(category, "window", _special);
        special.priority = 10;
        special.conditions.Add(new RuntimeTagActionCondition { tag = "icon.window" });
        if (category == AttackCategory.Skill) special.actionType = CombatActionType.Ultimate;
        ((List<PassiveActionModifierDefinition>)Get(_passive, "timedWindowModifiers")).Add(
            new PassiveActionModifierDefinition { category = category, actionId = special.actionId,
                requiredTags = new List<RuntimeTagRequirement> { new RuntimeTagRequirement { tag = "icon.window", expectedValue = true } } });

        Refresh();
        Assert.That(_image.sprite, Is.SameAs(ordinary.icon));
        Assert.That(_overlay.gameObject.activeSelf, Is.False);
        _passive.RuntimeState.SetTag("icon.window", 5f);
        Refresh();
        var tags = (IList)Get(_passive.RuntimeState, "runtimeTags");
        tags[0].GetType().GetField("expiresAt").SetValue(tags[0], Time.time - 1f);
        Refresh();
        Assert.That(_image.sprite, Is.SameAs(ordinary.icon), "Expiry restores the ordinary action too.");
        Assert.That(_overlay.gameObject.activeSelf, Is.False);
        _passive.RuntimeState.SetTag("icon.window", 5f);
        Refresh();
        Assert.That(_image.sprite, Is.SameAs(special.icon));
        Assert.That(_overlay.gameObject.activeSelf, Is.True);
        Assert.That(_overlay.sprite, Is.SameAs(special.icon), "The window fill must preserve the action silhouette.");
        Assert.That(_overlay.fillAmount, Is.InRange(0.99f, 1f));
        Assert.That(_passive.RuntimeState.HasTag("icon.window"), Is.True);
        Assert.That(_player.TryResolveActionForContext(category, out CombatAction selected, out _, true), Is.True);
        Assert.That(selected, Is.SameAs(special));
        _passive.RuntimeState.RemoveTag("icon.window");
        Refresh();
        Assert.That(_image.sprite, Is.SameAs(ordinary.icon));
        Assert.That(_overlay.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void HigherPriorityWindowWinsAndUnavailableCounterActionFallsBack()
    {
        Bind(AttackCategory.Normal);
        var ordinary = Action(AttackCategory.Normal, "normal", _ordinary);
        var special = Action(AttackCategory.Normal, "counter", _special);
        special.priority = 20; special.resourceCounterId = "charge"; special.resourceCost = 1;
        Refresh(); Assert.That(_image.sprite, Is.SameAs(ordinary.icon));
        _passive.RuntimeState.SetCounter("charge", 1);
        Refresh(); Assert.That(_image.sprite, Is.SameAs(special.icon));
        Assert.That(_passive.RuntimeState.GetCounter("charge"), Is.EqualTo(1));
    }

    [Test]
    public void MissingArtAfterCharacterChangeRestoresOriginalPrefabSprite()
    {
        Bind(AttackCategory.Normal);
        var action = Action(AttackCategory.Normal, "normal", _special);
        Refresh(); Assert.That(_image.sprite, Is.SameAs(_special));
        var other = ScriptableObject.CreateInstance<CharacterObject>();
        var table = ScriptableObject.CreateInstance<ActionResolverTable>();
        try
        {
            table.comboActions.Add(new ComboAction { comboTree = Tree() });
            other.actionResolverTable = table; _player.characterObject = other;
            typeof(AbilityUIManager).GetMethod("HandleCharacterChanged", Private).Invoke(_ui, new object[] { other });
            Assert.That(_image.sprite, Is.SameAs(_original));
            Assert.That(_overlay.gameObject.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(other); Object.DestroyImmediate(table); }
    }

    [Test]
    public void GroundMismatchDoesNotAdvertiseAnAvailableReplacement()
    {
        Bind(AttackCategory.Normal);
        Action(AttackCategory.Normal, "ground", _ordinary);
        var air = Action(AttackCategory.Normal, "air", _special);
        air.context = CombatActionContext.Air; air.priority = 100;
        Refresh(); Assert.That(_image.sprite, Is.SameAs(_ordinary));
    }

    [Test]
    public void CooldownKeepsTheSelectedPictureDimAndDoesNotConsumeTheTimer()
    {
        Bind(AttackCategory.Skill);
        var action = (SkillAction)Action(AttackCategory.Skill, "cooldown", _ordinary);
        action.cooldown = 20f;
        var combat = _host.AddComponent<PlayerCombat>(); combat.playerCharacter = _player; _player.Combat = combat;
        combat.StartActionCooldown(AttackCategory.Skill, action.actionName, action.actionId);
        float before = combat.GetRemainingCooldown(AttackCategory.Skill, action.cooldown, action.actionName, action.actionId);
        Refresh();
        Assert.That(_image.sprite, Is.SameAs(_ordinary));
        Assert.That(_image.color.a, Is.EqualTo(100f / 255f).Within(0.001f));
        Assert.That(combat.GetRemainingCooldown(AttackCategory.Skill, action.cooldown, action.actionName, action.actionId), Is.EqualTo(before).Within(0.01f));
    }

    [Test]
    public void InsufficientUltimateEnergyDimsTheAuthoredIcon()
    {
        Bind(AttackCategory.Ultimate);
        var action = (UltimateAction)Action(AttackCategory.Ultimate, "ultimate", _ordinary);
        action.energyCost = 100f;
        Refresh(); Assert.That(_image.sprite, Is.SameAs(_ordinary));
        Assert.That(_image.color.a, Is.EqualTo(100f / 255f).Within(0.001f));
        Assert.That(_overlay.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void PrefabWiresNormalIconAndItsExistingWindowOverlay()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/CombatUI/Player_UI.prefab");
        var ui = root.GetComponentInChildren<AbilityUIManager>(true);
        var icon = (Image)Get(ui, "normalAttackIcon");
        var overlay = (Image)Get(ui, "normalAttackFollowUpOverlay");
        Assert.That(icon, Is.Not.Null); Assert.That(overlay, Is.Not.Null);
        Assert.That(overlay.transform.IsChildOf(icon.transform), Is.True);
    }

    [Test]
    public void MainAuthoredPicturesRenderInThePrefabAndJudgmentCutStaysOnSkill()
    {
        const string main = "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset";
        Object.DestroyImmediate(_character); Object.DestroyImmediate(_table);
        _character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterObject>(main));
        _table = Object.Instantiate(_character.actionResolverTable);
        _character.actionResolverTable = _table; _player.characterObject = _character;
        var progression = ScriptableObject.CreateInstance<Project.Progression.PlayerMetaProgression>();
        _passive.SetMetaProgression(progression);
        progression.SetPotentialLevel(_character, PotentialTrack.Passive, 4);
        var root = PrefabUtility.LoadPrefabContents("Assets/Project/Prefab/CombatUI/Player_UI.prefab");
        try
        {
            var ui = root.GetComponentInChildren<AbilityUIManager>(true);
            Set(ui, "playerCharacter", _player); Set(ui, "teamLoader", Get(_ui, "teamLoader"));
            foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
            var skill = (Image)Get(ui, "skillIcon");
            typeof(AbilityUIManager).GetMethod("RefreshWithoutReadyFeedback", Private).Invoke(ui, null);
            Assert.That(skill.sprite, Is.SameAs(_table.skillActions.Single(a => a.actionId == "main.skill.dark_chain").icon));
            Assert.That(skill.sprite, Is.Not.Null);
            Assert.That(_table.comboActions.All(a => a.icon != null), Is.True);
            Assert.That(_table.standaloneActions.Single(a => a.actionId == "main.dash.dimension_cut").icon, Is.Not.Null);
            Assert.That(_table.ultimateActions.Single(a => a.actionId == "main.ultimate.jailbreak_punisher").icon, Is.Not.Null);
            Capture(root, "main-ordinary.png");
            _passive.RuntimeState.SetTag(MainDimensionSlashAuthoring.ReadyTag, 5f);
            typeof(AbilityUIManager).GetMethod("RefreshWithoutReadyFeedback", Private).Invoke(ui, null);
            Assert.That(skill.sprite, Is.SameAs(_table.skillActions.Single(a => a.actionId == MainDimensionSlashAuthoring.ActionId).icon));
            Assert.That(((Image)Get(ui, "skillFollowUpOverlay")).gameObject.activeSelf, Is.True);
            Assert.That(((Image)Get(ui, "skillFollowUpOverlay")).sprite, Is.SameAs(skill.sprite));
            Capture(root, "main-judgment-cut-window.png");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); Object.DestroyImmediate(progression); }
    }

    private static void Capture(GameObject root, string file)
    {
        const int width = 1280, height = 720;
        const string folder = "Docs_JJK/3_Outputs/Reports/ActionWindowIcons";
        Directory.CreateDirectory(folder);
        var canvas = root.GetComponent<Canvas>();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        var host = new GameObject("Ability preview camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(host, root.scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.scene = root.scene;
        camera.orthographic = true; camera.orthographicSize = 5.4f;
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f, .04f, .07f);
        var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
        var previous = RenderTexture.active; var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(folder + "/" + file, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(host); Object.DestroyImmediate(target);
        }
    }

    private void Bind(AttackCategory category)
    {
        string prefix = category == AttackCategory.Normal ? "normalAttack" : category == AttackCategory.Skill ? "skill" : "ult";
        Set(_ui, prefix + "Icon", _image); Set(_ui, prefix + "FollowUpOverlay", _overlay);
    }

    private CombatAction Action(AttackCategory category, string id, Sprite icon)
    {
        CombatAction action;
        if (category == AttackCategory.Normal) { var a = new ComboAction(); _table.comboActions.Add(a); action = a; }
        else if (category == AttackCategory.Skill) { var a = new SkillAction(); _table.skillActions.Add(a); action = a; }
        else { var a = new UltimateAction { energyCost = 0f }; _table.ultimateActions.Add(a); action = a; }
        action.actionId = id; action.actionName = id; action.icon = icon; action.comboTree = Tree();
        return action;
    }
    private static ComboTree Tree() => new ComboTree { rootNode = new ComboNode { stepData = new AttackStepData() } };
    private Image Image(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(_host.transform);
        return obj.GetComponent<Image>();
    }
    private void Refresh() => typeof(AbilityUIManager).GetMethod("RefreshWithoutReadyFeedback", Private).Invoke(_ui, null);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}
#endif
