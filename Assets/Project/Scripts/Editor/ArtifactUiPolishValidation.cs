#if UNITY_INCLUDE_TESTS
using System.Reflection;
using System;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArtifactUiPolishValidation
{
    [MenuItem("Tools/Neo-JJK/UI/Validate Artifact UI Polish")]
    public static void Run()
    {
        const string resultsPath = "Logs/ArtifactUiPolishTests.xml";
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene("Assets/Project/Scenes/Test_Lobby.unity", OpenSceneMode.Single);
        typeof(ArtifactIsolatedVisualTestRunner).GetMethod("RunIsolated", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { resultsPath, new[]
            {
                "ArtifactUiPolishTests", "ArtifactInstanceSystemTests", "PotentialScreenTests"
            } });
        var results = new XmlDocument();
        results.Load(resultsPath);
        if (results.DocumentElement.GetAttribute("failed") != "0")
            throw new InvalidOperationException($"Artifact UI tests failed. See {resultsPath}.");
        TeamBuilderInventoryValidation.Run();
        ArtifactIsolatedVisualTestRunner.CaptureRarityUi();
    }
}
#endif
