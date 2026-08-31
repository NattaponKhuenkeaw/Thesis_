using UnityEngine;

public static class CombatDamageProcessor
{
    public static DamageResult Apply(in DamageRequest request)
    {
        DamageResult result = Resolve(request);
        HitReactionData reactionData = HitReactionData.Default(
            result.Damage,
            request.hitDirection,
            0f);
        float damageDealt = ApplyResolved(request, result, reactionData, true);
        DamageProcPolicy policy = request.ResolveProcPolicy();
        DamageSideEffect sideEffects = damageDealt > 0f
            ? DamageSideEffect.DamageApplied
            : DamageSideEffect.None;
        if (damageDealt > 0f
            && result.CanApplyDot
            && (policy & DamageProcPolicy.ApplyElementDot) != 0)
        {
            sideEffects |= DamageSideEffect.DotRequested;
        }
        if (damageDealt > 0f
            && (policy & DamageProcPolicy.TriggerOrdinaryOnHit) != 0)
        {
            sideEffects |= DamageSideEffect.OnHitDispatched;
        }
        if (damageDealt > 0f
            && (policy & DamageProcPolicy.GrantAuraEnergy) != 0
            && request.sourceCombat != null
            && request.sourceCharacter != null)
        {
            sideEffects |= DamageSideEffect.AuraEnergyDispatched;
        }

        return new DamageResult(
            damageDealt,
            result.IsCriticalHit,
            result.Descriptor,
            sideEffects);
    }

    public static DamageResult Resolve(in DamageRequest request)
    {
        DamageRequest coreRequest = request;
        coreRequest.descriptor = ArtifactPassiveRuntimeSystem.ModifyOutgoingDescriptor(
            coreRequest.sourceCharacter,
            coreRequest.descriptor);
        if (!coreRequest.hasCriticalRoll)
        {
            coreRequest.hasCriticalRoll = true;
            coreRequest.criticalRoll01 = Random.value;
        }
        coreRequest.ignoreTypeReduction = true;
        DamageResult outgoingResult = CombatDamageResolver.ResolveOutgoing(coreRequest);
        float outgoingDamage = ApplyConditionalOutgoingModifiers(coreRequest, outgoingResult);
        outgoingResult = new DamageResult(
            outgoingDamage,
            outgoingResult.IsCriticalHit,
            outgoingResult.Descriptor);
        DamageResult coreResult = CombatDamageResolver.ResolveMitigation(coreRequest, outgoingResult);
        float resolvedDamage = coreResult.UnroundedDamage;

        if (!request.ignoreTypeReduction && request.target != null && request.target.DamageTypeState != null)
        {
            resolvedDamage = request.target.DamageTypeState.ModifyIncomingDamage(
                request.descriptor.elementType,
                resolvedDamage);
        }
        else if (!request.ignoreTypeReduction && Mathf.Abs(request.typeReductionPercent) > Mathf.Epsilon)
        {
            float reduction = Mathf.Clamp(request.typeReductionPercent, -95f, 95f);
            resolvedDamage *= 1f - reduction / 100f;
        }

        return new DamageResult(resolvedDamage, coreResult.IsCriticalHit, coreResult.Descriptor);
    }

    private static float ApplyConditionalOutgoingModifiers(
        in DamageRequest request,
        in DamageResult outgoingResult)
    {
        float damage = outgoingResult.UnroundedDamage;
        RuntimeEffectDamageContext context = new RuntimeEffectDamageContext
        {
            sourceCharacter = request.sourceCharacter,
            sourceStats = request.sourceStats,
            targetEnemy = request.target,
            category = request.category,
            actionId = request.actionId,
            actionName = request.actionName,
            stepData = request.stepData,
            elementType = outgoingResult.Descriptor.elementType,
            damageDelivery = outgoingResult.Descriptor.delivery,
            isCriticalHit = outgoingResult.IsCriticalHit,
            damage = damage,
            attackerPosition = request.attackerPosition,
            hitDirection = request.hitDirection
        };

        if (request.sourceCombat != null && request.sourceCharacter != null)
        {
            damage = request.sourceCombat.ModifyRuntimeOutgoingDamage(
                request.sourceCharacter,
                context,
                damage);
        }

        if (outgoingResult.Descriptor.delivery != DamageDelivery.Primary)
        {
            return Mathf.Max(0f, damage);
        }

        if (request.sourcePassiveController != null)
        {
            damage = request.sourcePassiveController.ModifyOutgoingDamage(context, damage);
            damage = request.sourcePassiveController.ModifyActionFinalDamage(
                request.category,
                request.actionId,
                request.actionName,
                damage,
                outgoingResult.IsCriticalHit);
        }
        else
        {
            damage = DeployedCharacterDamageModifierEvaluator.ModifyOutgoingDamage(
                request.sourceCharacter,
                context,
                damage);
            damage = DeployedCharacterDamageModifierEvaluator.ModifyFinalDamage(
                request.sourceCharacter,
                request.category,
                request.actionId,
                request.actionName,
                damage,
                outgoingResult.IsCriticalHit);
        }

        return Mathf.Max(0f, damage);
    }

    public static float Apply(
        in DamageRequest request,
        HitReactionData reactionData,
        bool applyElementDot = true)
    {
        if (request.target == null || !request.target.IsAlive)
        {
            return 0f;
        }

        DamageResult result = Resolve(request);
        return ApplyResolved(request, result, reactionData, applyElementDot);
    }

    public static float ApplyResolved(
        in DamageRequest request,
        in DamageResult result,
        HitReactionData reactionData,
        bool applyElementDot = true)
    {
        if (request.target == null || !request.target.IsAlive)
        {
            return 0f;
        }

        if (request.provenance.procDepth > CombatEventHub.MaximumProcDepth)
        {
            Debug.LogWarning("CombatDamageProcessor rejected damage above the maximum proc depth.");
            return 0f;
        }

        DamageProcPolicy policy = request.ResolveProcPolicy();

        reactionData.damage = result.Damage;
        reactionData.isCriticalHit = result.IsCriticalHit;
        reactionData.elementType = result.Descriptor.elementType;
        reactionData.damageDelivery = result.Descriptor.delivery;
        reactionData.sourceCharacter = request.sourceCharacter;
        reactionData.sourceStats = request.sourceStats;
        reactionData.sourceCategory = request.category;
        reactionData.sourceActionId = request.actionId;
        reactionData.sourceActionName = request.actionName;
        reactionData.sourceStepData = request.stepData;
        reactionData.damageRatio = request.damageRatio;
        reactionData.sourceStatValue = request.sourceStatValue;

        float damageDealt = request.target.TakeResolvedHit(reactionData, request.attackerPosition);
        if (damageDealt > 0f
            && applyElementDot
            && result.CanApplyDot
            && (policy & DamageProcPolicy.ApplyElementDot) != 0)
        {
            request.target.DamageTypeState?.ApplyDot(
                result.Descriptor.elementType,
                request.sourceStats,
                request.sourceCharacter);
        }

        if (damageDealt > 0f)
        {
            bool killedEnemy = !request.target.IsAlive;
            if ((policy & DamageProcPolicy.TriggerOrdinaryOnHit) != 0)
            {
                request.sourcePassiveController?.NotifyHitDealt(
                    request.category,
                    request.actionId,
                    request.actionName,
                    request.stepData,
                    request.target,
                    result.Descriptor.elementType,
                    result.IsCriticalHit,
                    damageDealt,
                    killedEnemy);

                if (request.sourceCombat != null && request.sourceCharacter != null)
                {
                    RuntimeEffectDamageContext hitContext = new RuntimeEffectDamageContext
                    {
                        sourceCharacter = request.sourceCharacter,
                        sourceStats = request.sourceStats,
                        targetEnemy = request.target,
                        category = request.category,
                        actionId = request.actionId,
                        actionName = request.actionName,
                        stepData = request.stepData,
                        elementType = result.Descriptor.elementType,
                        damageDelivery = result.Descriptor.delivery,
                        isCriticalHit = result.IsCriticalHit,
                        damage = damageDealt,
                        damageDealt = damageDealt,
                        killedEnemy = killedEnemy,
                        attackerPosition = request.attackerPosition,
                        hitDirection = request.hitDirection
                    };
                    request.sourceCombat.NotifyRuntimeHitDealt(request.sourceCharacter, hitContext);
                }
            }

            if ((policy & DamageProcPolicy.GrantAuraEnergy) != 0
                && request.sourceCombat != null
                && request.sourceCharacter != null)
            {
                request.sourceCombat.NotifyResolvedEnemyHit(
                    request.sourceCharacter,
                    request.auraEnergyRewardClaim);
            }

            if ((policy & DamageProcPolicy.PublishResolvedEvent) != 0)
            {
                DamageProvenance provenance = CombatEventHub.Shared.AssignDamageIdentity(request.provenance);
                Vector2 hitPosition = request.target != null
                    ? (Vector2)request.target.transform.position
                    : request.attackerPosition;
                DamageResolvedEvent damageEvent = new DamageResolvedEvent(
                    provenance,
                    result.Descriptor,
                    policy,
                    request.sourceCharacter,
                    request.sourceArtifact,
                    request.target,
                    request.category,
                    request.actionId,
                    request.baseDamage,
                    damageDealt,
                    result.IsCriticalHit,
                    killedEnemy,
                    hitPosition);
                CombatEventHub.Shared.Publish(damageEvent);
            }
        }

        return damageDealt;
    }
}
