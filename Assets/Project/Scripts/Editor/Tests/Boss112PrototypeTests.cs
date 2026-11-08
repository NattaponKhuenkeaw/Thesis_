#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.AIBehaviour.Boss;
using Project.Core.ZoneState;
using Project.Training;
using Project.Save;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class Boss112PrototypeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void AssetsContainFivePatternsAndAuthoredAdminButtons()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.BossPath);
        Assert.That(prefab.GetComponent<Boss112Controller>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<BossPatternController>(), Is.Null);
        Assert.That(prefab.GetComponent<EnemyBehaviorBrain>(), Is.Null);
        Assert.That(System.Enum.GetValues(typeof(Boss112Pattern)).Length, Is.EqualTo(5));
        var boss = prefab.GetComponent<Boss112Controller>();
        Assert.That(boss.BeamWarningDuration, Is.EqualTo(1.5f).Within(.001f));
        Assert.That(boss.SlamWarningDuration, Is.EqualTo(.7f).Within(.001f));
        Assert.That(boss.SlamWarningDuration, Is.InRange(.6f, .8f));
        Assert.That(System.Enum.GetNames(typeof(Boss112Phase)).Any(n => n.StartsWith("Overheat")), Is.False);
        Assert.That(System.Enum.GetNames(typeof(Boss112Phase)), Does.Not.Contain("FinisherWarning"));
        Assert.That(boss.AutoAttack, Is.False);
        Assert.That(prefab.GetComponent<EnemyController>().maxHP, Is.EqualTo(4000f));
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.PanelPath);
        var buttons = panel.GetComponentsInChildren<Button>(true);
        Assert.That(buttons.Length, Is.EqualTo(12));
        foreach (var button in buttons)
        {
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), button.name);
            Assert.That(button.onClick.GetPersistentTarget(0), Is.TypeOf<Boss112TestRoom>());
        }
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Boss112PrototypeAuthoring.Folder + "/Enm112a.png").OfType<Sprite>().ToArray();
        Assert.That(sprites.Length, Is.EqualTo(22));
        Assert.That(sprites.All(s => s.texture.filterMode == FilterMode.Point), Is.True);
        Assert.That(sprites.Where(s => s.name.StartsWith("Idle_") || s.name.StartsWith("Slam_"))
            .All(s => s.rect.size == new Vector2(208, 208)), Is.True);
    }

    [Test]
    public void EnlargedBossKeepsBodyWarningsAndDamageAreasInProportion()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.BossPath);
        Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(prefab.transform.Find("Visual").localScale, Is.EqualTo(new Vector3(1.4f, 1.4f, 1f)));
        var body = prefab.GetComponent<CircleCollider2D>();
        Assert.That(body.radius, Is.EqualTo(2.66f).Within(.001f));
        Assert.That(body.offset.y, Is.EqualTo(3.5f).Within(.001f));
        var boss = prefab.GetComponent<Boss112Controller>();
        Assert.That(boss.ImpactCollider.size, Is.EqualTo(new Vector2(5.32f, 5.32f)));
        Assert.That(boss.ImpactCollider.offset.y - boss.ImpactCollider.size.y * .5f, Is.Zero.Within(.001f));
        using var beamData = new SerializedObject(boss.Beam);
        var mouth = prefab.transform.Find("Visual/BeamMouth");
        Assert.That(mouth, Is.Not.Null);
        Assert.That(beamData.FindProperty("sourcePoint").objectReferenceValue, Is.EqualTo(mouth));
        Assert.That(mouth.localPosition, Is.EqualTo(new Vector3(0f, 2.3f, 0f)));
        Assert.That(beamData.FindProperty("warningWidth").floatValue, Is.EqualTo(.504f).Within(.001f));
        Assert.That(beamData.FindProperty("activeWidth").floatValue, Is.EqualTo(2.8f).Within(.001f));
        using var bossData = new SerializedObject(boss);
        Assert.That(bossData.FindProperty("beamOriginHeight").floatValue, Is.EqualTo(.84f).Within(.001f));
        Assert.That(bossData.FindProperty("targetInset").floatValue, Is.EqualTo(3.5f).Within(.001f));
        Assert.That(prefab.transform.Find("LandingWarning").localScale,
            Is.EqualTo(new Vector3(1.26f, .063f, 1f)));
        Assert.That(prefab.GetComponent<EnemyController>().attackDamage, Is.EqualTo(165f));
    }

    [Test]
    public void TestArenaHasAContinuousFloorThickWallsAndNoEncounterOrExit()
    {
        var arena = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.ArenaPath);
        var boxes = arena.GetComponentsInChildren<Collider2D>(true);
        Assert.That(boxes.Length, Is.EqualTo(4));
        var floor = boxes.Single(c => c.name == "Floor") as BoxCollider2D;
        Assert.That(floor.size, Is.EqualTo(new Vector2(48, 4)));
        Assert.That(floor.transform.position.y + floor.size.y * .5f, Is.Zero);
        foreach (var wall in boxes.Where(c => c.name.EndsWith("Wall")))
            Assert.That(((BoxCollider2D)wall).size.x, Is.GreaterThanOrEqualTo(6f));
        Assert.That(arena.GetComponentsInChildren<RoomWaveManager>(true), Is.Empty);
        Assert.That(arena.GetComponentsInChildren<TransitionTrigger>(true), Is.Empty);
        Assert.That(arena.GetComponentsInChildren<SceneBoundaryController>(true), Is.Empty);
        Assert.That(arena.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.name == "WalkableRoad_B2"), Is.True);
    }

    [Test]
    public void TestSceneArenaAndAdminStayOutOfTheRunEvenWhenTheBossHasAProductionVariant()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Project/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var dependencies = AssetDatabase.GetDependencies(path);
            Assert.That(dependencies, Does.Not.Contain(Boss112PrototypeAuthoring.ArenaPath), path);
            Assert.That(dependencies, Does.Not.Contain(Boss112PrototypeAuthoring.ScenePath), path);
            Assert.That(dependencies, Does.Not.Contain(Boss112PrototypeAuthoring.PanelPath), path);
        }
        Assert.That(EditorBuildSettings.scenes.Any(s => s.path == Boss112PrototypeAuthoring.ScenePath), Is.False);
    }

    [UnityTest]
    public IEnumerator TripleSlamRetargetsEveryStrikeAndOnlyRecoversAfterTheThirdImpact()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.TripleSlam), Is.True);
            float[] positions = { 6f, -8f, 10f };
            var lane = f.Boss.GetComponentInChildren<EnemyChargeTelegraph>();
            var background = lane.transform.Find("background").GetComponent<LineRenderer>();
            for (int strike = 1; strike <= 3; strike++)
            {
                yield return Until(() => f.Boss.Phase == Boss112Phase.SlamLocked);
                float locked = f.Boss.LockedX;
                Assert.That(locked, Is.EqualTo(positions[strike - 1]).Within(.03f));
                Assert.That(f.Boss.SlamImpactCount, Is.EqualTo(strike - 1));
                Assert.That(f.Boss.IsStaggered, Is.False);
                Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
                typeof(Boss112Controller).GetMethod("LateUpdate", Private).Invoke(f.Boss, null);
                Assert.That(background.enabled, Is.True);
                Assert.That(background.GetPosition(1).x, Is.EqualTo(locked).Within(.03f));
                f.Target.transform.position = new Vector3(strike < 3 ? positions[strike] : -12f, 0f, 0f);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (f.Boss.Phase != Boss112Phase.SlamImpact)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Each warned dive must reach its impact.");
                    Assert.That(f.Boss.LockedX, Is.EqualTo(locked));
                    Assert.That(f.Boss.IsStaggered, Is.False);
                    Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
                    yield return null;
                }
                Assert.That(f.Boss.SlamImpactCount, Is.EqualTo(strike));
                Assert.That(f.Boss.ImpactCollider.enabled, Is.True);
                Assert.That(f.Boss.transform.position.x, Is.EqualTo(locked).Within(.03f));
                if (strike < 3)
                {
                    while (f.Boss.Phase == Boss112Phase.SlamImpact) yield return null;
                    Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamTracking));
                    Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
                    Assert.That(f.Boss.IsStaggered, Is.False);
                }
            }
            yield return Until(() => f.Boss.IsStaggered);
            float recoveryStart = Time.time;
            yield return Wait(.7f);
            Assert.That(f.Boss.IsStunPoseHeld, Is.True);
            Assert.That(f.Boss.SlamImpactCount, Is.EqualTo(3));
            Assert.That(f.Boss.transform.position.x, Is.EqualTo(10f).Within(.03f));
            Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
            yield return Until(() => f.Boss.Phase == Boss112Phase.Retreat);
            Assert.That(Time.time - recoveryStart, Is.InRange(3.45f, 3.65f));
            yield return Until(() => !f.Boss.IsBusy);
            yield return Wait(.5f);
            Assert.That(f.Boss.SlamImpactCount, Is.EqualTo(3), "The combo must stop at exactly three impacts.");
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CancellingARepeatedSlamDropsTheRemainingStrikesAndResetsTheNextCombo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (bool die in new[] { false, true })
        {
            using (var f = new Fixture())
            {
                yield return f.Ready();
                Assert.That(f.Boss.TryStartPattern(Boss112Pattern.TripleSlam), Is.True);
                yield return Until(() => f.Boss.SlamImpactCount == 1 && f.Boss.Phase == Boss112Phase.SlamTracking);
                if (die) f.Enemy.Die(); else f.Boss.CancelPattern();
                Assert.That(f.Boss.SlamImpactCount, Is.Zero);
                Assert.That(f.Boss.IsBusy, Is.False);
                Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
                foreach (var line in f.Boss.GetComponentInChildren<EnemyChargeTelegraph>().GetComponentsInChildren<LineRenderer>())
                    Assert.That(line.enabled, Is.False);
                yield return Wait(1.5f);
                if (!die)
                {
                    Assert.That(f.Boss.SlamImpactCount, Is.Zero);
                    Assert.That(f.Boss.TryStartPattern(Boss112Pattern.TripleSlam), Is.True);
                    yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
                    Assert.That(f.Boss.SlamImpactCount, Is.EqualTo(1));
                    f.Boss.CancelPattern();
                }
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AuthoredTripleSlamButtonRunsThreeVisibleWarningsAndEndsInTheOriginalStunPose()
    {
        var saves = CaptureSaveData();
        EditorSceneManager.OpenScene(Boss112PrototypeAuthoring.ScenePath);
        yield return new EnterPlayMode();
        Directory.CreateDirectory("Logs/Boss112");
        var room = Object.FindFirstObjectByType<Boss112TestRoom>();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady && PlayerCharacter.Instance != null
            && PlayerCharacter.Instance.StateMachine.CurrentStateId == PlayerStateId.Idle, 15f);
        yield return Wait(.5f);
        var button = room.GetComponentsInChildren<Button>(true).Single(b => b.name == "TRIPLE SLAM");
        Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(Boss112TestRoom.TestTripleSlam)));
        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();
        Assert.That(room.Boss.Pattern, Is.EqualTo(Boss112Pattern.TripleSlam));
        var player = PlayerCharacter.Instance;
        var body = player.GetComponent<Rigidbody2D>();
        float health = player.CurrentHP;
        for (int strike = 1; strike <= 3; strike++)
        {
            yield return Until(() => room.Boss.Phase == Boss112Phase.SlamLocked);
            Capture($"14_TripleSlamWarning_{strike}");
            float nextX = room.Boss.LockedX < 0f ? 10f : -10f;
            body.position = new Vector2(nextX, 0f);
            body.linearVelocity = Vector2.zero;
            player.transform.position = new Vector3(nextX, 0f, 0f);
            yield return Until(() => room.Boss.Phase == Boss112Phase.SlamImpact);
            Assert.That(room.Boss.SlamImpactCount, Is.EqualTo(strike));
            if (strike < 3) yield return Until(() => room.Boss.Phase == Boss112Phase.SlamTracking);
        }
        yield return Until(() => room.Boss.IsStaggered);
        yield return Wait(.7f);
        Capture("15_TripleSlamFinalStun");
        Assert.That(room.Boss.IsStunPoseHeld, Is.True);
        Assert.That(room.Boss.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Slam_08"));
        Assert.That(player.CurrentHP, Is.EqualTo(health), "Each committed dive must be avoidable by leaving its warning.");
        var after = CaptureSaveData();
        CollectionAssert.AreEquivalent(saves.Keys, after.Keys);
        foreach (var pair in saves) Assert.That(after[pair.Key], Is.EqualTo(pair.Value));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AutoAddsTripleSlamToPhaseTwoWithoutChangingThePhaseOneRotation()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (bool phaseTwo in new[] { false, true })
        {
            using (var f = new Fixture())
            {
                yield return f.Ready();
                f.Enemy.currentHP = f.Enemy.maxHP * (phaseTwo ? .5f : 1f);
                f.Boss.AutoAttack = true;
                var sequence = phaseTwo
                    ? new[] { Boss112Pattern.DelayedSlam, Boss112Pattern.SweepingBeam, Boss112Pattern.TripleSlam, Boss112Pattern.DelayedSlam }
                    : new[] { Boss112Pattern.PrecisionBeam, Boss112Pattern.HeavySlam, Boss112Pattern.PrecisionBeam };
                foreach (var expected in sequence)
                {
                    yield return Until(() => f.Boss.IsBusy);
                    Assert.That(f.Boss.Pattern, Is.EqualTo(expected));
                    f.Boss.CancelPattern();
                }
                f.Boss.AutoAttack = false;
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ManualBeamAndSlamLockTheirWarningsAndEndInReachableRecovery()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            float started = Time.time;
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.PrecisionBeam), Is.True);
            yield return Until(() => f.Boss.Phase == Boss112Phase.BeamLocked);
            float locked = f.Boss.LockedX;
            Assert.That(f.Boss.Beam.IsDamaging, Is.False);
            f.Target.transform.position = new Vector3(-12, 0, 0);
            yield return Until(() => f.Boss.Beam.IsDamaging);
            Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(1.48f));
            Assert.That(f.Boss.LockedX, Is.EqualTo(locked));
            Assert.That(f.Boss.Beam.DamageCollider.bounds.center.x, Is.EqualTo(locked).Within(.05f));
            yield return Until(() => f.Boss.Phase == Boss112Phase.Recovery);
            Assert.That(f.Boss.Beam.IsDamaging, Is.False);
            yield return Wait(.8f);
            var rb = f.Enemy.GetComponent<Rigidbody2D>();
            Assert.That(f.Enemy.bodyCollider.bounds.min.y, Is.LessThan(1.3f),
                $"Ground combos must reach the punish window. phase={f.Boss.Phase}, position={rb.position}, simulated={rb.simulated}, type={rb.bodyType}, constraints={rb.constraints}, mode={Physics2D.simulationMode}");
            Assert.That(f.Enemy.IsTargetable, Is.True);
            yield return Until(() => !f.Boss.IsBusy);
            f.Target.transform.position = new Vector3(7, 0, 0);
            started = Time.time;
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.HeavySlam), Is.True);
            Assert.That(f.Boss.LockedX, Is.EqualTo(7f));
            f.Target.transform.position = new Vector3(-12, 0, 0);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamLocked);
            locked = f.Boss.LockedX;
            Assert.That(locked, Is.EqualTo(7f), "The initial warning must not chase a player who already moved away.");
            f.Target.transform.position = new Vector3(-12, 0, 0);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamFalling);
            Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(.68f));
            while (f.Boss.Phase == Boss112Phase.SlamFalling)
            {
                Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
                Assert.That(f.Boss.LockedX, Is.EqualTo(locked));
                yield return null;
            }
            Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamImpact));
            Assert.That(f.Boss.ImpactCollider.enabled, Is.True);
            Assert.That(f.Boss.transform.position.y, Is.EqualTo(0f).Within(.1f), "The first damage frame must already be on the floor.");
            yield return Wait(.03f);
            Assert.That(f.Boss.transform.position.y, Is.EqualTo(0f).Within(.1f), "Only the sprite buries; the actor must not enter terrain.");
            yield return Until(() => f.Boss.Phase == Boss112Phase.Recovery);
            Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
            Assert.That(f.Enemy.IsTargetable, Is.True);
            yield return Until(() => !f.Boss.IsBusy);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator VisualMotionBobsLeansAndChargesWithoutMovingThePhysicsOrLockedBeam()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            var visual = f.Boss.transform.Find("Visual");
            var motion = visual.GetComponent<Boss112VisualMotion>();
            Assert.That(motion, Is.Not.Null);
            yield return Wait(.05f);
            Vector2 root = f.Boss.GetComponent<Rigidbody2D>().position;
            Bounds hurtbox = f.Enemy.bodyCollider.bounds;
            Vector3 first = visual.localPosition;
            yield return Wait(.25f);
            Assert.That(Vector3.Distance(first, visual.localPosition), Is.GreaterThan(.005f));
            Assert.That(f.Boss.GetComponent<Rigidbody2D>().position, Is.EqualTo(root));
            Assert.That(f.Enemy.bodyCollider.bounds.center, Is.EqualTo(hurtbox.center));
            Assert.That(f.Enemy.bodyCollider.bounds.size, Is.EqualTo(hurtbox.size));
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.PrecisionBeam), Is.True);
            yield return Wait(.15f);
            Assert.That(Quaternion.Angle(Quaternion.identity, visual.localRotation), Is.GreaterThan(.5f));
            yield return Until(() => f.Boss.Phase == Boss112Phase.BeamLocked);
            Assert.That(f.Boss.BeamChargeProgress, Is.GreaterThan(.7f));
            yield return Wait(.03f);
            Bounds beamBounds = f.Boss.Beam.DamageCollider.bounds;
            Vector2 beamSize = f.Boss.Beam.DamageCollider.size;
            Vector2 beamOffset = f.Boss.Beam.DamageCollider.offset;
            first = visual.localPosition;
            yield return Wait(.1f);
            Assert.That(Vector3.Distance(first, visual.localPosition), Is.GreaterThan(.001f));
            Assert.That(f.Boss.Beam.DamageCollider.bounds.center, Is.EqualTo(beamBounds.center));
            Assert.That(f.Boss.Beam.DamageCollider.bounds.size, Is.EqualTo(beamBounds.size));
            Assert.That(f.Boss.Beam.DamageCollider.size, Is.EqualTo(beamSize));
            Assert.That(f.Boss.Beam.DamageCollider.offset, Is.EqualTo(beamOffset));
            Assert.That(f.Boss.Beam.IsDamaging, Is.False);
            first = visual.localPosition;
            Quaternion rotation = visual.localRotation;
            float timeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                yield return null;
                first = visual.localPosition;
                rotation = visual.localRotation;
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(visual.localPosition, Is.EqualTo(first));
                Assert.That(visual.localRotation, Is.EqualTo(rotation));
            }
            finally { Time.timeScale = timeScale; }
            f.Boss.Beam.MatchVisualTop(f.Boss.transform.position.y + 2f);
            Assert.That(f.Boss.Beam.DamageCollider.bounds.center, Is.EqualTo(beamBounds.center));
            Assert.That(f.Boss.Beam.DamageCollider.bounds.size, Is.EqualTo(beamBounds.size));
            Assert.That(f.Boss.Beam.DamageCollider.size, Is.EqualTo(beamSize));
            Assert.That(f.Boss.Beam.DamageCollider.offset, Is.EqualTo(beamOffset));
            f.Boss.CancelPattern();
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.HeavySlam), Is.True);
            yield return Wait(.05f);
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
            f.Boss.CancelPattern();
            yield return Wait(.1f);
            motion.enabled = false;
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(visual.localScale, Is.EqualTo(new Vector3(1.4f, 1.4f, 1f)));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ShotRecoilFollowsTheMouthWhileSlamContactNeverBouncesOrChangesHitboxes()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            var visual = f.Boss.transform.Find("Visual");
            using (var data = new SerializedObject(visual.GetComponent<Boss112VisualMotion>()))
            {
                foreach (string field in new[] { "bobHeight", "swayDegrees", "movementLeanDegrees", "chargeShake" })
                    data.FindProperty(field).floatValue = 0f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var body = f.Boss.GetComponent<Rigidbody2D>();
            Vector2 impactSize = f.Boss.ImpactCollider.size;
            Vector2 impactOffset = f.Boss.ImpactCollider.offset;
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.PrecisionBeam), Is.True);
            yield return Until(() => f.Boss.Beam.IsDamaging);
            Vector2 root = body.position;
            Bounds hurtbox = f.Enemy.bodyCollider.bounds;
            Bounds beamBounds = f.Boss.Beam.DamageCollider.bounds;
            yield return Wait(.08f);
            Assert.That(visual.localPosition.y, Is.GreaterThan(.2f).And.LessThanOrEqualTo(.321f));
            Assert.That(body.position, Is.EqualTo(root));
            Assert.That(f.Enemy.bodyCollider.bounds.center, Is.EqualTo(hurtbox.center));
            Assert.That(f.Enemy.bodyCollider.bounds.size, Is.EqualTo(hurtbox.size));
            Assert.That(f.Boss.Beam.DamageCollider.bounds.center, Is.EqualTo(beamBounds.center));
            Assert.That(f.Boss.Beam.DamageCollider.bounds.size, Is.EqualTo(beamBounds.size));
            var beamVisual = f.Boss.Beam.transform.Find("BeamVisual").GetComponent<SpriteRenderer>();
            var mouth = visual.Find("BeamMouth");
            Assert.That(mouth, Is.Not.Null);
            Assert.That(beamVisual.sortingLayerID, Is.EqualTo(visual.GetComponent<SpriteRenderer>().sortingLayerID));
            Assert.That(beamVisual.sortingOrder, Is.LessThan(visual.GetComponent<SpriteRenderer>().sortingOrder));
            Assert.That(beamVisual.bounds.max.y,
                Is.EqualTo(mouth.position.y).Within(.03f));
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                yield return null;
                Vector3 paused = visual.localPosition;
                for (int i = 0; i < 4; i++) yield return null;
                Assert.That(visual.localPosition, Is.EqualTo(paused));
            }
            finally { Time.timeScale = scale; }
            yield return Wait(.27f);
            Assert.That(visual.localPosition.y, Is.EqualTo(0f).Within(.01f));
            Assert.That(f.Boss.Beam.IsDamaging, Is.True, "Recoil must settle before the original beam window ends.");
            f.Boss.CancelPattern();
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.HeavySlam), Is.True);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
            yield return Wait(.01f);
            Assert.That(visual.localPosition.y, Is.EqualTo(0f).Within(.001f), "The impact pose must stay planted.");
            Bounds impactBounds = f.Boss.ImpactCollider.bounds;
            yield return Wait(.09f);
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(body.position.y, Is.EqualTo(f.Boss.FloorY).Within(.001f));
            Assert.That(f.Boss.ImpactCollider.bounds.center, Is.EqualTo(impactBounds.center));
            Assert.That(f.Boss.ImpactCollider.bounds.size, Is.EqualTo(impactBounds.size));
            yield return Until(() => f.Boss.Phase == Boss112Phase.Recovery);
            float settledAt = Time.time;
            while (Time.time - settledAt < .5f)
            {
                Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero), "The buried pose must not rebound after landing.");
                yield return null;
            }
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
            Assert.That(f.Boss.ImpactCollider.size, Is.EqualTo(impactSize));
            Assert.That(f.Boss.ImpactCollider.offset, Is.EqualTo(impactOffset));
            Assert.That(visual.localScale, Is.EqualTo(new Vector3(1.4f, 1.4f, 1f)));
            Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.identity), Is.LessThanOrEqualTo(3.01f));
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CancellationAndDeathAlwaysCloseBothDamageWindows()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            foreach (var pattern in new[] { Boss112Pattern.PrecisionBeam, Boss112Pattern.HeavySlam })
            {
                f.Boss.TryStartPattern(pattern);
                yield return Wait(.2f);
                f.Boss.CancelPattern();
                Assert.That(f.Boss.IsBusy, Is.False);
                Assert.That(f.Boss.Beam.IsDamaging, Is.False);
                Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
            }
            f.Boss.TryStartPattern(Boss112Pattern.PrecisionBeam);
            yield return Until(() => f.Boss.Beam.IsDamaging);
            typeof(EnemyController).GetMethod("Die", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(f.Enemy, null);
            Assert.That(f.Boss.Beam.IsDamaging, Is.False);
            Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.HeavySlam), Is.False);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SlamImpactUsesNormalDodgeRulesAndDamagesEachTargetOnlyOnce()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            f.Boss.TryStartPattern(Boss112Pattern.HeavySlam);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
            var host = new GameObject("Slam damage target"); host.SetActive(false);
            try
            {
                var player = host.AddComponent<PlayerCharacter>(); player.enabled = false;
                var box = host.AddComponent<BoxCollider2D>(); box.enabled = false;
                host.SetActive(true);
                typeof(PlayerCharacter).GetField("currentHP", Private).SetValue(player, 1000f);
                typeof(PlayerCharacter).GetField("damageInvulnerableUntil", Private).SetValue(player, float.NegativeInfinity);
                var hitbox = f.Boss.ImpactCollider.GetComponent<EnemyAttackHitbox>();
                Assert.That(hitbox.AttackProperty, Is.EqualTo(EnemyAttackProperty.Normal));
                var hit = typeof(EnemyAttackHitbox).GetMethod("TryDamagePlayer", Private);
                hit.Invoke(hitbox, new object[] { box });
                Assert.That(player.CurrentHP, Is.EqualTo(835f));
                typeof(PlayerCharacter).GetField("damageInvulnerableUntil", Private).SetValue(player, float.NegativeInfinity);
                hit.Invoke(hitbox, new object[] { box });
                Assert.That(player.CurrentHP, Is.EqualTo(835f));
            }
            finally { Object.DestroyImmediate(host); }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AuthoredSceneButtonsDamageRecoveryResetAndCameraWorkWithTheRealPlayer()
    {
        var saves = CaptureSaveData();
        int activeSlot = SaveSlotStore.ActiveSlot;
        EditorSceneManager.OpenScene(Boss112PrototypeAuthoring.ScenePath);
        yield return new EnterPlayMode();
        Directory.CreateDirectory("Logs/Boss112");
        Screen.SetResolution(1280, 720, false);
        var room = Object.FindFirstObjectByType<Boss112TestRoom>();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady && PlayerCharacter.Instance != null
            && PlayerCharacter.Instance.StateMachine.CurrentStateId == PlayerStateId.Idle, 15f);
        Assert.That(TrainingSession.IsTrainingContext, Is.True);
        Assert.That(TeamLoader.Instance.RunInventory, Is.SameAs(TrainingSession.Instance.Inventory));
        Assert.That(Object.FindObjectsByType<RoomWaveManager>(FindObjectsSortMode.None), Is.Empty);
        yield return Wait(.5f);
        Capture("01_Ready"); yield return null;
        float cameraX = Camera.main.transform.position.x;
        Button Find(string name) => room.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        Find("PRECISION BEAM").onClick.Invoke();
        yield return Until(() => room.Boss.Phase == Boss112Phase.BeamLocked);
        Capture("02_BeamLocked"); yield return null;
        var player = PlayerCharacter.Instance;
        var body = player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(room.Boss.LockedX, 0f); body.linearVelocity = Vector2.zero;
        float hp = player.CurrentHP;
        yield return Until(() => room.Boss.Beam.IsDamaging);
        Capture("03_BeamActive"); yield return null;
        yield return Until(() => player.CurrentHP < hp);
        float hitHp = player.CurrentHP;
        yield return Until(() => room.Boss.Phase == Boss112Phase.SlamTracking);
        body.position = new Vector2(12f, 0f); body.linearVelocity = Vector2.zero;
        yield return Until(() => room.Boss.Phase == Boss112Phase.Recovery);
        Assert.That(player.CurrentHP, Is.EqualTo(hitHp), "A beam deals at most one accepted hit per activation.");
        Assert.That(Camera.main.transform.position.x, Is.EqualTo(cameraX).Within(.2f));
        Find("RESET TEST").onClick.Invoke();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady);
        Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP));
        Assert.That(Object.FindObjectsByType<Boss112Controller>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Find("HEAVY SLAM").onClick.Invoke();
        yield return Until(() => room.Boss.Phase == Boss112Phase.SlamLocked);
        body.position = new Vector2(12f, 0f); body.linearVelocity = Vector2.zero;
        Capture("04_SlamWarning"); yield return null;
        yield return Until(() => room.Boss.Phase == Boss112Phase.SlamImpact);
        Capture("05_SlamImpact"); yield return null;
        yield return Until(() => room.Boss.Phase == Boss112Phase.Recovery);
        yield return Wait(1.8f);
        Capture("06_Recovery"); yield return null;
        Assert.That(player.CurrentHP, Is.EqualTo(player.MaxHP), "Repositioning outside the committed slam must avoid damage.");
        Assert.That(room.Boss.Enemy.bodyCollider.bounds.min.y, Is.LessThan(1.3f));
        Find("REMOVE BOSS").onClick.Invoke(); yield return null;
        Assert.That(room.Boss, Is.Null);
        Find("SPAWN BOSS").onClick.Invoke();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady);
        Assert.That(room.Boss.AutoAttack, Is.False);
        Find("AUTO: OFF").onClick.Invoke();
        yield return Until(() => room.Boss.IsBusy);
        Find("HITBOX: OFF").onClick.Invoke();
        yield return Until(() => Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Single(l => l.name == "Outline_0").enabled);
        Find("STOP ATTACK").onClick.Invoke();
        Assert.That(room.Boss.IsBusy, Is.False);
        Assert.That(room.Boss.Beam.IsDamaging, Is.False);
        var after = CaptureSaveData();
        foreach (var saved in saves) Assert.That(after[saved.Key], Is.EqualTo(saved.Value), saved.Key);
        Assert.That(SaveSlotStore.ActiveSlot, Is.EqualTo(activeSlot));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RecoveryStaysAtEachLandingPointForTheFullWindowThenBacksteps()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            var body = f.Boss.GetComponent<Rigidbody2D>();
            foreach (Boss112Pattern pattern in System.Enum.GetValues(typeof(Boss112Pattern)))
            {
                f.Enemy.currentHP = f.Enemy.maxHP * .5f;
                f.Target.transform.position = new Vector3(9f, 0f, 0f);
                Assert.That(f.Boss.TryStartPattern(pattern), Is.True);
                yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
                float landingX = f.Boss.LockedX;
                yield return Until(() => f.Boss.IsStaggered);
                float started = Time.time;
                f.Target.transform.position = new Vector3(landingX - 4f, 0f, 0f);
                while (f.Boss.IsStaggered)
                {
                    Assert.That(body.position.x, Is.EqualTo(landingX).Within(.03f));
                    Assert.That(body.position.y, Is.EqualTo(f.Boss.FloorY).Within(.03f));
                    Assert.That(f.Enemy.IsTargetable, Is.True);
                    Assert.That(f.Boss.Beam.IsDamaging || f.Boss.ImpactCollider.enabled, Is.False);
                    yield return null;
                }
                Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(pattern == Boss112Pattern.DelayedSlam ? 3.95f : 3.45f));
                Assert.That(Time.time - started, Is.LessThanOrEqualTo(4.1f));
                Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.Retreat));
                yield return Until(() => !f.Boss.IsBusy);
                Assert.That(body.position.x, Is.GreaterThan(landingX + 2f));
                Assert.That(body.position.y, Is.GreaterThan(5f));
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DelayedSlamUnlocksAtHalfHealthPausesThenSpawnsTwoConfiguredWavesAndStaysLocal()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.DelayedSlam), Is.False);
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.SweepingBeam), Is.False);
            f.Enemy.currentHP = f.Enemy.maxHP * .5f;
            Assert.That(f.Boss.IsSecondPhase, Is.True);
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.DelayedSlam), Is.True);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamFeint);
            float pause = Time.time;
            var body = f.Boss.GetComponent<Rigidbody2D>();
            float x = f.Boss.LockedX;
            f.Target.transform.position = Vector3.left * 12f;
            while (f.Boss.Phase == Boss112Phase.SlamFeint)
            {
                Assert.That(body.position.x, Is.EqualTo(x).Within(.03f));
                Assert.That(body.position.y, Is.EqualTo(4f).Within(.03f));
                Assert.That(f.Boss.ImpactCollider.enabled || f.Boss.Beam.IsDamaging, Is.False);
                yield return null;
            }
            Assert.That(Time.time - pause, Is.GreaterThanOrEqualTo(.48f));
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
            var waves = Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None);
            Assert.That(waves.Length, Is.EqualTo(2));
            Assert.That(waves.Any(w => w.transform.position.x < x), Is.True);
            Assert.That(waves.Any(w => w.transform.position.x > x), Is.True);
            var wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.WavePath);
            float expectedHeight = wavePrefab.GetComponent<BoxCollider2D>().size.y * Mathf.Abs(wavePrefab.transform.localScale.y);
            foreach (var wave in waves)
                Assert.That(wave.GetComponent<Collider2D>().bounds.size.y, Is.EqualTo(expectedHeight).Within(.001f));
            yield return Until(() => f.Boss.IsStaggered);
            yield return Wait(3.6f);
            Assert.That(f.Boss.IsStaggered, Is.True);
            Assert.That(body.position.x, Is.EqualTo(x).Within(.03f));
            f.Boss.CancelPattern();
            yield return null;
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.DelayedSlam), Is.True);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
            f.Enemy.Die();
            yield return null;
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(f.Boss.Beam.IsDamaging || f.Boss.ImpactCollider.enabled, Is.False);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SweepingBeamWarnsBeforeMovingBothWaysThenSlamsTheNewlyLockedPlayerPosition()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            f.Enemy.currentHP = f.Enemy.maxHP * .49f;
            foreach (float side in new[] { -1f, 1f })
            {
                f.Target.transform.position = new Vector3(side * 10f, 0f, 0f);
                Assert.That(f.Boss.TryStartPattern(Boss112Pattern.SweepingBeam), Is.True);
                yield return Until(() => f.Boss.Phase == Boss112Phase.SweepCharging);
                float warning = Time.time;
                while (f.Boss.Phase == Boss112Phase.SweepCharging)
                {
                    Assert.That(f.Boss.Beam.IsDamaging, Is.False);
                    yield return null;
                }
                Assert.That(Time.time - warning, Is.GreaterThanOrEqualTo(1.48f));
                Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SweepActive));
                float started = Time.time, previous = f.Boss.LockedX;
                f.Target.transform.position = new Vector3(side * 12f, 0f, 0f);
                while (f.Boss.Phase == Boss112Phase.SweepActive)
                {
                    Assert.That((f.Boss.LockedX - previous) * -side, Is.GreaterThanOrEqualTo(-.001f));
                    previous = f.Boss.LockedX;
                    yield return null;
                }
                Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(1.98f));
                Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamTracking));
                Assert.That(f.Boss.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(-side * 14.5f).Within(.03f));
                Assert.That(f.Boss.LockedX, Is.EqualTo(side * 12f).Within(.03f));
                f.Target.transform.position = Vector3.zero;
                Assert.That(f.Boss.Beam.IsDamaging, Is.False);
                yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
                Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty,
                    "The normal slam after the sweep must not emit waves; only DelayedSlam does.");
                Assert.That(f.Boss.ImpactCollider.enabled, Is.True);
                yield return Until(() => f.Boss.IsStaggered);
                yield return Wait(1f);
                Assert.That(f.Boss.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(side * 12f).Within(.03f));
                Assert.That(f.Boss.Enemy.IsTargetable, Is.True);
                f.Boss.CancelPattern();
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PhaseChangeWaitsForTheCurrentAttackAndAutoSelectsTheUpgrade()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            f.Boss.TryStartPattern(Boss112Pattern.HeavySlam);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamLocked);
            f.Enemy.currentHP = f.Enemy.maxHP * .49f;
            f.Boss.AutoAttack = true;
            Assert.That(f.Boss.Pattern, Is.EqualTo(Boss112Pattern.HeavySlam));
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
            Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty,
                "A normal slam must remain wave-free when health crosses into phase two.");
            yield return Until(() => f.Boss.Phase == Boss112Phase.Retreat);
            Assert.That(f.Boss.Pattern, Is.EqualTo(Boss112Pattern.HeavySlam));
            yield return Until(() => f.Boss.Pattern == Boss112Pattern.DelayedSlam);
            Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamTracking));
            f.Boss.AutoAttack = false;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AuthoredPhaseTwoButtonsRunBothUpgradesAndTheSweepAcceptsRealDashIFrames()
    {
        var saves = CaptureSaveData();
        EditorSceneManager.OpenScene(Boss112PrototypeAuthoring.ScenePath);
        yield return new EnterPlayMode();
        var room = Object.FindFirstObjectByType<Boss112TestRoom>();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady && PlayerCharacter.Instance != null, 15f);
        Button Find(string name) => room.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        Find("PHASE: 1").onClick.Invoke();
        yield return Wait(.05f);
        Assert.That(room.Boss.IsSecondPhase, Is.True);
        Assert.That(Find("DELAYED SLAM").interactable, Is.True);
        Find("DELAYED SLAM").onClick.Invoke();
        yield return Until(() => room.Boss.Phase == Boss112Phase.SlamFeint);
        Capture("08_Feint");
        var player = PlayerCharacter.Instance;
        var body = player.GetComponent<Rigidbody2D>();
        body.position = new Vector2(12f, 0f); body.linearVelocity = Vector2.zero;
        yield return Until(() => room.Boss.Phase == Boss112Phase.SlamImpact);
        Capture("09_Shockwaves");
        room.StopAttack();
        body.position = new Vector2(-7f, 0f); body.linearVelocity = Vector2.zero;
        yield return Wait(.1f);
        Find("SWEEPING BEAM").onClick.Invoke();
        yield return Until(() => room.Boss.Phase == Boss112Phase.SweepCharging);
        Capture("10_SweepWarning");
        yield return Until(() => room.Boss.Phase == Boss112Phase.SweepActive);
        yield return Wait(.05f);
        Capture("11_SweepActive");
        float hp = player.CurrentHP;
        player.StateMachine.RouteDashInput(1f);
        Assert.That(player.Movement.IsInvincible(), Is.True);
        body.position = new Vector2(room.Boss.LockedX, 0f);
        yield return Wait(.08f);
        Assert.That(player.CurrentHP, Is.EqualTo(hp), "A real dash must avoid the moving beam.");
        yield return Until(() => room.Boss.Phase == Boss112Phase.SlamTracking);
        Capture("12_FinisherWarning");
        Assert.That(player.CurrentHP, Is.EqualTo(hp), "The sweep must not re-hit a successful dodge later in the same window.");
        float landingX = room.Boss.LockedX;
        body.position = new Vector2(landingX < 0f ? 12f : -12f, 0f); body.linearVelocity = Vector2.zero;
        yield return Until(() => room.Boss.IsStaggered);
        yield return Until(() => room.Boss.IsStunPoseHeld);
        Capture("13_HeldStunPose");
        var groundedVisual = room.Boss.transform.Find("Visual");
        Assert.That(groundedVisual.localPosition.y, Is.InRange(0f, .181f));
        Assert.That(groundedVisual.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Slam_08"));
        Assert.That(player.CurrentHP, Is.EqualTo(hp), "Leaving the warned landing point must avoid the finisher.");
        Assert.That(room.Boss.GetComponent<Rigidbody2D>().position.x, Is.EqualTo(landingX).Within(.03f));
        var after = CaptureSaveData();
        foreach (var saved in saves) Assert.That(after[saved.Key], Is.EqualTo(saved.Value), saved.Key);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BeamFinishersReuseTheStandaloneSlamPreparationHeightAndLockedDescent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            f.Enemy.currentHP = f.Enemy.maxHP * .5f;
            var body = f.Boss.GetComponent<Rigidbody2D>();
            var marker = f.Boss.transform.Find("LandingWarning").GetComponent<SpriteRenderer>();
            var lane = f.Boss.GetComponentInChildren<EnemyChargeTelegraph>();
            Assert.That(lane, Is.Not.Null);
            var background = lane.transform.Find("background").GetComponent<LineRenderer>();
            var fill = lane.transform.Find("fill").GetComponent<LineRenderer>();
            foreach (var pattern in new[] { Boss112Pattern.HeavySlam, Boss112Pattern.PrecisionBeam, Boss112Pattern.SweepingBeam, Boss112Pattern.DelayedSlam })
            {
                f.Target.transform.position = new Vector3(-6f, 0f, 0f);
                Assert.That(f.Boss.TryStartPattern(pattern), Is.True);
                yield return Until(() => f.Boss.Phase == Boss112Phase.SlamTracking);
                float began = Time.time, x = f.Boss.LockedX;
                float previousFill = 0f;
                Vector2 held = body.position;
                Assert.That(x, Is.EqualTo(-6f).Within(.03f));
                f.Target.transform.position = new Vector3(12f, 0f, 0f);
                while (f.Boss.Phase == Boss112Phase.SlamTracking || f.Boss.Phase == Boss112Phase.SlamLocked)
                {
                    Assert.That(f.Boss.Beam.IsDamaging || f.Boss.ImpactCollider.enabled, Is.False);
                    Assert.That(f.Boss.LockedX, Is.EqualTo(x));
                    Assert.That(body.position.y, Is.InRange(held.y - .03f, 23.03f));
                    Assert.That(f.Boss.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Slam_01"));
                    if (f.Boss.Phase == Boss112Phase.SlamLocked)
                    {
                        Assert.That(body.position.x, Is.EqualTo(x).Within(.03f));
                        Assert.That(body.position.y, Is.EqualTo(23f).Within(.03f));
                    }
                    typeof(Boss112Controller).GetMethod("LateUpdate", Private).Invoke(f.Boss, null);
                    Assert.That(marker.enabled, Is.False, "The rectangular lane replaces the old landing dot.");
                    Assert.That(background.enabled, Is.True);
                    Assert.That(background.GetPosition(0).x, Is.EqualTo(x).Within(.03f));
                    Assert.That(background.GetPosition(1).x, Is.EqualTo(x).Within(.03f));
                    Assert.That(background.GetPosition(1).y, Is.EqualTo(f.Boss.FloorY).Within(.03f));
                    Assert.That(background.startWidth, Is.EqualTo(f.Boss.ImpactCollider.size.x).Within(.001f));
                    float filled = Vector3.Distance(fill.GetPosition(0), fill.GetPosition(1));
                    Assert.That(filled, Is.GreaterThanOrEqualTo(previousFill - .001f));
                    previousFill = filled;
                    yield return null;
                }
                Assert.That(Time.time - began, Is.InRange(.68f, .8f));
                Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamFalling));
                typeof(Boss112Controller).GetMethod("LateUpdate", Private).Invoke(f.Boss, null);
                Assert.That(background.enabled, Is.False, "The warning ends when the dive starts.");
                Assert.That(body.position.y, Is.EqualTo(23f).Within(.03f), "All slam variants must dive from the original Heavy Slam height.");
                while (f.Boss.Phase == Boss112Phase.SlamFalling)
                {
                    Assert.That(f.Boss.LockedX, Is.EqualTo(x));
                    Assert.That(f.Boss.ImpactCollider.enabled, Is.False);
                    f.Target.transform.position = new Vector3(-f.Target.transform.position.x, 0f, 0f);
                    yield return null;
                }
                if (pattern == Boss112Pattern.DelayedSlam)
                {
                    yield return Wait(.15f);
                    Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamFeint));
                    typeof(Boss112Controller).GetMethod("LateUpdate", Private).Invoke(f.Boss, null);
                    Assert.That(background.enabled, Is.True, "The feint also warns before the final drop.");
                    yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
                }
                Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.SlamImpact));
                Assert.That(body.position.x, Is.EqualTo(x).Within(.03f));
                Assert.That(body.position.y, Is.EqualTo(f.Boss.FloorY).Within(.03f));
                Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(pattern == Boss112Pattern.DelayedSlam ? 2 : 0));
                f.Boss.CancelPattern();
                yield return null;
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator CancellingOrDyingDuringTheFinisherWarningPreventsTheDelayedImpact()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        foreach (bool die in new[] { false, true })
        {
            using (var f = new Fixture())
            {
                yield return f.Ready();
                f.Enemy.currentHP = f.Enemy.maxHP * .5f;
                f.Boss.TryStartPattern(Boss112Pattern.SweepingBeam);
                yield return Until(() => f.Boss.Phase == Boss112Phase.SlamTracking);
                if (die) f.Enemy.Die(); else f.Boss.CancelPattern();
                Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.Idle));
                Assert.That(f.Boss.Beam.IsDamaging || f.Boss.ImpactCollider.enabled, Is.False);
                Assert.That(f.Boss.transform.Find("LandingWarning").GetComponent<SpriteRenderer>().enabled, Is.False);
                foreach (var line in f.Boss.GetComponentInChildren<EnemyChargeTelegraph>().GetComponentsInChildren<LineRenderer>())
                    Assert.That(line.enabled, Is.False);
                yield return Wait(1.3f);
                if (die) Assert.That(f.Boss == null || !f.Boss.Enemy.IsAlive, Is.True);
                else
                {
                    Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.Idle));
                    Assert.That(f.Boss.Beam.IsDamaging || f.Boss.ImpactCollider.enabled, Is.False);
                }
                Assert.That(Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None), Is.Empty);
            }
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LandingClipPlaysNormallyThenHoldsFrameEightAndOnlySwaysDuringStun()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using (var f = new Fixture())
        {
            yield return f.Ready();
            Assert.That(f.Boss.TryStartPattern(Boss112Pattern.HeavySlam), Is.True);
            yield return Until(() => f.Boss.Phase == Boss112Phase.SlamImpact);
            var visual = f.Boss.transform.Find("Visual");
            var sprite = visual.GetComponent<SpriteRenderer>();
            var animator = visual.GetComponent<Animator>();
            var body = f.Boss.GetComponent<Rigidbody2D>();
            float started = Time.time;
            var poses = new HashSet<string>();
            Assert.That(animator.speed, Is.EqualTo(1f));
            while (!f.Boss.IsStunPoseHeld && Time.time - started < 1.2f)
            {
                poses.Add(sprite.sprite.name);
                Assert.That(animator.speed, Is.EqualTo(1f));
                yield return null;
            }
            Assert.That(f.Boss.IsStunPoseHeld, Is.True);
            Assert.That(Time.time - started, Is.InRange(.45f, .8f), "Landing must finish at clip speed, not stretch across the whole stun window.");
            Assert.That(poses, Does.Contain("Slam_04"));
            Assert.That(poses, Does.Contain("Slam_06"));
            Assert.That(sprite.sprite.name, Is.EqualTo("Slam_08"));
            Assert.That(animator.speed, Is.Zero);
            Vector2 root = body.position;
            Bounds hurtbox = f.Enemy.bodyCollider.bounds;
            float minimum = 0f, maximum = 0f;
            float timeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                yield return null;
                Quaternion paused = visual.localRotation;
                for (int i = 0; i < 4; i++) yield return null;
                Assert.That(visual.localRotation, Is.EqualTo(paused));
            }
            finally { Time.timeScale = timeScale; }
            while (f.Boss.IsStaggered)
            {
                float angle = Mathf.DeltaAngle(0f, visual.localEulerAngles.z);
                minimum = Mathf.Min(minimum, angle); maximum = Mathf.Max(maximum, angle);
                Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(3.01f));
                Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero), "Stun sway may not lift or bounce the picture.");
                Assert.That(sprite.sprite.name, Is.EqualTo("Slam_08"));
                Assert.That(body.position, Is.EqualTo(root));
                Assert.That(f.Enemy.bodyCollider.bounds.center, Is.EqualTo(hurtbox.center));
                Assert.That(f.Enemy.bodyCollider.bounds.size, Is.EqualTo(hurtbox.size));
                Assert.That(f.Boss.Beam.IsDamaging || f.Boss.ImpactCollider.enabled, Is.False);
                yield return null;
            }
            Assert.That(minimum, Is.LessThan(-.5f));
            Assert.That(maximum, Is.GreaterThan(.5f));
            Assert.That(f.Boss.Phase, Is.EqualTo(Boss112Phase.Retreat));
            yield return Wait(.05f);
            Assert.That(f.Boss.IsStunPoseHeld, Is.False);
            Assert.That(animator.speed, Is.EqualTo(1f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"), Is.True);
        }
        yield return new ExitPlayMode();
    }

    private static Dictionary<string, string> CaptureSaveData()
    {
        var result = new Dictionary<string, string>();
        for (int i = 0; i < SaveSlotStore.SlotCount; i++)
            foreach (string key in new[] { SaveSlotStore.MetaKey(i), SaveSlotStore.BossRunKey(i), SaveSlotStore.BossMetaKey(i),
                $"project.save.slot{i}.header", $"project.save.slot{i}.occupied" })
                result[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, "<non-string>") + ":" + PlayerPrefs.GetInt(key, -1) : "<missing>";
        return result;
    }

    [UnityTest]
    public IEnumerator BossDefeatAndPlayerDeathResetStayInTheIsolatedRoomWithoutChangingSaves()
    {
        var saves = CaptureSaveData();
        int slot = SaveSlotStore.ActiveSlot;
        EditorSceneManager.OpenScene(Boss112PrototypeAuthoring.ScenePath);
        yield return new EnterPlayMode();
        var room = Object.FindFirstObjectByType<Boss112TestRoom>();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady && PlayerCharacter.Instance != null, 15f);
        var runEnd = Object.FindFirstObjectByType<Project.Core.RunEnd.RunEndController>(FindObjectsInactive.Include);
        Assert.That(runEnd.enabled, Is.False, "The normal training death flow must not send this test back to MainMenu.");
        int biome = RunProgressionManager.EnsureInstance().CurrentBiomeIndex;
        int currency = TrainingSession.Instance.Inventory.currency;
        room.Boss.Enemy.Die();
        yield return Wait(.2f);
        Assert.That(RunProgressionManager.Instance.CurrentBiomeIndex, Is.EqualTo(biome));
        Assert.That(TrainingSession.Instance.Inventory.currency, Is.EqualTo(currency));
        PlayerCharacter.Instance.ForceDeath();
        yield return Wait(.2f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Test_Boss112"));
        room.ResetTest();
        yield return Until(() => Object.FindFirstObjectByType<Boss112TestRoom>() != null
            && Object.FindFirstObjectByType<Boss112TestRoom>() != room, 15f);
        room = Object.FindFirstObjectByType<Boss112TestRoom>();
        yield return Until(() => room.Boss != null && room.Boss.Enemy.IsCombatReady && PlayerCharacter.Instance != null
            && !PlayerCharacter.Instance.IsDead, 15f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Test_Boss112"));
        Assert.That(TrainingSession.IsTrainingContext, Is.True);
        Assert.That(PlayerCharacter.Instance.CurrentHP, Is.EqualTo(PlayerCharacter.Instance.MaxHP));
        var after = CaptureSaveData();
        foreach (var saved in saves) Assert.That(after[saved.Key], Is.EqualTo(saved.Value), saved.Key);
        Assert.That(SaveSlotStore.ActiveSlot, Is.EqualTo(slot));
        Capture("07_AfterDeathReset");
        yield return new ExitPlayMode();
    }

    private static IEnumerator Until(System.Func<bool> predicate, float timeout = 10f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!predicate() && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(predicate(), Is.True, "Prototype did not reach the expected state before timeout.");
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        yield return Until(() => Time.time >= end, seconds + 5f);
    }

    internal static void Capture(string name)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var target = new RenderTexture(1280, 720, 24); target.Create();
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        float aspect = camera.aspect;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 16f / 9f;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Assert.That(image.GetPixels32().Count(p => p.r > 20 || p.g > 20 || p.b > 20), Is.GreaterThan(1280 * 720 / 5));
            File.WriteAllBytes($"Logs/Boss112/{name}.png", image.EncodeToPNG());
        }
        finally
        {
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
            camera.targetTexture = oldTarget; camera.aspect = aspect; RenderTexture.active = oldActive;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
        }
    }

    private sealed class Fixture : System.IDisposable
    {
        private readonly GameObject root = new GameObject("Boss112 test fixture");
        public readonly GameObject Target;
        public readonly Boss112Controller Boss;
        public EnemyController Enemy => Boss.Enemy;
        public Fixture()
        {
            var floor = new GameObject("Floor"); floor.transform.SetParent(root.transform); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = Vector3.down; floor.AddComponent<BoxCollider2D>().size = new Vector2(48, 2);
            Target = new GameObject("Target"); Target.transform.SetParent(root.transform); Target.transform.position = Vector3.right * 6;
            var holder = new GameObject("Inactive boss"); holder.transform.SetParent(root.transform); holder.SetActive(false);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Boss112PrototypeAuthoring.BossPath), holder.transform);
            Boss = instance.GetComponent<Boss112Controller>();
            using (var data = new SerializedObject(Boss))
            {
                data.FindProperty("bindHealthHud").boolValue = false;
                foreach (string cue in new[] { "beamChargeSfx", "lockSfx", "impactSfx" }) data.FindProperty(cue).objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            instance.transform.position = Vector3.up * 7;
            Boss.ConfigureArena(-18, 18, 0); Boss.SetTarget(Target.transform);
            holder.SetActive(true); Enemy.InitializeSpawn(instance.transform.position, Quaternion.identity);
        }
        public IEnumerator Ready() => Until(() => Enemy.IsCombatReady);
        public void Dispose() => Object.DestroyImmediate(root);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
