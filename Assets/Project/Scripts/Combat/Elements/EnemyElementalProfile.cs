using UnityEngine;

[CreateAssetMenu(fileName = "EnemyElementalProfile_", menuName = "Game/Character/Enemy Elemental Profile")]
public class EnemyElementalProfile : ScriptableObject
{
    [Header("Damage Type Reduction")]
    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Physical damage.")]
    public float physicalDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Memory damage.")]
    public float memoryDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Echo damage.")]
    public float echoDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Ash damage.")]
    public float ashDamageReduction;

    [Range(0f, 95f)]
    [Tooltip("Percentage reduction applied to Void damage.")]
    public float voidDamageReduction;

    public float GetDamageReductionPercent(ElementType damageType)
    {
        return damageType switch
        {
            ElementType.Memory => memoryDamageReduction,
            ElementType.Echo => echoDamageReduction,
            ElementType.Ash => ashDamageReduction,
            ElementType.Void => voidDamageReduction,
            _ => physicalDamageReduction
        };
    }
}
