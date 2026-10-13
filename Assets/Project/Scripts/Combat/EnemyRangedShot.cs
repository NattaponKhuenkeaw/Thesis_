using UnityEngine;

/// <summary>
/// Simple enemy bullet. Damages the player once on trigger, then despawns.
/// Independent of the player ProjectileData pipeline so trash ranged kits stay prefab-local.
/// </summary>
public class EnemyRangedShot : MonoBehaviour
{
    private EnemyController owner;
    private Vector2 velocity;
    private float lifeRemaining;
    private bool consumed;

    public void Launch(EnemyController source, Vector2 direction, float speed, float lifetime)
    {
        owner = source;
        velocity = direction.sqrMagnitude > 0.0001f
            ? direction.normalized * Mathf.Max(0.1f, speed)
            : Vector2.right * speed;
        lifeRemaining = Mathf.Max(0.05f, lifetime);
        consumed = false;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifeRemaining -= Time.deltaTime;
        if (lifeRemaining <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHitPlayer(other);
    }

    private void TryHitPlayer(Collider2D other)
    {
        if (consumed || owner == null || !owner.IsAlive || other == null
            || EnemyController.IsRoomTraversalBlockingCombat)
            return;

        if (other.GetComponentInParent<EnemyController>() != null)
            return;

        PlayerCharacter playerCharacter = other.GetComponentInParent<PlayerCharacter>();
        if (playerCharacter == null)
            return;

        consumed = true;
        float damage = Mathf.Max(0f, owner.attackDamage);
        if (damage > 0f)
        {
            IncomingAttackContext context = IncomingAttackContext.FromEnemy(owner, EnemyAttackProperty.Normal);
            if (!playerCharacter.TryCounter(owner.gameObject) &&
                playerCharacter.TakeDamage(damage, context))
            {
                if (CameraManager.instance != null)
                    CameraManager.instance.ShakeByImpact(DamageImpactTier.Low);

                owner.SetInCombat(true);
            }
        }

        Destroy(gameObject);
    }
}
