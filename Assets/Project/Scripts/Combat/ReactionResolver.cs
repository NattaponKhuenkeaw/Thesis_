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
        if (enemy == null)
        {
            return;
        }

        // Grab is a capture, not a juggle follow-up. Knockup/Knockfall/Knockdown rules
        // exist to keep airborne combos launching; they must not rewrite an authored grab
        // (Main ult uses a NormalHitbox with reactionKind Grab).
        if (reactionData.reactionKind == EnemyReactionKind.Grab)
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

    public static void NormalizeForExecution(ref HitReactionData data)
    {
        if (data.reactionTiming == null)
        {
            int recoveryFrames = data.reactionKind == EnemyReactionKind.Grab ? 6 : 9;
            data.reactionTiming = HitReactionData.CreateTiming(data.hitstunFrames, data.knockbackFrames, recoveryFrames);
        }

        if (data.reactionKind == EnemyReactionKind.Flinch &&
            (data.isKnockdown || data.reactionVelocity != Vector2.zero || data.horizontalSpeed > 0f || data.launchSpeed > 0f))
        {
            data.reactionKind = HitReactionData.InferLegacyReactionKind(data.reactionVelocity, data.isKnockdown);
        }

        if (data.reactionKind == EnemyReactionKind.Grab)
        {
            if (data.grabSocket == null && data.attackerTransform != null)
            {
                data.grabSocket = data.attackerTransform;
            }

            if (data.reactionTiming == null || data.reactionTiming.TotalFrames <= 0)
            {
                data.reactionTiming = HitReactionData.CreateTiming(4, 24, 6);
            }
        }

        if (data.reactionOwnerId == 0 && data.attackerTransform != null)
        {
            data.reactionOwnerId = data.attackerTransform.root.GetInstanceID();
        }

        // Preserve legacy defaults while preventing exponential growth on follow-up hits.
        data.juggleLaunchScale = data.juggleLaunchScale <= 0f ? 1f : Mathf.Min(data.juggleLaunchScale, 1f);
        data.juggleStunScale = data.juggleStunScale <= 0f ? 1f : Mathf.Min(data.juggleStunScale, 1f);
    }

    public static void ApplyAirJuggle(ref HitReactionData data, ref int currentJuggleCount)
    {
        if (!data.canAirJuggle)
        {
            data.launchSpeed = 0f;
            data.horizontalSpeed *= 0.5f;
            data.canAirJuggle = false;
            data.knockupFrames = 0;
            data.reactionTiming = HitReactionData.CreateTiming(
                data.ImpactFreezeFrames,
                0,
                data.RecoveryFrames);
            return;
        }

        float launchScale = Mathf.Pow(data.juggleLaunchScale, currentJuggleCount);
        float stunScale = Mathf.Pow(data.juggleStunScale, currentJuggleCount);

        data.launchSpeed = Mathf.Max(0f, data.LaunchSpeed * launchScale);
        data.horizontalSpeed = Mathf.Max(0f, data.HorizontalSpeed * Mathf.Lerp(1f, launchScale, 0.5f));

        int scaledKnockupFrames = Mathf.Max(1, Mathf.RoundToInt(data.knockupFrames * stunScale));
        int scaledKnockfallFrames = Mathf.Max(1, Mathf.RoundToInt(data.knockfallFrames * stunScale));
        int scaledRecoveryFrames = Mathf.Max(0, Mathf.RoundToInt(data.RecoveryFrames * stunScale));

        // The executor reads the concrete airborne frame fields as well as the timing object.
        data.knockupFrames = scaledKnockupFrames;
        data.knockfallFrames = scaledKnockfallFrames;
        data.reactionTiming = HitReactionData.CreateTiming(
            data.ImpactFreezeFrames,
            scaledKnockupFrames,
            scaledRecoveryFrames);

        currentJuggleCount++;
    }

    public static void ResolveAcceptedHitRecovery(
        ref HitReactionData data,
        float now,
        float lightHitStreakWindow,
        ref int consecutiveLightHits,
        ref float lastLightHitTime)
    {
        // Count only accepted reactions; absorbed hits and re-grabs must not advance the streak.
        if (data.reactionKind == EnemyReactionKind.Grab || data.textImpactTier > DamageImpactTier.Low)
        {
            consecutiveLightHits = 0;
        }
        else
        {
            if (now - lastLightHitTime > lightHitStreakWindow)
                consecutiveLightHits = 0;

            consecutiveLightHits++;
            lastLightHitTime = now;
        }

        data.landingBehavior = ResolveRecovery(data, consecutiveLightHits);
    }

    public static EnemyLandingBehavior ResolveRecovery(in HitReactionData data, int consecutiveLightHits)
    {
        // Preserve the existing finish rules, including using the attack tier, not combo shake tier.
        // These rules also took precedence over authored landingBehavior in the original executor.
        bool knockdown = data.reactionKind == EnemyReactionKind.Grab ||
                         data.textImpactTier > DamageImpactTier.Low ||
                         consecutiveLightHits >= 2;
        return knockdown ? EnemyLandingBehavior.Knockdown : EnemyLandingBehavior.Recover;
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
