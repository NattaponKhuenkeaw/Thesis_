using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_LifestealOnHit", menuName = "Game/Runtime Effects/Lifesteal On Hit", order = 15)]
public class LifestealOnHitRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [Header("Filters")]
    public bool filterByElement;
    public ElementType elementType = ElementType.None;
    public bool filterByCategory;
    public AttackCategory category = AttackCategory.Normal;

    [Header("Heal")]
    [Min(0f)] public float damageDealtRatio = 0.5f;
    [Range(0f, 1f)] public float lowHpThreshold = 0.3f;
    [Min(1f)] public float lowHpMultiplier = 2f;

    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        if (context == null
            || context.sourceCharacter == null
            || context.damageDealt <= 0f
            || !Matches(context))
        {
            return;
        }

        float healAmount = context.damageDealt * Mathf.Max(0f, damageDealtRatio);
        if (healAmount <= 0f)
        {
            return;
        }

        TeamLoader teamLoader = Object.FindFirstObjectByType<TeamLoader>();
        PlayerCharacter activePlayer = Object.FindFirstObjectByType<PlayerCharacter>();
        bool activeSource = activePlayer != null && activePlayer.characterObject == context.sourceCharacter;

        float maxHp = activeSource && activePlayer != null
            ? activePlayer.MaxHP
            : teamLoader != null ? teamLoader.GetCharacterMaxHP(context.sourceCharacter) : 0f;
        float currentHp = activeSource && activePlayer != null
            ? activePlayer.CurrentHP
            : teamLoader != null ? teamLoader.GetCharacterCurrentHP(context.sourceCharacter) : 0f;

        if (maxHp > 0f && currentHp / maxHp < Mathf.Clamp01(lowHpThreshold))
        {
            healAmount *= Mathf.Max(1f, lowHpMultiplier);
        }

        if (activeSource && activePlayer != null)
        {
            activePlayer.Heal(healAmount);
            return;
        }

        if (teamLoader == null)
        {
            return;
        }

        teamLoader.SetCharacterCurrentHP(
            context.sourceCharacter,
            Mathf.Min(maxHp, currentHp + healAmount));
    }

    private bool Matches(RuntimeEffectDamageContext context)
    {
        if (filterByElement && context.elementType != elementType)
        {
            return false;
        }

        return !filterByCategory || context.category == category;
    }
}
