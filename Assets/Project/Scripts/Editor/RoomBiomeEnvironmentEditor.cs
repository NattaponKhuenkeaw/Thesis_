using System.Linq;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoomBiomeEnvironment), true)]
public class RoomBiomeEnvironmentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var environment = (RoomBiomeEnvironment)target;
        EditorGUILayout.LabelField("Background Preview", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(Application.isPlaying || environment.BiomeOneEnvironment == null || environment.BiomeTwoEnvironment == null))
        {
            int current = environment.BiomeTwoEnvironment != null && environment.BiomeTwoEnvironment.activeSelf ? 1 : 0;
            int selected = GUILayout.Toolbar(current, new[] { "Biome 1", "Biome 2" });
            if (selected != current) Preview(environment, selected);
            var group = selected == 1 ? environment.BiomeTwoEnvironment : environment.BiomeOneEnvironment;
            string path = group == null ? "" : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(group);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                if (GUILayout.Button("Open Background Prefab")) PrefabStageUtility.OpenPrefab(path);
        }
        EditorGUILayout.HelpBox("Preview changes only the visible artwork. Play Mode uses the current run biome. Apply scene overrides to the background prefab to share edits with gameplay.", MessageType.Info);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    public static void Preview(RoomBiomeEnvironment environment, int biome)
    {
        if (Application.isPlaying) return;
        var first = environment.BiomeOneEnvironment;
        var second = environment.BiomeTwoEnvironment;
        if (first == null || second == null) return;
        Undo.RecordObjects(new Object[] { first, second }, "Preview room biome");
        first.SetActive(biome == 0); second.SetActive(biome == 1);
        PrefabUtility.RecordPrefabInstancePropertyModifications(first);
        PrefabUtility.RecordPrefabInstancePropertyModifications(second);
        if (environment.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(environment.gameObject.scene);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Project/Room Backgrounds/Boss")]
    public static void OpenBoss() => Open("Boss");
    [MenuItem("Tools/Project/Room Backgrounds/Rest")]
    public static void OpenRest() => Open("Rest");
    [MenuItem("Tools/Project/Room Backgrounds/Level")]
    public static void OpenLevel() => Open("Level");

    private static void Open(string kind)
    {
        var stage = PrefabStageUtility.OpenPrefab(kind == "Boss" ? LayoutRoadAuthoring.BossPath : SafeRoomBiomeAuthoring.LayoutPath(kind));
        Selection.activeGameObject = stage.prefabContentsRoot.GetComponentsInChildren<RoomBiomeEnvironment>(true).Single().gameObject;
    }
}
