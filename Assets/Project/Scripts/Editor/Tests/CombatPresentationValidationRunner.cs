using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class CombatPresentationValidationRunner
{
    private const string RunningPath = "Temp/CombatPresentationTests.running";
    private static TestRunnerApi _runner;
    private static readonly Results Callback = new Results();

    static CombatPresentationValidationRunner()
    {
        EditorApplication.delayCall += () => { if (File.Exists(RunningPath)) Register(); };
    }

    private static void Register()
    {
        if (_runner == null) _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        _runner.UnregisterCallbacks(Callback);
        _runner.RegisterCallbacks(Callback);
    }

    [MenuItem("Tools/Project/Combat/Run Overlay and Camera Tests")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.WriteAllText(RunningPath, "requested");
        Register();
        _runner.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { "^ScreenImpulseCameraTests",
                "^CameraAnimationSequenceTests", "^MainDimensionSlashTests.CameraPositionControlPreservesImpulseNoise" }
        }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!File.Exists(RunningPath)) return;
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("Temp/CombatPresentationTests.xml"));
            File.WriteAllText("Temp/CombatPresentationTests-summary.txt",
                $"{result.PassCount} passed; {result.FailCount} failed; {result.SkipCount} skipped.\n{result.Message}");
            Debug.Log($"[Combat Presentation Tests] {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
            File.Delete(RunningPath);
        }
    }
}
