using UnityEngine;

public class GustBreakSkill : MonoBehaviour
{
    // =========================================================
    // Skill Settings
    // =========================================================
    [Header("Skill Settings")]
    public float lifetime = 0.2f;

    // =========================================================
    // Runtime Data
    // =========================================================
    private float damageMultiplier;
    private float knockbackForce;
    private Vector2 direction;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        EnemyController enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null)
            return;

        enemy.TakeHit(damageMultiplier, direction, knockbackForce);
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void Initialize(float newDamageMultiplier, float newKnockbackForce, Vector2 newDirection)
    {
        damageMultiplier = newDamageMultiplier;
        knockbackForce = newKnockbackForce;
        direction = newDirection;

        ApplyFacing();
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void ApplyFacing()
    {
        Vector3 scale = transform.localScale;

        if (direction.x < 0f)
            scale.x = -Mathf.Abs(scale.x);
        else
            scale.x = Mathf.Abs(scale.x);

        transform.localScale = scale;
    }
    //ถ้าสกิลเป็น sprite ที่ควรหมุนแทน flip
    /* private void ApplyFacing()
     {
         if (direction.x < 0f)
             transform.rotation = Quaternion.Euler(0f, 180f, 0f);
         else
             transform.rotation = Quaternion.identity;
     } */
}