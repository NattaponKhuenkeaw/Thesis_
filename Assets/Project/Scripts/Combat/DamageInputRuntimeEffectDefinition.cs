// Force Unity compilation refresh
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "RuntimeEffect_DamageInput", menuName = "Game/Runtime Effects/Damage Input Modifier", order = 14)]
public class DamageInputRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [HideInInspector]
    public bool filterByDelivery;
    [HideInInspector]
    public DamageDelivery delivery = DamageDelivery.Primary;
    [HideInInspector]
    public bool filterByElement;
    [HideInInspector]
    public ElementType elementType = ElementType.None;
    [HideInInspector]
    public bool filterByCategory;
    [HideInInspector]
    public AttackCategory category = AttackCategory.Normal;

    [HideInInspector]
    public float defenderDefensePercentAdd;
    [HideInInspector]
    public float defenderDamageResistancePercentAdd;
    [HideInInspector]
    public bool ignoreDefense;
    [HideInInspector]
    public bool ignoreDamageResistance;
    [HideInInspector]
    [FormerlySerializedAs("ignoreElementalIncomingModifiers")]
    public bool ignoreTypeReduction;

    public override void ModifyDamageInput(RuntimeEffectDamageInput input)
    {
        RuntimeEffectDamageInputParameters parameters = ResolveParameters(input?.context);
        if (input == null || !Matches(input.context, parameters))
        {
            return;
        }

        if (parameters.ignoreDefense)
        {
            input.defenderDefense = 0f;
        }
        else if (Mathf.Abs(parameters.defenderDefensePercentAdd) > 0.0001f)
        {
            input.defenderDefense = Mathf.Max(0f, input.defenderDefense * (1f + parameters.defenderDefensePercentAdd / 100f));
        }

        if (parameters.ignoreDamageResistance)
        {
            input.defenderDamageResistance = 0f;
        }
        else if (Mathf.Abs(parameters.defenderDamageResistancePercentAdd) > 0.0001f)
        {
            input.defenderDamageResistance += parameters.defenderDamageResistancePercentAdd;
        }

        if (parameters.ignoreTypeReduction)
        {
            input.ignoreTypeReduction = true;
        }
    }

    private RuntimeEffectDamageInputParameters ResolveParameters(RuntimeEffectDamageContext context)
    {
        RuntimeEffectParameterSet parameters = RuntimeEffectRuntimeUtility.GetParameters(context?.instance);
        if (parameters != null && parameters.overrideDamageInput && parameters.damageInput != null)
        {
            return parameters.damageInput;
        }

        return new RuntimeEffectDamageInputParameters
        {
            filter = new RuntimeEffectDamageFilter
            {
                filterByDelivery = filterByDelivery,
                delivery = delivery,
                filterByElement = filterByElement,
                elementType = elementType,
                filterByCategory = filterByCategory,
                category = category
            },
            defenderDefensePercentAdd = defenderDefensePercentAdd,
            defenderDamageResistancePercentAdd = defenderDamageResistancePercentAdd,
            ignoreDefense = ignoreDefense,
            ignoreDamageResistance = ignoreDamageResistance,
            ignoreTypeReduction = ignoreTypeReduction
        };
    }

    private static bool Matches(RuntimeEffectDamageContext context, RuntimeEffectDamageInputParameters parameters)
    {
        if (context == null || parameters == null)
        {
            return false;
        }

        if (parameters.filter == null)
        {
            return context.damageDelivery == DamageDelivery.Primary;
        }

        if (!parameters.filter.Matches(context))
        {
            return false;
        }

        // Legacy source-side input modifiers had no delivery selector and are
        // migrated to Primary coverage. Authors must opt in to secondary damage.
        return parameters.filter.filterByDelivery
            || context.damageDelivery == DamageDelivery.Primary;
    }
}
