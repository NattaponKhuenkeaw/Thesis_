using UnityEngine;

[CreateAssetMenu(fileName = "Element_", menuName = "Game/Combat/Element Definition")]
public class ElementDefinition : ScriptableObject
{
    [Header("Core Info")]
    public ElementType elementType;
    public string displayName;
    public Color damageTextColor = Color.white;
    public Sprite icon;

    [Header("DOT Properties")]
    [Min(0.05f)]
    [Tooltip("How long this element's DOT remains active, in seconds.")]
    public float dotDuration = 5f;
    [Min(0f)]
    [Tooltip("Damage per tick as a ratio of attacker ATK before DOT Mastery.")]
    public float dotDamageRatio = 0.3f;
    [Min(0.05f)]
    [Tooltip("Time in seconds between DOT damage ticks.")]
    public float dotTickInterval = 1f;
}
