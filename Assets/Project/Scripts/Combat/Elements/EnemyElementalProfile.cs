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
}

[CreateAssetMenu(fileName = "EnemyElementalProfile_", menuName = "Game/Character/Enemy Elemental Profile")]
public class EnemyElementalProfile : ScriptableObject
{
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
}
