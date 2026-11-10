using UnityEngine;

public static class CombatDamageResolver
{
    public static DamageResult Resolve(in DamageRequest request)
    {
        DamageResult outgoing = ResolveOutgoing(request);
        return ResolveMitigation(request, outgoing);
    }

    public static DamageResult ResolveOutgoing(in DamageRequest request)
    {
        CharacterStats stats = request.sourceStats;
        float damage = Mathf.Max(0f, request.baseDamage);
        if (request.skipSourceDamageScaling) return new DamageResult(damage, false, request.descriptor);

        if (request.descriptor.delivery == DamageDelivery.Primary)
        {
            damage *= PercentMultiplier(stats != null ? stats.damageIncrease : 0f);
        }
        else if (request.descriptor.delivery == DamageDelivery.Additional)
        {
            damage *= PercentMultiplier(stats != null ? stats.additionalDamageIncrease : 0f);
        }

        if (request.descriptor.elementType != ElementType.None)
        {
            damage *= PercentMultiplier(stats != null ? stats.dotMastery : 0f);
        }

        bool canCrit = ResolveCanCrit(request.descriptor);
        float critRate = stats != null ? Mathf.Clamp(stats.critRate, 0f, 100f) : 0f;
        float criticalRoll01 = request.hasCriticalRoll ? Mathf.Clamp01(request.criticalRoll01) : 1f;
        bool isCriticalHit = canCrit && critRate > 0f && criticalRoll01 < critRate / 100f;
        if (isCriticalHit)
        {
            float critDamage = stats != null ? Mathf.Max(0f, stats.critDamage) : 50f;
            damage *= 1f + critDamage / 100f;
        }

        return new DamageResult(Mathf.Max(0f, damage), isCriticalHit, request.descriptor);
    }

    public static DamageResult ResolveMitigation(
        in DamageRequest request,
        in DamageResult outgoing)
    {
        float damage = outgoing.UnroundedDamage;
        // QTE entry damage retains its DEF bypass on projectiles after the entry ends.
        bool ignoreDefense = request.ignoreDefense || request.execution?.isQteIntro == true;
        float defense = ignoreDefense ? 0f : Mathf.Max(0f, request.defenderDefense);
        float resistance = request.ignoreDamageResistance
            ? 0f
            : request.defenderDamageResistance;

        switch (request.descriptor.delivery)
        {
            case DamageDelivery.Primary:
                damage = CombatFormula.CalculateIncomingDamage(damage, defense, resistance);
                break;
            case DamageDelivery.Dot:
                damage = CombatFormula.CalculateIncomingDamage(damage, 0f, resistance);
                break;
            case DamageDelivery.Additional:
                break;
        }

        if (!request.ignoreTypeReduction)
        {
            float reduction = Mathf.Clamp(request.typeReductionPercent, -95f, 95f);
            damage *= 1f - reduction / 100f;
        }

        return new DamageResult(
            Mathf.Max(0f, damage),
            outgoing.IsCriticalHit,
            outgoing.Descriptor);
    }

    public static bool ResolveCanCrit(DamageDescriptor descriptor)
    {
        if (descriptor.criticalPolicy == DamageCriticalPolicy.Allow)
        {
            return true;
        }

        if (descriptor.criticalPolicy == DamageCriticalPolicy.Prevent)
        {
            return false;
        }

        return descriptor.delivery == DamageDelivery.Primary;
    }

    private static float PercentMultiplier(float percentagePoints)
    {
        return Mathf.Max(0f, 1f + percentagePoints / 100f);
    }
}
