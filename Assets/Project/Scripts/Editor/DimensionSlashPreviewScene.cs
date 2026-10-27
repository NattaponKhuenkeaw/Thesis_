#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Combat;

[InitializeOnLoad]
public static class DimensionSlashPreviewScene
{
    private const string ReturnSceneKey = "DimensionSlash.ReturnScene";
    private const string RootStateKey = "DimensionSlash.RootStates";
    [System.Serializable] private sealed class RootStates
    {
        public string[] names;
        public bool[] active;
    }
    static DimensionSlashPreviewScene() => EditorApplication.playModeStateChanged += Restore;

    [MenuItem("Tools/Project/Combat/Capture Dimension Slash Runtime")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play mode before capture.");
        if (SceneManager.sceneCount != 1)
            throw new System.InvalidOperationException("Capture requires one open scene; no scene is saved automatically.");
        Scene previous = SceneManager.GetActiveScene();
        SessionState.SetString(ReturnSceneKey, previous.path);
        GameObject[] roots = previous.GetRootGameObjects();
        var rootStates = new RootStates { names = new string[roots.Length], active = new bool[roots.Length] };
        for (int i = 0; i < roots.Length; i++)
        {
            rootStates.names[i] = roots[i].name; rootStates.active[i] = roots[i].activeSelf;
        }
        SessionState.SetString(RootStateKey, JsonUtility.ToJson(rootStates));
        try
        {
        // Keep the user's scene loaded, including unsaved edits, but out of the Play-mode simulation.
        for (int i = 0; i < roots.Length; i++) roots[i].SetActive(false);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var ground = new GameObject("Preview Ground", typeof(BoxCollider2D));
        ground.layer = LayerMask.NameToLayer("Ground"); ground.transform.position = new Vector3(0f, -1.5f, 0f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(100f, 1f);
        GameObject playerObject = Prefab("Assets/Project/Prefab/Player.prefab");
        playerObject.transform.position = Vector3.zero;
        var player = playerObject.GetComponent<PlayerCharacter>();
        var main = AssetDatabase.LoadAssetAtPath<CharacterObject>(MainDimensionSlashAuthoring.MainPath + "CharacterObject_main.asset");
        player.characterObject = main;
        playerObject.GetComponent<PlayerMovement>().profile = main.movementProfile;
        var passive = playerObject.GetComponent<CharacterPassiveController>();
        if (passive == null) passive = playerObject.AddComponent<CharacterPassiveController>();
        passive.playerCharacter = player; player.PassiveController = passive;
        playerObject.GetComponent<PlayerCombat>().passiveController = passive;
        GameObject enemyObject = Prefab("Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab");
        enemyObject.transform.position = new Vector3(3f, 0f, 0f);
        var cameraRig = Prefab("Assets/Project/Prefab/Camera/CameraSetUp.prefab");
        var manager = cameraRig.GetComponent<CameraManager>();
        var settings = new SerializedObject(manager);
        settings.FindProperty("_playerTransform").objectReferenceValue = player.transform; settings.ApplyModifiedPropertiesWithoutUndo();
        var vfxObject = new GameObject("Preview VFX Pool"); var vfx = vfxObject.AddComponent<VFXManager>();
        var vfxSettings = new SerializedObject(vfx);
        vfxSettings.FindProperty("library").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VFXLibrary>("Assets/Project/Character/Hero/Material/VFXLibrary.asset");
        vfxSettings.FindProperty("vfxPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/Hero/Material/TestingHeroData/VFXGenericPrefab.prefab").GetComponent<GenericVFXPlayer>();
        vfxSettings.ApplyModifiedPropertiesWithoutUndo();
        var freeze = new GameObject("Preview Hitstop", typeof(GameFreezeManager));
        new GameObject("Preview Audio", typeof(GameAudioManager));
        var lightObject = new GameObject("Preview Light", typeof(UnityEngine.Rendering.Universal.Light2D));
        var light = lightObject.GetComponent<UnityEngine.Rendering.Universal.Light2D>(); light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        var probeObject = new GameObject("Dimension Slash Runtime Probe", typeof(DimensionSlashSceneIsolation));
        var probe = probeObject.AddComponent<DimensionSlashRuntimeProbe>();
        probe.player = player; probe.target = enemyObject.GetComponent<EnemyController>();
        probe.captureCamera = cameraRig.GetComponentInChildren<Camera>();
        probe.characterSource = main;
        EditorSceneManager.SaveScene(scene, "Assets/Project/Scenes/DimensionSlash_Validation.unity");
        EditorApplication.isPlaying = true;
        }
        catch
        {
            Restore(PlayModeStateChange.EnteredEditMode);
            throw;
        }
    }

    private static GameObject Prefab(string path) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));

    private static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        string path = SessionState.GetString(ReturnSceneKey, "");
        if (string.IsNullOrEmpty(path)) return;
        SessionState.EraseString(ReturnSceneKey);
        Scene original = SceneManager.GetSceneByPath(path);
        if (!original.IsValid() || !original.isLoaded) return;
        var rootState = JsonUtility.FromJson<RootStates>(SessionState.GetString(RootStateKey, ""));
        GameObject[] roots = original.GetRootGameObjects();
        if (rootState != null)
            for (int i = 0; i < roots.Length && i < rootState.names.Length; i++)
                if (roots[i].name == rootState.names[i]) roots[i].SetActive(rootState.active[i]);
        SessionState.EraseString(RootStateKey);
        SceneManager.SetActiveScene(original);
        Scene preview = SceneManager.GetSceneByPath("Assets/Project/Scenes/DimensionSlash_Validation.unity");
        if (preview.IsValid() && preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
    }
}
#endif
