using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_DamageInput", menuName = "Game/Runtime Effects/Damage Input Modifier", order = 14)]
public class DamageInputRuntimeEffectDefinition : RuntimeEffectDefinition
{
    [Header("Filters")]
    public bool filterByElement;
    public ElementType elementType = ElementType.None;
    public bool filterByCategory;
    public AttackCategory category = AttackCategory.Normal;

    [Header("Mitigation")]
    [Tooltip("Positive values increase defender defense before mitigation. Negative values reduce it.")]
    public float defenderDefensePercentAdd;
    [Tooltip("Positive values increase defender damage resistance. Negative values reduce it.")]
    public float defenderDamageResistancePercentAdd;
    public bool ignoreDefense;
    public bool ignoreDamageResistance;
    public bool ignoreElementalIncomingModifiers;

    public override void ModifyDamageInput(RuntimeEffectDamageInput input)
    {
        if (input == null || !Matches(input.context))
        {
            return;
        }

        if (ignoreDefense)
        {
            input.defenderDefense = 0f;
        }
        else if (Mathf.Abs(defenderDefensePercentAdd) > 0.0001f)
        {
            input.defenderDefense = Mathf.Max(0f, input.defenderDefense * (1f + defenderDefensePercentAdd / 100f));
        }

        if (ignoreDamageResistance)
        {
            input.defenderDamageResistance = 0f;
        }
        else if (Mathf.Abs(defenderDamageResistancePercentAdd) > 0.0001f)
        {
            input.defenderDamageResistance += defenderDamageResistancePercentAdd;
        }

        if (ignoreElementalIncomingModifiers)
        {
            input.ignoreElementalIncomingModifiers = true;
        }
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
