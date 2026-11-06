#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

public class BossFacingCommitTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(BossPatternId.HeavyCleave, 1f, false)]
    [TestCase(BossPatternId.HeavyCleave, -1f, false)]
    [TestCase(BossPatternId.HeavyCleave, 1f, true)]
    [TestCase(BossPatternId.HeavyCleave, -1f, true)]
    [TestCase(BossPatternId.JumpSmash, 1f, false)]
    [TestCase(BossPatternId.JumpSmash, -1f, false)]
    [TestCase(BossPatternId.JumpSmash, 1f, true)]
    [TestCase(BossPatternId.JumpSmash, -1f, true)]
    [TestCase(BossPatternId.BullCharge, 1f, false)]
    [TestCase(BossPatternId.BullCharge, -1f, false)]
    [TestCase(BossPatternId.BullCharge, 1f, true)]
    [TestCase(BossPatternId.BullCharge, -1f, true)]
    public void WindupCommitsFacingUntilCleanup(BossPatternId pattern, float facing, bool fromSpriteChild)
    {
        var host = new GameObject("BossFacingTest");
        var target = new GameObject("CrossingPlayer");
        try
        {
            EnemyController enemy = EnemyLifecycleTestUtility.AddActive(host);
            var boss = host.AddComponent<BossPatternController>();
            // Stop autonomous AI without hiding the owner from active-hierarchy lookups.
            boss.enabled = false;
            typeof(BossPatternController).GetField("enemy", Flags).SetValue(boss, enemy);
            typeof(BossPatternController).GetField("target", Flags).SetValue(boss, target.transform);
            typeof(BossPatternController).GetField("flashDuringTelegraph", Flags).SetValue(boss, false);
            EnemyFacingUtil.FaceHorizontal(host.transform, facing);
            Transform caller = host.transform;
            if (fromSpriteChild)
            {
                caller = new GameObject("SpriteChild").transform;
                caller.SetParent(host.transform, false);
            }
            Assert.That(host.activeInHierarchy, Is.True);
            Assert.That(caller.GetComponentInParent<EnemyController>(), Is.SameAs(enemy));
            Assert.That(EnemyFacingUtil.GetFacingSign(host.transform), Is.EqualTo(facing));
            var timing = new BossPatternTiming { telegraphDuration = 1f };
            var windup = (IEnumerator)typeof(BossPatternController).GetMethod("Telegraph", Flags)
                .Invoke(boss, new object[] { timing, pattern, null, 0f });

            Assert.That(windup.MoveNext(), Is.True);
            Assert.That(enemy.FacingLocked, Is.True);
            target.transform.position = Vector3.left * facing * 5f;
            EnemyFacingUtil.FaceHorizontal(caller, -facing);
            Assert.That(EnemyFacingUtil.GetFacingSign(host.transform), Is.EqualTo(facing));
            Assert.That(EnemyFacingUtil.GetFacingSign(caller), Is.EqualTo(facing));

            typeof(BossPatternController).GetMethod("CleanupAttackState", Flags).Invoke(boss, null);
            Assert.That(enemy.FacingLocked, Is.False);
            EnemyFacingUtil.FaceHorizontal(host.transform, -facing);
            Assert.That(EnemyFacingUtil.GetFacingSign(host.transform), Is.EqualTo(-facing));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(target);
        }
    }
}

public class BossFacingRuntimeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator ActiveBossPrefabKeepsFacingAcrossAllWindupsAndReleasesAfterCleanup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var container = new GameObject("BossFacingRuntimeTest");
        container.SetActive(false);
        var target = new GameObject("CrossingPlayer");
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab"), container.transform);
        var boss = root.GetComponent<BossPatternController>();
        var enemy = root.GetComponent<EnemyController>();
        boss.enabled = false;
        using (var settings = new SerializedObject(boss))
        {
            settings.FindProperty("bindScreenHealthHud").boolValue = false;
            settings.FindProperty("revealOnSpawn").boolValue = false;
            settings.FindProperty("flashDuringTelegraph").boolValue = false;
            settings.FindProperty("attackWarningSfx").objectReferenceValue = null;
            settings.FindProperty("cleaveSfx").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        try
        {
            container.SetActive(true);
            yield return null;
            EnemyLifecycleTestUtility.Activate(enemy);
            enemy.rb.simulated = false;
            Assert.That(enemy.IsCombatReady, Is.True);
            typeof(BossPatternController).GetField("target", Flags).SetValue(boss, target.transform);
            var sprite = root.GetComponentInChildren<SpriteRenderer>().transform;
            var telegraph = typeof(BossPatternController).GetMethod("Telegraph", Flags);
            var cleanup = typeof(BossPatternController).GetMethod("CleanupAttackState", Flags);
            var faceTarget = typeof(BossPatternController).GetMethod("FaceTarget", Flags);
            var lane = root.GetComponentInChildren<EnemyChargeTelegraph>();
            Assert.That(lane, Is.Not.Null);
            var background = lane.transform.Find("background").GetComponent<LineRenderer>();
            foreach (BossPatternId pattern in new[] { BossPatternId.HeavyCleave, BossPatternId.JumpSmash, BossPatternId.BullCharge })
            {
                foreach (float facing in new[] { 1f, -1f })
                {
                    target.transform.position = root.transform.position + Vector3.right * facing * 5f;
                    faceTarget.Invoke(boss, null);
                    Assert.That(EnemyFacingUtil.GetFacingSign(root.transform), Is.EqualTo(facing));
                    var windup = (IEnumerator)telegraph.Invoke(boss, new object[] {
                        new BossPatternTiming { telegraphDuration = .12f }, pattern, null, 0f });
                    int frames = 0;
                    while (windup.MoveNext())
                    {
                        target.transform.position = root.transform.position - Vector3.right * facing * 5f;
                        faceTarget.Invoke(boss, null);
                        EnemyFacingUtil.FaceHorizontal(root.transform, -facing);
                        EnemyFacingUtil.FaceHorizontal(sprite, -facing);
                        Assert.That(enemy.FacingLocked, Is.True, pattern.ToString());
                        Assert.That(EnemyFacingUtil.GetFacingSign(root.transform), Is.EqualTo(facing));
                        Assert.That(EnemyFacingUtil.GetFacingSign(sprite), Is.EqualTo(facing));
                        Assert.That(background.enabled, Is.EqualTo(pattern == BossPatternId.BullCharge));
                        if (pattern == BossPatternId.BullCharge)
                        {
                            Vector3 delta = background.GetPosition(1) - background.GetPosition(0);
                            Assert.That(Mathf.Sign(delta.x), Is.EqualTo(facing));
                            Assert.That(delta.y, Is.Zero.Within(.001f));
                            Assert.That(background.startWidth, Is.EqualTo(enemy.bodyCollider.bounds.size.y).Within(.001f));
                            Assert.That(enemy.attackHitbox.enabled, Is.False);
                        }
                        frames++;
                        yield return null;
                    }
                    Assert.That(frames, Is.GreaterThan(0));
                    Assert.That(enemy.FacingLocked, Is.True, "The active attack still owns its direction after wind-up");
                    cleanup.Invoke(boss, null);
                    foreach (var line in lane.GetComponentsInChildren<LineRenderer>())
                        Assert.That(line.enabled, Is.False, "An interrupted windup must hide its entire warning.");
                    Assert.That(enemy.FacingLocked, Is.False);
                    faceTarget.Invoke(boss, null);
                    Assert.That(EnemyFacingUtil.GetFacingSign(root.transform), Is.EqualTo(-facing));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(container);
            Object.DestroyImmediate(target);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
