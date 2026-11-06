using System;
using UnityEngine;

public enum ArtifactPassiveTrigger
{
    ActionStarted = 0,
    DamageResolved = 1,
    CriticalHitResolved = 2,
    CharacterSwitchCompleted = 3,
    CharacterHealthChanged = 4,
    IntervalElapsed = 5
}

[Flags]
public enum CombatDamageTag
{
    None = 0,
    Artifact = 1 << 0,
    Particle = 1 << 1,
    Explosion = 1 << 2,
    Splash = 1 << 3,
    DotApplication = 1 << 4,
    DotTick = 1 << 5
}

[Flags]
public enum DamageProcPolicy
{
    None = 0,
    PublishResolvedEvent = 1 << 0,
    PublishKillEvent = 1 << 1,
    TriggerOrdinaryOnHit = 1 << 2,
    TriggerArtifactRules = 1 << 3,
    GrantAuraEnergy = 1 << 4,
    ApplyElementDot = 1 << 5,
    ApplyHitReaction = 1 << 6
}

public readonly struct DamageProvenance
{
    public readonly ulong eventId;
    public readonly ulong rootEventId;
    public readonly ulong parentEventId;
    public readonly byte procDepth;
    public readonly string sourceArtifactInstanceId;
    public readonly CombatDamageTag tags;

    public DamageProvenance(ulong eventId, ulong rootEventId, ulong parentEventId, byte procDepth,
        string sourceArtifactInstanceId, CombatDamageTag tags)
    {
        this.eventId = eventId;
        this.rootEventId = rootEventId;
        this.parentEventId = parentEventId;
        this.procDepth = procDepth;
        this.sourceArtifactInstanceId = sourceArtifactInstanceId;
        this.tags = tags;
    }

    public DamageProvenance WithIdentity(ulong assignedEventId)
    {
        return new DamageProvenance(assignedEventId, rootEventId != 0 ? rootEventId : assignedEventId,
            parentEventId, procDepth, sourceArtifactInstanceId, tags);
    }
}

public readonly struct ActionStartedEvent
{
    public readonly CharacterObject sourceCharacter;
    public readonly AttackCategory category;
    public readonly string actionId;
    public readonly string actionName;
    public readonly AttackStepData stepData;
    public readonly CombatExecutionContext execution;

    public ActionStartedEvent(CharacterObject sourceCharacter, AttackCategory category, string actionId,
        string actionName, AttackStepData stepData, CombatExecutionContext execution = null)
    {
        this.sourceCharacter = sourceCharacter;
        this.category = category;
        this.actionId = actionId;
        this.actionName = actionName;
        this.stepData = stepData;
        this.execution = execution;
    }
}

public readonly struct CharacterSwitchCompletedEvent
{
    public readonly CharacterObject outgoingCharacter;
    public readonly CharacterObject incomingCharacter;
    public readonly int outgoingSlotIndex;
    public readonly int incomingSlotIndex;
    public readonly Vector2 worldPosition;

    public CharacterSwitchCompletedEvent(CharacterObject outgoingCharacter, CharacterObject incomingCharacter,
        int outgoingSlotIndex, int incomingSlotIndex, Vector2 worldPosition)
    {
        this.outgoingCharacter = outgoingCharacter;
        this.incomingCharacter = incomingCharacter;
        this.outgoingSlotIndex = outgoingSlotIndex;
        this.incomingSlotIndex = incomingSlotIndex;
        this.worldPosition = worldPosition;
    }
}

public readonly struct CharacterHealthChangedEvent
{
    public readonly CharacterObject character;
    public readonly float currentHealth;
    public readonly float maximumHealth;

    public CharacterHealthChangedEvent(CharacterObject character, float currentHealth, float maximumHealth)
    {
        this.character = character;
        this.currentHealth = currentHealth;
        this.maximumHealth = maximumHealth;
    }

    public float NormalizedHealth => maximumHealth > Mathf.Epsilon
        ? Mathf.Clamp01(currentHealth / maximumHealth)
        : 0f;
}

public readonly struct DamageResolvedEvent
{
    public readonly CombatExecutionContext execution;
    public readonly DamageProvenance provenance;
    public readonly DamageDescriptor descriptor;
    public readonly DamageProcPolicy acceptedPolicy;
    public readonly CharacterObject sourceCharacter;
    public readonly ArtifactRuntimeSnapshot sourceArtifact;
    public readonly EnemyController target;
    public readonly AttackCategory category;
    public readonly string actionId;
    public readonly float baseDamage;
    public readonly float damageDealt;
    public readonly bool isCriticalHit;
    public readonly bool killedTarget;
    public readonly Vector2 hitPosition;

    public DamageResolvedEvent(DamageProvenance provenance, DamageDescriptor descriptor,
        DamageProcPolicy acceptedPolicy, CharacterObject sourceCharacter,
        ArtifactRuntimeSnapshot sourceArtifact, EnemyController target, AttackCategory category,
        string actionId, float baseDamage, float damageDealt, bool isCriticalHit, bool killedTarget,
        Vector2 hitPosition, CombatExecutionContext execution = null)
    {
        this.execution = execution;
        this.provenance = provenance;
        this.descriptor = descriptor;
        this.acceptedPolicy = acceptedPolicy;
        this.sourceCharacter = sourceCharacter;
        this.sourceArtifact = sourceArtifact;
        this.target = target;
        this.category = category;
        this.actionId = actionId;
        this.baseDamage = baseDamage;
        this.damageDealt = damageDealt;
        this.isCriticalHit = isCriticalHit;
        this.killedTarget = killedTarget;
        this.hitPosition = hitPosition;
    }
}

