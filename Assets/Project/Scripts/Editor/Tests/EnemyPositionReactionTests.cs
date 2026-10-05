#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EnemyPositionReactionTests
{
    private GameObject actor;
    private GameObject obstacle;
    private Rigidbody2D body;
    private object motor;
    private Type motorType;
    private SimulationMode2D previousSimulation;

    [SetUp]
    public void SetUp()
    {
        previousSimulation = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        actor = new GameObject("PositionReactionTest");
        actor.transform.position = new Vector3(10000, 10000, 0);
        body = actor.AddComponent<Rigidbody2D>();
        body.gravityScale = 2f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var collider = actor.AddComponent<BoxCollider2D>();
        motorType = typeof(EnemyHitReactionHandler).GetNestedType("PositionReactionMotion", BindingFlags.NonPublic);
        motor = Activator.CreateInstance(motorType, body, collider, (LayerMask)Physics2D.DefaultRaycastLayers, .35f, 2f);
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        (motor as IDisposable)?.Dispose();
        UnityEngine.Object.DestroyImmediate(actor);
        if (obstacle != null) UnityEngine.Object.DestroyImmediate(obstacle);
        Physics2D.simulationMode = previousSimulation;
    }

    private void Tick(EnemyReactionState state, HitReactionData data, int steps)
    {
        var method = motorType.GetMethod("Tick");
        for (int i = 0; i < steps; i++)
        {
            method.Invoke(motor, new object[] { state, data, 1f, .02f });
            Physics2D.Simulate(.02f);
        }
    }

    [Test]
    public void FreezeHoldsPositionAndDisposeRestoresBody()
    {
        Vector2 start = body.position;
        Tick(EnemyReactionState.ImpactFreeze, default, 10);
        Assert.That(Vector2.Distance(body.position, start), Is.LessThan(.001f));
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        ((IDisposable)motor).Dispose();
        motor = null;
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(body.gravityScale, Is.EqualTo(2f));
        Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation2D.None));
    }

    [Test]
    public void FlinchSpacingEasesAwayWithoutPullingOrExceedingTravelLimit()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Flinch, knockbackFrames = 12 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, 0f, false, 0f });
        motorType.GetMethod("EnsureMinimumSpacing").Invoke(motor, new object[] { -1f });
        Tick(EnemyReactionState.Recovering, data, 1);
        Assert.That(body.position.x, Is.EqualTo(start.x).Within(.002f));
        motorType.GetMethod("EnsureMinimumSpacing").Invoke(motor, new object[] { 10f });
        Tick(EnemyReactionState.Recovering, data, 1);
        Assert.That(body.position.x - start.x, Is.GreaterThan(0f).And.LessThan(.2f));
        Tick(EnemyReactionState.Recovering, data, 15);
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
    }

    [Test]
    public void SofterFallAccelerationSlowsDescentWithoutRelaunching()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Flinch, knockbackFrames = 12 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, 0f, false, 0f });
        motorType.GetMethod("SetFallAcceleration").Invoke(motor, new object[] { 12f });
        Tick(EnemyReactionState.Recovering, data, 1);
        float speed = (float)motorType.GetField("verticalSpeed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
        Assert.That(speed, Is.EqualTo(-.24f).Within(.001f));
    }

    [Test]
    public void LaunchIsBoundedAndHorizontalTravelDoesNotAccumulate()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, horizontalSpeed = 30, launchSpeed = 100, knockupFrames = 30 };
        float peak = start.y;
        for (int i = 0; i < 30; i++)
        {
            Tick(EnemyReactionState.Knockup, data, 1);
            peak = Mathf.Max(peak, body.position.y);
        }
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
        Assert.That(peak - start.y, Is.InRange(1.99f, 2.01f));
        Assert.That(body.position.y, Is.LessThan(peak));
    }

    [Test]
    public void KnockbackEasesAcrossItsDurationInsteadOfStoppingOnFirstTick()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockback, horizontalSpeed = 30, knockbackFrames = 24 };
        Vector2 start = body.position;
        Tick(EnemyReactionState.Knockback, data, 1);
        float first = body.position.x - start.x;
        Assert.That(first, Is.InRange(.02f, .05f));
        Tick(EnemyReactionState.Knockback, data, 1);
        float second = body.position.x - start.x - first;
        Assert.That(second, Is.GreaterThan(0f).And.LessThan(first));
        Tick(EnemyReactionState.Knockback, data, 18);
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
    }

    [Test]
    public void AirFollowupHoldsThenContinuesDescentWithoutRelaunching()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, launchSpeed = 10, knockupFrames = 18, canAirJuggle = true };
        Tick(EnemyReactionState.Knockup, data, 18);
        Vector2 hitPosition = body.position;
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, .12f, true, .06f });
        Tick(EnemyReactionState.ImpactFreeze, data, 4);
        Tick(EnemyReactionState.Knockup, data, 3);
        Assert.That(Vector2.Distance(body.position, hitPosition), Is.LessThan(.003f));
        Tick(EnemyReactionState.Knockup, data, 6);
        Assert.That(body.position.y, Is.LessThan(hitPosition.y - .01f));
    }

    [Test]
    public void HorizontalMotionStopsAtWall()
    {
        obstacle = new GameObject("ReactionWall");
        obstacle.transform.position = new Vector3(10000.7f, 10000, 0);
        var wall = obstacle.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(.1f, 10f);
        Physics2D.SyncTransforms();
        Tick(EnemyReactionState.Knockback, new HitReactionData { reactionKind = EnemyReactionKind.Knockback, horizontalSpeed = 20f }, 3);
        Assert.That(body.position.x, Is.GreaterThan(10000.05f));
        Assert.That(actor.GetComponent<Collider2D>().bounds.max.x,
            Is.LessThanOrEqualTo(wall.bounds.min.x + .005f));
    }

    [Test]
    public void DescentCannotPassThroughThinFloor()
    {
        obstacle = new GameObject("ThinFloor");
        obstacle.transform.position = new Vector3(10000, 9999, 0);
        var floor = obstacle.AddComponent<BoxCollider2D>();
        floor.size = new Vector2(10, .05f);
        Physics2D.SyncTransforms();
        Tick(EnemyReactionState.Knockfall, default, 100);
        Assert.That(actor.GetComponent<Collider2D>().bounds.min.y,
            Is.GreaterThanOrEqualTo(floor.bounds.max.y - .005f));
    }

    [Test]
    public void FlightPlaneReactionMovesHorizontallyWithoutFalling()
    {
        Vector2 start = body.position;
        motorType.GetMethod("SetVerticalMotionEnabled").Invoke(motor, new object[] { false });
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockback, horizontalSpeed = 5f, knockbackFrames = 12 };
        Tick(EnemyReactionState.Knockback, data, 12);
        Tick(EnemyReactionState.Recovering, data, 30);
        Assert.That(body.position.x, Is.GreaterThan(start.x));
        Assert.That(body.position.y, Is.EqualTo(start.y).Within(.002f));
    }
}

public class EnemyReactionAnimationTests
{
    private GameObject actor;
    private Animator animator;
    private AnimationHandler_Enemy handler;
    private const string Root = "Assets/Project/Character/enemy/enemy/";

    [SetUp]
    public void SetUp()
    {
        actor = new GameObject("ReactionAnimationTest");
        animator = actor.AddComponent<Animator>();
        animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            Root + "Enm101/animation/Enm101Animator.overrideController");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        handler = actor.AddComponent<AnimationHandler_Enemy>();
        typeof(AnimationHandler_Enemy).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(handler, null);
        handler.reactionAnimations.enabled = true;
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(actor);

    private int Pose => animator.GetCurrentAnimatorStateInfo(0).fullPathHash;

    private HitReactionData MainComboHit(string step, DamageImpactTier tier = DamageImpactTier.Low)
    {
        var main = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterObject>(
            "Assets/Project/Character/Hero/test_0_Main/CharacterObject_main.asset");
        Assert.That(main, Is.Not.Null);
        handler.reactionAnimations.comboAccentCharacter = main;
        return new HitReactionData
        {
            sourceCharacter = main,
            sourceCategory = AttackCategory.Normal,
            sourceStepData = new AttackStepData { stepName = step },
            reactionKind = EnemyReactionKind.Knockback,
            knockbackFrames = 24,
            knockbackDirection = Vector2.right,
            impactTier = tier
        };
    }

    [Test]
    public void EachLightHitRestartsOnlyTheVisualAccentAndItReturnsToNeutral()
    {
        var data = MainComboHit("Attack2", DamageImpactTier.Heavy);
        handler.reactionAnimations.heavyHurt = "Base Layer.Launch";
        Vector3 rootPosition = actor.transform.position;
        handler.BeginReactionAnimation(data, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.Hurt")), "Use the source step, not the promoted combo tier.");
        handler.SampleComboHitAccent(.06f, out var first, out _, out _);
        handler.SampleComboHitAccent(.06f, out var next, out _, out _);
        Assert.That(first.x, Is.EqualTo(handler.reactionAnimations.lightRecoil));
        Assert.That(next.x, Is.LessThan(first.x));
        handler.BeginReactionAnimation(data, false);
        handler.SampleComboHitAccent(1f, out var rehit, out _, out _);
        Assert.That(rehit.x, Is.EqualTo(first.x));
        handler.SampleComboHitAccent(0f, out var end, out var rotation, out var scale);
        Assert.That(end, Is.EqualTo(Vector3.zero));
        Assert.That(rotation, Is.EqualTo(Quaternion.identity));
        Assert.That(scale, Is.EqualTo(Vector3.one));
        Assert.That(actor.transform.position, Is.EqualTo(rootPosition));
        Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(actor.transform.rotation, Is.EqualTo(Quaternion.identity));
        Assert.That(data.knockbackFrames, Is.EqualTo(24));
    }

    [Test]
    public void MainFinisherImmediatelySamplesStrongerPoseEvenDuringEarlyRehit()
    {
        var light = MainComboHit("Attack1");
        handler.BeginReactionAnimation(light, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        var finisher = MainComboHit("Attack3", DamageImpactTier.Medium);
        handler.SetReactionState(EnemyReactionState.None, preserveVisual: true);
        handler.BeginReactionAnimation(finisher, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
            Is.EqualTo(handler.reactionAnimations.finisherPoseStart).Within(.001f));
        handler.SampleComboHitAccent(0f, out var offset, out _, out _);
        Assert.That(offset.x, Is.EqualTo(handler.reactionAnimations.finisherRecoil));
        Assert.That(offset.x, Is.GreaterThan(handler.reactionAnimations.lightRecoil));
        handler.SetReactionState(EnemyReactionState.None);
        handler.SampleComboHitAccent(0f, out offset, out _, out _);
        Assert.That(offset, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void AccentDoesNotApplyToOtherCharactersSkillsAirFollowupsOrGrabs()
    {
        var data = MainComboHit("Attack1");
        data.sourceCharacter = null;
        handler.BeginReactionAnimation(data, false);
        handler.SampleComboHitAccent(0f, out var offset, out _, out _);
        Assert.That(offset, Is.EqualTo(Vector3.zero));
        data = MainComboHit("Attack1");
        data.sourceCategory = AttackCategory.Skill;
        handler.BeginReactionAnimation(data, false);
        handler.SampleComboHitAccent(0f, out offset, out _, out _);
        Assert.That(offset, Is.EqualTo(Vector3.zero));
        data = MainComboHit("Attack1");
        handler.BeginReactionAnimation(data, true);
        handler.SampleComboHitAccent(0f, out offset, out _, out _);
        Assert.That(offset, Is.EqualTo(Vector3.zero));
        data.reactionKind = EnemyReactionKind.Grab;
        handler.BeginReactionAnimation(data, false);
        handler.SampleComboHitAccent(0f, out offset, out _, out _);
        Assert.That(offset, Is.EqualTo(Vector3.zero));
    }

    [TestCase("Enm101/Enemy1.prefab")]
    [TestCase("Enm102/Enemy102.prefab")]
    [TestCase("Enm103/Enemy103.prefab")]
    public void PrefabsUseTheirOwnHurtClipAndOptInProfile(string path)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Root + path);
        Assert.That(prefab, Is.Not.Null);
        var profile = prefab.GetComponent<AnimationHandler_Enemy>().reactionAnimations;
        Assert.That(profile.enabled, Is.True);
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorOverrideController;
        Assert.That(controller, Is.Not.Null);
        var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        var hurt = overrides.Find(pair => pair.Key.name == "Enemy_Hurt");
        Assert.That(hurt.Value, Is.Not.Null);
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(hurt.Value), Does.Contain(path.Split('/')[0]));
    }

    [Test]
    public void EarlyRehitAndGroundRecoveryContinueHurtInsteadOfRestartOrGetUp()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Flinch, knockbackFrames = 20 };
        handler.BeginReactionAnimation(data, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.Hurt")));
        animator.Update(.04f);
        float progress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        handler.SetReactionState(EnemyReactionState.None, preserveVisual: true);
        handler.BeginReactionAnimation(data, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(progress).Within(.001f));
        handler.SetReactionState(EnemyReactionState.Recovering);
        animator.Update(.01f);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.Hurt")));
        Assert.That(animator.speed, Is.EqualTo(1f));
    }

    [Test]
    public void AirFollowupUsesHurtInsteadOfReplayingLaunch()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, knockupFrames = 20 };
        handler.BeginReactionAnimation(data, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.Launch")));
        handler.SetReactionState(EnemyReactionState.None, preserveVisual: true);
        handler.BeginReactionAnimation(data, true);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        handler.SetReactionState(EnemyReactionState.Knockup);
        animator.Update(.01f);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.AirHurt")));
    }

    [Test]
    public void CompletionRestoresIdleAndDisablingProfileRestoresLegacyParameters()
    {
        handler.BeginReactionAnimation(new HitReactionData { reactionKind = EnemyReactionKind.Flinch, knockbackFrames = 20 }, false);
        handler.SetReactionState(EnemyReactionState.ImpactFreeze);
        handler.SetReactionState(EnemyReactionState.None);
        animator.Update(.1f);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.Idle")));
        Assert.That(animator.GetFloat("ReactionPlaybackSpeed"), Is.EqualTo(1f));
        handler.reactionAnimations.enabled = false;
        handler.SetReactionState(EnemyReactionState.Knockback);
        Assert.That(animator.GetBool("ManualReactionAnimation"), Is.False);
        animator.Update(.1f);
        animator.Update(.1f);
        Assert.That(Pose, Is.EqualTo(Animator.StringToHash("Base Layer.Hurt")));
    }
}
#endif
