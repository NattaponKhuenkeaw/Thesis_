using UnityEngine;

public class AshReaction : IElementalReaction
{
    public ElementReactionType ReactionType => ElementReactionType.LostHP;

    public void Execute(EnemyController enemy, CharacterStats attackerStats, float lostHP)
    {
        float ashDamage = lostHP;
        float cap = enemy != null ? enemy.maxHP * 0.08f : 0f;
        if (ashDamage > cap)
        {
            ashDamage = cap;
        }

        Debug.Log($"[AshReaction] Triggered on {enemy.gameObject.name}. Lost HP: {lostHP}, Cap: {cap}, Final Ash Damage: {ashDamage}");

        if (ashDamage > 0.01f && enemy != null && enemy.IsAlive)
        {
            HitReactionData ashHit = HitReactionData.Default(ashDamage, Vector2.zero, 0f);
            ashHit.damageIsFinal = true;
            ashHit.damageRoutine = DamageRoutine.Additional;
            ashHit.elementType = ElementType.Ash;
            ashHit.flashFrames = 4;
            ashHit.flashColor = new Color(1f, 0.45f, 0f); // Orange-red / Ash

            enemy.TakeHit(ashHit, enemy.transform.position);
        }
    }

    public void OnExpired(EnemyController enemy, float accumulatedDamage)
    {
        Debug.Log($"[AshReaction] Expired on {enemy.gameObject.name}.");
    }
}
