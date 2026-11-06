using Project.Core.ZoneState;
using UnityEditor;
using UnityEngine;

public static class Elite101Authoring
{
    public const string SourcePath = "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab";
    public const string ElitePath = "Assets/Project/Character/enemy/enemy/Enm101/Enemy101_Elite.prefab";
    public const string KitPath = "Assets/Project/Resources/EnemySo/EK_Elite101.asset";

    [MenuItem("Tools/Never Night/Enemies/Configure Elite 101")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play mode first.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var normal = source != null ? source.GetComponent<EnemyController>() : null;
        if (normal == null || normal.Kit == null)
            throw new System.InvalidOperationException("Normal 101 or its kit is missing.");
        var kit = AssetDatabase.LoadAssetAtPath<EnemyKit>(KitPath);
        if (kit == null)
        {
            kit = Object.Instantiate(normal.Kit);
            kit.name = "EK_Elite101";
            AssetDatabase.CreateAsset(kit, KitPath);
        }
        using (var data = new SerializedObject(kit))
        {
            data.FindProperty("combatRole").enumValueIndex = (int)EnemyCombatRole.CommitMelee;
            data.FindProperty("attackRange").floatValue = 2.4f;
            data.FindProperty("holdRange").floatValue = 1.8f;
            data.FindProperty("warningDuration").floatValue = .9f;
            data.FindProperty("hitWindowDuration").floatValue = .22f;
            data.FindProperty("attackCooldown").floatValue = 3.8f;
            data.FindProperty("recoveryPlantDuration").floatValue = .8f;
            data.FindProperty("lungeDistance").floatValue = 2f;
            data.FindProperty("bodyCharge").boolValue = true;
            data.FindProperty("chargesPerAttack").intValue = 2;
            data.FindProperty("repeatChargeWarning").floatValue = .3f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssetIfDirty(kit);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = "Enemy101_Elite";
            ConfigurePresentation(instance, source);
            var enemy = instance.GetComponent<EnemyController>();
            using (var data = new SerializedObject(enemy))
            {
                data.FindProperty("maxHP").floatValue = normal.maxHP * 3f;
                data.FindProperty("currentHP").floatValue = normal.maxHP * 3f;
                data.FindProperty("kit").objectReferenceValue = kit;
                data.FindProperty("resistOrdinaryHitReactions").boolValue = true;
                data.FindProperty("startArmored").boolValue = false;
                data.FindProperty("isEliteOrBoss").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.SaveAsPrefabAsset(instance, ElitePath);
        }
        finally { Object.DestroyImmediate(instance); }
        var hard = Resources.Load<EncounterSO>("EncounterSO/Encounter_Hard");
        if (hard == null) throw new System.InvalidOperationException("Hard encounter is missing.");
        using (var data = new SerializedObject(hard))
        {
            data.FindProperty("finalChamberElite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ElitePath);
            data.FindProperty("eliteReplaces").objectReferenceValue = source;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssetIfDirty(hard);
        Debug.Log("Elite 101 configured: one replacement in the last Hard chamber, two short rushes, 3x HP, ordinary reaction resistance.");
    }

    [MenuItem("Tools/Never Night/Enemies/Update Elite 101 Rush Presentation")]
    public static void UpdatePresentation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play mode first.");
        var kit = AssetDatabase.LoadAssetAtPath<EnemyKit>(KitPath);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (kit == null || source == null)
            throw new System.InvalidOperationException("Elite 101 must be created first.");
        using (var data = new SerializedObject(kit))
        {
            data.FindProperty("hitWindowDuration").floatValue = .22f;
            data.FindProperty("lungeDistance").floatValue = 2f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssetIfDirty(kit);
        var root = PrefabUtility.LoadPrefabContents(ElitePath);
        try
        {
            ConfigurePresentation(root, source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, ElitePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigurePresentation(GameObject root, GameObject source)
    {
        Vector3 scale = source.transform.localScale;
        root.transform.localScale = new Vector3(scale.x * 1.5f, scale.y * 1.5f, scale.z);
        var visual = root.GetComponentInChildren<EnemyChargeTelegraph>(true);
        if (visual == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/EnemyChargeTelegraph.prefab");
            if (prefab == null) throw new System.InvalidOperationException("Charge telegraph prefab is missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            instance.transform.localPosition = Vector3.zero;
            visual = instance.GetComponent<EnemyChargeTelegraph>();
        }
        using var data = new SerializedObject(visual);
        data.FindProperty("externallyDriven").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
    }
}
