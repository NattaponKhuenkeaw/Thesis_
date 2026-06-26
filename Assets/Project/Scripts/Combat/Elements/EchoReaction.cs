using UnityEngine;

public class EchoReaction : IElementalReaction
{
    public ElementReactionType ReactionType => ElementReactionType.Accumulate;

    public void Execute(EnemyController enemy, CharacterStats attackerStats, float lostHP)
    {
        Debug.Log($"[EchoReaction] Triggered on {enemy.gameObject.name}. Tracking damage accumulation for 20 seconds.");
    }

    public void OnExpired(EnemyController enemy, float accumulatedDamage)
    {
        float echoDamage = accumulatedDamage / 2f;
        Debug.Log($"[EchoReaction] Expired on {enemy.gameObject.name}. Dealing half of accumulated damage ({accumulatedDamage}) = {echoDamage}");

        if (echoDamage > 0.01f && enemy != null && enemy.IsAlive)
        {
            HitReactionData echoHit = HitReactionData.Default(echoDamage, Vector2.zero, 0f);
            echoHit.damageIsFinal = true;
            echoHit.damageRoutine = DamageRoutine.Additional;
            echoHit.elementType = ElementType.Echo;
            echoHit.flashFrames = 4;
            echoHit.flashColor = Color.magenta;

            enemy.TakeHit(echoHit, enemy.transform.position);
        }
    }
}
