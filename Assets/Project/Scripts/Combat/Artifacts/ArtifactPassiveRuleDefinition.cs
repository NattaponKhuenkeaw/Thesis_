using System;
using System.Collections.Generic;

[Serializable]
public sealed class ArtifactPassiveRuleDefinition
{
    public string ruleId;
    public string stateKey;
    public ArtifactPassiveTrigger trigger;
    public CombatEventFilter filter = new CombatEventFilter();
    public ArtifactPassiveConditionSet conditions = new ArtifactPassiveConditionSet();
    [UnityEngine.SerializeReference]
    public List<ArtifactPassiveResponseDefinition> responses = new List<ArtifactPassiveResponseDefinition>();
    public ArtifactPassiveFeedbackDefinition feedback = new ArtifactPassiveFeedbackDefinition();
}

[Serializable]
public sealed class CombatEventFilter
{
    public bool filterByCategory;
    public AttackCategory category = AttackCategory.Normal;
    public bool filterByDelivery;
    public DamageDelivery delivery = DamageDelivery.Primary;
    public CombatDamageTag requiredDamageTags;
    public CombatDamageTag forbiddenDamageTags;
    public bool requirePositiveDamage = true;
    public bool requireCriticalHit;
    public bool requireNonCriticalHit;
    public bool requireWearerAsSource = true;
    public ArtifactSwitchRelation switchRelation;
}

public enum ArtifactSwitchRelation
{
    Any = 0,
    IncomingIsWearer = 1,
    OutgoingIsWearer = 2
}

[Serializable]
public sealed class ArtifactPassiveConditionSet
{
    public float cooldownSeconds;
    public float durationSeconds;
    public float intervalSeconds;
    public float chance = 1f;
    public int maximumStacks = 1;
    public float minimumHealthRatio;
    public float maximumHealthRatio = 1f;
    public bool minimumHealthExclusive;
    public bool maximumHealthExclusive;
    public bool requireArmed;
    public bool requireDisarmed;
    public bool oncePerRootDamageEvent;
    public bool requireStoredTarget;
}

[Serializable]
public sealed class ArtifactPassiveFeedbackDefinition
{
    public string feedbackId;
    public bool showOwnerPulse = true;
    public bool showStatusIcon = true;
    public UnityEngine.GameObject visualPrefab;
    public UnityEngine.AudioClip audioClip;
}
