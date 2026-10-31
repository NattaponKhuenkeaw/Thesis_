using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class AbilityIconValidationRunner
{
    private static TestRunnerApi _runner;
    private static bool _running;
    private static readonly Results Callback = new Results();

    [MenuItem("Tools/Project/UI/Wire Timed Opportunity Prefabs")]
    public static void WirePrefabs()
    {
        const string path = "Assets/Project/Prefab/UI/Hero_TeamSwitchPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var slot = root.GetComponent<CharacterSwitchSlot>();
            var group = root.transform.Find("InactiveCanvasGroup");
            if (slot == null || group == null || !group.TryGetComponent<CanvasGroup>(out var canvasGroup))
                throw new System.InvalidOperationException("The authored standby CanvasGroup is missing.");
            slot.inactiveCanvasGroup = canvasGroup;
            // Preserve the authored opacity from the intake snapshot as tuning data;
            // the group itself rests at full opacity until cooldown begins.
            slot.inactiveAlpha = 0.34f;
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        const string abilityPath = "Assets/Project/Prefab/CombatUI/Player_UI.prefab";
        root = PrefabUtility.LoadPrefabContents(abilityPath);
        try
        {
            var ui = root.GetComponentInChildren<AbilityUIManager>(true);
            var serialized = new SerializedObject(ui);
            foreach (string prefix in new[] { "normalAttack", "skill", "ult" })
            {
                var icon = serialized.FindProperty(prefix + "Icon").objectReferenceValue as UnityEngine.UI.Image;
                var effect = serialized.FindProperty(prefix + "WindowEffect").objectReferenceValue as TimedOpportunityGraphic;
                if (icon == null || effect == null)
                    throw new System.InvalidOperationException("The authored ability icon/outline is missing: " + prefix);
                effect.transform.SetParent(icon.transform, false);
                effect.rectTransform.anchorMin = Vector2.zero;
                effect.rectTransform.anchorMax = Vector2.one;
                effect.rectTransform.offsetMin = effect.rectTransform.offsetMax = Vector2.zero;
                effect.maskable = false;
                effect.raycastTarget = false;
                effect.Clear();
            }
            PrefabUtility.SaveAsPrefabAsset(root, abilityPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static string _reportFolder = "Docs_JJK/3_Outputs/Reports/ActionWindowIcons";
    private static bool _includeTeamTests;

    [MenuItem("Tools/Project/UI/Apply Stronger Opportunity Outlines")]
    public static void ApplyStrongerOutlines()
    {
        const string path = "Assets/Project/Prefab/CombatUI/Player_UI.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var effect in root.GetComponentsInChildren<TimedOpportunityGraphic>(true))
            {
                var serialized = new SerializedObject(effect);
                serialized.FindProperty("pulseSeconds").floatValue = 1.35f;
                serialized.FindProperty("expandedScale").floatValue = 1.85f;
                serialized.FindProperty("padding").floatValue = 6f;
                serialized.FindProperty("lineWidth").floatValue = 3.5f;
                serialized.FindProperty("glowWidth").floatValue = 14f;
                serialized.FindProperty("innerSpillFraction").floatValue = 0.025f;
                serialized.FindProperty("outlineColor").colorValue = new Color(0.82f, 0.97f, 1f, 1f);
                serialized.FindProperty("glowColor").colorValue = new Color(0.24f, 0.78f, 1f, 1f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Project/UI/Run Timed Opportunity Tests")]
    public static void RunTimedOpportunities()
    {
        _includeTeamTests = true;
        _reportFolder = "Docs_JJK/3_Outputs/Reports/TimedOpportunityUI";
        Run();
    }

    [MenuItem("Tools/Project/UI/Run Ability Icon Tests")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (_runner == null) _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        _runner.UnregisterCallbacks(Callback); _runner.RegisterCallbacks(Callback);
        _running = true;
        _runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = _includeTeamTests
                ? new[] { "^AbilityUIFeedbackTests", "^ActionWindowIconTests", "^UIFeedbackWiggleTests", "^QteTeamSwapTests" }
                : new[] { "^AbilityUIFeedbackTests", "^ActionWindowIconTests", "^UIFeedbackWiggleTests" } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!_running) return;
            _running = false;
            string folder = _reportFolder;
            _includeTeamTests = false;
            _reportFolder = "Docs_JJK/3_Outputs/Reports/ActionWindowIcons";
            Directory.CreateDirectory(folder);
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(folder + "/tests.xml"));
            File.WriteAllText(folder + "/test-summary.txt", $"{result.PassCount} passed; {result.FailCount} failed; {result.SkipCount} skipped.\n{result.Message}");
            Debug.Log($"[Ability Icon Tests] {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
