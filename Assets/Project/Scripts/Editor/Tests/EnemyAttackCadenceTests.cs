#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EnemyAttackCadenceTests
{
    [TestCase("Enm101/Enemy1.prefab", 2.7f, .8f)]
    [TestCase("Enm102/Enemy102.prefab", 1.5f, 1.17f)]
    [TestCase("Enm103/Enemy103.prefab", 3.5f, .72f)]
    public void ProductionEnemiesResolveTheirTunedRecoveryFromTheKit(string path, float cooldown, float plant)
    {
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/" + path).GetComponent<EnemyController>();
        Assert.That(enemy.Kit, Is.Not.Null);
        Assert.That(enemy.ResolveAttackCooldown(10f), Is.EqualTo(cooldown).Within(.0001f));
        Assert.That(enemy.ResolveRecoveryPlantDuration(10f), Is.EqualTo(plant).Within(.0001f));
        Assert.That(enemy.Kit.WalkingSpeedMultiplier, Is.EqualTo(path.StartsWith("Enm103") ? .65f : 1f));
    }

    [Test]
    public void ShieldUsesItsOwnShorterGuardWaitWithoutShorteningTheWarningOrRush()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab");
        using (var data = new SerializedObject(root.GetComponent<EnemyShieldGuard>()))
            Assert.That(data.FindProperty("guardWaitDuration").floatValue, Is.EqualTo(4.5f));
        var kit = root.GetComponent<EnemyController>().Kit;
        Assert.That(kit.WarningDuration, Is.EqualTo(1f));
        Assert.That(kit.HitWindowDuration, Is.EqualTo(.48f));
        Assert.That(kit.LungeDistance, Is.EqualTo(9f));
    }

    [Test]
    public void FlyerRetainsItsReadableWarningAndProjectileSpeed()
    {
        var kit = AssetDatabase.LoadAssetAtPath<EnemyKit>("Assets/Project/Resources/EnemySo/EK_FlyingAimed.asset");
        Assert.That(kit.WarningDuration, Is.EqualTo(.7f));
        Assert.That(kit.HitWindowDuration, Is.EqualTo(.15f));
        Assert.That(kit.ShotSpeed, Is.EqualTo(10f));
    }

    [Test]
    public void BossAndTrainingDummyDoNotUseTheNormalEnemyBalance()
    {
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab").GetComponent<EnemyController>();
        Assert.That(boss.Kit, Is.Null);
        Assert.That(boss.maxHP, Is.EqualTo(4000f));
        var dummy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab").GetComponent<EnemyController>();
        Assert.That(dummy.maxHP, Is.EqualTo(1e9f));
    }
}
#endif
