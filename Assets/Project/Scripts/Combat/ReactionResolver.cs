using UnityEngine;

public static class ReactionResolver
{
    private static readonly ReactionRuleTable.Rule[] DefaultRules =
    {
        ReactionRuleTable.CreateRule(
            EnemyReactionState.Knockdown,
            EnemyReactionKind.Knockup,
            EnemyLandingBehavior.Knockdown,
            true,
            3f,
            15, 12, 10, 9),
        ReactionRuleTable.CreateRule(
            EnemyReactionState.Knockfall,
            EnemyReactionKind.Knockup,
            EnemyLandingBehavior.Knockdown,
            true,
            1f,
            15, 12, 10, 9)
    };

    public static void ResolveAgainstTarget(EnemyController enemy, ref HitReactionData reactionData)
    {
        if (enemy == null || reactionData.reactionKind == EnemyReactionKind.Grab || reactionData.hasAuthoredReactionTarget)
        {
            return;
        }

        EnemyReactionState targetState = enemy.CurrentReactionState;
        if (TryGetRule(targetState, out ReactionRuleTable.Rule rule))
        {
            ApplyRule(ref reactionData, rule);
        }

        if (reactionData.reactionKind == EnemyReactionKind.Knockup && reactionData.LaunchSpeed <= 0f)
        {
            reactionData.reactionKind = EnemyReactionKind.Knockback;
        }
    }

    private static bool TryGetRule(EnemyReactionState targetState, out ReactionRuleTable.Rule rule)
    {
        ReactionRuleTable configuredTable = ReactionRuleTable.Load();
        if (configuredTable != null && configuredTable.TryGetRule(targetState, out rule))
        {
            return true;
        }

        for (int i = 0; i < DefaultRules.Length; i++)
        {
            if (DefaultRules[i].targetState == targetState)
            {
                rule = DefaultRules[i];
                return true;
            }
        }

        rule = default;
        return false;
    }

    private static void ApplyRule(ref HitReactionData reactionData, ReactionRuleTable.Rule rule)
    {
        reactionData.reactionKind = rule.reactionKind;
        reactionData.landingBehavior = rule.landingBehavior;
        reactionData.canAirJuggle = rule.canAirJuggle;
        reactionData.launchSpeed = Mathf.Max(reactionData.LaunchSpeed, rule.minimumLaunchSpeed);
        
        // Apply Frame-Data overrides
        reactionData.knockupFrames = rule.knockupFrames;
        reactionData.knockfallFrames = rule.knockfallFrames;
        
        // Update ActionFrameData timing
        int activeFrames = (rule.reactionKind == EnemyReactionKind.Knockup) ? rule.knockupFrames : rule.knockbackFrames;
        reactionData.reactionTiming = HitReactionData.CreateTiming(
            reactionData.ImpactFreezeFrames,
            activeFrames,
            rule.recoveryFrames);
    }
}
