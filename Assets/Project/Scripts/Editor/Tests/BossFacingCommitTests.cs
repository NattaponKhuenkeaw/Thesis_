#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEngine;

public class BossFacingCommitTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(BossPatternId.HeavyCleave)]
    [TestCase(BossPatternId.JumpSmash)]
    [TestCase(BossPatternId.BullCharge)]
    public void WindupCommitsFacingUntilCleanup(BossPatternId pattern)
    {
        var host = new GameObject("BossFacingTest");
        var target = new GameObject("CrossingPlayer");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(host);
            host.SetActive(false);
            var boss = host.AddComponent<BossPatternController>();
            typeof(BossPatternController).GetField("enemy", Flags).SetValue(boss, enemy);
            typeof(BossPatternController).GetField("target", Flags).SetValue(boss, target.transform);
            typeof(BossPatternController).GetField("flashDuringTelegraph", Flags).SetValue(boss, false);
            EnemyFacingUtil.FaceHorizontal(host.transform, 1f);
            var timing = new BossPatternTiming { telegraphDuration = 1f };
            var windup = (IEnumerator)typeof(BossPatternController).GetMethod("Telegraph", Flags)
                .Invoke(boss, new object[] { timing, pattern, null, 0f });

            Assert.That(windup.MoveNext(), Is.True);
            Assert.That(enemy.FacingLocked, Is.True);
            target.transform.position = Vector3.left * 5f;
            EnemyFacingUtil.FaceHorizontal(host.transform, -1f);
            Assert.That(EnemyFacingUtil.GetFacingSign(host.transform), Is.EqualTo(1f));

            typeof(BossPatternController).GetMethod("CleanupAttackState", Flags).Invoke(boss, null);
            Assert.That(enemy.FacingLocked, Is.False);
            EnemyFacingUtil.FaceHorizontal(host.transform, -1f);
            Assert.That(EnemyFacingUtil.GetFacingSign(host.transform), Is.EqualTo(-1f));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }
}
#endif
