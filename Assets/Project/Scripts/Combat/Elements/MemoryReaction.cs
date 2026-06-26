using UnityEngine;

public class MemoryReaction : IElementalReaction
{
    public ElementReactionType ReactionType => ElementReactionType.Stagger;

    public void Execute(EnemyController enemy, CharacterStats attackerStats, float lostHP)
    {
        Debug.Log($"[MemoryReaction] Triggered on {enemy.gameObject.name}. Enemy unable to react for 8 seconds.");
        enemy.ApplyStatus(StatusEffectType.MemoryStagger, true);
    }

    public void OnExpired(EnemyController enemy, float accumulatedDamage)
    {
        Debug.Log($"[MemoryReaction] Expired on {enemy.gameObject.name}.");
        enemy.ApplyStatus(StatusEffectType.MemoryStagger, false);
    }
}
