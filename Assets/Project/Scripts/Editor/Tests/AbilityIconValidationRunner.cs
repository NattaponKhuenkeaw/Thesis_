using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class AbilityIconValidationRunner
{
    private static TestRunnerApi _runner;
    private static bool _running;
    private static readonly Results Callback = new Results();

    [MenuItem("Tools/Project/UI/Run Ability Icon Tests")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (_runner == null) _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        _runner.UnregisterCallbacks(Callback); _runner.RegisterCallbacks(Callback);
        _running = true;
        _runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            groupNames = new[] { "^AbilityUIFeedbackTests", "^ActionWindowIconTests", "^UIFeedbackWiggleTests" } }));
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
            const string folder = "Docs_JJK/3_Outputs/Reports/ActionWindowIcons";
            Directory.CreateDirectory(folder);
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(folder + "/tests.xml"));
            File.WriteAllText(folder + "/test-summary.txt", $"{result.PassCount} passed; {result.FailCount} failed; {result.SkipCount} skipped.\n{result.Message}");
            Debug.Log($"[Ability Icon Tests] {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        }
    }
}
