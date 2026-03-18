using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MeleeHitboxController : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerMovement movement;
    public BasicAttack basicAttack;
    public BoxCollider2D hitboxCollider;

    [Header("Anchor")]
    public Transform attackAnchor;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponentInParent<PlayerCharacter>();

        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponentInParent<BasicAttack>();

        if (hitboxCollider == null)
            hitboxCollider = GetComponent<BoxCollider2D>();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void UpdateHitboxShape()
    {
        if (playerCharacter == null || playerCharacter.characterObject == null)
            return;

        if (playerCharacter.characterObject.attackHitboxData == null)
            return;

        int comboStep = 1;
        if (basicAttack != null)
            comboStep = basicAttack.GetComboStep();

        AttackHitboxData data = playerCharacter.characterObject.attackHitboxData;

        Vector2 offset = data.GetOffset(comboStep);
        Vector2 size = data.GetSize(comboStep);

        if (movement != null && movement.spriteRenderer != null && movement.spriteRenderer.flipX)
            offset.x = -Mathf.Abs(offset.x);
        else
            offset.x = Mathf.Abs(offset.x);

        Vector3 origin = attackAnchor != null ? attackAnchor.position : transform.parent.position;
        transform.position = origin + new Vector3(offset.x, offset.y, 0f);

        hitboxCollider.size = size;
    }
}