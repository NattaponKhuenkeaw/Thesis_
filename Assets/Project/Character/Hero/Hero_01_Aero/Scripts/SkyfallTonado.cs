using UnityEngine;
using System.Collections.Generic;

public class SkyfallTornado : MonoBehaviour
{
    // =========================================================
    // Tornado Settings
    // =========================================================
    [Header("Tornado Settings")]
    public float lifetime = 4f;
    public float damageMultiplier = 0.5f;
    public float hitInterval = 0.4f;
    public float liftForce = 6f;

    // =========================================================
    // Runtime
    // =========================================================
    private readonly Dictionary<EnemyTarget, float> nextHitTime = new Dictionary<EnemyTarget, float>();
    private Vector2 direction = Vector2.right;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        EnemyTarget enemy = other.GetComponentInParent<EnemyTarget>();
        if (enemy == null)
            return;

        if (!nextHitTime.ContainsKey(enemy))
        {
            nextHitTime[enemy] = 0f;
        }

        if (Time.time >= nextHitTime[enemy])
        {
            enemy.TakeHit(damageMultiplier, Vector2.up, liftForce);
            nextHitTime[enemy] = Time.time + hitInterval;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        EnemyTarget enemy = other.GetComponentInParent<EnemyTarget>();
        if (enemy == null)
            return;

        if (nextHitTime.ContainsKey(enemy))
        {
            nextHitTime.Remove(enemy);
        }
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void Initialize(float newDamageMultiplier, float newLifetime, float newHitInterval, Vector2 newDirection)
    {
        damageMultiplier = newDamageMultiplier;
        lifetime = newLifetime;
        hitInterval = newHitInterval;
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
}