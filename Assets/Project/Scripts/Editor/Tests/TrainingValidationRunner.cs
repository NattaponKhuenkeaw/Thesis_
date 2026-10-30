using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class TrainingValidationRunner
{
    private static TestRunnerApi _runner;
    private static readonly Results Callback = new Results();
    static TrainingValidationRunner()
    {
        EditorApplication.delayCall += Register;
        EditorApplication.delayCall += RunRequestedValidation;
    }

    // File-triggered recovery also works when a restarted Editor has no MCP connection.
    private static void RunRequestedValidation()
    {
        const string request = "Temp/TrainingValidation.request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequestedValidation;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        TrainingSceneAuthoring.Author();
        EditorApplication.delayCall += Run;
    }

    private static void Register()
    {
        if (_runner == null) _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        _runner.UnregisterCallbacks(Callback);
        _runner.RegisterCallbacks(Callback);
    }
    [MenuItem("Tools/Project/Training/Run Focused Tests")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Register();
        _runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^TrainingSessionTests", "^TrainingFlowTests", "^MainMenuAnimationTests.AnimationIsAuthored",
                "^CameraAnimationSequenceTests", "^RunGlobalLevelTests", "^EnemyNoticeCombatRegistrationTests", "^EnemyLifecycleTests" }
        }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            const string folder = "Docs_JJK/3_Outputs/Reports/TrainingScene";
            Directory.CreateDirectory(folder);
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(folder + "/tests.xml"));
            File.WriteAllText(folder + "/test-summary.txt", $"{result.PassCount} passed; {result.FailCount} failed; {result.SkipCount} skipped.\n{result.Message}");
            Debug.Log($"[Training Tests] {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
