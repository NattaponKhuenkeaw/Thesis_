#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class AdminConsoleSpawnTests
{
    private const string AdminPath = "Assets/Project/Prefab/UI/AdminCanvas.prefab";
    private const string EnemyRoot = "Assets/Project/Character/enemy/enemy/";

    [TestCase("enemy101Prefab", "Enm101/Enemy1.prefab")]
    [TestCase("enemy102Prefab", "Enm102/Enemy102.prefab")]
    [TestCase("enemy103Prefab", "Enm103/Enemy103.prefab")]
    public void DedicatedSpawnReferenceResolvesToTheAuthoredVariant(string field, string path)
    {
        var admin = LoadAdmin();
        var data = new SerializedObject(admin);
        var expected = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoot + path);
        Assert.That(expected, Is.Not.Null);
        Assert.That(data.FindProperty(field).objectReferenceValue, Is.EqualTo(expected),
            "Spawn needs the variant root reference, not EnemyCore's root file ID.");
    }

    [Test]
    public void RandomSpawnPoolAndFallbackContainValidEnemyPrefabs()
    {
        var data = new SerializedObject(LoadAdmin());
        Assert.That(data.FindProperty("enemyPrefab").objectReferenceValue, Is.Not.Null);
        var pool = data.FindProperty("enemyPrefabs");
        Assert.That(pool.arraySize, Is.GreaterThanOrEqualTo(3));
        for (int i = 0; i < pool.arraySize; i++)
        {
            var prefab = pool.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            Assert.That(prefab, Is.Not.Null, "Random spawn slot " + i);
            Assert.That(prefab.GetComponent<EnemyController>(), Is.Not.Null);
        }
    }

    [TestCase(nameof(AdminConsole.SpawnEnemy101))]
    [TestCase(nameof(AdminConsole.SpawnEnemy102))]
    [TestCase(nameof(AdminConsole.SpawnEnemy103))]
    public void SpawnButtonCallsItsExistingConsoleMethod(string method)
    {
        var admin = LoadAdmin();
        int matches = 0;
        foreach (var button in admin.GetComponentsInChildren<Button>(true))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) != method)
                    continue;
                matches++;
                Assert.That(button.onClick.GetPersistentTarget(i), Is.EqualTo(admin));
                Assert.That(button.onClick.GetPersistentListenerState(i),
                    Is.Not.EqualTo(UnityEngine.Events.UnityEventCallState.Off));
                Assert.That(button.interactable, Is.True);
            }
        }
        Assert.That(matches, Is.EqualTo(1));
    }

    private static AdminConsole LoadAdmin()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdminPath);
        Assert.That(prefab, Is.Not.Null);
        var admin = prefab.GetComponent<AdminConsole>();
        Assert.That(admin, Is.Not.Null);
        return admin;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RepairOnlyFillsMissingReferences(bool hasCustomAssignment)
    {
        var host = new GameObject("AdminReferenceTest");
        var custom = new GameObject("CustomEnemyPrefab");
        var replacement = new GameObject("ReplacementEnemyPrefab");
        try
        {
            var data = new SerializedObject(host.AddComponent<AdminConsole>());
            var field = data.FindProperty("enemy102Prefab");
            field.objectReferenceValue = hasCustomAssignment ? custom : null;
            data.ApplyModifiedPropertiesWithoutUndo();
            var assign = typeof(AdminEnemyPrefabRelink).GetMethod("Assign",
                BindingFlags.Static | BindingFlags.NonPublic);
            bool changed = (bool)assign.Invoke(null, new object[] { field, replacement });
            Assert.That(changed, Is.EqualTo(!hasCustomAssignment));
            Assert.That(field.objectReferenceValue, Is.EqualTo(hasCustomAssignment ? custom : replacement));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(custom);
            Object.DestroyImmediate(replacement);
        }
    }
}
#endif
