using System.Collections.Generic;
using UnityEngine;

public enum ElementStrength
{
    Weak,
    Normal,
    Strong
}

[System.Serializable]
public class ElementResistance
{
    public ElementType elementType;
    public ElementStrength strength = ElementStrength.Normal;
    [Tooltip("When enabled, derive resistance from legacy strength using default +/-30% values.")]
    public bool useLegacyStrengthForResistance = true;
    [Tooltip("Positive values reduce incoming damage of this element. Negative values increase incoming damage.")]
    public float resistancePercent = 0f;
    [Tooltip("When enabled, derive mastery from legacy strength using default +/-30% values.")]
    public bool useLegacyStrengthForMastery = true;
    [Tooltip("Positive values increase outgoing damage against characters of this element. Negative values reduce it.")]
    public float masteryPercent = 0f;
}

[CreateAssetMenu(fileName = "EnemyElementalProfile_", menuName = "Game/Character/Enemy Elemental Profile")]
public class EnemyElementalProfile : ScriptableObject
{
    private const float DefaultPercentMagnitude = 30f;

    public List<ElementResistance> resistances = new List<ElementResistance>();

    public ElementStrength GetStrength(ElementType element)
    {
        if (element == ElementType.None)
        {
            return ElementStrength.Normal;
        }

        foreach (var res in resistances)
        {
            if (res.elementType == element)
            {
                return res.strength;
            }
        }
        return ElementStrength.Normal;
    }

    public float GetResistancePercent(ElementType element)
    {
        ElementResistance entry = FindEntry(element);
        if (entry == null)
        {
            return 0f;
        }

        return entry.useLegacyStrengthForResistance
            ? ResolveLegacyPercent(entry.strength)
            : entry.resistancePercent;
    }

    public float GetMasteryPercent(ElementType element)
    {
        ElementResistance entry = FindEntry(element);
        if (entry == null)
        {
            return 0f;
        }

        return entry.useLegacyStrengthForMastery
            ? ResolveLegacyPercent(entry.strength)
            : entry.masteryPercent;
    }

    private ElementResistance FindEntry(ElementType element)
    {
        if (element == ElementType.None)
        {
            return null;
        }

        for (int i = 0; i < resistances.Count; i++)
        {
            ElementResistance resistance = resistances[i];
            if (resistance != null && resistance.elementType == element)
            {
                return resistance;
            }
        }

        return null;
    }

    private static float ResolveLegacyPercent(ElementStrength strength)
    {
        return strength switch
        {
            ElementStrength.Weak => -DefaultPercentMagnitude,
            ElementStrength.Strong => DefaultPercentMagnitude,
            _ => 0f
        };
    }
}
