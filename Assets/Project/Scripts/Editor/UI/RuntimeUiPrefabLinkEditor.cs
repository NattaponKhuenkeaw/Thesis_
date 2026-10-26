#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.EditorTools
{
    public static class RuntimeUiPrefabLinkEditor
    {
        [MenuItem("Project/UI/Refresh Runtime Prefab Links")]
        public static void EnsureAll()
        {
            foreach (string name in new[] { "SaveSelectPanel", "PauseMenu", "RoomChoiceUI" }) EnsureLink(name);
        }

        public static GameObject EnsureLink(string name)
        {
            string sourcePath = $"Assets/Project/Prefab/UI/{name}.prefab";
            string runtimePath = $"Assets/Project/Resources/UI/{name}.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException($"Missing authored UI: {sourcePath}");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(runtimePath);
            if (existing != null)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing) != source)
                    throw new InvalidOperationException($"{runtimePath} must be a variant of {sourcePath}; no assets were overwritten.");
                return existing;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Project/Resources/UI"))
                AssetDatabase.CreateFolder("Assets/Project/Resources", "UI");

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                // A connected root saves as a variant, so art edits stay on the original prefab.
                var result = PrefabUtility.SaveAsPrefabAsset(instance, runtimePath);
                if (result == null) throw new InvalidOperationException($"Failed to save {runtimePath}");
                return result;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
