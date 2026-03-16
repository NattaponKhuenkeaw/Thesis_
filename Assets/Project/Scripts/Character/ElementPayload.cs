using UnityEngine;

public class ElementPayload : MonoBehaviour
{
    // =========================================================
    // Element Info
    // =========================================================
    [Header("Element Info")]
    public ElementType elementType = ElementType.None;
    public string sourceCharacterId;
    public bool canReact = true;

    // =========================================================
    // Public Methods
    // =========================================================
    public void Initialize(ElementType newElementType, string newSourceCharacterId, bool newCanReact = true)
    {
        elementType = newElementType;
        sourceCharacterId = newSourceCharacterId;
        canReact = newCanReact;
    }
}