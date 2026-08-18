#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public class EnemyCombatRoleTests
{
    [Test]
    public void CommitMeleeLocksWindupButNotIdleProfile()
    {
        var go = new GameObject(nameof(CommitMeleeLocksWindupButNotIdleProfile));
        try
        {
            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.CommitMelee;

            Assert.That(enemy.UninterruptibleOnceWindup, Is.True);
            Assert.That(enemy.ArmorDuringCommittedSwing, Is.True);
            Assert.That(enemy.UsesRangedAttack, Is.False);
            Assert.That(enemy.NeverEvade, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void InterruptibleMeleeHasNoSwingArmor()
    {
        var go = new GameObject(nameof(InterruptibleMeleeHasNoSwingArmor));
        try
        {
            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.InterruptibleMelee;

            Assert.That(enemy.UninterruptibleOnceWindup, Is.False);
            Assert.That(enemy.ArmorDuringCommittedSwing, Is.False);
            Assert.That(enemy.NeverEvade, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RangedHoldUsesLongRangeAndDoesNotEvade()
    {
        var go = new GameObject(nameof(RangedHoldUsesLongRangeAndDoesNotEvade));
        try
        {
            EnemyController enemy = go.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;

            Assert.That(enemy.UsesRangedAttack, Is.True);
            Assert.That(enemy.NeverEvade, Is.True);
            Assert.That(enemy.ResolveAttackRange(1.5f), Is.EqualTo(7f));
            Assert.That(enemy.ResolveLungeDistance(0.55f), Is.EqualTo(0f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RangedHoldDoesNotMoveAwayWhenPlayerIsAlreadyInRange()
    {
        var agent = new GameObject(nameof(RangedHoldDoesNotMoveAwayWhenPlayerIsAlreadyInRange));
        var target = new GameObject(nameof(RangedHoldDoesNotMoveAwayWhenPlayerIsAlreadyInRange) + "_Target");
        try
        {
            agent.AddComponent<Rigidbody2D>();
            EnemyController enemy = agent.AddComponent<EnemyController>();
            enemy.CombatRole = EnemyCombatRole.RangedHold;
            enemy.AttackRangeOverride = 7f;
            EnemyLocomotion locomotion = agent.AddComponent<EnemyLocomotion>();

            agent.transform.position = new Vector3(1f, 0f, 0f);
            target.transform.position = Vector3.zero;

            bool inRange = locomotion.MoveTowardsTargetRange(target.transform, 7f, 6f, 0.16f);
            Assert.That(inRange, Is.True);
            Assert.That(agent.transform.position.x, Is.EqualTo(1f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(agent);
            Object.DestroyImmediate(target);
        }
    }
}
#endif
