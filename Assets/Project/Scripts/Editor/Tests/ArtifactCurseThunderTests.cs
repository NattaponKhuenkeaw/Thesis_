#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ArtifactCurseThunderTests
{
    private GameObject _host;
    private GameObject _actor;
    private GameObject _floor;

    [TearDown]
    public void TearDown()
    {
        if (_host != null) Object.DestroyImmediate(_host);
        if (_actor != null) Object.DestroyImmediate(_actor);
        if (_floor != null) Object.DestroyImmediate(_floor);
    }

    [Test]
    public void GroundAnchorUsesNearestSupportAndIgnoresTriggers()
    {
        _floor = new GameObject("GroundFixture");
        for (int i = 0; i < 3; i++)
        {
            GameObject support = new GameObject("Support" + i);
            support.transform.SetParent(_floor.transform);
            support.layer = LayerMask.NameToLayer("Ground");
            support.transform.position = new Vector3(5000f, i * 1.5f - 0.5f, 0f);
            BoxCollider2D collider = support.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(4f, 1f);
            collider.isTrigger = i == 2;
        }
        Physics2D.SyncTransforms();
        ParticleVolumeDefinition volume = new ParticleVolumeDefinition { anchorToGround = true };
        Assert.That(ArtifactAreaGeometry.Center(new Vector2(5000f, 5f), volume), Is.EqualTo(new Vector2(5000f, 1.5f)));
        volume.anchorToGround = false;
        Assert.That(ArtifactAreaGeometry.Center(new Vector2(5000f, 5f), volume), Is.EqualTo(new Vector2(5000f, 5f)));
    }

    [Test]
    public void GroundAnchorWithoutSupportKeepsTheAuthoredCenter()
    {
        ParticleVolumeDefinition volume = new ParticleVolumeDefinition
            { anchorToGround = true, centerOffset = new Vector2(0f, 0.4f) };
        Vector2 anchor = new Vector2(7000f, 7000f);
        Assert.That(ArtifactAreaGeometry.Center(anchor, volume), Is.EqualTo(anchor + volume.centerOffset));
    }

    [Test]
    public void ThunderMatchesSavedCircleAndCompletesItsTailUnderScaledParent()
    {
        ParticleBehaviorDefinition definition = Resources.Load<ParticleBehaviorDefinition>("ItemSO/Artifacts/Phase1/P1_A09_ThunderStrike");
        _host = Object.Instantiate(definition.runtimePrefab);
        ArtifactAreaVisual view = _host.GetComponent<ArtifactAreaVisual>();
        _actor = new GameObject("FlippedScaledParent");
        _actor.transform.localScale = new Vector3(-2f, 3f, 1f);
        _host.transform.SetParent(_actor.transform, false);
        Vector2 center = new Vector2(11f, 2f);
        view.Begin(center, definition.volume.radius);
        Assert.That(view.boltCore.startColor.a, Is.Zero);
        view.Pulse(center, definition.volume.radius);
        Sample(view, view.expansionSeconds);
        Assert.That(view.CurrentAttackRadii, Is.EqualTo(Vector2.one * 3.5f));
        Assert.That(view.attackRing.transform.position, Is.EqualTo((Vector3)center));
        Assert.That(Vector3.Distance(view.attackRing.transform.TransformPoint(view.attackRing.GetPosition(16)), center),
            Is.EqualTo(3.5f).Within(0.001f));
        Assert.That(view.boltCore.transform.TransformPoint(view.boltCore.GetPosition(view.boltCore.positionCount - 1)).y,
            Is.EqualTo(center.y + view.boltHeight).Within(0.001f));
        Assert.That(view.droplets.particleCount, Is.InRange(1, 48));
        Vector3 boltPoint = view.boltCore.GetPosition(3);
        Sample(view, 0f);
        Assert.That(view.boltCore.GetPosition(3), Is.EqualTo(boltPoint));
        view.End();
        Assert.That(view.IsFinished, Is.False);
        Sample(view, view.attackFadeSeconds + 0.01f);
        Assert.That(view.IsFinished, Is.True);
        Assert.That(view.droplets.particleCount, Is.Zero);
        view.Begin(center, definition.volume.radius);
        Assert.That(view.boltCore.startColor.a, Is.Zero, "Reusing a volume must not replay the last strike.");
        view.End();
        Sample(view, view.exitSeconds + 0.01f);
        Assert.That(view.IsFinished, Is.True, "A proc without a damaging hit must also finish cleanly.");
    }

    [Test]
    public void CursedEnemyDamageFlashesMarkAndUnsubscribesOnRemoval()
    {
        _actor = new GameObject("CursedEnemy");
        EnemyController enemy = EnemyLifecycleTestUtility.AddActive(_actor);
        enemy.currentHP = enemy.maxHP = 1000f;
        _host = Object.Instantiate(Resources.Load<GameObject>("FieldEffects/FieldChronoMark"));
        FieldEffectVisual mark = _host.GetComponent<FieldEffectVisual>();
        mark.Begin(_actor.transform, null);
        Sample(mark, 0.4f);
        float idleOpacity = mark.bodySeal.color.a;
        enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero);
        Assert.That(mark.DamagePulseCount, Is.EqualTo(1));
        Assert.That(mark.bodySeal.color.a, Is.GreaterThan(idleOpacity));
        Assert.That(mark.damageSparks.particleCount, Is.InRange(1, 16));
        mark.End();
        enemy.TakeResolvedHit(new HitReactionData { damage = 10f }, Vector2.zero);
        Assert.That(mark.DamagePulseCount, Is.EqualTo(1), "An expired/consumed curse must not react to future damage.");
        mark.ClearImmediately();
        Assert.That(mark.damageSparks.particleCount, Is.Zero);
    }

    private static void Sample(object view, float seconds) => view.GetType()
        .GetMethod("Sample", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { seconds });
}
#endif
