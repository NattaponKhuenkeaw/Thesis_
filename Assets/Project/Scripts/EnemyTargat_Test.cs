using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyTarget : MonoBehaviour
{
    // =========================================================
    // Enemy Stats
    // =========================================================
    [Header("Enemy Stats")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    // =========================================================
    // Dependencies
    // =========================================================
    private Rigidbody2D rb;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void TakeHit(float damageMultiplier, Vector2 direction, float knockbackForce)
    {
        currentHP -= damageMultiplier;
        currentHP = Mathf.Max(currentHP, 0f);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        Debug.Log($"{gameObject.name} took hit | Damage={damageMultiplier} | HP={currentHP}");
    }
}