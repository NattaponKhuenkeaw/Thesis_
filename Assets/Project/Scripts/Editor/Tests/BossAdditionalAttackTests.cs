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
    public void WaveIsAnAuthoredPixelPrefabWithTheCoreCharacterTrigger()
    {
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>(BossAdditionalAttacksAuthoring.WavePath);
        Assert.That(wave, Is.Not.Null);
        var hitbox = wave.GetComponent<BoxCollider2D>();
        Assert.That(hitbox.isTrigger, Is.True);
        Assert.That(hitbox.size, Is.EqualTo(new Vector2(1.1f, .65f)));
        Assert.That(hitbox.offset, Is.EqualTo(new Vector2(0f, .325f)));
        Assert.That(wave.transform.localScale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
        Assert.That(wave.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        using var settings = new SerializedObject(wave.GetComponent<BossGroundWave>());
        var frames = settings.FindProperty("frames");
        Assert.That(frames.arraySize, Is.EqualTo(3));
        for (int i = 0; i < 3; i++)
        {
            var sprite = (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(sprite.pivot.y, Is.EqualTo(0f));
            Assert.That(sprite.GetPhysicsShapeCount(), Is.GreaterThan(0));
        }
    }

    [UnityTest]
    public IEnumerator FirstWindupDoesNotRememberAnExistingHitTint()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            Set(f.Boss, "flashDuringTelegraph", true);
            var renderer = f.Enemy.GetComponentInChildren<Animator>().GetComponent<SpriteRenderer>();
            Color original = renderer.color;
            renderer.color = Color.red;

            f.Start(BossPatternId.HeavyCleave);
            Assert.That(renderer.color, Is.Not.EqualTo(original), "The windup still flashes.");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!f.Enemy.attackHitbox.enabled && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(f.Enemy.attackHitbox.enabled, Is.True);
            Assert.That(renderer.color, Is.EqualTo(original), "The strike must not inherit the hit tint.");

            Call(f.Boss, "StartTelegraphFlash");
            f.Boss.ForceClearCombatHazards();
            Assert.That(renderer.color, Is.EqualTo(original), "Cancelling a warning must also restore the base tint.");
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator FourCleavesAlternateCommittedSidesAndFinishWithRecovery()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture(playWarning: true))
        {
            yield return f.Ready();
            f.Start(BossPatternId.AlternatingCleave);
            var directions = new List<float>();
            var attackSprites = new HashSet<Sprite>();
            var animator = f.Enemy.GetComponentInChildren<Animator>();
            var renderer = animator.GetComponent<SpriteRenderer>();
            bool activeLast = false;
            Sprite heldSprite = null;
            float roarStarted = -1f;
            float lastClose = 0f, start = Time.time;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (f.Boss.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                bool active = f.Enemy.attackHitbox.enabled;
                var warning = f.Boss.transform.Find("BossAttackWarningAudio")?.GetComponent<AudioSource>();
                if (!active && directions.Count == 1 && !activeLast)
                {
                    if (warning?.clip != null)
                    {
                        if (roarStarted < 0f)
                        {
                            roarStarted = Time.time;
                            Assert.That(Time.time - lastClose, Is.GreaterThanOrEqualTo(.25f));
                            Assert.That(warning.clip, Is.SameAs(f.Boss.AttackWarningSfx));
                        }
                    }
                    else if (roarStarted < 0f)
                    {
                        Assert.That(EnemyFacingUtil.GetFacingSign(f.Enemy.transform), Is.EqualTo(1f));
                        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.AttackCleave"), Is.True);
                        var clip = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
                        var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                            .Single(binding => binding.propertyName == "m_Sprite");
                        heldSprite = renderer.sprite;
                        Assert.That(heldSprite, Is.SameAs(AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding).Last().value));
                    }
                }
                if (active && renderer != null) attackSprites.Add(renderer.sprite);
                if (active && !activeLast)
                {
                    directions.Add(EnemyFacingUtil.GetFacingSign(f.Enemy.transform));
                    Assert.That(f.Enemy.FacingLocked, Is.True);
                    if (directions.Count == 1)
                        Assert.That(Time.time - start, Is.GreaterThanOrEqualTo(1.15f));
                    else if (directions.Count == 2)
                    {
                        Assert.That(heldSprite, Is.Not.Null);
                        Assert.That(roarStarted, Is.GreaterThan(lastClose));
                        Assert.That(Time.time - roarStarted, Is.GreaterThanOrEqualTo(1.1f));
                        Assert.That(warning?.clip, Is.Null);
                    }
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
    public IEnumerator SeismicBarrageCrouchesAndRoarsOnlyBeforeItsFirstJumpWithoutWarningUi()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture(playWarning: true))
        {
            yield return f.Ready();
            Set(f.Boss, "phase", 2);
            var animator = f.Enemy.GetComponentInChildren<Animator>();
            var renderer = animator.GetComponent<SpriteRenderer>();
            // The world canvas detaches at runtime; use the controller's bound UI.
            var worldUi = (EnemyWorldUI)typeof(EnemyController).GetField("worldUI", Private).GetValue(f.Enemy);
            Assert.That(worldUi, Is.Not.Null);
            var icon = (GameObject)typeof(EnemyWorldUI).GetField("attackWarningIcon", Private).GetValue(worldUi);
            var marker = f.Enemy.GetComponentInChildren<BossGroundTelegraph>(true).GetComponent<SpriteRenderer>();
            var waveIds = new HashSet<int>();
            bool wasAirborne = false, wasRoaring = false, crouchedBeforeRoar = false;
            float started = Time.time, roarStarted = 0f;
            int roars = 0, jumps = 0, landingsWithWaves = 0;
            f.Start(BossPatternId.SeismicBarrage);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (f.Boss.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                bool airborne = f.Enemy.rb.bodyType == RigidbodyType2D.Kinematic;
                var warning = f.Boss.transform.Find("BossAttackWarningAudio")?.GetComponent<AudioSource>();
                bool roaring = warning?.clip != null;
                bool crouching = renderer.sprite != null && renderer.sprite.name.StartsWith("Gif_Jump_00");
                Assert.That(icon.activeSelf, Is.False);
                Assert.That(marker.enabled, Is.False);
                Assert.That(f.Enemy.attackHitbox.enabled, Is.False);
                if (roaring)
                {
                    Assert.That(airborne, Is.False);
                    Assert.That(jumps, Is.Zero);
                    Assert.That(warning.clip, Is.SameAs(f.Boss.AttackWarningSfx));
                    Assert.That(animator.GetFloat("JumpPhase"), Is.Zero);
                    Assert.That(crouching, Is.True, "The actual crouch sprite must remain visible throughout the roar.");
                    if (!wasRoaring)
                    {
                        roars++;
                        roarStarted = Time.time;
                        Assert.That(crouchedBeforeRoar, Is.True);
                        Assert.That(roarStarted - started, Is.GreaterThanOrEqualTo(.27f));
                    }
                }
                else if (jumps == 0 && !airborne && crouching)
                    crouchedBeforeRoar = true;

                if (airborne && !wasAirborne)
                {
                    if (jumps == 0)
                    {
                        Assert.That(roars, Is.EqualTo(1));
                        Assert.That(Time.time - roarStarted, Is.GreaterThanOrEqualTo(f.Boss.AttackWarningSfx.length - .05f));
                    }
                    jumps++;
                }
                int freshWaves = Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None)
                    .Count(wave => waveIds.Add(wave.GetInstanceID()));
                if (freshWaves > 0)
                {
                    landingsWithWaves++;
                    Assert.That(freshWaves, Is.EqualTo(2));
                    Assert.That(airborne, Is.False);
                    Assert.That((bool)Call(f.Enemy, "HasSpawnGroundSupport"), Is.True);
                }
                wasAirborne = airborne;
                wasRoaring = roaring;
                yield return null;
            }
            Assert.That(f.Boss.IsBusy, Is.False);
            Assert.That(jumps, Is.EqualTo(3));
            Assert.That(landingsWithWaves, Is.EqualTo(3));
            Assert.That(roars, Is.EqualTo(1));
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
                        var outline = wave.GetComponent<Collider2D>();
                        Assert.That(outline.bounds.min.y, Is.EqualTo(.02f).Within(.05f));
                        Assert.That(outline.bounds.max.y,
                            Is.LessThanOrEqualTo(wave.GetComponent<SpriteRenderer>().bounds.max.y + .01f));
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
            Assert.That(f.Boss.JumpArcHeight, Is.EqualTo(8f));
            Assert.That(f.Boss.SeismicJumpArcHeight, Is.EqualTo(5f));
            Assert.That(f.Boss.JumpTravelDuration, Is.EqualTo(.9f));
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
            float expected = pattern == BossPatternId.JumpSmash ? f.Boss.JumpArcHeight : f.Boss.SeismicJumpArcHeight;
            foreach (float height in peaks) Assert.That(height, Is.EqualTo(expected).Within(.25f));
            if (pattern == BossPatternId.JumpSmash)
                Assert.That(f.Enemy.rb.position.x, Is.EqualTo(8f).Within(.1f));
            f.AssertClean();
            TestContext.WriteLine($"{pattern}: peak rises = {string.Join(", ",peaks)}");
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ARealSingleJumpClearsTheWaveFromEitherSide()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
            yield return CheckSingleJumpAgainstWave(f);
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckSingleJumpAgainstWave(Fixture f)
    {
        yield return f.Ready();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossAdditionalAttacksAuthoring.WavePath);
        foreach (float direction in new[] { -1f, 1f })
        {
            var standing = CreateWaveTarget(direction * 3f);
            var jumping = CreateWaveTarget(direction * 6f);
            var motor = jumping.GetComponent<PlayerMovement>();
            var wave = Object.Instantiate(prefab).GetComponent<BossGroundWave>();
            try
            {
                float settleUntil = Time.time + 1f;
                while (!motor.IsPhysicallyGrounded() && Time.time < settleUntil)
                    yield return new WaitForFixedUpdate();
                Assert.That(motor.IsPhysicallyGrounded(), Is.True, "The jump must start with the player on the ground.");
                wave.Initialize(f.Enemy, Vector2.zero, direction, 14f, 80f, new HashSet<int>());
                Physics2D.SyncTransforms();
                var outline = wave.GetComponent<BoxCollider2D>();
                Assert.That(outline.OverlapPoint(new Vector2(direction * .5f, 1f)), Is.True, "The wave lane must hit.");
                Assert.That(outline.OverlapPoint(new Vector2(0f, 1.5f)), Is.False, "Air above the wave is safe.");
                float deadline = Time.time + 2f;
                var waveBody = wave.GetComponent<Rigidbody2D>();
                var playerCollider = jumping.GetComponent<Collider2D>();
                string contact = "none";
                jumping.OnDamaged += _ => contact = $"wave x={waveBody.position.x:F2}, crest={outline.bounds.max.y:F2}, feet={playerCollider.bounds.min.y:F2}";
                // Meet the wave near the single jump's apex.
                while (waveBody.position.x * direction < .83f && Time.time < deadline)
                    yield return new WaitForFixedUpdate();
                motor.ConsumeJumpCount();
                motor.ApplyJumpLaunch();
                bool passedInTheAir = false;
                float highestFeet = 0f;
                while ((!motor.IsPhysicallyGrounded() || wave.transform.position.x * direction < 9f) && Time.time < deadline)
                {
                    float feet = playerCollider.bounds.min.y;
                    highestFeet = Mathf.Max(highestFeet, feet);
                    if (Mathf.Abs(wave.transform.position.x - jumping.transform.position.x) < .3f)
                        passedInTheAir |= feet > outline.bounds.max.y;
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(passedInTheAir, Is.True, $"Wave did not pass the single jump (feet reached {highestFeet:F2}).");
                Assert.That(standing.CurrentHP, Is.EqualTo(1000f - f.Enemy.attackDamage));
                Assert.That(jumping.CurrentHP, Is.EqualTo(1000f), $"Facing {direction}: {contact}; apex={highestFeet:F2}");
            }
            finally
            {
                if (wave != null) Object.DestroyImmediate(wave.gameObject);
                Object.DestroyImmediate(standing.gameObject);
                Object.DestroyImmediate(jumping.gameObject);
            }
        }
    }

    private static PlayerCharacter CreateWaveTarget(float x)
    {
        var host = new GameObject("Wave damage target");
        host.SetActive(false);
        host.layer = LayerMask.NameToLayer("Player");
        host.transform.position = new Vector3(x, .01f);
        host.AddComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
        var collider = host.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.55f, 2.6f);
        collider.offset = Vector2.up * 1.3f;
        var groundCheck = new GameObject("Feet");
        groundCheck.transform.SetParent(host.transform, false);
        var motor = host.AddComponent<PlayerMovement>();
        motor.profile = Resources.Load<MovementProfile>("MovementSO/MovementProfile_Sample");
        Set(motor, "groundCheck", groundCheck.transform);
        var player = host.AddComponent<PlayerCharacter>();
        player.enabled = false;
        Set(player, "currentHP", 1000f);
        host.SetActive(true);
        return player;
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
                playerBox.enabled = false;
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
        public Fixture(bool playWarning = false)
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
            foreach (string sound in new[] { "cleaveSfx", "jumpTakeoffSfx", "jumpLandSfx" }) Set(Boss, sound, null);
            if (!playWarning) Set(Boss, "attackWarningSfx", null);
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
