using UnityEngine;

public enum DamageDelivery
{
    Primary = 0,
    Dot = 1,
    Additional = 2
}

public enum DamageCriticalPolicy
{
    Default = 0,
    Allow = 1,
    Prevent = 2
}

[System.Flags]
public enum DamageSideEffect
{
    None = 0,
    DamageApplied = 1 << 0,
    DotRequested = 1 << 1,
    OnHitDispatched = 1 << 2,
    AuraEnergyDispatched = 1 << 3
}

[System.Serializable]
public struct DamageDescriptor
{
    public DamageDelivery delivery;
    public ElementType elementType;
    public DamageCriticalPolicy criticalPolicy;

    public DamageDescriptor(
        DamageDelivery delivery,
        ElementType elementType,
        DamageCriticalPolicy criticalPolicy = DamageCriticalPolicy.Default)
    {
        this.delivery = delivery;
        this.elementType = elementType;
        this.criticalPolicy = criticalPolicy;
    }
}

public struct DamageRequest
{
    public EnemyController target;
    public CharacterObject sourceCharacter;
    public ArtifactRuntimeSnapshot sourceArtifact;
    public CharacterStats sourceStats;
    public PlayerCombat sourceCombat;
    public CharacterPassiveController sourcePassiveController;
    public AuraEnergyRewardClaim auraEnergyRewardClaim;
    public float baseDamage;
    public float defenderDefense;
    public float defenderDamageResistance;
    public float typeReductionPercent;
    public DamageDescriptor descriptor;
    public DamageProvenance provenance;
    public DamageProcPolicy procPolicy;
    public bool hasExplicitProcPolicy;
    public AttackCategory category;
    public string actionId;
    public string actionName;
    public AttackStepData stepData;
    public Vector2 attackerPosition;
    public Vector2 hitDirection;
    public bool ignoreDefense;
    public bool ignoreDamageResistance;
    public bool ignoreTypeReduction;
    public bool hasCriticalRoll;
    public float criticalRoll01;
    public float damageRatio;
    public float sourceStatValue;

    public DamageProcPolicy ResolveProcPolicy()
    {
        if (hasExplicitProcPolicy)
        {
            return procPolicy;
        }

        if (descriptor.delivery == DamageDelivery.Primary)
        {
            return DamageProcPolicy.PublishResolvedEvent
                | DamageProcPolicy.PublishKillEvent
                | DamageProcPolicy.TriggerOrdinaryOnHit
                | DamageProcPolicy.TriggerArtifactRules
                | DamageProcPolicy.GrantAuraEnergy
                | DamageProcPolicy.ApplyElementDot
                | DamageProcPolicy.ApplyHitReaction;
        }

        return DamageProcPolicy.PublishResolvedEvent
            | DamageProcPolicy.PublishKillEvent
            | DamageProcPolicy.ApplyHitReaction;
    }
}

public readonly struct DamageResult
{
    private readonly float unroundedDamage;

    public DamageResult(
        float damage,
        bool isCriticalHit,
        DamageDescriptor descriptor,
        DamageSideEffect acceptedSideEffects = DamageSideEffect.None)
    {
        unroundedDamage = Mathf.Max(0f, damage);
        IsCriticalHit = isCriticalHit;
        Descriptor = descriptor;
        AcceptedSideEffects = acceptedSideEffects;
    }

    /// <summary>Final combat damage, quantized exactly once after all formulas.</summary>
    public int Damage => Mathf.Max(0, Mathf.RoundToInt(unroundedDamage));
    internal float UnroundedDamage => unroundedDamage;
    public bool IsCriticalHit { get; }
    public DamageDescriptor Descriptor { get; }
    public DamageSideEffect AcceptedSideEffects { get; }
    public bool CanApplyDot => Damage > 0
        && Descriptor.elementType != ElementType.None
        && Descriptor.delivery != DamageDelivery.Dot;
}
