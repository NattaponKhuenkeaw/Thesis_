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
    public void ShortGroundFlinchReachesSpacingWithoutShorteningHitstun()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Flinch, knockbackFrames = 24 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, 0f, false, 0f });
        motorType.GetMethod("ShortenHorizontalTravel").Invoke(motor, new object[] { .1f });
        motorType.GetMethod("EnsureMinimumSpacing").Invoke(motor, new object[] { .35f });
        Tick(EnemyReactionState.ImpactFreeze, data, 5);
        Assert.That(body.position.x, Is.EqualTo(start.x).Within(.002f));
        Tick(EnemyReactionState.Recovering, data, 1);
        Assert.That(body.position.x - start.x, Is.InRange(.17f, .18f));
        Tick(EnemyReactionState.Recovering, data, 4);
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
        Assert.That(data.ActiveReactionFrames, Is.EqualTo(24));
    }

    [Test]
    public void ShortGroundKnockbackPreservesAuthoredDistance()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockback, horizontalSpeed = .5f, knockbackFrames = 24 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, .35f, false, 0f });
        motorType.GetMethod("ShortenHorizontalTravel").Invoke(motor, new object[] { .1f });
        Tick(EnemyReactionState.Knockback, data, 5);
        Assert.That(body.position.x - start.x, Is.InRange(.19f, .21f));
    }

    [Test]
    public void GroundRecoilDurationCannotExtendShortAuthoredMotion()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockback, horizontalSpeed = 30f, knockbackFrames = 3 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, .35f, false, 0f });
        motorType.GetMethod("ShortenHorizontalTravel").Invoke(motor, new object[] { .1f });
        Tick(EnemyReactionState.Knockback, data, 3);
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
    }

    [Test]
    public void GroundImpactRecoilStartsStrongThenDecelerates()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockback, horizontalSpeed = 30f, knockbackFrames = 24 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, .35f, false, 0f });
        motorType.GetMethod("ShortenHorizontalTravel").Invoke(motor, new object[] { .1f });
        float previousPosition = start.x;
        float previousStep = float.PositiveInfinity;
        for (int i = 0; i < 5; i++)
        {
            Tick(EnemyReactionState.Knockback, data, 1);
            float step = body.position.x - previousPosition;
            Assert.That(step, Is.GreaterThan(0f).And.LessThan(previousStep));
            previousStep = step;
            previousPosition = body.position.x;
        }
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
    }

    [Test]
    public void LaunchAfterGroundRecoilUsesOriginalLaunchMotion()
    {
        var groundHit = new HitReactionData { reactionKind = EnemyReactionKind.Knockback, knockbackFrames = 24 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { groundHit, .35f, false, 0f });
        motorType.GetMethod("ShortenHorizontalTravel").Invoke(motor, new object[] { .1f });
        var launch = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, horizontalSpeed = 30f, launchSpeed = 100f, knockupFrames = 30 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { launch, .35f, false, 0f });
        Vector2 start = body.position;
        Tick(EnemyReactionState.Knockup, launch, 1);
        Assert.That(body.position.x - start.x, Is.InRange(.025f, .03f));
        Tick(EnemyReactionState.Knockup, launch, 24);
        Assert.That(body.position.y - start.y, Is.InRange(1.99f, 2.01f));
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
    public void LaunchSlowsTowardApexWithoutChangingHeightOrFallAcceleration()
    {
        Vector2 start = body.position;
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, horizontalSpeed = 7.5f, launchSpeed = 8f, knockupFrames = 12 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, .35f, false, 0f });
        motorType.GetMethod("SetFallAcceleration").Invoke(motor, new object[] { 12f });
        motorType.GetMethod("ConfigureLaunchArc").Invoke(motor, new object[] { .48f });
        motorType.GetMethod("ConfigureLaunchRecoil").Invoke(motor, new object[] { .3f });
        Tick(EnemyReactionState.Knockup, data, 1);
        Assert.That(body.position.y - start.y, Is.InRange(.06f, .07f));
        Assert.That(body.position.x - start.x, Is.InRange(.04f, .05f));
        float peak = body.position.y;
        float previousRise = body.position.y - start.y;
        for (int i = 1; i < 24; i++)
        {
            float previousY = body.position.y;
            Tick(EnemyReactionState.Knockup, data, 1);
            float rise = body.position.y - previousY;
            Assert.That(rise, Is.GreaterThanOrEqualTo(0f).And.LessThan(previousRise));
            previousRise = rise;
            peak = Mathf.Max(peak, body.position.y);
            if (i == 9)
                Assert.That(body.position.x - start.x, Is.InRange(.30f, .32f));
        }
        Assert.That(peak - start.y, Is.InRange(.78f, .82f));
        Assert.That(previousRise, Is.LessThan(.006f));
        Tick(EnemyReactionState.Knockfall, data, 1);
        Assert.That(body.position.y, Is.LessThan(peak));
        Assert.That(peak - body.position.y, Is.LessThan(.005f));
        Assert.That(body.position.x - start.x, Is.InRange(.34f, .36f));
        float fall = (float)motorType.GetField("fallAcceleration", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
        Assert.That(fall, Is.EqualTo(12f));
    }

    [Test]
    public void ConfigureLaunchDoesNotRelaunchDescendingEnemyOrFlyingEnemy()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, launchSpeed = 8f, knockupFrames = 12 };
        Tick(EnemyReactionState.Knockup, data, 15);
        var speedField = motorType.GetField("verticalSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        float descendingSpeed = (float)speedField.GetValue(motor);
        Assert.That(descendingSpeed, Is.LessThan(0f));
        motorType.GetMethod("ConfigureLaunchArc").Invoke(motor, new object[] { .3f });
        Assert.That((float)speedField.GetValue(motor), Is.EqualTo(descendingSpeed));
        motorType.GetMethod("SetVerticalMotionEnabled").Invoke(motor, new object[] { false });
        motorType.GetMethod("ConfigureLaunchArc").Invoke(motor, new object[] { .3f });
        Vector2 start = body.position;
        Tick(EnemyReactionState.Knockup, data, 5);
        Assert.That(body.position.y, Is.EqualTo(start.y).Within(.002f));
    }

    [Test]
    public void StepAcrossApexStartsDescentFromZeroVelocity()
    {
        var data = new HitReactionData { reactionKind = EnemyReactionKind.Knockup, launchSpeed = 8f, knockupFrames = 11 };
        motorType.GetMethod("BeginHit").Invoke(motor, new object[] { data, 0f, false, 0f });
        motorType.GetMethod("SetFallAcceleration").Invoke(motor, new object[] { 12f });
        Tick(EnemyReactionState.Knockup, data, 9);
        Assert.That((bool)motorType.GetProperty("IsRising").GetValue(motor), Is.True);
        Tick(EnemyReactionState.Knockup, data, 1);
        float speed = (float)motorType.GetField("verticalSpeed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
        Assert.That(speed, Is.EqualTo(-.2f).Within(.002f));
        Assert.That((bool)motorType.GetProperty("IsRising").GetValue(motor), Is.False);
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
public class PlayerComboReactionAuthoringTests
{
    [TestCase("enemy/EnemyCore/EnemyCore")]
    [TestCase("enemy/EnemyCore/Enemy2")]
    [TestCase("enemy/EnemyCore/Enemy3")]
    [TestCase("enemy/EnemyCore/Enemy_Dummy")]
    [TestCase("enemy/Enm101/Enemy1")]
    [TestCase("enemy/Enm102/Enemy102")]
    [TestCase("enemy/Enm103/Enemy103")]
    [TestCase("Boss/Executioner/Boss_Executioner")]
    public void EveryEnemyPrefabUsesPositionReactions(string path)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Project/Character/enemy/{path}.prefab");
        Assert.That(prefab, Is.Not.Null);
        var reaction = prefab.GetComponent<EnemyHitReactionHandler>();
        Assert.That(reaction, Is.Not.Null);
        Assert.That(reaction.UsesPositionReactions, Is.True);
    }

    [Test]
    public void DummyUsesPositionMotionAndManualReactionAnimationWithShortRehitPause()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Project/Character/enemy/enemy/EnemyCore/Enemy_Dummy.prefab");
        Assert.That(prefab, Is.Not.Null);
        var handler = prefab.GetComponent<EnemyHitReactionHandler>();
        Assert.That(handler, Is.Not.Null);
        var serialized = new UnityEditor.SerializedObject(handler);
        Assert.That(serialized.FindProperty("usePositionReactions").boolValue, Is.True);
        Assert.That(serialized.FindProperty("minimumLightImpactFreeze").intValue, Is.EqualTo(1));
        Assert.That(serialized.FindProperty("minimumStrongImpactFreeze").intValue, Is.EqualTo(2));
        Assert.That(serialized.FindProperty("airFollowupHold").floatValue, Is.EqualTo(.03f).Within(.001f));
        var animation = prefab.GetComponent<AnimationHandler_Enemy>();
        Assert.That(animation, Is.Not.Null);
        Assert.That(animation.reactionAnimations.enabled, Is.True);
        var animator = prefab.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(GroundNode("main", 2).stepData.hitData.hitboxes[0], Is.TypeOf<NormalHitbox>());
        Assert.That(((NormalHitbox)GroundNode("main", 2).stepData.hitData.hitboxes[0]).impactFreezeFrames, Is.EqualTo(2));
    }

    private static ComboNode GroundNode(string character, int index)
    {
        string folder = character == "main" ? "test_0_Main" : "test_1_CameraGirl";
        string suffix = character == "main" ? "main" : "cg";
        var table = UnityEditor.AssetDatabase.LoadAssetAtPath<ActionResolverTable>(
            $"Assets/Project/Character/Hero/{folder}/ActionResolverTable_{suffix}.asset");
        Assert.That(table, Is.Not.Null);
        var node = table.comboActions[0].comboTree.rootNode;
        for (int i = 0; i < index; i++) node = node.defaultNext;
        Assert.That(node, Is.Not.Null);
        return node;
    }

    [TestCase("main")]
    [TestCase("camera")]
    public void OpenerFlinchesAndMiddleHitKeepsTargetsClose(string character)
    {
        var opener = (NormalHitbox)GroundNode(character, 0).stepData.hitData.hitboxes[0];
        Assert.That(opener.reactionKind, Is.EqualTo(EnemyReactionKind.Flinch));
        Assert.That(opener.horizontalSpeed, Is.Zero);
        foreach (var hitbox in GroundNode(character, 1).stepData.hitData.hitboxes)
        {
            var hit = (NormalHitbox)hitbox;
            Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
            Assert.That(hit.horizontalSpeed, Is.EqualTo(character == "main" ? 7f : 2f));
        }
    }

    [TestCase("main")]
    [TestCase("camera")]
    public void ThirdHitAuthorsLowLaunchWithRecoverLanding(string character)
    {
        var hit = (NormalHitbox)GroundNode(character, 2).stepData.hitData.hitboxes[0];
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockup));
        Assert.That(hit.launchSpeed, Is.EqualTo(8f));
        Assert.That(hit.knockupFrames, Is.EqualTo(character == "main" ? 12 : 14));
        Assert.That(hit.knockfallFrames, Is.GreaterThan(0));
        Assert.That(hit.horizontalSpeed, Is.EqualTo(character == "main" ? 7.5f : 1.5f));
        Assert.That(hit.landingBehavior, Is.EqualTo(EnemyLandingBehavior.Recover));
        Assert.That(hit.canAirJuggle, Is.True);
    }

    [Test]
    public void CameraRushFlinchesUntilItsSinglePushFinisher()
    {
        var hits = GroundNode("camera", 3).stepData.hitData.hitboxes;
        Assert.That(hits.Count, Is.EqualTo(6));
        for (int i = 0; i < hits.Count - 1; i++)
            Assert.That(((NormalHitbox)hits[i]).reactionKind, Is.EqualTo(EnemyReactionKind.Flinch));
        var finisher = (NormalHitbox)hits[hits.Count - 1];
        Assert.That(finisher.reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
        Assert.That(finisher.horizontalSpeed, Is.EqualTo(4f));
    }

    [TestCase(0, 20)]
    [TestCase(1, 21)]
    [TestCase(2, 27)]
    public void MainFollowupWindowIsEarlierButPreservesActiveHitboxes(int index, int expectedFrame)
    {
        var step = GroundNode("main", index).stepData;
        Assert.That(step.cancelWindowStartFrame, Is.EqualTo(expectedFrame));
        Assert.That(step.cancelWindowStartFrame, Is.GreaterThanOrEqualTo(step.startupFrames + step.activeFrames));
        foreach (var hitbox in step.hitData.hitboxes)
            Assert.That(step.startupFrames + hitbox.startFrameOffset + hitbox.activeFrames,
                Is.LessThanOrEqualTo(step.cancelWindowStartFrame));
    }
}
#endif
