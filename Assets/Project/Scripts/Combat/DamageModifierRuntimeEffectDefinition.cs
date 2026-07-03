using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_DamageModifier", menuName = "Game/Runtime Effects/Damage Modifier", order = 11)]
public class DamageModifierRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [Header("Filters")]
    public bool filterByElement;
    public ElementType elementType = ElementType.None;
    public bool filterByCategory;
    public AttackCategory category = AttackCategory.Normal;

    [Header("Damage Percent")]
    [Tooltip("Positive values increase outgoing damage. -20 means outgoing damage is reduced by 20%.")]
    public float outgoingDamagePercentAdd;
    [Tooltip("Positive values increase incoming damage taken. -20 means incoming damage is reduced by 20%.")]
    public float incomingDamageTakenPercentAdd;

    public override float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        if (!Matches(context) || Mathf.Abs(outgoingDamagePercentAdd) <= Mathf.Epsilon)
        {
            return currentDamage;
        }

        return Mathf.Max(0f, currentDamage * (1f + outgoingDamagePercentAdd / 100f));
    }

    public override float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        if (!Matches(context) || Mathf.Abs(incomingDamageTakenPercentAdd) <= Mathf.Epsilon)
        {
            return currentDamage;
        }

        return Mathf.Max(0f, currentDamage * (1f + incomingDamageTakenPercentAdd / 100f));
    }

    private bool Matches(RuntimeEffectDamageContext context)
    {
        if (context == null)
        {
            return false;
        }

        if (filterByElement && context.elementType != elementType)
        {
            return false;
        }

        return !filterByCategory || context.category == category;
    }
}
