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
        var artwork = serializedObject.FindProperty("biomeOneArtwork");
        using (new EditorGUI.DisabledScope(Application.isPlaying || environment.BiomeTwoEnvironment == null ||
            (environment.BiomeOneEnvironment == null && artwork.arraySize == 0)))
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
        if (second == null) return;
        var data = new SerializedObject(environment);
        var artwork = data.FindProperty("biomeOneArtwork");
        var parallax = data.FindProperty("biomeOneParallax");
        if (first == null && artwork.arraySize == 0) return;
        if (first == null)
        {
            var artworkDefaults = data.FindProperty("biomeOneArtworkDefaults");
            var parallaxDefaults = data.FindProperty("biomeOneParallaxDefaults");
            if (artworkDefaults.arraySize != artwork.arraySize)
            {
                artworkDefaults.arraySize = artwork.arraySize;
                for (int i = 0; i < artwork.arraySize; i++)
                    artworkDefaults.GetArrayElementAtIndex(i).boolValue =
                        ((SpriteRenderer)artwork.GetArrayElementAtIndex(i).objectReferenceValue)?.enabled ?? false;
            }
            if (parallaxDefaults.arraySize != parallax.arraySize)
            {
                parallaxDefaults.arraySize = parallax.arraySize;
                for (int i = 0; i < parallax.arraySize; i++)
                    parallaxDefaults.GetArrayElementAtIndex(i).boolValue =
                        ((Project.Parallax.ParallaxBackground)parallax.GetArrayElementAtIndex(i).objectReferenceValue)?.enabled ?? false;
            }
            data.ApplyModifiedProperties();
            for (int i = 0; i < artwork.arraySize; i++)
            {
                var renderer = (SpriteRenderer)artwork.GetArrayElementAtIndex(i).objectReferenceValue;
                if (renderer == null) continue;
                Undo.RecordObject(renderer, "Preview room biome");
                renderer.enabled = biome == 0 && artworkDefaults.GetArrayElementAtIndex(i).boolValue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            for (int i = 0; i < parallax.arraySize; i++)
            {
                var background = (Project.Parallax.ParallaxBackground)parallax.GetArrayElementAtIndex(i).objectReferenceValue;
                if (background == null) continue;
                Undo.RecordObject(background, "Preview room biome");
                background.enabled = biome == 0 && parallaxDefaults.GetArrayElementAtIndex(i).boolValue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(background);
            }
        }
        else
        {
            Undo.RecordObject(first, "Preview room biome");
            first.SetActive(biome == 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(first);
        }
        Undo.RecordObject(second, "Preview room biome");
        second.SetActive(biome == 1);
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
