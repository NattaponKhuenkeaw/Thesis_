using System.Collections.Generic;
using UnityEngine;

public class EnemyAttackHitbox : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyController enemyController;

    [Header("Settings")]
    [SerializeField] private bool triggerPlayerTakeDamageAnimation = true;

    private readonly HashSet<int> hitTargetsThisWindow = new HashSet<int>();
    private bool hitboxWindowActive;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        hitboxWindowActive = false;
        hitTargetsThisWindow.Clear();
    }

    public void SetHitboxWindowActive(bool active)
    {
        hitboxWindowActive = active;

        if (active)
        {
            hitTargetsThisWindow.Clear();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void CacheReferences()
    {
        if (enemyController == null)
            enemyController = GetComponentInParent<EnemyController>();
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!hitboxWindowActive || enemyController == null || !enemyController.IsAlive)
            return;

        PlayerCharacter playerCharacter = other.GetComponentInParent<PlayerCharacter>();
        if (playerCharacter == null)
            return;

        int targetId = playerCharacter.transform.root.gameObject.GetInstanceID();
        if (!hitTargetsThisWindow.Add(targetId))
            return;

        float damage = Mathf.Max(0f, enemyController.attackDamage);
        if (damage <= 0f)
            return;

        playerCharacter.TakeDamage(damage);

        if (!triggerPlayerTakeDamageAnimation)
            return;

        if (playerCharacter.Animator != null)
            playerCharacter.Animator.TriggerTakeDamage();
    }
}
