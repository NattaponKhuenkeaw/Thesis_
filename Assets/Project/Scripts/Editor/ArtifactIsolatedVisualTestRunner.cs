#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArtifactIsolatedVisualTestRunner
{
    public static void ValidateAndCapture()
    {
        // A fresh batch editor starts in an untitled scene, which Unity cannot keep
        // while opening the additive test scene. Read the saved lobby without saving it.
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene("Assets/Project/Scenes/Test_Lobby.unity", OpenSceneMode.Single);
        Run();
        ArtifactAdvancedVisualPreviewWindow.Capture();
    }

    [MenuItem("Tools/Project/Field Effects/Run Isolated Artifact Tests")]
    public static void Run()
    {
        RunIsolated("Docs_JJK/3_Outputs/Reports/FieldEffectVisuals/scales-crest-tests.xml",
            "ArtifactAdvancedVisualTests", "ArtifactAreaGeometryTests",
            "ArtifactCurseThunderTests", "FieldEffectArtifactSequenceTests");
    }

    [MenuItem("Tools/Project/Field Effects/Run Artifact Inventory Tests")]
    public static void RunInventory()
    {
        RunIsolated("Temp/ArtifactInventoryTests.xml", "ArtifactInstanceSystemTests");
    }

    [MenuItem("Tools/Project/Field Effects/Run Artifact Regression Tests")]
    public static void RunRegression()
    {
        RunIsolated("Temp/ArtifactRegressionTests.xml", "ArtifactInstanceSystemTests",
            "ArtifactPassiveDiagnosticTests", "ArtifactAdvancedVisualTests", "ArtifactAreaGeometryTests",
            "ArtifactCurseThunderTests", "FieldEffectArtifactSequenceTests");
    }

    [MenuItem("Tools/Project/Field Effects/Capture Artifact Rarity UI")]
    public static void CaptureRarityUi()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(isolated);
        var inventory = ScriptableObject.CreateInstance<Project.ShopSystem.PlayerRunInventory>();
        try
        {
            var picker = UnityEngine.Object.Instantiate(Resources.Load<Project.UI.ArtifactRewardPickerUI>(
                Project.UI.ArtifactRewardPickerUI.ResourcePath));
            string[] paths = { "RarityR/R_RustedOathRing", "RaritySR/SR_10_ProtectionOfJoseph",
                "Phase1/P1_04_BrokenChronoRelic" };
            var choices = new Project.InventorySystem.Artifacts.ArtifactInstance[3];
            var generator = new Project.InventorySystem.Artifacts.ArtifactRollGenerator(inventory.ArtifactRollTable);
            for (int i = 0; i < choices.Length; i++)
            {
                var definition = Resources.Load<Project.InventorySystem.Items.ArtifactDefinition>(
                    "ItemSO/Artifacts/" + paths[i]);
                choices[i] = generator.Generate(definition, i, 160 + i, 2);
            }
            picker.Present(choices, inventory, 1, 2, _ => false).MoveNext();
            for (int i = 1; i <= 3; i++) picker.transform.Find($"Card{i}").gameObject.SetActive(true);
            MethodInfo capture = typeof(TeamBuilderInventoryValidation).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
            capture.Invoke(null, new object[] { picker.gameObject, "artifact-rarity-picker", 1920, 1080 });
            capture.Invoke(null, new object[] { picker.gameObject, "artifact-rarity-picker-4x3", 1024, 768 });

            for (int i = 1; i <= 3; i++) picker.transform.Find($"Card{i}").gameObject.SetActive(false);
            var tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/UI/TeamArtifactInventoryCard.prefab");
            for (int i = 0; i < choices.Length; i++)
            {
                var tile = UnityEngine.Object.Instantiate(tilePrefab, picker.transform).GetComponent<Project.TeamBuilder.UI.ArtifactGridItemUI>();
                ((RectTransform)tile.transform).anchoredPosition = new Vector2((i - 1) * 220, 0);
                choices[i].isFavourite = i == 1;
                tile.Init(choices[i], inventory.ResolveArtifactDefinition(choices[i].definitionId),
                    picker.GetComponent<Canvas>(), inventory.ArtifactRollTable);
            }
            capture.Invoke(null, new object[] { picker.gameObject, "artifact-rarity-tiles", 1280, 720 });
            var dialog = UnityEngine.Object.Instantiate(Resources.Load<Project.TeamBuilder.UI.ArtifactCopyMenuUI>(
                Project.TeamBuilder.UI.ArtifactCopyMenuUI.DialogPath), picker.transform);
            capture.Invoke(null, new object[] { picker.gameObject, "artifact-discard-dialog", 1280, 720 });
            dialog.gameObject.SetActive(false);
            UnityEngine.Object.Instantiate(Resources.Load<Project.TeamBuilder.UI.ArtifactCopyMenuUI>(
                Project.TeamBuilder.UI.ArtifactCopyMenuUI.MenuPath), picker.transform);
            capture.Invoke(null, new object[] { picker.gameObject, "artifact-copy-menu", 1280, 720 });
            Debug.Log("Artifact rarity UI captures saved under Logs/TeamBuilderInventoryPreviews.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventory);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (isolated.IsValid() && isolated.isLoaded) EditorSceneManager.CloseScene(isolated, true);
        }
    }

    private static void RunIsolated(string resultPath, params string[] fixtures)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before running isolated artifact checks.");
        Scene previous = SceneManager.GetActiveScene();
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(isolated);
        DateTime started = DateTime.UtcNow;
        try
        {
            // These fixtures do not load or replace scenes. Unity's main-thread executor is used
            // directly to avoid the TestRunnerApi save/reload task for the user's dirty scene.
            var filter = new OrFilter();
            foreach (string fixture in fixtures)
                filter.Add(new FullNameFilter(fixture));
            ITestResult result = RunOnUnityThread(filter);
            if (result.PassCount + result.FailCount + result.SkipCount == 0)
                throw new InvalidOperationException("No isolated artifact tests were selected.");
            string path = Path.GetFullPath(resultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (XmlWriter writer = XmlWriter.Create(path, new XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartElement("test-run");
                writer.WriteAttributeString("runner", "Unity main-thread test executor, isolated additive scene");
                writer.WriteAttributeString("result", result.ResultState.Status.ToString());
                writer.WriteAttributeString("total", (result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount).ToString());
                writer.WriteAttributeString("passed", result.PassCount.ToString());
                writer.WriteAttributeString("failed", result.FailCount.ToString());
                writer.WriteAttributeString("skipped", result.SkipCount.ToString());
                writer.WriteAttributeString("start-time", started.ToString("u"));
                writer.WriteAttributeString("end-time", DateTime.UtcNow.ToString("u"));
                result.ToXml(true).WriteTo(writer);
                writer.WriteEndElement();
            }
            Debug.Log($"Isolated artifact tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped. Results: {path}");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (isolated.IsValid() && isolated.isLoaded) EditorSceneManager.CloseScene(isolated, true);
        }
    }

    private static ITestResult RunOnUnityThread(ITestFilter filter)
    {
        Assembly engine = typeof(UnityEngine.TestTools.LogAssert).Assembly;
        Assembly editor = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).Assembly;
        Type builderType = engine.GetType("UnityEngine.TestTools.NUnitExtensions.UnityTestAssemblyBuilder", true);
        Type contextType = engine.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext", true);
        Type runnerType = engine.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestAssemblyRunner", true);
        Type factoryType = editor.GetType("UnityEditor.TestTools.TestRunner.EditmodeWorkItemFactory", true);
        PropertyInfo currentContext = contextType.GetProperty("CurrentContext", BindingFlags.Public | BindingFlags.Static);
        FieldInfo nunitContext = typeof(NUnit.Framework.TestContext).GetField("CurrentTestExecutionContext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        object previousUnityContext = currentContext.GetValue(null);
        object previousNunitContext = nunitContext.GetValue(null);
        object context = Activator.CreateInstance(contextType);
        PropertyInfo featureFlags = contextType.GetProperty("FeatureFlags");
        featureFlags.SetValue(context, Activator.CreateInstance(featureFlags.PropertyType, true));
        try
        {
            currentContext.SetValue(null, context);
            object builder = Activator.CreateInstance(builderType, new object[] { null, 0 });
            object factory = Activator.CreateInstance(factoryType, true);
            object runner = Activator.CreateInstance(runnerType, new[] { builder, factory, context });
            object platform = UnityEngine.TestTools.TestPlatform.EditMode;
            object settings = builderType.GetMethod("GetNUnitTestBuilderSettings", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { platform });
            runnerType.GetMethod("Load").Invoke(runner,
                new object[] { new[] { typeof(ArtifactCurseThunderTests).Assembly }, platform, settings });
            IEnumerator steps = ((IEnumerable)runnerType.GetMethod("Run")
                .Invoke(runner, new object[] { TestListener.NULL, filter })).GetEnumerator();
            while (steps.MoveNext())
                if (steps.Current != null)
                    throw new InvalidOperationException("This isolated runner supports synchronous tests only.");
            return (ITestResult)runnerType.GetProperty("Result").GetValue(runner);
        }
        finally
        {
            currentContext.SetValue(null, previousUnityContext);
            nunitContext.SetValue(null, previousNunitContext);
        }
    }
}
#endif
