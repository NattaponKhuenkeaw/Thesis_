using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_DamageModifier", menuName = "Game/Runtime Effects/Damage Modifier", order = 11)]
public class DamageModifierRuntimeEffectDefinition : RuntimeEffectDefinition
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
    public float outgoingDamagePercentAdd;
    [HideInInspector]
    public float incomingDamageTakenPercentAdd;

    public override float ModifyOutgoingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        RuntimeEffectDamageModifierParameters parameters = ResolveParameters(context);
        if (!Matches(context, parameters) || Mathf.Abs(parameters.outgoingDamagePercentAdd) <= Mathf.Epsilon)
        {
            return currentDamage;
        }

        return Mathf.Max(0f, currentDamage * (1f + parameters.outgoingDamagePercentAdd / 100f));
    }

    public override float ModifyIncomingDamage(RuntimeEffectDamageContext context, float currentDamage)
    {
        RuntimeEffectDamageModifierParameters parameters = ResolveParameters(context);
        if (!Matches(context, parameters) || Mathf.Abs(parameters.incomingDamageTakenPercentAdd) <= Mathf.Epsilon)
        {
            return currentDamage;
        }

        return Mathf.Max(0f, currentDamage * (1f + parameters.incomingDamageTakenPercentAdd / 100f));
    }

    private RuntimeEffectDamageModifierParameters ResolveParameters(RuntimeEffectDamageContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context?.instance);
        if (parameters != null && parameters.overrideDamageModifier && parameters.damageModifier != null)
        {
            return parameters.damageModifier;
        }

        return new RuntimeEffectDamageModifierParameters
        {
            filter = new RuntimeEffectDamageFilter
            {
                filterByElement = filterByElement,
                elementType = elementType,
                filterByCategory = filterByCategory,
                category = category
            },
            outgoingDamagePercentAdd = outgoingDamagePercentAdd,
            incomingDamageTakenPercentAdd = incomingDamageTakenPercentAdd
        };
    }

    private static bool Matches(RuntimeEffectDamageContext context, RuntimeEffectDamageModifierParameters parameters)
    {
        if (context == null || parameters == null)
        {
            return false;
        }

        return parameters.filter == null || parameters.filter.Matches(context);
    }
}
