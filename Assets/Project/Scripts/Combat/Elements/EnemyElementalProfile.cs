using UnityEngine;

[CreateAssetMenu(fileName = "EnemyElementalProfile_", menuName = "Game/Character/Enemy Elemental Profile")]
public class EnemyElementalProfile : ScriptableObject
{
    [Header("Damage Type Reduction")]
    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Physical damage.")]
    public float physicalDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Lightning damage.")]
    public float lightningDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Bleed damage.")]
    public float bleedDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Darkness damage.")]
    public float darknessDamageReduction;

    public float GetDamageReductionPercent(ElementType damageType)
    {
        return damageType switch
        {
            ElementType.Lightning => lightningDamageReduction,
            ElementType.Bleed => bleedDamageReduction,
            ElementType.Darkness => darknessDamageReduction,
            _ => physicalDamageReduction
        };
    }
}
