#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Xml;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal.Filters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShadowMeterValidation
{
    [MenuItem("Tools/Project/Combat/Validate Shadow Meter")]
    public static void Run()
    {
        RunFixtures(new[] { "ShadowMeterTests", "PlayerActionInterruptTests", "CameraGirlPotentialTests", "AbilityUIFeedbackTests" },
            "Temp/shadow-meter-tests.xml", true);
    }

    [MenuItem("Tools/Project/Combat/Validate Main Potential")]
    public static void RunMainPotential()
    {
        RunFixtures(new[] { "ShadowMeterTests", "MainSkillMagnetismTests", "EnemyPositionReactionTests", "CombatDamageTypeTests" }, "Temp/main-potential-tests.xml", false);
    }

    [MenuItem("Tools/Project/Combat/Validate Main Skill Revision")]
    public static void RunMainSkillRevision()
    {
        RunFixtures(new[]
        {
            "MainSkillMagnetismTests", "EnemyPositionReactionTests", "CombatDamageTypeTests",
            "ShadowMeterTests.SkillThreeVulnerabilityReplacesTwentyInsteadOfStackingToSixty",
            "ShadowMeterTests.SkillHitsSnapshotMainsAttackForAlliesMasteryAndRefreshWithoutStacking",
            "ShadowMeterTests.SkillPotentialLevelsResolveTheNewProgression",
            "ShadowMeterTests.ReceiverStatBuffRefreshCapturesLatestSourceSnapshot"
        }, "Temp/main-skill-revision-tests.xml", false);
    }

    private static void RunFixtures(string[] fixtures, string resultPath, bool capturePreviews)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before validation.");
        Scene previous = SceneManager.GetActiveScene();
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(isolated);
        try
        {
            // Reuse the project's synchronous executor so the user's dirty scene is never saved or replaced.
            var filter = new OrFilter();
            foreach (string fixture in fixtures)
                filter.Add(new FullNameFilter(fixture));
            MethodInfo execute = typeof(ArtifactIsolatedVisualTestRunner).GetMethod("RunOnUnityThread", BindingFlags.NonPublic | BindingFlags.Static);
            ITestResult result = (ITestResult)execute.Invoke(null, new object[] { filter });
            string path = Path.GetFullPath(resultPath);
            using (XmlWriter writer = XmlWriter.Create(path, new XmlWriterSettings { Indent = true })) result.ToXml(true).WriteTo(writer);
            Debug.Log($"Shadow Meter validation: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped. Results: {path}");
            if (capturePreviews)
            {
                CaptureMeterPreview();
                CaptureHudLayoutPreview();
            }
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (isolated.IsValid() && isolated.isLoaded) EditorSceneManager.CloseScene(isolated, true);
        }
    }

    private static void CaptureMeterPreview()
    {
        var canvasObject = new GameObject("Meter Preview Canvas", typeof(Canvas));
        var cameraObject = new GameObject("Meter Preview Camera", typeof(Camera));
        var owner = new GameObject("Meter Preview Player");
        owner.SetActive(false);
        var progression = ScriptableObject.CreateInstance<Project.Progression.PlayerMetaProgression>();
        RenderTexture target = new RenderTexture(600, 220, 24);
        Texture2D capture = new Texture2D(600, 220, TextureFormat.RGB24, false);
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
            camera.targetTexture = target;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            PlayerCharacter player = owner.AddComponent<PlayerCharacter>();
            player.characterObject = AssetDatabase.LoadAssetAtPath<CharacterObject>(
                "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
            var passive = owner.AddComponent<CharacterPassiveController>();
            passive.playerCharacter = player;
            player.PassiveController = passive;
            passive.SetMetaProgression(progression);
            MethodInfo refresh = typeof(CombatMeterUI).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int row = 0; row < 3; row++)
            {
                progression.SetPotentialLevel(player.characterObject, PotentialTrack.Passive, row == 2 ? 4 : 0);
                passive.RuntimeState.SetCounter("main.shadow", row == 0 ? 0 : row == 1 ? 30 : 100);
                var meterObject = new GameObject("Meter Preview " + row, typeof(RectTransform), typeof(CombatMeterUI));
                RectTransform rect = meterObject.GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(70f, -45f - row * 62f);
                rect.localScale = Vector3.one * 1.2f;
                CombatMeterUI meter = meterObject.GetComponent<CombatMeterUI>();
                meter.Bind(player);
                refresh.Invoke(meter, null);
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0f, 0f, 600f, 220f), 0, 0);
            capture.Apply();
            File.WriteAllBytes(Path.GetFullPath("Temp/shadow-meter-preview.png"), capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            cameraObject.GetComponent<Camera>().targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(progression);
        }
    }

    private static void CaptureHudLayoutPreview()
    {
        GameObject hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Prefab/CombatUI/Player_UI.prefab"));
        var cameraObject = new GameObject("HUD Preview Camera", typeof(Camera));
        var owner = new GameObject("HUD Preview Owner");
        owner.SetActive(false);
        var progression = ScriptableObject.CreateInstance<Project.Progression.PlayerMetaProgression>();
        var texture = new RenderTexture(1024, 576, 24);
        var capture = new Texture2D(1024, 576, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            camera.targetTexture = texture;
            Canvas canvas = hud.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var player = owner.AddComponent<PlayerCharacter>();
            player.characterObject = AssetDatabase.LoadAssetAtPath<CharacterObject>(
                "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
            var passive = owner.AddComponent<CharacterPassiveController>();
            passive.playerCharacter = player;
            player.PassiveController = passive;
            passive.SetMetaProgression(progression);
            passive.RuntimeState.SetCounter("main.shadow", 30);
            passive.RuntimeState.SetCounter("main.shadow.cached_cuts", 2);
            CombatMeterUI meter = hud.GetComponentInChildren<CombatMeterUI>(true);
            meter.Bind(player);
            typeof(CombatMeterUI).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(meter, null);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            capture.ReadPixels(new Rect(0f, 0f, 1024f, 576f), 0, 0);
            capture.Apply();
            File.WriteAllBytes(Path.GetFullPath("Temp/shadow-hud-layout.png"), capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            cameraObject.GetComponent<Camera>().targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(capture);
            UnityEngine.Object.DestroyImmediate(hud);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(progression);
        }
    }
}
#endif
