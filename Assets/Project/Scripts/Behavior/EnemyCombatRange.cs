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

        if (locomotion != null)
        {
            return locomotion.IsInAttackRangeOfTarget(target.transform, range);
        }

        return EnemyFacingUtil.HorizontalDistance(agent.transform.position, target.transform.position) <= range;
    }
}
