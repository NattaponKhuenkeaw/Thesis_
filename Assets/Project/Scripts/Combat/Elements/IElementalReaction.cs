public interface IElementalReaction
{
    ElementReactionType ReactionType { get; }
    void Execute(EnemyController enemy, CharacterStats attackerStats, float lostHP);
    void OnExpired(EnemyController enemy, float accumulatedDamage);
}
