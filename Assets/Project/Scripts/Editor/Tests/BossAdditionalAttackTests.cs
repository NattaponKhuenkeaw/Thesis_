#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BossAdditionalAttackTests
{
    private const string BossPath = "Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);

    [TestCase(1, 2f, false)]
    [TestCase(2, 2f, true)]
    [TestCase(2, 15f, true)]
    public void NewPatternsRespectPhaseDistanceAndRepeatLimit(int phase, float distance, bool seismic)
    {
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath).GetComponent<BossPatternController>();
        var random = Random.state;
        object previousPhase = boss.GetType().GetField("phase", Private).GetValue(boss);
        object previousPattern = boss.GetType().GetField("lastPattern", Private).GetValue(boss);
        object previousCount = boss.GetType().GetField("consecutivePatternCount", Private).GetValue(boss);
        try
        {
            Random.InitState(810);
            Set(boss, "phase", phase);
            Set(boss, "consecutivePatternCount", 0);
            var seen = new HashSet<BossPatternId>();
            BossPatternId last = default;
            int streak = 0;
            for (int i = 0; i < 1024; i++)
            {
                var pattern = (BossPatternId)Call(boss, "ChoosePattern", distance);
                streak = pattern == last ? streak + 1 : 1;
                Assert.That(streak, Is.LessThanOrEqualTo(2));
                seen.Add(pattern);
                Call(boss, "RememberPattern", pattern);
                last = pattern;
            }
            Assert.That(seen.Contains(BossPatternId.SeismicBarrage), Is.EqualTo(seismic));
            Assert.That(seen.Contains(BossPatternId.AlternatingCleave), Is.EqualTo(distance <= boss.CleaveRange));
        }
        finally
        {
            Random.state = random;
            Set(boss, "phase", previousPhase);
            Set(boss, "lastPattern", previousPattern);
            Set(boss, "consecutivePatternCount", previousCount);
        }
    }

    [Test]
    public void WaveIsAnAuthoredPixelPrefabLowEnoughToJumpOver()
    {
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>(BossAdditionalAttacksAuthoring.WavePath);
        Assert.That(wave, Is.Not.Null);
        Assert.That(wave.GetComponent<BoxCollider2D>().isTrigger, Is.True);
        Assert.That(wave.GetComponent<BoxCollider2D>().size.y, Is.LessThan(.8f));
        Assert.That(wave.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        using var settings = new SerializedObject(wave.GetComponent<BossGroundWave>());
        var frames = settings.FindProperty("frames");
        Assert.That(frames.arraySize, Is.EqualTo(3));
        for (int i = 0; i < 3; i++)
        {
            var sprite = (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.pivot.y, Is.EqualTo(0f));
        }
    }

    [UnityTest]
    public IEnumerator FourCleavesAlternateCommittedSidesAndFinishWithRecovery()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            f.Start(BossPatternId.AlternatingCleave);
            var directions = new List<float>();
            var attackSprites = new HashSet<Sprite>();
            var renderer = f.Enemy.GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>();
            bool activeLast = false;
            float lastClose = 0f, start = Time.time;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (f.Boss.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                bool active = f.Enemy.attackHitbox.enabled;
                if (active && renderer != null) attackSprites.Add(renderer.sprite);
                if (active && !activeLast)
                {
                    directions.Add(EnemyFacingUtil.GetFacingSign(f.Enemy.transform));
                    Assert.That(f.Enemy.FacingLocked, Is.True);
                    if (directions.Count == 1)
                        Assert.That(Time.time - start, Is.GreaterThanOrEqualTo(1.15f));
                    else Assert.That(Time.time - lastClose, Is.GreaterThanOrEqualTo(.30f));
                    // Escaping does not cancel or retarget an already committed flurry.
                    f.Target.transform.position = Vector3.right * 20f;
                }
                if (!active && activeLast) lastClose = Time.time;
                activeLast = active;
                yield return null;
            }
            Assert.That(f.Boss.IsBusy, Is.False);
            Assert.That(directions, Is.EqualTo(new[] { 1f, -1f, 1f, -1f }));
            Assert.That(attackSprites.Count, Is.GreaterThanOrEqualTo(2), "The slash and follow-through sprites must both be visible");
            Assert.That(Time.time - lastClose, Is.GreaterThan(1.45f));
            f.AssertClean();
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ThreeStationaryJumpsEmitPairsOnlyOnLandingThenClearAllHazards()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            Set(f.Boss, "phase", 2);
            float startX = f.Enemy.rb.position.x;
            f.Start(BossPatternId.SeismicBarrage);
            int pairs = 0;
            var wavesSeen = new HashSet<int>();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (f.Boss.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(f.Enemy.rb.position.x, Is.EqualTo(startX).Within(.06f));
                Assert.That(f.Enemy.attackHitbox.enabled, Is.False, "Only the low waves damage during this pattern");
                var fresh = Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None)
                    .Where(w => wavesSeen.Add(w.GetInstanceID())).ToArray();
                if (fresh.Length > 0)
                {
                    pairs++;
                    Assert.That(fresh.Length, Is.EqualTo(2));
                    Assert.That((bool)Call(f.Enemy, "HasSpawnGroundSupport"), Is.True);
                    foreach (var wave in fresh)
                    {
                        var bounds = wave.GetComponent<BoxCollider2D>().bounds;
                        Assert.That(bounds.max.y, Is.LessThan(.8f));
                        // Standing target overlaps, but raising feet one unit clears the entire wave.
                        Assert.That(bounds.Intersects(new Bounds(new Vector3(bounds.center.x, 1.3f), new Vector3(1f, 2.6f))), Is.True);
                        Assert.That(bounds.Intersects(new Bounds(new Vector3(bounds.center.x, 2.3f), new Vector3(1f, 2.6f))), Is.False);
                    }
                }
                yield return null;
            }
            Assert.That(pairs, Is.EqualTo(3));
            Assert.That(f.Boss.IsBusy, Is.False);
            f.AssertClean();
            f.Boss.ForceClearCombatHazards();
            yield return null;
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator TravelingJumpRisesHigherWithoutChangingTheThreeStationaryJumps()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (var pattern in new[] { BossPatternId.JumpSmash, BossPatternId.SeismicBarrage })
        {
            using var f = new Fixture();
            yield return f.Ready();
            Set(f.Boss, "phase", 1);
            f.Target.transform.position = Vector3.right * 8f;
            Assert.That(f.Boss.JumpArcHeight, Is.EqualTo(5f));
            Assert.That(f.Boss.SeismicJumpArcHeight, Is.EqualTo(3.5f));
            Assert.That(f.Boss.JumpTravelDuration, Is.EqualTo(.75f));
            float startY = f.Enemy.rb.position.y;
            float startX = f.Enemy.rb.position.x;
            var peaks = new List<float>();
            bool airborne = false;
            float peak = startY;
            f.Start(pattern);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (f.Boss.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                if (f.Enemy.rb.bodyType == RigidbodyType2D.Kinematic)
                {
                    if (!airborne) peak = startY;
                    airborne = true;
                    peak = Mathf.Max(peak, f.Enemy.rb.position.y);
                }
                else if (airborne)
                {
                    peaks.Add(peak-startY);
                    airborne = false;
                }
                if (pattern == BossPatternId.SeismicBarrage)
                    Assert.That(f.Enemy.rb.position.x, Is.EqualTo(startX).Within(.06f));
                yield return null;
            }
            Assert.That(f.Boss.IsBusy, Is.False);
            Assert.That(peaks.Count, Is.EqualTo(pattern == BossPatternId.JumpSmash ? 1 : 3));
            float expected = pattern == BossPatternId.JumpSmash ? 5f : 3.5f;
            foreach (float height in peaks) Assert.That(height, Is.EqualTo(expected).Within(.25f));
            if (pattern == BossPatternId.JumpSmash)
                Assert.That(f.Enemy.rb.position.x, Is.EqualTo(8f).Within(.1f));
            f.AssertClean();
            TestContext.WriteLine($"{pattern}: peak rises = {string.Join(", ",peaks)}");
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator WaveDamagesOnlyOncePerLandingPairAndStopsAtWalls()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            var playerHost = new GameObject("Damage target");
            playerHost.SetActive(false);
            var player = playerHost.AddComponent<PlayerCharacter>();
            var playerBox = playerHost.AddComponent<BoxCollider2D>();
            playerBox.enabled = false;
            player.enabled = false;
            playerHost.SetActive(true);
            Set(player, "currentHP", 1000f);
            Set(player, "damageInvulnerableUntil", float.NegativeInfinity);
            Assert.That(playerBox.GetComponentInParent<PlayerCharacter>(), Is.SameAs(player));
            var shared = new HashSet<int>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossAdditionalAttacksAuthoring.WavePath);
            var left = Object.Instantiate(prefab).GetComponent<BossGroundWave>();
            var right = Object.Instantiate(prefab).GetComponent<BossGroundWave>();
            try
            {
                left.Initialize(f.Enemy, Vector2.zero, -1f, 14f, 80f, shared);
                right.Initialize(f.Enemy, Vector2.zero, 1f, 14f, 80f, shared);
                Call(left, "TryDamage", playerBox);
                Set(player, "damageInvulnerableUntil", float.NegativeInfinity);
                Call(right, "TryDamage", playerBox);
                Assert.That(player.CurrentHP, Is.EqualTo(835f));
                float deadline = Time.time + .3f;
                while (Time.time < deadline) yield return null;
                Assert.That(left.transform.position.x, Is.LessThan(-3f));
                Assert.That(right.transform.position.x, Is.GreaterThan(3f));
                var wall = new GameObject("Wave-blocking wall");
                wall.layer = LayerMask.NameToLayer("Ground");
                wall.transform.position = new Vector3(5f, 1f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 2f);
                try
                {
                    Physics2D.SyncTransforms();
                    deadline = Time.time + .5f;
                    while (right != null && Time.time < deadline) yield return null;
                    Assert.That(right == null, Is.True, "Wave must not travel through a solid wall");
                    f.Boss.ForceClearCombatHazards();
                }
                finally { Object.DestroyImmediate(wall); }
            }
            finally
            {
                if (left != null) Object.DestroyImmediate(left.gameObject);
                if (right != null) Object.DestroyImmediate(right.gameObject);
                Object.DestroyImmediate(playerHost);
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CancellingEitherNewPatternDuringWindupLeavesNoAttackOrFacingLock()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (var pattern in new[] { BossPatternId.AlternatingCleave, BossPatternId.SeismicBarrage })
        {
            using var f = new Fixture();
            yield return f.Ready();
            f.Start(pattern);
            yield return null;
            f.Boss.ForceClearCombatHazards();
            yield return null;
            Assert.That(f.Boss.IsBusy, Is.False);
            f.AssertClean();
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
        }
        yield return new ExitPlayMode();
    }

    private sealed class Fixture : System.IDisposable
    {
        private readonly GameObject root = new GameObject("Boss new attacks fixture");
        public readonly GameObject Target;
        public readonly BossPatternController Boss;
        public readonly EnemyController Enemy;
        public Fixture()
        {
            root.AddComponent<AudioListener>();
            var floor = new GameObject("Arena floor");
            floor.transform.SetParent(root.transform);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = Vector3.down * .5f;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(240f, 1f);
            Target = new GameObject("Target");
            Target.transform.SetParent(root.transform);
            Target.transform.position = Vector3.right * 2f;
            var holder = new GameObject("Inactive boss");
            holder.transform.SetParent(root.transform);
            holder.SetActive(false);
            var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath), holder.transform);
            Boss = boss.GetComponent<BossPatternController>();
            Enemy = boss.GetComponent<EnemyController>();
            Boss.enabled = false;
            Set(Boss, "revealOnSpawn", false);
            Set(Boss, "bindScreenHealthHud", false);
            Set(Boss, "flashDuringTelegraph", false);
            foreach (string sound in new[] { "cleaveSfx", "attackWarningSfx", "jumpTakeoffSfx", "jumpLandSfx" }) Set(Boss, sound, null);
            Set(Boss, "target", Target.transform);
            holder.SetActive(true);
            Enemy.InitializeSpawn(boss.transform.position, boss.transform.rotation);
        }
        public IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!Enemy.IsCombatReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Enemy.IsCombatReady, Is.True);
            float before = Time.fixedTime;
            while (Time.fixedTime <= before) yield return null;
        }
        public void Start(BossPatternId pattern)
        {
            var routine = Boss.StartCoroutine((IEnumerator)Call(Boss, "ExecutePattern", pattern));
            Set(Boss, "brainRoutine", routine);
        }
        public void AssertClean()
        {
            Assert.That(Enemy.attackHitbox.enabled, Is.False);
            Assert.That(Enemy.FacingLocked, Is.False);
            Assert.That(Enemy.rb.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(Enemy.maxHP, Is.EqualTo(4000f));
            Assert.That(Enemy.attackDamage, Is.EqualTo(165f));
        }
        public void Dispose()
        {
            Boss.ForceClearCombatHazards();
            Object.DestroyImmediate(root);
        }
    }

    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
