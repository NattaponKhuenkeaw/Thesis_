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
    public bool hasDOT;
    [Tooltip("DOT tick ratio: 0.3 means 30% of Attacker's ATK * ElementalMastery%")]
    public float dotDamageRatio = 0.3f;

    [Header("Reaction Properties")]
    public ElementReactionType reactionType;
    public float reactionDuration = 20f;
    [TextArea(3, 5)]
    public string reactionDescription;
}
