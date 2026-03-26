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

        CharacterRuntime runtime = other.GetComponentInParent<CharacterRuntime>();
        if (runtime == null)
            return;

        if (runtime.GetComponentInParent<EnemyController>() != null)
            return;

        int targetId = runtime.transform.root.gameObject.GetInstanceID();
        if (!hitTargetsThisWindow.Add(targetId))
            return;

        float damage = Mathf.Max(0f, enemyController.attackDamage);
        if (damage <= 0f)
            return;

        runtime.TakeDamage(damage);

        if (!triggerPlayerTakeDamageAnimation)
            return;

        PlayerCharacter playerCharacter = runtime.GetComponent<PlayerCharacter>();
        if (playerCharacter == null)
            playerCharacter = runtime.GetComponentInParent<PlayerCharacter>();

        if (playerCharacter != null && playerCharacter.animationHandler != null)
            playerCharacter.animationHandler.TriggerTakeDamage();
    }
}
