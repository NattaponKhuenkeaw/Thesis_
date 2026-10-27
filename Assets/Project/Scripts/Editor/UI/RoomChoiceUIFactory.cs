#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Project.EditorTools
{
    public static class RoomChoiceUIFactory
    {
        private const string PrefabPath = "Assets/Project/Prefab/UI/RoomChoiceUI.prefab";

        [MenuItem("Project/UI/Open Room Choice UI Prefab")]
        public static void CreatePrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (source == null)
            {
                Debug.LogError($"[RoomChoiceUI] Missing authored prefab: {PrefabPath}");
                return;
            }
            AssetDatabase.OpenAsset(source);
        }

        [MenuItem("Project/UI/Sync Room Choice Prefab → Resources")]
        public static void SyncPrefabToResources()
        {
            var runtime = RuntimeUiPrefabLinkEditor.EnsureLink("RoomChoiceUI");
            AssetDatabase.SaveAssets();
            Selection.activeObject = runtime;
            EditorGUIUtility.PingObject(runtime);
        }
    }
}
#endif
