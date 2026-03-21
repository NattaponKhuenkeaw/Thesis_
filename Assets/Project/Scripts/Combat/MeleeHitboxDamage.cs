using UnityEngine;

public class MeleeHitboxDamage : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerMovement movement;

    // =========================================================
    // Runtime
    // =========================================================
    private int lastHitFrame = -1;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (playerCharacter == null)
            playerCharacter = GetComponentInParent<PlayerCharacter>();

        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (lastHitFrame == Time.frameCount)
            return;

        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null)
            return;

        float damage = 1f;
        float knockback = 4f;
        Vector2 direction = Vector2.right;

        if (playerCharacter != null && playerCharacter.characterObject != null)
        {
            CharacterStats stats = playerCharacter.characterObject.stats;
            BasicAttack basicAttack = GetComponentInParent<BasicAttack>();

            damage = stats.attack;

            if (basicAttack != null)
            {
                int comboStep = 1;
                // ถ้าอยากละเอียดภายหลังค่อยดึง combo step จริงมาใช้
                damage *= stats.attack1Multiplier;
            }
        }

        if (movement != null && movement.spriteRenderer != null && movement.spriteRenderer.flipX)
            direction = Vector2.left;

        enemy.TakeHit(damage, direction, knockback);
        lastHitFrame = Time.frameCount;
    }
}