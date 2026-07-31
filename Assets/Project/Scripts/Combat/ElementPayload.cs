using UnityEngine;

public class ElementPayload : MonoBehaviour
{
    // =========================================================
    // Element Info
    // =========================================================
    [Header("Element Info")]
    public ElementType elementType = ElementType.None;
    public string sourceCharacterId;

    [Header("Combat Payload Data")]
    public AttackCategory attackCategory = AttackCategory.Normal;
    public float baseDamage = 0f;
    public CharacterStats attackerStats;

    // =========================================================
    // Public Methods
    // =========================================================
    public void Initialize(ElementType newElementType, string newSourceCharacterId)
    {
        elementType = newElementType;
        sourceCharacterId = newSourceCharacterId;
    }

    public void Initialize(ElementType newElementType, string newSourceCharacterId, AttackCategory category, float damage, CharacterStats stats)
    {
        elementType = newElementType;
        sourceCharacterId = newSourceCharacterId;
        attackCategory = category;
        baseDamage = damage;
        attackerStats = stats;
    }
}
