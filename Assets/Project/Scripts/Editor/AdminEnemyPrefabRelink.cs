using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot repair. Prefab variants of EnemyCore no longer expose the old root id,
/// so AdminCanvas and encounter pools were left with a missing enemy reference.
/// </summary>
[InitializeOnLoad]
static class AdminEnemyPrefabRelink
{
    const string Enemy1Path = "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab";
    const string Enemy102Path = "Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab";
    const string Enemy103Path = "Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab";
    const string AdminPath = "Assets/Project/Prefab/UI/AdminCanvas.prefab";

    static AdminEnemyPrefabRelink()
    {
        EditorApplication.delayCall += Relink;
    }

    [MenuItem("Tools/Project/Repair Missing Enemy Prefab References")]
    static void Relink()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Relink;
            return;
        }

        GameObject enemy1 = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy1Path);
        GameObject enemy102 = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy102Path);
        GameObject enemy103 = AssetDatabase.LoadAssetAtPath<GameObject>(Enemy103Path);
        Debug.Log($"[AdminEnemyPrefabRelink] loaded 101={enemy1 != null} 102={enemy102 != null} 103={enemy103 != null}");
        if (enemy1 == null || enemy102 == null || enemy103 == null)
            return;

        bool changed = false;
        changed |= RelinkAdmin(enemy1, enemy102, enemy103);
        changed |= RelinkEncounter("Assets/Project/Resources/EncounterSO/Encounter_Easy.asset", enemy1, enemy102, enemy103);
        changed |= RelinkEncounter("Assets/Project/Resources/EncounterSO/Encounter_Medium.asset", enemy1, enemy102, enemy103);
        changed |= RelinkEncounter("Assets/Project/Resources/EncounterSO/Encounter_Hard.asset", enemy1, enemy102, enemy103);
        changed |= RelinkRoutes(enemy1, enemy102, enemy103);

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[AdminEnemyPrefabRelink] Rewrote missing enemy prefab references.");
        }
        else
        {
            Debug.Log("[AdminEnemyPrefabRelink] Enemy prefab references were already assigned.");
        }
    }

    static bool RelinkAdmin(GameObject enemy1, GameObject enemy102, GameObject enemy103)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(AdminPath);
        var admin = root != null ? root.GetComponent<AdminConsole>() : null;
        if (admin == null)
            return false;

        var so = new SerializedObject(admin);
        bool changed = false;
        changed |= Assign(so.FindProperty("enemyPrefab"), enemy1);
        changed |= Assign(so.FindProperty("enemy101Prefab"), enemy1);
        changed |= Assign(so.FindProperty("enemy102Prefab"), enemy102);
        changed |= Assign(so.FindProperty("enemy103Prefab"), enemy103);
        SerializedProperty list = so.FindProperty("enemyPrefabs");
        if (list != null && list.isArray && list.arraySize >= 3)
        {
            changed |= Assign(list.GetArrayElementAtIndex(0), enemy1);
            changed |= Assign(list.GetArrayElementAtIndex(1), enemy102);
            changed |= Assign(list.GetArrayElementAtIndex(2), enemy103);
        }

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(root);
        }
        return changed;
    }

    static bool RelinkEncounter(string path, GameObject enemy1, GameObject enemy102, GameObject enemy103)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (asset == null)
            return false;

        var so = new SerializedObject(asset);
        SerializedProperty types = so.FindProperty("enemyTypes");
        if (types == null || !types.isArray || types.arraySize < 3)
            return false;

        bool changed = false;
        changed |= Assign(types.GetArrayElementAtIndex(0).FindPropertyRelative("prefab"), enemy1);
        changed |= Assign(types.GetArrayElementAtIndex(1).FindPropertyRelative("prefab"), enemy102);
        changed |= Assign(types.GetArrayElementAtIndex(2).FindPropertyRelative("prefab"), enemy103);
        if (changed)
            so.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    static bool RelinkRoutes(GameObject enemy1, GameObject enemy102, GameObject enemy103)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Project/Resources/MapSO/RouteOfferTables.asset");
        if (asset == null)
            return false;

        var so = new SerializedObject(asset);
        SerializedProperty profiles = so.FindProperty("profiles");
        if (profiles == null || !profiles.isArray)
            return false;

        bool changed = false;
        for (int i = 0; i < profiles.arraySize; i++)
        {
            SerializedProperty pool = profiles.GetArrayElementAtIndex(i).FindPropertyRelative("enemyPool");
            if (pool == null || !pool.isArray || pool.arraySize < 3)
                continue;
            changed |= Assign(pool.GetArrayElementAtIndex(0).FindPropertyRelative("prefab"), enemy1);
            changed |= Assign(pool.GetArrayElementAtIndex(1).FindPropertyRelative("prefab"), enemy102);
            changed |= Assign(pool.GetArrayElementAtIndex(2).FindPropertyRelative("prefab"), enemy103);
        }

        if (changed)
            so.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    static bool Assign(SerializedProperty property, GameObject prefab)
    {
        if (property == null || property.objectReferenceValue != null)
            return false;
        property.objectReferenceValue = prefab;
        return true;
    }
}
