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
            coreRequest.descriptor,
            coreRequest.execution?.cast);
        if (!coreRequest.hasCriticalRoll)
        {
            coreRequest.hasCriticalRoll = true;
            coreRequest.criticalRoll01 = Random.value;
        }
        ApplyActionCriticalRate(ref coreRequest);
        DamageResult outgoingResult = CombatDamageResolver.ResolveOutgoing(coreRequest);
        float outgoingDamage = ApplyConditionalOutgoingModifiers(coreRequest, outgoingResult);
        outgoingResult = new DamageResult(
            outgoingDamage,
            outgoingResult.IsCriticalHit,
            outgoingResult.Descriptor);
        ApplyActionDamageInput(ref coreRequest, outgoingResult.IsCriticalHit);
        ApplyTargetDamageInput(ref coreRequest, outgoingResult.IsCriticalHit);
        bool ignoreTypeReduction = coreRequest.ignoreTypeReduction;
        coreRequest.ignoreTypeReduction = true;
        DamageResult coreResult = CombatDamageResolver.ResolveMitigation(coreRequest, outgoingResult);
        float resolvedDamage = coreResult.UnroundedDamage;

        if (!ignoreTypeReduction && request.target != null && request.target.DamageTypeState != null)
        {
            resolvedDamage = request.target.DamageTypeState.ModifyIncomingDamage(
                request.descriptor.elementType,
                resolvedDamage);
        }
        else if (!ignoreTypeReduction && Mathf.Abs(request.typeReductionPercent) > Mathf.Epsilon)
        {
            float reduction = Mathf.Clamp(request.typeReductionPercent, -95f, 95f);
            resolvedDamage *= 1f - reduction / 100f;
        }

        return new DamageResult(resolvedDamage, coreResult.IsCriticalHit, coreResult.Descriptor);
    }

    private static void ApplyActionCriticalRate(ref DamageRequest request)
    {
        if (request.descriptor.delivery != DamageDelivery.Primary || request.sourceStats == null) return;
        float current = request.sourceStats.critRate;
        float resolved = request.sourcePassiveController != null
            ? request.sourcePassiveController.ModifyActionCriticalRate(request.category, request.actionId, request.actionName, request.stepData?.stepName, current)
            : DeployedCharacterDamageModifierEvaluator.ModifyCriticalRate(request.sourceCharacter, request.category, request.actionId, request.actionName, request.stepData?.stepName, current);
        if (Mathf.Approximately(current, resolved)) return;
        request.sourceStats = request.sourceStats.Clone();
        request.sourceStats.critRate = resolved;
    }

    private static void ApplyTargetDamageInput(ref DamageRequest request, bool isCriticalHit)
    {
        var receiver = request.target?.RuntimeEffects;
        if (receiver == null) return;
        var input = new RuntimeEffectDamageInput
        {
            context = new RuntimeEffectDamageContext { sourceCharacter = request.sourceCharacter,
                sourceStats = request.sourceStats, targetEnemy = request.target, category = request.category,
                actionId = request.actionId, actionName = request.actionName, stepData = request.stepData,
                elementType = request.descriptor.elementType, damageDelivery = request.descriptor.delivery,
                isCriticalHit = isCriticalHit },
            baseDamage = request.baseDamage, defenderDefense = request.defenderDefense,
            defenderDamageResistance = request.defenderDamageResistance, ignoreTypeReduction = request.ignoreTypeReduction
        };
        receiver.ModifyDamageInput(input);
        request.defenderDefense = input.defenderDefense;
        request.defenderDamageResistance = input.defenderDamageResistance;
        request.ignoreTypeReduction = input.ignoreTypeReduction;
    }

    private static void ApplyActionDamageInput(ref DamageRequest request, bool isCriticalHit)
    {
        if (request.descriptor.delivery != DamageDelivery.Primary) return;
        var input = new RuntimeEffectDamageInput
        {
            context = new RuntimeEffectDamageContext { isCriticalHit = isCriticalHit },
            defenderDefense = request.defenderDefense,
            defenderDamageResistance = request.defenderDamageResistance,
            ignoreTypeReduction = request.ignoreTypeReduction
        };
        if (request.sourcePassiveController != null)
            request.sourcePassiveController.ModifyActionDamageInput(request.category, request.actionId, request.actionName, request.stepData?.stepName, input);
        else
            DeployedCharacterDamageModifierEvaluator.ModifyDamageInput(request.sourceCharacter, request.category, request.actionId, request.actionName, request.stepData?.stepName, input);
        request.defenderDefense = input.defenderDefense;
        request.defenderDamageResistance = input.defenderDamageResistance;
        request.ignoreTypeReduction = input.ignoreTypeReduction;
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
        if (request.target == null || !request.target.IsTargetable)
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
        if (request.target == null || !request.target.IsTargetable)
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
            if (result.Descriptor.delivery == DamageDelivery.Primary)
                TeamLoader.Instance?.KitRuntimes?.OnPositivePrimaryHit(request, result, damageDealt);
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
                        execution = request.execution, provenance = request.provenance,
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
                    hitPosition,
                    request.execution);
                CombatEventHub.Shared.Publish(damageEvent);
            }
        }

        return damageDealt;
    }
}
