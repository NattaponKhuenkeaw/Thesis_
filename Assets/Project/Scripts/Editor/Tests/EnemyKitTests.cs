#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EnemyKitTests
{
    [Test]
    public void KitOverridesPrefabRoleAndRange()
    {
        var go = new GameObject(nameof(KitOverridesPrefabRoleAndRange));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            Author(kit, EnemyCombatRole.RangedHold, 9f);

            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;
            enemy.AttackRangeOverride = 1.2f;
            enemy.Kit = kit;

            Assert.That(enemy.CombatRole, Is.EqualTo(EnemyCombatRole.RangedHold));
            Assert.That(enemy.UsesRangedAttack, Is.True);
            Assert.That(enemy.NeverEvade, Is.True);
            Assert.That(enemy.ResolveAttackRange(1.5f), Is.EqualTo(9f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(kit);
        }
    }

    [Test]
    public void ClearingKitRestoresPrefabValues()
    {
        var go = new GameObject(nameof(ClearingKitRestoresPrefabValues));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            Author(kit, EnemyCombatRole.RangedHold, 9f);

            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.CommitMelee;
            enemy.AttackRangeOverride = 1.2f;
            enemy.Kit = kit;
            enemy.Kit = null;

            Assert.That(enemy.CombatRole, Is.EqualTo(EnemyCombatRole.CommitMelee));
            Assert.That(enemy.UninterruptibleOnceWindup, Is.True);
            Assert.That(enemy.ResolveAttackRange(1.5f), Is.EqualTo(1.2f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(kit);
        }
    }

    private static void Author(EnemyKit kit, EnemyCombatRole role, float attackRange)
    {
        var serialized = new SerializedObject(kit);
        serialized.FindProperty("combatRole").enumValueIndex = (int)role;
        serialized.FindProperty("attackRange").floatValue = attackRange;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
