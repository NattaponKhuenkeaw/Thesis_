using UnityEngine;

public class VoidReaction : IElementalReaction
{
    public ElementReactionType ReactionType => ElementReactionType.Explosion;

    public void Execute(EnemyController enemy, CharacterStats attackerStats, float lostHP)
    {
        float atk = attackerStats != null ? attackerStats.attack : 10f;
        float voidDamage = atk * 1.5f;

        Debug.Log($"[VoidReaction] Triggered on {enemy.gameObject.name}. Dealing 150% ATK Void damage ({voidDamage}) as AOE.");

        if (enemy != null && enemy.IsAlive)
        {
            HitReactionData voidHit = HitReactionData.Default(voidDamage, Vector2.zero, 0f);
            voidHit.damageIsFinal = true;
            voidHit.damageRoutine = DamageRoutine.Additional;
            voidHit.elementType = ElementType.Void;
            voidHit.flashFrames = 4;
            voidHit.flashColor = new Color(0.5f, 0f, 0.5f); // Purple / Void

            enemy.TakeHit(voidHit, enemy.transform.position);
        }

        // Apply AOE damage to surrounding enemies in 3m radius
        Collider2D[] cols = Physics2D.OverlapCircleAll(enemy.transform.position, 3f);
        foreach (var col in cols)
        {
            if (col.gameObject != enemy.gameObject)
            {
                EnemyController otherEnemy = col.GetComponent<EnemyController>();
                if (otherEnemy != null && otherEnemy.IsAlive)
                {
                    HitReactionData aoeHit = HitReactionData.Default(voidDamage, Vector2.zero, 0f);
                    aoeHit.damageIsFinal = true;
                    aoeHit.damageRoutine = DamageRoutine.Additional;
                    aoeHit.elementType = ElementType.Void;
                    aoeHit.flashFrames = 4;
                    aoeHit.flashColor = new Color(0.5f, 0f, 0.5f);

                    otherEnemy.TakeHit(aoeHit, enemy.transform.position);
                }
            }
        }

        // Apply debuffs based on enemy tier
        if (enemy.isEliteOrBoss)
        {
            Debug.Log($"[VoidReaction] {enemy.gameObject.name} is Elite/Boss. Decreasing Action Speed by 30% for 15 seconds.");
            enemy.ApplyStatus(StatusEffectType.SlowAction, true);
        }
        else
        {
            Debug.Log($"[VoidReaction] {enemy.gameObject.name} is Normal enemy. Confusing for 15 seconds.");
            enemy.ApplyStatus(StatusEffectType.Confusion, true);
        }
    }

    public void OnExpired(EnemyController enemy, float accumulatedDamage)
    {
        Debug.Log($"[VoidReaction] Expired on {enemy.gameObject.name}. Clearing debuffs.");
        if (enemy != null)
        {
            enemy.ApplyStatus(StatusEffectType.SlowAction, false);
            enemy.ApplyStatus(StatusEffectType.Confusion, false);
        }
    }
}
