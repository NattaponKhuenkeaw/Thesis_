using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

public class CombatRewardDamageTests
{
    [Test]
    public void CombatAction_DefaultsGuaranteedAuraRewardToZero()
    {
        ComboAction action = new ComboAction();

        Assert.That(action.auraEnergyReward, Is.EqualTo(0f));
    }

    [Test]
    public void AuraEnergyRewardClaim_CanOnlyBeClaimedOnce()
    {
        AuraEnergyRewardClaim claim = new AuraEnergyRewardClaim(25f);

        Assert.That(claim.TryClaim(), Is.True);
        Assert.That(claim.TryClaim(), Is.False);
        Assert.That(claim.Amount, Is.EqualTo(25f));
    }

    [Test]
    public void PlayerCombat_ReusesRewardClaimAcrossStepsOfSameAction()
    {
        GameObject playerObject = new GameObject("Reward Claim Test Player");
        PlayerCombat combat = playerObject.AddComponent<PlayerCombat>();
        ComboAction action = new ComboAction { auraEnergyReward = 25f };

        combat.BeginResolvedAction(true, false, false, action);
        AuraEnergyRewardClaim firstClaim = combat.CurrentAuraEnergyRewardClaim;
        combat.BeginResolvedAction(true, false, false, action);

        Assert.That(combat.CurrentAuraEnergyRewardClaim, Is.SameAs(firstClaim));

        Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void AuraEnergyChanceGainGate_LimitsGrantsWithinWindow()
    {
        GameObject playerObject = new GameObject("Chance Gate Test Player");
        PlayerCombat combat = playerObject.AddComponent<PlayerCombat>();
        MethodInfo consumeSlot = typeof(PlayerCombat).GetMethod(
            "TryConsumeAuraEnergyChanceGrantSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(consumeSlot, Is.Not.Null);
        for (int i = 0; i < 5; i++)
        {
            Assert.That((bool)consumeSlot.Invoke(combat, null), Is.True);
        }

        Assert.That((bool)consumeSlot.Invoke(combat, null), Is.False);

        Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void EnemyTakeHit_ReturnsFinalDamageInsteadOfRemainingHpLoss()
    {
        GameObject enemyObject = new GameObject("Damage Test Enemy");
        enemyObject.AddComponent<Rigidbody2D>();
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();
        enemy.maxHP = 64f;
        enemy.currentHP = 64f;

        float eventDamage = 0f;
        enemy.OnDamaged += data => eventDamage = data.damage;

        HitReactionData hit = HitReactionData.Default(150f, Vector2.right, 0f);
        hit.damageIsFinal = true;
        hit.flashFrames = 0;

        LogAssert.Expect(
            LogType.Error,
            "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
        float finalDamage = enemy.TakeHit(hit, Vector2.zero);

        Assert.That(finalDamage, Is.EqualTo(150f));
        Assert.That(eventDamage, Is.EqualTo(150f));
        Assert.That(enemy.currentHP, Is.EqualTo(0f));

        if (enemyObject != null)
        {
            Object.DestroyImmediate(enemyObject);
        }
    }
}
