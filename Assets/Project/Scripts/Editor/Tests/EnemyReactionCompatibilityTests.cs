#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using Unity.Behavior;
using UnityEngine;
using Object = UnityEngine.Object;

public class EnemyReactionCompatibilityTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameObject attacker;
    private EnemyHitReactionHandler handler;
    private EnemyController enemy;
    private Rigidbody2D body;
    private int completions;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("ReactionCompatibility");
        attacker = new GameObject("ReactionAttacker");
        body = host.AddComponent<Rigidbody2D>();
        enemy = EnemyLifecycleTestUtility.AddActive(host);
        handler = host.AddComponent<EnemyHitReactionHandler>();
        Invoke("Awake");
        enemy.hitReactionHandler = handler;
        Set("knockdownHoldFrames", 2);
        Set("getUpFrames", 3);
        completions = 0;
        handler.OnReactionComplete += () => completions++;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(attacker);
    }

    [TestCase(EnemyReactionKind.Flinch, DamageImpactTier.Low, 3)]
    [TestCase(EnemyReactionKind.Flinch, DamageImpactTier.Medium, 9)]
    [TestCase(EnemyReactionKind.Flinch, DamageImpactTier.Heavy, 9)]
    [TestCase(EnemyReactionKind.Flinch, DamageImpactTier.Ultimate, 9)]
    [TestCase(EnemyReactionKind.Knockback, DamageImpactTier.Low, 6)]
    [TestCase(EnemyReactionKind.Knockback, DamageImpactTier.Medium, 12)]
    public void GroundReactionKeepsBaselineFrameTrace(EnemyReactionKind kind, DamageImpactTier tier, int completeFrame)
    {
        handler.StartReaction(Hit(kind, tier), Vector2.left);
        int recoveryStart = kind == EnemyReactionKind.Flinch ? 3 : 6;
        for (int frame = 1; frame <= completeFrame; frame++)
        {
            Step(frame);
            EnemyReactionState expected = frame <= 2 ? EnemyReactionState.ImpactFreeze
                : frame < recoveryStart ? EnemyReactionState.Knockback
                : frame == completeFrame ? EnemyReactionState.None
                : frame <= recoveryStart + 2 ? EnemyReactionState.Knockdown
                : EnemyReactionState.Recovering;
            Assert.That(handler.CurrentState, Is.EqualTo(expected), $"Frame {frame}");
            if (expected == EnemyReactionState.Knockback)
                Assert.That(body.linearVelocity.x, Is.EqualTo(4f));
        }
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void SecondLowHitKnocksDownButExpiredStreakDoesNot()
    {
        var hit = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(handler.IsInReaction, Is.False);
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));
        Step(6);
        Step(9);
        Assert.That(handler.IsInReaction, Is.False);
        Assert.That(Get<int>("consecutiveLightHits"), Is.Zero);

        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Set("lastLightHitTime", Time.time - 1f);
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(handler.IsInReaction, Is.False);
    }

    [Test]
    public void RecoveryUsesTextTierNotPromotedShakeTier()
    {
        var hit = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        hit.impactTier = DamageImpactTier.Ultimate;
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(handler.IsInReaction, Is.False);
    }

    [Test]
    public void AuthoredRecoveryFramesDoNotReplaceExistingHoldAndGetUpTiming()
    {
        var hit = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Medium);
        hit.reactionTiming.recoveryFrames = 100;
        Set("knockdownHoldFrames", 4);
        Set("getUpFrames", 5);
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Step(7);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));
        Step(8);
        Step(12);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Recovering));
        Step(13);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.None));
    }

    [Test]
    public void RegrabAbsorptionAndForeignOwnerKeepOriginalSession()
    {
        var hit = Hit(EnemyReactionKind.Grab, DamageImpactTier.Low);
        hit.reactionOwnerId = 11;
        hit.grabSocket = attacker.transform;
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Grabbed));
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
        handler.StartReaction(hit, Vector2.left);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Grabbed));
        Assert.That(Get<int>("consecutiveLightHits"), Is.Zero);

        var low = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        low.reactionOwnerId = 11;
        handler.StartReaction(low, Vector2.left);
        low.reactionOwnerId = 22;
        handler.StartReaction(low, Vector2.left);
        Assert.That(handler.CurrentReactionOwnerId, Is.EqualTo(11));
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Grabbed));
        Assert.That(Get<int>("consecutiveLightHits"), Is.Zero);
        Assert.That(handler.ReleaseGrab(22), Is.False);
        Assert.That(handler.ReleaseGrab(11), Is.True);
        Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
        Assert.That(completions, Is.Zero);
    }

    [Test]
    public void JuggleKeepsVelocityFrameScalingAndCount()
    {
        var hit = Hit(EnemyReactionKind.Knockup, DamageImpactTier.Medium);
        hit.launchSpeed = 8f;
        hit.knockupFrames = 4;
        hit.knockfallFrames = 6;
        hit.canAirJuggle = true;
        hit.juggleLaunchScale = 0.5f;
        hit.juggleStunScale = 0.5f;
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(body.linearVelocity.y, Is.EqualTo(8f));
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        handler.StartReaction(hit, Vector2.left);
        var resolved = Get<HitReactionData>("currentReaction");
        Assert.That(resolved.launchSpeed, Is.EqualTo(4f));
        Assert.That(resolved.horizontalSpeed, Is.EqualTo(3f));
        Assert.That(resolved.knockupFrames, Is.EqualTo(2));
        Assert.That(resolved.knockfallFrames, Is.EqualTo(3));
        Assert.That(Get<int>("currentJuggleCount"), Is.EqualTo(2));
    }

    [TestCase(DamageImpactTier.Low, 9)]
    [TestCase(DamageImpactTier.Medium, 14)]
    public void AirborneReactionKeepsTouchdownFrameBeforeRecovery(DamageImpactTier tier, int completeFrame)
    {
        var floor = new GameObject("ReactionTestFloor");
        try
        {
            floor.transform.position = new Vector3(0f, -1.02f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            var collider = host.AddComponent<BoxCollider2D>();
            enemy.bodyCollider = collider;
            Set("bodyCollider", collider);
            Physics2D.SyncTransforms();

            var hit = Hit(EnemyReactionKind.Knockup, tier);
            hit.launchSpeed = 8f;
            handler.StartReaction(hit, Vector2.left);
            Step(3);
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockup));
            Step(5);
            Step(7);
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockfall));
            Assert.That(completions, Is.Zero, "Must wait for descending ground contact.");

            body.linearVelocity = Vector2.down;
            Step(8);
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));
            Assert.That(completions, Is.Zero, "Even the first Low keeps its original touchdown frame.");
            for (int frame = 9; frame <= completeFrame; frame++)
            {
                Step(frame);
                EnemyReactionState expected = frame == completeFrame ? EnemyReactionState.None
                    : frame <= 10 ? EnemyReactionState.Knockdown : EnemyReactionState.Recovering;
                Assert.That(handler.CurrentState, Is.EqualTo(expected), $"Frame {frame}");
            }
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(Get<int>("consecutiveLightHits"), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void ForceClearDoesNotCompleteOrEraseAcceptedLowHitHistory()
    {
        var hit = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        handler.StartReaction(hit, Vector2.left);
        handler.ForceClearReaction();
        Assert.That(handler.IsInReaction, Is.False);
        Assert.That(completions, Is.Zero);
        Assert.That(Get<int>("consecutiveLightHits"), Is.EqualTo(1));
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));
    }

    [TestCase(0.84f, EnemyLandingBehavior.Knockdown, 2)]
    [TestCase(0.85f, EnemyLandingBehavior.Knockdown, 2)]
    [TestCase(0.86f, EnemyLandingBehavior.Recover, 1)]
    public void ResolverPreservesInclusiveLowHitWindow(float now, EnemyLandingBehavior expected, int expectedCount)
    {
        var hit = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        int count = 1;
        float lastTime = 0f;
        ReactionResolver.ResolveAcceptedHitRecovery(ref hit, now, 0.85f, ref count, ref lastTime);
        Assert.That(hit.landingBehavior, Is.EqualTo(expected));
        Assert.That(count, Is.EqualTo(expectedCount));
        Assert.That(lastTime, Is.EqualTo(now));
    }

    [TestCase(EnemyReactionKind.Flinch, DamageImpactTier.Low, EnemyLandingBehavior.Knockdown, EnemyLandingBehavior.Recover)]
    [TestCase(EnemyReactionKind.Flinch, DamageImpactTier.Medium, EnemyLandingBehavior.Recover, EnemyLandingBehavior.Knockdown)]
    [TestCase(EnemyReactionKind.Grab, DamageImpactTier.Low, EnemyLandingBehavior.Recover, EnemyLandingBehavior.Knockdown)]
    public void FinalRecoveryPreservesLegacyPrecedenceOverAuthoredLanding(
        EnemyReactionKind kind, DamageImpactTier tier, EnemyLandingBehavior authored, EnemyLandingBehavior expected)
    {
        var hit = Hit(kind, tier);
        hit.landingBehavior = authored;
        int count = 0;
        float lastTime = 0f;
        ReactionResolver.ResolveAcceptedHitRecovery(ref hit, 1f, 0.85f, ref count, ref lastTime);
        Assert.That(hit.landingBehavior, Is.EqualTo(expected));
        Assert.That(count, Is.EqualTo(expected == EnemyLandingBehavior.Recover ? 1 : 0));
    }

    [Test]
    public void DisabledAirJuggleKeepsExistingZeroLaunchTimingAndDoesNotIncrementCount()
    {
        var hit = Hit(EnemyReactionKind.Knockup, DamageImpactTier.Medium);
        hit.launchSpeed = 8f;
        hit.canAirJuggle = false;
        int count = 2;
        ReactionResolver.ApplyAirJuggle(ref hit, ref count);
        Assert.That(count, Is.EqualTo(2));
        Assert.That(hit.launchSpeed, Is.Zero);
        Assert.That(hit.horizontalSpeed, Is.EqualTo(2f));
        Assert.That(hit.knockupFrames, Is.Zero);
        Assert.That(hit.knockfallFrames, Is.EqualTo(2));
        Assert.That(hit.ImpactFreezeFrames, Is.EqualTo(2));
        Assert.That(hit.ActiveReactionFrames, Is.Zero);
        Assert.That(hit.RecoveryFrames, Is.EqualTo(9));
    }

    [Test]
    public void GrabIsNotConvertedByAirborneOrKnockdownContinuationRules()
    {
        handler.StartReaction(Hit(EnemyReactionKind.Knockup, DamageImpactTier.Medium), Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockup));
        AssertGrabUnchanged();

        handler.StartReaction(Hit(EnemyReactionKind.Flinch, DamageImpactTier.Medium), Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));
        AssertGrabUnchanged();

        var floor = new GameObject("GrabRuleFloor");
        try
        {
            floor.transform.position = new Vector3(0f, -1.02f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            var collider = host.GetComponent<BoxCollider2D>() ?? host.AddComponent<BoxCollider2D>();
            enemy.bodyCollider = collider;
            Set("bodyCollider", collider);
            Physics2D.SyncTransforms();

            var hit = Hit(EnemyReactionKind.Knockup, DamageImpactTier.Medium);
            hit.launchSpeed = 8f;
            handler.StartReaction(hit, Vector2.left);
            Step(3);
            Step(5);
            Step(7);
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockfall));
            AssertGrabUnchanged();
        }
        finally
        {
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void NonGrabHitsStillConvertOnKnockdownContinuation()
    {
        handler.StartReaction(Hit(EnemyReactionKind.Flinch, DamageImpactTier.Medium), Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));

        var followUp = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        ReactionResolver.ResolveAgainstTarget(enemy, ref followUp);
        Assert.That(followUp.reactionKind, Is.EqualTo(EnemyReactionKind.Knockup));
    }

    [Test]
    public void DirectEntryDoesNotReapplyUpstreamTargetRules()
    {
        handler.StartReaction(Hit(EnemyReactionKind.Flinch, DamageImpactTier.Medium), Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockdown));

        handler.StartReaction(Hit(EnemyReactionKind.Knockback, DamageImpactTier.Medium), Vector2.left);
        Assert.That(Get<HitReactionData>("currentReaction").reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockback));
    }

    [Test]
    public void LegacyNormalizationKeepsTimingOwnerInferenceAndScalingDefaults()
    {
        var hit = new HitReactionData
        {
            reactionKind = EnemyReactionKind.Flinch,
            reactionVelocity = new Vector2(2f, 3f),
            hitstunFrames = 5,
            knockbackFrames = 8,
            attackerTransform = attacker.transform,
            juggleLaunchScale = 0f,
            juggleStunScale = 2f
        };
        ReactionResolver.NormalizeForExecution(ref hit);
        Assert.That(hit.reactionKind, Is.EqualTo(EnemyReactionKind.Knockup));
        Assert.That(hit.ImpactFreezeFrames, Is.EqualTo(5));
        Assert.That(hit.ActiveReactionFrames, Is.EqualTo(8));
        Assert.That(hit.RecoveryFrames, Is.EqualTo(9));
        Assert.That(hit.reactionOwnerId, Is.EqualTo(attacker.transform.root.GetInstanceID()));
        Assert.That(hit.juggleLaunchScale, Is.EqualTo(1f));
        Assert.That(hit.juggleStunScale, Is.EqualTo(1f));
    }

    [Test]
    public void BehaviorActionWaitsUntilExactRecoveryCompletion()
    {
        handler.StartReaction(Hit(EnemyReactionKind.Flinch, DamageImpactTier.Medium), Vector2.left);
        var action = new TakeDamageReactionAction
        {
            Agent = new BlackboardVariable<GameObject>(host),
            IsTakingDamage = new BlackboardVariable<bool>(true)
        };
        var type = typeof(TakeDamageReactionAction);
        Assert.That(type.GetMethod("OnStart", Private).Invoke(action, null), Is.EqualTo(Node.Status.Running));
        Step(3);
        Step(6);
        Step(8);
        Assert.That(type.GetMethod("OnUpdate", Private).Invoke(action, null), Is.EqualTo(Node.Status.Running));
        Step(9);
        Assert.That(type.GetMethod("OnUpdate", Private).Invoke(action, null), Is.EqualTo(Node.Status.Success));
        Assert.That(action.IsTakingDamage.Value, Is.False);
        Assert.That(completions, Is.EqualTo(1));
        type.GetMethod("OnEnd", Private).Invoke(action, null);
    }

    private void AssertGrabUnchanged()
    {
        var grab = Hit(EnemyReactionKind.Grab, DamageImpactTier.Low);
        grab.grabSocket = attacker.transform;
        ReactionResolver.ResolveAgainstTarget(enemy, ref grab);
        Assert.That(grab.reactionKind, Is.EqualTo(EnemyReactionKind.Grab));
    }

    private static HitReactionData Hit(EnemyReactionKind kind, DamageImpactTier tier) => new HitReactionData
    {
        reactionKind = kind,
        textImpactTier = tier,
        reactionTiming = HitReactionData.CreateTiming(2, 3, 9),
        horizontalSpeed = kind == EnemyReactionKind.Flinch ? 0f : 4f,
        knockupFrames = 2,
        knockfallFrames = 2,
        landingBehavior = EnemyLandingBehavior.Recover
    };

    private void Step(int frame)
    {
        Set("currentReactionFrame", frame);
        Invoke("ProcessReactionFrame");
    }
    private void Invoke(string method) => typeof(EnemyHitReactionHandler).GetMethod(method, Private).Invoke(handler, null);
    private void Set(string name, object value) => typeof(EnemyHitReactionHandler).GetField(name, Private).SetValue(handler, value);
    private T Get<T>(string name) => (T)typeof(EnemyHitReactionHandler).GetField(name, Private).GetValue(handler);
}
#endif
