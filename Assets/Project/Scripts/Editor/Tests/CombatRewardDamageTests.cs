using NUnit.Framework;
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
