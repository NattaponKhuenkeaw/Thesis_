using UnityEngine;

/// <summary>
/// Attack-range checks shared by Behavior Graph conditions.
/// Uses <see cref="EnemyLocomotion"/> when present so fly/warp enemies are not X-only.
/// </summary>
public static class EnemyCombatRange
{
    public static bool IsInAttackRange(GameObject agent, GameObject target, float attackRange)
    {
        if (agent == null || target == null)
        {
            return false;
        }

        EnemyLocomotion locomotion = agent.GetComponent<EnemyLocomotion>();
        EnemyController controller = agent.GetComponent<EnemyController>();
        float range = attackRange;
        if (controller != null)
            range = controller.ResolveAttackRange(attackRange);

        if (controller != null && controller.CombatRole == EnemyCombatRole.ShieldGuard && controller.ShieldGuard != null)
            return controller.ShieldGuard.CanEngage(target.transform);

        if (locomotion != null)
        {
            return locomotion.IsInAttackRangeOfTarget(target.transform, range);
        }

        return EnemyFacingUtil.HorizontalDistance(agent.transform.position, target.transform.position) <= range;
    }

    /// <summary>
    /// Aim at the body, not the transform pivot (often the feet).
    /// </summary>
    public static Vector3 GetAimPoint(Transform target)
    {
        if (target == null)
            return Vector3.zero;

        Collider2D body = ResolveAimCollider(target);
        return body != null ? body.bounds.center : target.position;
    }

    private static Collider2D ResolveAimCollider(Transform target)
    {
        PlayerCharacter player = target.GetComponent<PlayerCharacter>()
            ?? target.GetComponentInParent<PlayerCharacter>();
        if (player != null)
        {
            Collider2D playerBody = player.GetComponent<Collider2D>();
            if (playerBody != null)
                return playerBody;
        }

        EnemyController enemy = target.GetComponent<EnemyController>()
            ?? target.GetComponentInParent<EnemyController>();
        if (enemy != null && enemy.bodyCollider != null)
            return enemy.bodyCollider;

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
                return colliders[i];
        }

        return colliders.Length > 0 ? colliders[0] : null;
    }
}
