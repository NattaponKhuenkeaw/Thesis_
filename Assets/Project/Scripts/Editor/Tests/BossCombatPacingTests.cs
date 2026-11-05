#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class BossCombatPacingTests
{
    private const string BossPath = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

    [TestCase(1, 2f)]
    [TestCase(2, 2f)]
    [TestCase(1, 5f)]
    [TestCase(2, 5f)]
    [TestCase(1, 12f)]
    [TestCase(2, 12f)]
    public void DistanceSelectionHasVarietyWithoutRepeatingAnAttackMoreThanTwice(int phase, float distance)
    {
        var host = new GameObject("Boss choice test");
        host.SetActive(false);
        var state = Random.state;
        try
        {
            var boss = host.AddComponent<BossPatternController>();
            Set(boss, "phase", phase);
            Random.InitState(710 + phase);
            var seen = new HashSet<BossPatternId>();
            BossPatternId previous = default;
            int streak = 0;
            for (int i = 0; i < 512; i++)
            {
                var pattern = (BossPatternId)Call(boss, "ChoosePattern", distance);
                streak = i > 0 && pattern == previous ? streak + 1 : 1;
                Assert.That(streak, Is.LessThanOrEqualTo(2));
                if (distance <= boss.CleaveRange) Assert.That(pattern, Is.Not.EqualTo(BossPatternId.BullCharge));
                if (distance > boss.CleaveRange * 3f) Assert.That(pattern, Is.Not.EqualTo(BossPatternId.HeavyCleave));
                seen.Add(pattern);
                Call(boss, "RememberPattern", pattern);
                previous = pattern;
            }
            Assert.That(seen, Does.Contain(BossPatternId.JumpSmash));
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(distance == 5f ? 3 : 2));
            Assert.That(seen.Contains(BossPatternId.AlternatingCleave), Is.EqualTo(distance <= boss.CleaveRange));
            if (phase == 1) Assert.That(seen.Contains(BossPatternId.SeismicBarrage), Is.False);
        }
        finally { Random.state = state; Object.DestroyImmediate(host); }
    }

    [TestCase(1, BossPatternId.HeavyCleave, 1f)]
    [TestCase(2, BossPatternId.HeavyCleave, .85f)]
    [TestCase(1, BossPatternId.JumpSmash, 1.9f)]
    [TestCase(2, BossPatternId.JumpSmash, 1.615f)]
    [TestCase(1, BossPatternId.BullCharge, 1.8f)]
    [TestCase(2, BossPatternId.BullCharge, 1.53f)]
    public void LargeAttacksHaveLongerPunishWindowsThanSingleCleave(int phase, BossPatternId pattern, float seconds)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        var boss = prefab.GetComponent<BossPatternController>();
        int originalPhase = Get<int>(boss, "phase");
        try
        {
            Set(boss, "phase", phase);
            var timing = pattern == BossPatternId.HeavyCleave ? boss.CleaveTiming
                : pattern == BossPatternId.JumpSmash ? boss.JumpSmashTiming : boss.BullChargeTiming;
            Assert.That((float)Call(boss, "ResolveRecovery", timing), Is.EqualTo(seconds).Within(.0001f));
        }
        finally { Set(boss, "phase", originalPhase); }
    }

    [Test]
    public void AuthoredBossKeepsHealthDamageAndReadableWarnings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        var enemy = prefab.GetComponent<EnemyController>();
        var boss = prefab.GetComponent<BossPatternController>();
        Assert.That(enemy.maxHP, Is.EqualTo(4000f));
        Assert.That(enemy.attackDamage, Is.EqualTo(165f));
        Assert.That(Get<float>(boss, "defaultMaxHp"), Is.EqualTo(4000f));
        Assert.That(Get<float>(boss, "defaultAttackDamage"), Is.EqualTo(165f));
        Assert.That(Get<int>(boss, "healthBarCount"), Is.EqualTo(5));
        Assert.That(boss.CleaveTiming.telegraphDuration, Is.EqualTo(1.2f));
        Assert.That(boss.JumpSmashTiming.telegraphDuration, Is.EqualTo(1.35f));
        Assert.That(boss.BullChargeTiming.telegraphDuration, Is.EqualTo(1.1f));
        Assert.That(Get<float>(boss, "comboRecoveryDuration"), Is.GreaterThanOrEqualTo(1.6f));
        Assert.That(Get<float>(boss, "maxChaseDuration"), Is.LessThanOrEqualTo(1.4f));
    }

    [UnityTest]
    public IEnumerator RealBossUsesSingleAttacksAboveHalfHealthAndAtMostTwoBelowHalf()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (int phase in new[] { 1, 2 })
            foreach (BossPatternId pattern in new[] { BossPatternId.HeavyCleave, BossPatternId.JumpSmash, BossPatternId.BullCharge })
                yield return ObserveAttack(phase, pattern, false, false);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LeavingDuringComboLinkCancelsFollowupAndStillGivesAPunishWindow()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (BossPatternId pattern in new[] { BossPatternId.JumpSmash, BossPatternId.BullCharge })
            yield return ObserveAttack(2, pattern, true, false);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ClearingBossDuringSecondWarningCancelsTheWholeSequence()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ObserveAttack(2, BossPatternId.JumpSmash, false, true);
        yield return new ExitPlayMode();
    }

    private static IEnumerator ObserveAttack(int phase, BossPatternId pattern, bool escape, bool clear)
    {
        var fixture = new GameObject("Boss combat pacing runtime fixture");
        var floor = new GameObject("Solid arena floor");
        floor.transform.SetParent(fixture.transform);
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = new Vector3(0f, -.5f, 0f);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(60f, 1f);
        var target = new GameObject("Player position");
        target.transform.SetParent(fixture.transform);
        target.transform.position = Vector3.right * 2f;
        target.AddComponent<AudioListener>();
        var holder = new GameObject("Inactive boss spawn");
        holder.transform.SetParent(fixture.transform);
        holder.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath), holder.transform);
        var boss = root.GetComponent<BossPatternController>();
        var enemy = root.GetComponent<EnemyController>();
        boss.enabled = false;
        Set(boss, "bindScreenHealthHud", false);
        Set(boss, "revealOnSpawn", false);
        Set(boss, "flashDuringTelegraph", false);
        foreach (string sound in new[] { "cleaveSfx", "attackWarningSfx", "chargeHitSfx", "chargeRushSfx", "jumpTakeoffSfx", "jumpLandSfx" })
            Set(boss, sound, null);
        // Keep production windups/recovery; shorten only arena travel for this fixture.
        Set(boss, "chargeDistance", 4f);
        Set(boss, "phase2ChainCleaveChance", 1f);
        try
        {
            holder.SetActive(true);
            enemy.InitializeSpawn(root.transform.position, root.transform.rotation);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!enemy.IsGameplayActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(enemy.IsCombatReady, Is.True);
            yield return new WaitForFixedUpdate();
            Set(boss, "target", target.transform);
            Set(boss, "phase", phase);
            enemy.currentHP = phase == 1 ? enemy.maxHP : enemy.maxHP * .4f;
            var worldUi = Get<EnemyWorldUI>(enemy, "worldUI");
            Assert.That(worldUi, Is.Not.Null);
            var warning = Get<GameObject>(worldUi, "attackWarningIcon");
            Assert.That(warning, Is.Not.Null);
            var routine = boss.StartCoroutine((IEnumerator)Call(boss, "ExecutePattern", pattern));
            Set(boss, "brainRoutine", routine);
            int warnings = 0, windows = 0;
            bool previousWarning = false, previousWindow = false, escaped = false, cleared = false;
            float warningStart = 0f, windowEnd = 0f;
            deadline = Time.realtimeSinceStartup + 16f;
            while (boss.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                bool showing = warning.activeInHierarchy;
                bool active = enemy.attackHitbox.enabled && enemy.attackHitbox.gameObject.activeInHierarchy;
                if (showing && !previousWarning)
                {
                    warnings++;
                    warningStart = Time.time;
                }
                if (active && !previousWindow)
                {
                    windows++;
                    float expectedWarning = windows > 1 ? boss.CleaveTiming.telegraphDuration
                        : pattern == BossPatternId.HeavyCleave ? boss.CleaveTiming.telegraphDuration
                        : pattern == BossPatternId.JumpSmash ? boss.JumpSmashTiming.telegraphDuration : boss.BullChargeTiming.telegraphDuration;
                    Assert.That(Time.time - warningStart, Is.GreaterThanOrEqualTo(expectedWarning - .06f));
                    Assert.That(enemy.FacingLocked, Is.True);
                }
                if (previousWindow && !active) windowEnd = Time.time;
                if (active && !escaped) target.transform.position = root.transform.position + Vector3.right;
                if (escape && previousWindow && !active && !escaped)
                {
                    // Let ExecutePattern select the link, then escape during its stopping pause.
                    yield return null;
                    target.transform.position = Vector3.right * 25f;
                    escaped = true;
                }
                if (clear && warnings == 2)
                {
                    boss.ForceClearCombatHazards();
                    cleared = true;
                }
                previousWarning = showing;
                previousWindow = active;
                yield return null;
            }
            Assert.That(boss.IsBusy, Is.False, "Sequence must finish, not leave the brain busy forever");
            int expected = phase == 2 && pattern != BossPatternId.HeavyCleave && !escape && !clear ? 2 : 1;
            Assert.That(windows, Is.EqualTo(expected), $"{phase} / {pattern} / escape={escape}");
            Assert.That(warnings, Is.EqualTo(clear ? 2 : expected));
            Assert.That(enemy.attackHitbox.enabled, Is.False);
            Assert.That(enemy.FacingLocked, Is.False);
            Assert.That(warning.activeSelf, Is.False);
            Assert.That(enemy.rb.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            if (!clear)
            {
                var timing = pattern == BossPatternId.HeavyCleave ? boss.CleaveTiming
                    : pattern == BossPatternId.JumpSmash ? boss.JumpSmashTiming : boss.BullChargeTiming;
                float recovery = expected == 2 ? Get<float>(boss, "comboRecoveryDuration") : (float)Call(boss, "ResolveRecovery", timing);
                Assert.That(Time.time - windowEnd, Is.GreaterThanOrEqualTo(recovery - .08f));
            }
            else Assert.That(cleared, Is.True);
            Assert.That(enemy.maxHP, Is.EqualTo(4000f));
            Assert.That(enemy.attackDamage, Is.EqualTo(165f));
            TestContext.WriteLine($"Phase {phase}, {pattern}, escape={escape}, clear={clear}: {warnings} warnings, {windows} windows, recovery={Time.time - windowEnd:0.00}s");
            Set(boss, "brainRoutine", null);
        }
        finally { Object.DestroyImmediate(fixture); }
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator ExitPlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
