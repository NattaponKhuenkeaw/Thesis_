#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.InventorySystem.Items;
using Project.Progression;
using Project.TeamBuilder.UI.Panels;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PotentialScreenTests
{
    private GameObject _prefab;
    private CharacterPotentialPanel _panel;
    private CharacterProgressionState _progression;
    private CharacterItem _item;
    private CharacterObject _character;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        _prefab = PrefabUtility.LoadPrefabContents("Assets/Project/Prefab/UI/TeamCanvas.prefab");
        _panel = _prefab.GetComponentInChildren<CharacterPotentialPanel>(true);
        _character = AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
        _progression = ScriptableObject.CreateInstance<CharacterProgressionState>();
        _item = ScriptableObject.CreateInstance<CharacterItem>(); _item.characterObjectData = _character; _item.SyncFromCharacterObjectData();
        var serialized = new SerializedObject(_panel);
        serialized.FindProperty("metaProgression").objectReferenceValue = _progression;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _panel.PopulateData(_item, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_prefab != null) PrefabUtility.UnloadPrefabContents(_prefab);
        Object.DestroyImmediate(_item); Object.DestroyImmediate(_progression);
    }

    [Test]
    public void MissingFragmentsAndPreviousLevelStillBlockUpgrade()
    {
        var first = _character.kitDefinition.potentialNodes.First(n => n.track == PotentialTrack.Skill && n.level == 1);
        var later = _character.kitDefinition.potentialNodes.First(n => n.track == PotentialTrack.Skill && n.level == 2);
        Assert.That(CharacterPotentialPanel.ResolveNodeState(_character, _progression, first), Is.EqualTo(CharacterPotentialNodeState.InsufficientFragments));
        _panel.SelectNode(first); Assert.That(_panel.TryUpgradeSelectedNode(), Is.False);
        _progression.AddFragments(_item, 97);
        Assert.That(CharacterPotentialPanel.ResolveNodeState(_character, _progression, later), Is.EqualTo(CharacterPotentialNodeState.Locked));
        _panel.SelectNode(later); Assert.That(_panel.TryUpgradeSelectedNode(), Is.False);
        Assert.That(_progression.GetFragmentCount(_character.characterId), Is.EqualTo(97));
    }

    [Test]
    public void ConfirmSpendsOnceAndRefreshesProgressAndUnlockedPreview()
    {
        _progression.AddFragments(_item, 97);
        var node = _progression.GetNextPotentialNode(_character, PotentialTrack.Skill);
        _panel.SelectNode(node);
        Assert.That(_panel.TryUpgradeSelectedNode(), Is.True);
        Assert.That(_progression.GetFragmentCount(_character.characterId), Is.EqualTo(97 - node.fragmentCost));
        Assert.That(_progression.GetPotentialLevel(_character, PotentialTrack.Skill), Is.EqualTo(1));
        Assert.That(_panel.TryUpgradeSelectedNode(), Is.False);
        var window = _panel.GetComponentInChildren<CharacterPotentialUpgradeWindow>(true);
        Assert.That(window.gameObject.activeSelf, Is.True);
        var windowData = new SerializedObject(window);
        Assert.That(((GameObject)windowData.FindProperty("upgradedStateObject").objectReferenceValue).activeSelf, Is.True);
        Assert.That(windowData.FindProperty("fragmentText").objectReferenceValue, Is.Null);
    }

    [Test]
    public void SwitchingCharacterRefreshesIconsAndClosesPreview()
    {
        _panel.SelectNode(_progression.GetNextPotentialNode(_character, PotentialTrack.Skill));
        _item.characterObjectData = AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Character/Hero/test_1_CameraGirl/CharacterObject_cg.asset");
        _panel.PopulateData(_item, null);
        var data = new SerializedObject(_panel);
        Assert.That(_panel.GetComponentInChildren<CharacterPotentialUpgradeWindow>(true).gameObject.activeSelf, Is.False);
        var slots = data.FindProperty("nodeSlots");
        Assert.That(slots.arraySize, Is.EqualTo(12));
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = (CharacterPotentialNodeUI)slots.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(slot, Is.Not.Null);
            var icon = (Image)new SerializedObject(slot).FindProperty("iconImage").objectReferenceValue;
            var kit = _item.characterObjectData.kitDefinition;
            var art = _item.characterObjectData.potentialArtwork;
            var expected = i < 4 ? art?.skill : i < 8 ? art?.ultimate : art?.passive;
            if (expected == null) expected = i < 4 ? kit.skill.icon : i < 8 ? kit.ultimate.icon : kit.passive.icon;
            Assert.That(icon.sprite, Is.EqualTo(expected));
        }
    }

    [Test]
    public void CancelClosesPreviewAndClearsSelection()
    {
        var window = _panel.GetComponentInChildren<CharacterPotentialUpgradeWindow>(true);
        typeof(CharacterPotentialUpgradeWindow).GetMethod("OnEnable", Private).Invoke(window, null);
        _panel.SelectNode(_progression.GetNextPotentialNode(_character, PotentialTrack.Skill));
        var cancel = (Button)new SerializedObject(window).FindProperty("cancelButton").objectReferenceValue;
        cancel.onClick.Invoke();
        Assert.That(window.gameObject.activeSelf, Is.False);
        Assert.That(_panel.TryUpgradeSelectedNode(), Is.False);
    }

    [Test]
    public void BoardIsScrollableAndLevelsRiseFromBottomWithoutProfile()
    {
        var data = new SerializedObject(_panel);
        var scroll = (ScrollRect)data.FindProperty("contentScrollRect").objectReferenceValue;
        Assert.That(scroll, Is.Not.Null);
        Assert.That(scroll.horizontal && scroll.vertical, Is.True);
        Assert.That(_panel.transform.Find("PotentialPresentation"), Is.Null);
        var slots = data.FindProperty("nodeSlots");
        for (int track = 0; track < 3; track++)
        {
            float previous = float.NegativeInfinity;
            for (int level = 0; level < 4; level++)
            {
                var slot = (CharacterPotentialNodeUI)slots.GetArrayElementAtIndex(track * 4 + level).objectReferenceValue;
                float y = ((RectTransform)slot.transform).anchoredPosition.y;
                Assert.That(y, Is.GreaterThan(previous), "Levels must rise from I at the bottom to IV at the top.");
                previous = y;
            }
        }
        var window = _panel.GetComponentInChildren<CharacterPotentialUpgradeWindow>(true);
        Assert.That(((RectTransform)window.transform).anchorMax.x, Is.EqualTo(1f));
    }

    private static UnityEditor.TestTools.TestRunner.Api.TestRunnerApi _runner;

    [MenuItem("Project/UI/Run Potential Screen Tests")]
    public static void RunTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (_runner == null) _runner = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        _runner.RegisterCallbacks(new Results());
        _runner.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter
        {
            testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
            groupNames = new[] { "^PotentialScreenTests" }
        }));
    }

    private sealed class Results : UnityEditor.TestTools.TestRunner.Api.ICallbacks
    {
        public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor tests) { }
        public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
        public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result) { }
        public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
        {
            const string folder = "Docs_JJK/3_Outputs/Reports/PotentialScreen";
            System.IO.Directory.CreateDirectory(folder);
            UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.SaveResultToFile(result, System.IO.Path.GetFullPath(folder + "/tests.xml"));
            System.IO.File.WriteAllText(folder + "/test-summary.txt", $"{result.PassCount} passed; {result.FailCount} failed; {result.SkipCount} skipped.");
            _runner.UnregisterCallbacks(this);
        }
    }
}
#endif
