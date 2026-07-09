using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_LifestealOnHit", menuName = "Game/Runtime Effects/Lifesteal On Hit", order = 15)]
public class LifestealOnHitRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public bool filterByElement;
    [HideInInspector]
    public ElementType elementType = ElementType.None;
    [HideInInspector]
    public bool filterByCategory;
    [HideInInspector]
    public AttackCategory category = AttackCategory.Normal;

    [HideInInspector]
    [Min(0f)] public float damageDealtRatio = 0.5f;
    [HideInInspector]
    [Range(0f, 1f)] public float lowHpThreshold = 0.3f;
    [HideInInspector]
    [Min(1f)] public float lowHpMultiplier = 2f;

    public override void OnHitDealt(RuntimeEffectDamageContext context)
    {
        RuntimeEffectLifestealParameters parameters = ResolveParameters(context);
        if (context == null
            || context.sourceCharacter == null
            || context.damageDealt <= 0f
            || !Matches(context, parameters))
        {
            return;
        }

        float healAmount = context.damageDealt * Mathf.Max(0f, parameters.damageDealtRatio);
        if (healAmount <= 0f)
        {
            return;
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        PlayerCharacter activePlayer = PlayerCharacter.Instance;
        bool activeSource = activePlayer != null && activePlayer.characterObject == context.sourceCharacter;

        float maxHp = activeSource && activePlayer != null
            ? activePlayer.MaxHP
            : teamLoader != null ? teamLoader.GetCharacterMaxHP(context.sourceCharacter) : 0f;
        float currentHp = activeSource && activePlayer != null
            ? activePlayer.CurrentHP
            : teamLoader != null ? teamLoader.GetCharacterCurrentHP(context.sourceCharacter) : 0f;

        if (maxHp > 0f && currentHp / maxHp < Mathf.Clamp01(parameters.lowHpThreshold))
        {
            healAmount *= Mathf.Max(1f, parameters.lowHpMultiplier);
        }

        if (activeSource && activePlayer != null)
        {
            RuntimeEffectHealUtility.ApplyHeal(context.sourceCharacter, activePlayer, healAmount);
            return;
        }

        RuntimeEffectHealUtility.ApplyHeal(context.sourceCharacter, null, healAmount);
    }

    private RuntimeEffectLifestealParameters ResolveParameters(RuntimeEffectDamageContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context?.instance);
        if (parameters != null && parameters.overrideLifesteal && parameters.lifesteal != null)
        {
            return parameters.lifesteal;
        }

        return new RuntimeEffectLifestealParameters
        {
            filter = new RuntimeEffectDamageFilter
            {
                filterByElement = filterByElement,
                elementType = elementType,
                filterByCategory = filterByCategory,
                category = category
            },
            damageDealtRatio = damageDealtRatio,
            lowHpThreshold = lowHpThreshold,
            lowHpMultiplier = lowHpMultiplier
        };
    }

    private static bool Matches(RuntimeEffectDamageContext context, RuntimeEffectLifestealParameters parameters)
    {
        if (context == null || parameters == null)
        {
            return false;
        }

        return parameters.filter == null || parameters.filter.Matches(context);
    }
}
