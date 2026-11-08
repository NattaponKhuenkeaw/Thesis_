#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Project.Core.ZoneState;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class PlayerIntroRevealTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase("test_0_Main/CharacterObject_main", 1f)]
    [TestCase("test_0_Main/CharacterObject_main", -1f)]
    [TestCase("test_1_CameraGirl/CharacterObject_cg", 1f)]
    [TestCase("test_1_CameraGirl/CharacterObject_cg", -1f)]
    public void IntroUsesTheRealRunningClipAndAdvancesSpriteFramesAtNormalSpeed(string characterPath, float direction)
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(direction);
        try
        {
            var driver = AttachRealAnimator(player, characterPath);
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance); player.StateMachine.ChangeState(intro);
            intro.BeginVisibleWalk(); intro.Update();
            player.Movement.RB.linearVelocity = Vector2.right * player.Movement.HorizontalInput * profile.moveSpeed;
            Assert.That(Mathf.Abs(player.Movement.RB.linearVelocity.x), Is.EqualTo(profile.moveSpeed).Within(.001f));
            SampleAnimator(driver, .02f);
            AssertRunning(driver.anim);
            var renderer = driver.GetComponent<SpriteRenderer>();
            var first = renderer.sprite;
            var run = driver.anim.GetCurrentAnimatorClipInfo(0).Single(c => c.clip.name.ToLowerInvariant().Contains("run") && c.weight > .99f).clip;
            driver.anim.Update(run.length * .37f);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.EqualTo(first), "The Run clip must actually change the displayed sprite, not only the Speed parameter");
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    [TestCase("test_0_Main/CharacterObject_main")]
    [TestCase("test_1_CameraGirl/CharacterObject_cg")]
    public void CinematicAnimationDoesNotRunAgainstAWallOrChangeNormalLocomotion(string characterPath)
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(1f);
        try
        {
            var driver = AttachRealAnimator(player, characterPath);
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance); player.StateMachine.ChangeState(intro);
            intro.BeginVisibleWalk(); intro.Update();
            player.Movement.RB.linearVelocity = Vector2.zero;
            SampleAnimator(driver, .05f);
            Assert.That(driver.anim.GetFloat("Speed"), Is.Zero, "Blocked motion must not pretend to be running");
            Assert.That(driver.anim.GetCurrentAnimatorClipInfo(0).Where(c => c.clip.name.ToLowerInvariant().Contains("idle")).Sum(c => c.weight), Is.GreaterThan(.99f));
            player.Movement.RB.linearVelocity = Vector2.right * profile.moveSpeed;
            SampleAnimator(driver, .02f); AssertRunning(driver.anim);
            player.StateMachine.ChangeState(player.StateMachine.IdleState);
            SampleAnimator(driver, .02f);
            Assert.That(driver.anim.GetFloat("Speed"), Is.Zero);
            player.Movement.SetHorizontalInput(.1f); player.Movement.RB.linearVelocity = Vector2.right * .84f;
            SampleAnimator(driver, .02f);
            Assert.That(driver.anim.GetFloat("Speed"), Is.EqualTo(.84f).Within(.001f), "Ordinary gameplay must keep its original velocity-driven animation");
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    internal static PlayerAnimator AttachRealAnimator(PlayerCharacter player, string path)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>($"Assets/Project/Character/Hero/{path}.asset");
        Assert.That(character, Is.Not.Null);
        var visual = new GameObject("Actual character animator"); visual.transform.SetParent(player.transform, false);
        visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>(); animator.runtimeAnimatorController = character.metaData.animatorController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var driver = visual.AddComponent<PlayerAnimator>(); driver.anim = animator; driver.movement = player.Movement;
        player.Animator = driver;
        player.gameObject.SetActive(true);
        typeof(PlayerAnimator).GetMethod("Awake", Private).Invoke(driver, null);
        animator.Rebind(); animator.Update(0f);
        return driver;
    }

    private static void SampleAnimator(PlayerAnimator driver, float delta)
    {
        typeof(PlayerAnimator).GetMethod("Update", Private).Invoke(driver, null);
        driver.anim.Update(delta);
    }

    private static void AssertRunning(Animator animator)
    {
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        Assert.That(clips.Where(c => c.clip.name.ToLowerInvariant().Contains("run")).Sum(c => c.weight), Is.GreaterThan(.99f),
            "Actual clip weights: " + string.Join(", ", clips.Select(c => c.clip.name + "=" + c.weight.ToString("F3"))));
    }

    [TestCase(3f)] [TestCase(9.5f)] [TestCase(20f)]
    public void ShortEntranceWaitsForRevealThenWalksAtNormalSpeed(float speed)
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(1f);
        try
        {
            player.Movement.moveSpeedOverride = speed;
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance);
            player.StateMachine.ChangeState(intro);
            intro.Update();
            Assert.That(player.Movement.HorizontalInput, Is.Zero);
            Assert.That(player.StateMachine.CurrentStateId, Is.EqualTo(PlayerStateId.Intro));
            Assert.That(intro.AcceptsAttackInput || intro.AcceptsJumpInput || intro.AcceptsMoveInput, Is.False);
            intro.BeginVisibleWalk(); intro.Update();
            float travelSpeed = player.Movement.HorizontalInput * speed;
            Assert.That(travelSpeed, Is.EqualTo(speed).Within(.001f));
            Assert.That(player.Movement.moveSpeedOverride, Is.EqualTo(speed), "Intro must not change the character's normal speed");
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    [Test]
    public void LongEntranceKeepsNormalSpeedAndFreshTimerAfterReveal()
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(10f);
        try
        {
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance); player.StateMachine.ChangeState(intro);
            typeof(PlayerIntroState).GetField("introStartedAt", Private).SetValue(intro, Time.unscaledTime - 11f);
            intro.BeginVisibleWalk(); intro.Update();
            Assert.That(player.Movement.HorizontalInput, Is.EqualTo(1f));
            Assert.That(player.StateMachine.CurrentStateId, Is.EqualTo(PlayerStateId.Intro));
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    [TestCase(1f)] [TestCase(-1f)] [TestCase(10f)]
    public void WalkStartsAtFullSpeedAndEstimatesTheRemainingTravel(float distance)
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(distance);
        try
        {
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance); player.StateMachine.ChangeState(intro);
            intro.BeginVisibleWalk(); intro.Update();
            float speed = player.Movement.HorizontalInput * profile.moveSpeed;
            Assert.That(speed, Is.EqualTo(Mathf.Sign(distance) * profile.moveSpeed).Within(.001f));
            Assert.That(intro.EstimatedRemainingWalkSeconds,
                Is.EqualTo((Mathf.Abs(distance) - .2f) / profile.moveSpeed).Within(.001f));
            Assert.That(player.StateMachine.IsCinematicTraversal, Is.True);
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    [Test]
    public void AbortedRevealTimesOutAndAReusedIntroDoesNotKeepItsWaitFlag()
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(1f);
        try
        {
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(entrance); player.StateMachine.ChangeState(intro);
            typeof(PlayerIntroState).GetField("introStartedAt", Private).SetValue(intro, Time.unscaledTime - 11f);
            LogAssert.Expect(LogType.Warning, "[PlayerIntroState] Reveal never completed. Releasing the cinematic lock.");
            intro.Update();
            Assert.That(player.StateMachine.CurrentStateId, Is.EqualTo(PlayerStateId.Idle));
            Assert.That(player.Movement.HorizontalInput, Is.Zero);
            intro.TargetEntrance = entrance;
            player.StateMachine.ChangeState(intro); intro.Update();
            Assert.That(player.Movement.HorizontalInput, Is.GreaterThan(0f));
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    [TestCase(false)] [TestCase(true)]
    public void MissingOrZeroLengthEntranceReleasesControlAfterReveal(bool missing)
    {
        var player = CreatePlayer(false, out var profile);
        var entrance = CreateEntrance(0f);
        try
        {
            var intro = player.StateMachine.IntroState;
            intro.PrepareForReveal(missing ? null : entrance); player.StateMachine.ChangeState(intro);
            intro.BeginVisibleWalk(); intro.Update();
            Assert.That(player.StateMachine.CurrentStateId, Is.EqualTo(PlayerStateId.Idle));
            Assert.That(player.Movement.HorizontalInput, Is.Zero);
        }
        finally { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile); }
    }

    [UnityTest]
    public IEnumerator RealFadeRevealsAnAlreadyWalkingPlayerWithoutHidingTheWholeShortIntro()
        => VerifyWalkingReveal(1f);

    [UnityTest]
    public IEnumerator CameragirlAlsoRunsThroughTheRealFade()
        => VerifyWalkingReveal(1f, "test_1_CameraGirl/CharacterObject_cg");

    [UnityTest]
    public IEnumerator AuthoredLv1EntranceAlsoRevealsAnAlreadyWalkingPlayer()
    {
        var source = Resources.Load<GameObject>("RoomLayouts/LV/RoomLayout_LV_01");
        var entrance = source.GetComponentInChildren<EntrancePoint>();
        Assert.That(entrance, Is.Not.Null);
        return VerifyWalkingReveal(entrance.StopPosition.x - entrance.SpawnPosition.x);
    }

    [TestCase(-4f)] [TestCase(-20f)] [TestCase(2f)]
    public void IntroMovesOnlyTheEntranceWallAndPreservesTheRoomCameraBounds(float spawnX)
    {
        var cameraRoot = new GameObject("Intro confiner test");
        var bounds = new GameObject("Camera bounds").AddComponent<BoxCollider2D>();
        bounds.isTrigger = true; bounds.size = new Vector2(60f, 30f);
        cameraRoot.AddComponent<CinemachineCamera>();
        cameraRoot.AddComponent<CinemachineConfiner2D>().BoundingShape2D = bounds;
        var layout = CreateBoundary(out var boundary, out var left, out var right);
        var entrance = CreateEntrance(4f); entrance.transform.position = Vector3.right * spawnX;
        try
        {
            Physics2D.SyncTransforms();
            boundary.BindChamberWalls(left, right);
            var originalSize = bounds.size;
            var originalOffset = bounds.offset;
            boundary.PrepareForIntro(entrance);
            Assert.That(bounds.size, Is.EqualTo(originalSize), "Intro must not expand the camera toward an offscreen player");
            Assert.That(bounds.offset, Is.EqualTo(originalOffset));
            Physics2D.SyncTransforms();
            Assert.That(left.bounds.max.x, Is.LessThanOrEqualTo(Mathf.Min(0f, spawnX - .75f) + .001f));
            boundary.CompleteIntro();
            Physics2D.SyncTransforms();
            Assert.That(left.bounds.max.x, Is.EqualTo(0f).Within(.001f));
            Assert.That(bounds.size, Is.EqualTo(originalSize));
            Assert.That(bounds.offset, Is.EqualTo(originalOffset), "Restoring the physical wall must not reframe the camera");
        }
        finally
        {
            Object.DestroyImmediate(layout); Object.DestroyImmediate(entrance.gameObject);
            Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(bounds.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator MainRunsFromOutsideTheFrameWithoutDraggingTheActualRoomCamera()
        => VerifyOffscreenEntrance("test_0_Main/CharacterObject_main", 16f / 9f);

    [UnityTest]
    public IEnumerator CameragirlRunsFromOutsideTheFrameWithoutDraggingTheActualRoomCamera()
        => VerifyOffscreenEntrance("test_1_CameraGirl/CharacterObject_cg", 21f / 9f);

    private static GameObject CreateBoundary(out SceneBoundaryController boundary, out BoxCollider2D left, out BoxCollider2D right)
    {
        var layout = new GameObject("Intro test room"); layout.SetActive(false);
        left = new GameObject("StageLeftWall").AddComponent<BoxCollider2D>();
        right = new GameObject("StageRightWall").AddComponent<BoxCollider2D>();
        left.transform.SetParent(layout.transform); right.transform.SetParent(layout.transform);
        left.size = right.size = new Vector2(6f, 30f);
        left.transform.position = new Vector3(-3f, 0f); right.transform.position = new Vector3(63f, 0f);
        var floor = new GameObject("Ground_01").AddComponent<BoxCollider2D>();
        floor.transform.SetParent(layout.transform); floor.size = new Vector2(100f, 2f);
        floor.transform.position = new Vector3(20f, -1f);
        floor.gameObject.layer = LayerMask.NameToLayer("Ground");
        boundary = layout.AddComponent<SceneBoundaryController>();
        typeof(SceneBoundaryController).GetField("_entranceWall", Private).SetValue(boundary, left);
        typeof(SceneBoundaryController).GetField("_exitWall", Private).SetValue(boundary, right);
        layout.SetActive(true);
        return layout;
    }

    private static IEnumerator VerifyOffscreenEntrance(string characterPath, float aspect)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var player = CreatePlayer(true, out var profile); player.tag = "Player";
        player.transform.position = new Vector3(-4f, 0f); player.Movement.RB.position = new Vector2(-4f, 0f);
        var driver = AttachRealAnimator(player, characterPath);
        var room = ScriptableObject.CreateInstance<RoomData>();
        typeof(RoomData).GetField("sceneName", Private).SetValue(room, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        var provider = ScriptableObject.CreateInstance<ZoneStateProvider>(); provider.SetCurrentRoom(room);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefab/Camera/CameraSetUp.prefab");
        var rig = Object.Instantiate(source);
        var cameraManager = rig.GetComponent<CameraManager>();
        typeof(CameraManager).GetField("_zoneStateProvider", Private).SetValue(cameraManager, provider);
        var output = rig.GetComponentInChildren<Camera>(); output.aspect = aspect;
        var layout = CreateBoundary(out var boundary, out _, out _);
        var entrance = CreateEntrance(8f); entrance.transform.position = new Vector3(-4f, 0f);
        var manager = new GameObject("Offscreen intro transition").AddComponent<SceneTransitionManager>();
        try
        {
            Physics2D.SyncTransforms();
            boundary.PrepareForIntro(entrance);
            cameraManager.SnapToPlayerForRoomEnter(Vector3.zero);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(output.WorldToViewportPoint(player.transform.position).x, Is.LessThan(0f), "The entrance must really start outside the camera frame");
            float fixedCameraX = output.transform.position.x;
            float fixedCameraY = output.transform.position.y;
            float fixedCameraSize = output.orthographicSize;
            var confiner = rig.GetComponentsInChildren<CinemachineConfiner2D>().First(c => c.BoundingShape2D is BoxCollider2D);
            var cameraBounds = (BoxCollider2D)confiner.BoundingShape2D;
            var fixedBounds = cameraBounds.bounds;
            var routine = (IEnumerator)typeof(SceneTransitionManager).GetMethod("RevealEntranceRoutine", Private)
                .Invoke(manager, new object[] { player, entrance });
            manager.StartCoroutine(routine);
            float deadline = Time.realtimeSinceStartup + 4f;
            bool sawVisibleRun = false;
            while (player.StateMachine.CurrentStateId == PlayerStateId.Intro && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.That(output.transform.position.x, Is.EqualTo(fixedCameraX).Within(.08f), "Camera chased the offscreen entrance instead of waiting inside the room");
                Assert.That(output.transform.position.y, Is.EqualTo(fixedCameraY).Within(.08f));
                Assert.That(output.orthographicSize, Is.EqualTo(fixedCameraSize).Within(.001f));
                Assert.That(cameraBounds.bounds.min.x, Is.EqualTo(fixedBounds.min.x).Within(.001f));
                float viewportX = output.WorldToViewportPoint(player.transform.position).x;
                if (viewportX > .02f && player.StateMachine.CurrentStateId == PlayerStateId.Intro)
                {
                    AssertRunning(driver.anim);
                    sawVisibleRun = true;
                }
            }
            Assert.That(sawVisibleRun, Is.True);
            Assert.That(player.StateMachine.CurrentStateId, Is.EqualTo(PlayerStateId.Idle));
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                Assert.That(output.transform.position.x, Is.EqualTo(fixedCameraX).Within(.08f), "Completing Intro must not pull the camera back");
                Assert.That(output.transform.position.y, Is.EqualTo(fixedCameraY).Within(.08f));
            }
            player.Movement.RB.position = new Vector2(30f, 0f);
            float followDeadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < followDeadline) yield return null;
            Assert.That(output.transform.position.x, Is.GreaterThan(fixedCameraX + 1f), "Normal camera following must still work after Intro");
        }
        finally
        {
            Object.DestroyImmediate(layout); Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(rig); Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile);
            Object.DestroyImmediate(provider); Object.DestroyImmediate(room);
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyWalkingReveal(float distance, string characterPath = "test_0_Main/CharacterObject_main")
    {
        Assert.That(Mathf.Abs(distance), Is.GreaterThan(.2f), "Entrance and StopPoint must leave room to walk");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var player = CreatePlayer(true, out var profile);
        var driver = AttachRealAnimator(player, characterPath);
        var entrance = CreateEntrance(distance);
        var manager = new GameObject("Intro reveal transition test").AddComponent<SceneTransitionManager>();
        try
        {
            // Let Play Mode startup settle before measuring a sub-second fade.
            yield return null;
            yield return null;
            yield return null;
            Assert.That((float)typeof(SceneTransitionManager).GetField("fadeDuration", Private).GetValue(manager), Is.EqualTo(.15f));
            var routine = (IEnumerator)typeof(SceneTransitionManager).GetMethod("RevealEntranceRoutine", Private)
                .Invoke(manager, new object[] { player, entrance });
            manager.StartCoroutine(routine);
            var fade = (Image)typeof(SceneTransitionManager).GetField("fadeImage", Private).GetValue(manager);
            float deadline = Time.realtimeSinceStartup + Mathf.Abs(distance) / profile.moveSpeed + 3f;
            bool sawVisibleRun = false;
            bool sawFullyRevealedWalk = false;
            while (player.StateMachine.CurrentStateId == PlayerStateId.Intro && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (player.StateMachine.CurrentStateId != PlayerStateId.Intro || fade.color.a >= .99f) continue;
                Assert.That(Mathf.Abs(player.Movement.RB.position.x), Is.GreaterThan(0f), "Player was still standing during the reveal");
                Assert.That(Mathf.Abs(player.Movement.RB.linearVelocity.x), Is.EqualTo(profile.moveSpeed).Within(.001f));
                AssertRunning(driver.anim);
                Assert.That(driver.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
                sawVisibleRun = true;
                if (fade.color.a <= .001f) sawFullyRevealedWalk = true;
            }
            while (fade.color.a > .001f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(fade.color.a, Is.EqualTo(0f).Within(.001f));
            Assert.That(sawVisibleRun, Is.True, "The whole run was hidden behind the fade");
            Assert.That(sawFullyRevealedWalk, Is.True, "The fade must finish before the short entrance walk ends");
            Assert.That(player.StateMachine.CurrentStateId, Is.EqualTo(PlayerStateId.Idle));
            Assert.That(player.Movement.RB.position.x, Is.InRange(distance - .21f, distance + .21f));
            Assert.That(player.Movement.HorizontalInput, Is.Zero);
            Assert.That(player.StateMachine.IsCinematicTraversal, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(manager.gameObject); Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(entrance.gameObject); Object.DestroyImmediate(profile);
        }
        yield return new ExitPlayMode();
    }

    internal static PlayerCharacter CreatePlayer(bool activate, out MovementProfile profile)
    {
        var host = new GameObject("Intro test player"); host.SetActive(false);
        host.layer = LayerMask.NameToLayer("Player");
        var rb = host.AddComponent<Rigidbody2D>(); rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
        var body = host.AddComponent<BoxCollider2D>(); body.size = new Vector2(1.55f, 2.6f); body.offset = Vector2.up * 1.3f;
        var player = host.AddComponent<PlayerCharacter>();
        var fsm = host.AddComponent<PlayerStateMachine>(); fsm.playerCharacter = player; fsm.showFsmLogs = false;
        var movement = host.AddComponent<PlayerMovement>();
        profile = ScriptableObject.CreateInstance<MovementProfile>(); profile.moveSpeed = 9.5f;
        movement.profile = profile;
        player.StateMachine = fsm; player.Movement = movement;
        if (activate) host.SetActive(true);
        else
        {
            typeof(PlayerMovement).GetMethod("Awake", Private).Invoke(movement, null);
            typeof(PlayerStateMachine).GetMethod("Awake", Private).Invoke(fsm, null);
        }
        return player;
    }

    private static EntrancePoint CreateEntrance(float distance)
    {
        var host = new GameObject("Intro test entrance");
        var entrance = host.AddComponent<EntrancePoint>();
        var stop = new GameObject("StopPoint"); stop.transform.SetParent(host.transform, false); stop.transform.localPosition = Vector3.right * distance;
        typeof(EntrancePoint).GetField("stopPoint", Private).SetValue(entrance, stop.transform);
        return entrance;
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
#endif
