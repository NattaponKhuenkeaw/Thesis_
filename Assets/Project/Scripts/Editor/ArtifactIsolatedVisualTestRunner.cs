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
            foreach (string fixture in new[] { "ArtifactAdvancedVisualTests", "ArtifactAreaGeometryTests",
                "ArtifactCurseThunderTests", "FieldEffectArtifactSequenceTests" })
                filter.Add(new FullNameFilter(fixture));
            ITestResult result = RunOnUnityThread(filter);
            if (result.PassCount + result.FailCount + result.SkipCount == 0)
                throw new InvalidOperationException("No isolated artifact tests were selected.");
            string path = Path.GetFullPath("Docs_JJK/3_Outputs/Reports/FieldEffectVisuals/scales-crest-tests.xml");
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
