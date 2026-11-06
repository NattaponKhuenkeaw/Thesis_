#if UNITY_EDITOR
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEngine;

public static class BossChargeTelegraphAuthoring
{
    public const string Boss111Path = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";
    public const string Boss112Path = "Assets/Project/Character/enemy/Boss/Observer112/Boss112.prefab";
    private const string LanePath = "Assets/Project/Prefab/EnemyChargeTelegraph.prefab";

    public static void Apply()
    {
        Attach<BossPatternController>(Boss111Path);
        Attach<Boss112Controller>(Boss112Path);
        AssetDatabase.SaveAssets();
    }

    private static void Attach<T>(string path) where T : MonoBehaviour
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var lane = root.GetComponentInChildren<EnemyChargeTelegraph>(true);
            if (lane == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LanePath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = "ChargeWarning";
                lane = instance.GetComponent<EnemyChargeTelegraph>();
            }
            using (var data = new SerializedObject(lane))
            {
                data.FindProperty("externallyDriven").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var data = new SerializedObject(root.GetComponent<T>()))
            {
                data.FindProperty("chargeTelegraph").objectReferenceValue = lane;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
