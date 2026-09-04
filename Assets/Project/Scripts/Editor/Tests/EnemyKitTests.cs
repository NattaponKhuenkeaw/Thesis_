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

    [Test]
    public void KitTimingBeatsTheGraphValue()
    {
        var go = new GameObject(nameof(KitTimingBeatsTheGraphValue));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            AuthorTiming(kit, ("warningDuration", 0.8f), ("attackCooldown", 1.1f));

            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.Kit = kit;

            Assert.That(enemy.ResolveWarningDuration(0.5f), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(enemy.ResolveAttackCooldown(0.55f), Is.EqualTo(1.1f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(kit);
        }
    }

    [Test]
    public void UnauthoredTimingLeavesTheGraphInCharge()
    {
        var go = new GameObject(nameof(UnauthoredTimingLeavesTheGraphInCharge));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            // A fresh kit authors no beat, so wearing one changes nothing until somebody fills a field.
            // This is what keeps per-enemy timing opt-in instead of a silent behaviour change.
            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.Kit = kit;

            Assert.That(enemy.ResolveWarningDuration(0.5f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(enemy.ResolveHitWindowDuration(0.2f), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(enemy.ResolveAttackCooldown(0.55f), Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(enemy.ResolveInterruptCooldown(0.35f), Is.EqualTo(0.35f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(kit);
        }
    }

    [Test]
    public void ZeroIsARealBeatNotAnUnsetField()
    {
        var go = new GameObject(nameof(ZeroIsARealBeatNotAnUnsetField));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            // Guards the sentinel choice: attack range reads 0 as "unset", timing must not, or an enemy
            // authored to swing with no recovery would silently inherit the graph's recovery instead.
            AuthorTiming(kit, ("attackCooldown", 0f), ("commitTelegraphFraction", 0f));

            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.Kit = kit;

            Assert.That(enemy.ResolveAttackCooldown(0.55f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(enemy.ResolveCommitTelegraphFraction(0.35f), Is.EqualTo(0f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(kit);
        }
    }

    [Test]
    public void RangedRoleStillRefusesKitLunge()
    {
        var go = new GameObject(nameof(RangedRoleStillRefusesKitLunge));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            Author(kit, EnemyCombatRole.RangedHold, 9f);
            AuthorTiming(kit, ("lungeDistance", 2f));

            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.Kit = kit;

            Assert.That(enemy.UsesRangedAttack, Is.True);
            Assert.That(enemy.ResolveLungeDistance(0.55f), Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(kit);
        }
    }

    [Test]
    public void AuthoredHoldRangeBeatsTheRangedDefault()
    {
        var go = new GameObject(nameof(AuthoredHoldRangeBeatsTheRangedDefault));
        EnemyKit kit = ScriptableObject.CreateInstance<EnemyKit>();
        try
        {
            Author(kit, EnemyCombatRole.RangedHold, 12f);
            AuthorTiming(kit, ("holdRange", 8.5f));

            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.Kit = kit;

            Assert.That(enemy.ResolveHoldRange(12f), Is.EqualTo(8.5f).Within(0.0001f));
            Assert.That(enemy.ResolveAttackRange(1.5f), Is.EqualTo(12f));
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

    private static void AuthorTiming(EnemyKit kit, params (string field, float value)[] beats)
    {
        var serialized = new SerializedObject(kit);
        foreach ((string field, float value) in beats)
        {
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, $"'{field}' is gone. The kit timing plumbing was renamed.");
            property.floatValue = value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
