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
        completions = 0;
        handler.OnReactionComplete += () => completions++;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(attacker);
    }

    [TestCase(EnemyReactionKind.Flinch, 3)]
    [TestCase(EnemyReactionKind.Knockback, 6)]
    public void GroundReactionRecoversFromAuthoredFrames(EnemyReactionKind kind, int recoveryStart)
    {
        handler.StartReaction(Hit(kind, DamageImpactTier.Medium), Vector2.left);
        const int completeFrame = 15;
        for (int frame = 1; frame <= completeFrame; frame++)
        {
            Step(frame);
            EnemyReactionState expected = frame <= 2 ? EnemyReactionState.ImpactFreeze
                : frame < recoveryStart ? EnemyReactionState.Knockback
                : frame == completeFrame ? EnemyReactionState.None
                : EnemyReactionState.Recovering;
            Assert.That(handler.CurrentState, Is.EqualTo(expected), $"Frame {frame}");
            if (expected == EnemyReactionState.Knockback)
                Assert.That(body.linearVelocity.x, Is.EqualTo(4f));
        }
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void HeavyGroundHitDoesNotForceKnockdown()
    {
        handler.StartReaction(Hit(EnemyReactionKind.Flinch, DamageImpactTier.Heavy), Vector2.left);
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Recovering));
        Assert.That(Get<HitReactionData>("currentReaction").landingBehavior, Is.EqualTo(EnemyLandingBehavior.Recover));
    }

    [Test]
    public void AuthoredRecoveryFramesControlGroundRecovery()
    {
        var hit = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Medium);
        hit.reactionTiming.recoveryFrames = 100;
        handler.StartReaction(hit, Vector2.left);
        Step(3);
        Step(8);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Recovering));
        Step(105);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Recovering));
        Step(106);
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

        var low = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        low.reactionOwnerId = 11;
        handler.StartReaction(low, Vector2.left);
        low.reactionOwnerId = 22;
        handler.StartReaction(low, Vector2.left);
        Assert.That(handler.CurrentReactionOwnerId, Is.EqualTo(11));
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Grabbed));
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

    [Test]
    public void DisabledAirJuggleZerosLaunchWithoutIncrementingCount()
    {
        var hit = Hit(EnemyReactionKind.Knockup, DamageImpactTier.Medium);
        hit.launchSpeed = 8f;
        hit.canAirJuggle = true;
        handler.StartReaction(hit, Vector2.left);
        Step(3);

        hit.canAirJuggle = false;
        handler.StartReaction(hit, Vector2.left);
        var resolved = Get<HitReactionData>("currentReaction");
        Assert.That(Get<int>("currentJuggleCount"), Is.Zero);
        Assert.That(resolved.launchSpeed, Is.Zero);
        Assert.That(resolved.horizontalSpeed, Is.EqualTo(2f));
        Assert.That(resolved.knockupFrames, Is.Zero);
        Assert.That(resolved.landingBehavior, Is.EqualTo(EnemyLandingBehavior.Knockdown));
    }

    [Test]
    public void AirborneRecoverLandingDoesNotForceKnockdown()
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

            var hit = Hit(EnemyReactionKind.Knockup, DamageImpactTier.Low);
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
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Recovering));
            Step(15);
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Recovering));
            Step(16);
            Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.None));
            Assert.That(completions, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(floor);
        }
    }

    [Test]
    public void ForceClearEndsReactionWithoutCompletion()
    {
        handler.StartReaction(Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low), Vector2.left);
        handler.ForceClearReaction();
        Assert.That(handler.IsInReaction, Is.False);
        Assert.That(completions, Is.Zero);
    }

    [Test]
    public void KnockdownContinuationPreservesGrabAndRewritesOtherHitsToKnockup()
    {
        Set("currentState", EnemyReactionState.Knockdown);
        var grab = Hit(EnemyReactionKind.Grab, DamageImpactTier.Low);
        grab.grabSocket = attacker.transform;
        ReactionResolver.ResolveAgainstTarget(enemy, ref grab);
        Assert.That(grab.reactionKind, Is.EqualTo(EnemyReactionKind.Grab));
        Assert.That(grab.grabSocket, Is.EqualTo(attacker.transform));

        Set("currentState", EnemyReactionState.Knockfall);
        var followUp = Hit(EnemyReactionKind.Flinch, DamageImpactTier.Low);
        ReactionResolver.ResolveAgainstTarget(enemy, ref followUp);
        Assert.That(followUp.reactionKind, Is.EqualTo(EnemyReactionKind.Knockup));
    }

    [Test]
    public void DirectEntryDoesNotReapplyUpstreamTargetRules()
    {
        Set("currentState", EnemyReactionState.Knockdown);
        handler.StartReaction(Hit(EnemyReactionKind.Knockback, DamageImpactTier.Medium), Vector2.left);
        Assert.That(Get<HitReactionData>("currentReaction").reactionKind, Is.EqualTo(EnemyReactionKind.Knockback));
        Step(3);
        Assert.That(handler.CurrentState, Is.EqualTo(EnemyReactionState.Knockback));
    }

    [Test]
    public void StartReactionInfersLegacyKindTimingOwnerAndScalingDefaults()
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
        handler.StartReaction(hit, Vector2.left);
        HitReactionData resolved = Get<HitReactionData>("currentReaction");
        Assert.That(resolved.reactionKind, Is.EqualTo(EnemyReactionKind.Knockup));
        Assert.That(resolved.ImpactFreezeFrames, Is.EqualTo(5));
        Assert.That(resolved.ActiveReactionFrames, Is.EqualTo(8));
        Assert.That(resolved.RecoveryFrames, Is.EqualTo(9));
        Assert.That(resolved.reactionOwnerId, Is.EqualTo(attacker.transform.root.GetInstanceID()));
        Assert.That(resolved.juggleLaunchScale, Is.EqualTo(1f));
        Assert.That(resolved.juggleStunScale, Is.EqualTo(1f));
    }

    [Test]
    public void BehaviorActionWaitsUntilAuthoredRecoveryCompletes()
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
        Step(14);
        Assert.That(type.GetMethod("OnUpdate", Private).Invoke(action, null), Is.EqualTo(Node.Status.Running));
        Step(15);
        Assert.That(type.GetMethod("OnUpdate", Private).Invoke(action, null), Is.EqualTo(Node.Status.Success));
        Assert.That(action.IsTakingDamage.Value, Is.False);
        Assert.That(completions, Is.EqualTo(1));
        type.GetMethod("OnEnd", Private).Invoke(action, null);
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
