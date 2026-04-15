using UnityEngine;

public static class ReactionResolver
{
    public static void ResolveAgainstTarget(EnemyController enemy, ref HitReactionData reactionData)
    {
        if (enemy == null)
        {
            return;
        }

        EnemyReactionState targetState = enemy.CurrentReactionState;
        switch (targetState)
        {
            case EnemyReactionState.Knockdown:
                reactionData.reactionKind = EnemyReactionKind.Knockup;
                reactionData.landingBehavior = EnemyLandingBehavior.Knockdown;
                reactionData.launchSpeed = Mathf.Max(reactionData.LaunchSpeed, 3f);
                break;

            case EnemyReactionState.Knockup:
            case EnemyReactionState.Knockfall:
                reactionData.reactionKind = EnemyReactionKind.Knockup;
                reactionData.landingBehavior = EnemyLandingBehavior.Knockdown;
                reactionData.canAirJuggle = true;
                reactionData.launchSpeed = Mathf.Max(reactionData.LaunchSpeed, 1f);
                break;

            default:
                if (reactionData.reactionKind == EnemyReactionKind.Knockup && reactionData.LaunchSpeed <= 0f)
                {
                    reactionData.reactionKind = EnemyReactionKind.Knockback;
                }

                break;
        }
    }
}
