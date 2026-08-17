using System;
using System.Collections.Generic;
using Project.ShopSystem;
using UnityEngine;

/// <summary>Run-scoped authority for deployed Artifact passive instances.</summary>
[DisallowMultipleComponent]
public sealed class ArtifactPassiveRuntimeSystem : MonoBehaviour
{
    private readonly struct InfusionState
    {
        public readonly ElementType elementType;
        public readonly float expiresAt;

        public InfusionState(ElementType elementType, float expiresAt)
        {
            this.elementType = elementType;
            this.expiresAt = expiresAt;
        }
    }

    private static ArtifactPassiveRuntimeSystem _instance;

    private readonly List<ArtifactPassiveRuntime> _runtimes = new List<ArtifactPassiveRuntime>();
    private readonly Dictionary<string, ArtifactPassiveRuntime> _runtimesByInstance =
        new Dictionary<string, ArtifactPassiveRuntime>(StringComparer.Ordinal);
    private readonly Dictionary<CharacterObject, InfusionState> _infusions =
        new Dictionary<CharacterObject, InfusionState>();
    private readonly List<string> _staleInstanceIds = new List<string>();
    private readonly List<EnemyController> _roomEnemies = new List<EnemyController>();
    private TeamLoader _teamLoader;
    private ParticleBehaviorPool _particlePool;
    private bool _subscribed;
    [Header("Diagnostics")]
    [SerializeField] private bool enableDebugLogs = true;
    private static bool s_globalDebugLogs = true;

    public static ArtifactPassiveRuntimeSystem Instance => _instance;
    public static bool GlobalDebugLogs { get => s_globalDebugLogs; set => s_globalDebugLogs = value; }
    public bool EnableDebugLogs { get => enableDebugLogs; set => enableDebugLogs = value; }
    public bool IsLoggingEnabled => enableDebugLogs && s_globalDebugLogs;
    public IReadOnlyList<ArtifactPassiveRuntime> ActiveRuntimes => _runtimes;
    public IReadOnlyDictionary<string, ArtifactPassiveRuntime> RuntimesByInstance => _runtimesByInstance;
    public int ActiveInfusionCount => _infusions.Count;

    public void Initialize(TeamLoader teamLoader)
    {
        _instance = this;
        if (_particlePool == null)
        {
            _particlePool = GetComponent<ParticleBehaviorPool>();
            if (_particlePool == null) _particlePool = gameObject.AddComponent<ParticleBehaviorPool>();
            _particlePool.Initialize(teamLoader);
        }
        if (_teamLoader != teamLoader)
        {
            if (_teamLoader != null) _teamLoader.OnTeamChanged -= ReconcileDeployment;
            _teamLoader = teamLoader;
            if (_teamLoader != null) _teamLoader.OnTeamChanged += ReconcileDeployment;
        }

        Subscribe();
        ReconcileDeployment();

        if (IsLoggingEnabled)
        {
            Debug.Log($"[ArtifactPassiveRuntimeSystem] Initialized with TeamLoader. Registered passives: {_runtimes.Count}");
        }
    }

    private void OnDestroy()
    {
        if (_teamLoader != null) _teamLoader.OnTeamChanged -= ReconcileDeployment;
        Unsubscribe();
        if (_instance == this) _instance = null;
    }

    public static DamageDescriptor ModifyOutgoingDescriptor(CharacterObject sourceCharacter,
        DamageDescriptor descriptor)
    {
        ArtifactPassiveRuntimeSystem system = _instance;
        if (system == null || sourceCharacter == null) return descriptor;
        if (!system._infusions.TryGetValue(sourceCharacter, out InfusionState infusion)) return descriptor;
        if (Time.time >= infusion.expiresAt)
        {
            system._infusions.Remove(sourceCharacter);
            return descriptor;
        }

        descriptor.elementType = infusion.elementType;
        return descriptor;
    }

    public void ReconcileDeployment()
    {
        if (_teamLoader == null) return;

        _staleInstanceIds.Clear();
        foreach (string instanceId in _runtimesByInstance.Keys) _staleInstanceIds.Add(instanceId);

        CharacterObject[] team = _teamLoader.GetTeamSnapshot();
        for (int teamIndex = 0; teamIndex < team.Length; teamIndex++)
        {
            CharacterObject wearer = team[teamIndex];
            if (wearer == null || !_teamLoader.TryGetDeployedCharacterRuntime(wearer,
                    out DeployedCharacterRuntime deployed))
            {
                continue;
            }

            IReadOnlyList<ArtifactRuntimeSnapshot> artifacts = deployed.EquippedArtifacts;
            for (int artifactIndex = 0; artifactIndex < artifacts.Count; artifactIndex++)
            {
                ArtifactRuntimeSnapshot snapshot = artifacts[artifactIndex];
                if (snapshot?.Definition?.passiveSet == null || string.IsNullOrWhiteSpace(snapshot.InstanceId))
                {
                    continue;
                }

                _staleInstanceIds.Remove(snapshot.InstanceId);
                if (_runtimesByInstance.TryGetValue(snapshot.InstanceId, out ArtifactPassiveRuntime existing)
                    && ReferenceEquals(existing.Wearer, wearer)
                    && ReferenceEquals(existing.Artifact.Definition, snapshot.Definition))
                {
                    continue;
                }

                if (existing != null) RemoveRuntime(existing);
                AddRuntime(new ArtifactPassiveRuntime(wearer, snapshot));
            }
        }

        for (int i = 0; i < _staleInstanceIds.Count; i++)
        {
            if (_runtimesByInstance.TryGetValue(_staleInstanceIds[i], out ArtifactPassiveRuntime runtime))
            {
                RemoveRuntime(runtime);
            }
        }

        if (IsLoggingEnabled && _teamLoader != null)
        {
            Debug.Log($"[ArtifactPassiveRuntimeSystem] ReconcileDeployment complete. Active artifact passives: {_runtimes.Count}");
        }
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        CombatEventHub.Shared.ActionStarted += HandleActionStarted;
        CombatEventHub.Shared.DamageResolved += HandleDamageResolved;
        CombatEventHub.Shared.CharacterSwitchCompleted += HandleSwitchCompleted;
        CombatEventHub.Shared.CharacterHealthChanged += HandleHealthChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        CombatEventHub.Shared.ActionStarted -= HandleActionStarted;
        CombatEventHub.Shared.DamageResolved -= HandleDamageResolved;
        CombatEventHub.Shared.CharacterSwitchCompleted -= HandleSwitchCompleted;
        CombatEventHub.Shared.CharacterHealthChanged -= HandleHealthChanged;
        _subscribed = false;
    }

    private void HandleActionStarted(ActionStartedEvent combatEvent)
    {
        for (int runtimeIndex = 0; runtimeIndex < _runtimes.Count; runtimeIndex++)
        {
            ArtifactPassiveRuntime runtime = _runtimes[runtimeIndex];
            if (!ReferenceEquals(runtime.Wearer, combatEvent.sourceCharacter)) continue;
            EvaluateActionRules(runtime, combatEvent);
        }
    }

    private void HandleDamageResolved(DamageResolvedEvent combatEvent)
    {
        if ((combatEvent.acceptedPolicy & DamageProcPolicy.TriggerArtifactRules) == 0) return;
        for (int runtimeIndex = 0; runtimeIndex < _runtimes.Count; runtimeIndex++)
        {
            ArtifactPassiveRuntime runtime = _runtimes[runtimeIndex];
            if (!ReferenceEquals(runtime.Wearer, combatEvent.sourceCharacter)) continue;
            bool wasArmed = HasArmedState(runtime);
            EvaluateDamageRules(runtime, combatEvent, ArtifactPassiveTrigger.DamageResolved);
            bool consumedArmedState = wasArmed && !HasArmedState(runtime);
            if (combatEvent.isCriticalHit && !consumedArmedState)
            {
                EvaluateDamageRules(runtime, combatEvent, ArtifactPassiveTrigger.CriticalHitResolved);
            }
        }
    }

    private void HandleSwitchCompleted(CharacterSwitchCompletedEvent combatEvent)
    {
        for (int runtimeIndex = 0; runtimeIndex < _runtimes.Count; runtimeIndex++)
        {
            ArtifactPassiveRuntime runtime = _runtimes[runtimeIndex];
            List<ArtifactPassiveRuleDefinition> rules = runtime.Artifact.Definition.passiveSet.rules;
            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                ArtifactPassiveRuleDefinition rule = rules[ruleIndex];
                if (rule.trigger != ArtifactPassiveTrigger.CharacterSwitchCompleted
                    || !MatchesSwitch(rule.filter, runtime.Wearer, combatEvent)
                    || !CanActivate(runtime.Rules[ruleIndex], rule.conditions)) continue;
                Activate(runtime, rule, ruleIndex, null, combatEvent.incomingCharacter);
            }
        }
    }

    private bool _isEvaluatingHealthChanged;

    private void HandleHealthChanged(CharacterHealthChangedEvent combatEvent)
    {
        if (_isEvaluatingHealthChanged) return;
        _isEvaluatingHealthChanged = true;
        try
        {
            float ratio = combatEvent.NormalizedHealth;
            for (int runtimeIndex = 0; runtimeIndex < _runtimes.Count; runtimeIndex++)
            {
                ArtifactPassiveRuntime runtime = _runtimes[runtimeIndex];
                if (!ReferenceEquals(runtime.Wearer, combatEvent.character)) continue;
                List<ArtifactPassiveRuleDefinition> rules = runtime.Artifact.Definition.passiveSet.rules;
                for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
                {
                    ArtifactPassiveRuleDefinition rule = rules[ruleIndex];
                    if (rule.trigger != ArtifactPassiveTrigger.CharacterHealthChanged) continue;
                    ArtifactRuleRuntimeState state = runtime.Rules[ruleIndex];

                    bool shouldBeActive = MatchesHealth(rule.conditions, ratio);
                    if (shouldBeActive && !state.active)
                    {
                        state.active = true;
                        Activate(runtime, rule, ruleIndex, null, null);
                    }
                    else if (!shouldBeActive && state.active)
                    {
                        state.active = false;
                        RemoveRuleRuntimeEffects(runtime, rule);
                    }
                }
            }
        }
        finally
        {
            _isEvaluatingHealthChanged = false;
        }
    }

    private void EvaluateActionRules(ArtifactPassiveRuntime runtime, in ActionStartedEvent combatEvent)
    {
        List<ArtifactPassiveRuleDefinition> rules = runtime.Artifact.Definition.passiveSet.rules;
        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            ArtifactPassiveRuleDefinition rule = rules[ruleIndex];
            if (rule.trigger != ArtifactPassiveTrigger.ActionStarted
                || (rule.filter.filterByCategory && rule.filter.category != combatEvent.category)
                || !CanActivate(runtime.Rules[ruleIndex], rule.conditions)) continue;
            Activate(runtime, rule, ruleIndex, null, null);
        }
    }

    private void EvaluateDamageRules(ArtifactPassiveRuntime runtime, in DamageResolvedEvent combatEvent,
        ArtifactPassiveTrigger trigger)
    {
        List<ArtifactPassiveRuleDefinition> rules = runtime.Artifact.Definition.passiveSet.rules;
        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            ArtifactPassiveRuleDefinition rule = rules[ruleIndex];
            ArtifactRuleRuntimeState state = runtime.Rules[ruleIndex];
            if (rule.trigger != trigger || !MatchesDamage(rule.filter, combatEvent)
                || !CanActivate(state, rule.conditions)) continue;
            if (rule.conditions.requireStoredTarget
                && !ReferenceEquals(state.storedTarget, combatEvent.target)) continue;
            if (rule.conditions.oncePerRootDamageEvent)
            {
                ulong rootId = combatEvent.provenance.rootEventId;
                if (rootId != 0 && state.lastRootDamageEventId == rootId) continue;
                state.lastRootDamageEventId = rootId;
            }
            Activate(runtime, rule, ruleIndex, combatEvent, null);
        }
    }

    private void Activate(ArtifactPassiveRuntime runtime, ArtifactPassiveRuleDefinition rule, int ruleIndex,
        DamageResolvedEvent? damageEvent, CharacterObject incomingCharacter)
    {
        ArtifactRuleRuntimeState state = runtime.Rules[ruleIndex];
        if (rule.conditions.chance < 1f && UnityEngine.Random.value > Mathf.Clamp01(rule.conditions.chance)) return;

        bool wasArmed = state.armed;
        for (int responseIndex = 0; responseIndex < rule.responses.Count; responseIndex++)
        {
            ArtifactPassiveResponseDefinition response = rule.responses[responseIndex];
            if (response is ApplyRuntimeEffectResponseDefinition effectResponse)
            {
                ApplyRuntimeEffect(runtime, effectResponse, damageEvent, incomingCharacter);
            }
            else if (response is ModifyArtifactStateResponseDefinition stateResponse)
            {
                ModifyState(state, rule.conditions, stateResponse, damageEvent);
            }
            else if (response is InfuseDamageTypeResponseDefinition infusionResponse)
            {
                ApplyInfusion(runtime, infusionResponse);
            }
            else if (response is SpawnParticleResponseDefinition particleResponse)
            {
                _particlePool?.Spawn(runtime, particleResponse.definition, damageEvent);
            }
        }

        if (!wasArmed && state.armed) RemoveRuleRuntimeEffects(runtime, rule);
        state.activationSequence++;
        ArtifactPassiveFeedbackEvents.Publish(new ArtifactPassiveActivatedEvent(runtime, rule, ruleIndex));

        if (IsLoggingEnabled)
        {
            string wearerName = runtime.Wearer != null ? runtime.Wearer.name : "None";
            string artifactName = runtime.Artifact.Definition != null ? runtime.Artifact.Definition.itemName : "Unknown Artifact";
            Debug.Log($"[ArtifactPassive] Activated: '{artifactName}' on '{wearerName}' (Rule #{ruleIndex}, Trigger: {rule.trigger})");
        }
    }

    private void ApplyRuntimeEffect(ArtifactPassiveRuntime runtime,
        ApplyRuntimeEffectResponseDefinition response, DamageResolvedEvent? damageEvent,
        CharacterObject incomingCharacter)
    {
        if (response.recipe?.definition == null || _teamLoader?.RunInventory == null) return;
        CharacterObject[] team = _teamLoader.GetTeamSnapshot();
        if (response.target == ArtifactResponseTarget.Wearer)
        {
            ApplyCharacterEffect(runtime, runtime.Wearer, response.recipe);
            return;
        }
        if (response.target == ArtifactResponseTarget.IncomingCharacter)
        {
            ApplyCharacterEffect(runtime, incomingCharacter, response.recipe);
            return;
        }
        if (response.target == ArtifactResponseTarget.Team || response.target == ArtifactResponseTarget.Allies)
        {
            for (int i = 0; i < team.Length; i++)
            {
                CharacterObject target = team[i];
                if (target == null || (response.target == ArtifactResponseTarget.Allies
                    && ReferenceEquals(target, runtime.Wearer))) continue;
                ApplyCharacterEffect(runtime, target, response.recipe);
            }
            return;
        }
        if (response.target == ArtifactResponseTarget.TriggerTarget && damageEvent.HasValue)
        {
            damageEvent.Value.target?.RuntimeEffects?.ApplyEffect(response.recipe, runtime.Wearer,
                ResolveSourceStats(runtime));
            return;
        }
        if (response.target == ArtifactResponseTarget.RoomEnemies)
        {
            RoomEnemyRegistry.FillLiving(_roomEnemies);
            for (int i = 0; i < _roomEnemies.Count; i++)
            {
                _roomEnemies[i].RuntimeEffects.ApplyEffect(response.recipe, runtime.Wearer,
                    ResolveSourceStats(runtime));
            }
        }
    }

    private void ApplyCharacterEffect(ArtifactPassiveRuntime runtime, CharacterObject target,
        RuntimeEffectRecipe recipe)
    {
        if (target == null) return;
        _teamLoader.RunInventory.ApplyRuntimeEffect(target, recipe, runtime.Wearer,
            ResolveSourceStats(runtime));
    }

    private CharacterStats ResolveSourceStats(ArtifactPassiveRuntime runtime)
    {
        return _teamLoader != null ? _teamLoader.GetCharacterRuntimeStats(runtime.Wearer) : null;
    }

    private void ApplyInfusion(ArtifactPassiveRuntime runtime, InfuseDamageTypeResponseDefinition response)
    {
        CharacterObject[] team = _teamLoader.GetTeamSnapshot();
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null || (response.target == ArtifactResponseTarget.Allies
                && ReferenceEquals(target, runtime.Wearer))) continue;
            _infusions[target] = new InfusionState(response.elementType,
                Time.time + Mathf.Max(0f, response.durationSeconds));
        }
    }

    private void RemoveRuleRuntimeEffects(ArtifactPassiveRuntime runtime, ArtifactPassiveRuleDefinition rule)
    {
        if (_teamLoader?.RunInventory == null) return;
        for (int responseIndex = 0; responseIndex < rule.responses.Count; responseIndex++)
        {
            if (!(rule.responses[responseIndex] is ApplyRuntimeEffectResponseDefinition response)
                || response.recipe?.definition == null) continue;
            _teamLoader.RunInventory.RemoveRuntimeEffect(runtime.Wearer, response.recipe.definition,
                response.recipe, runtime.Wearer);
        }
    }

    private static void ModifyState(ArtifactRuleRuntimeState state,
        ArtifactPassiveConditionSet conditions, ModifyArtifactStateResponseDefinition response,
        DamageResolvedEvent? damageEvent)
    {
        switch (response.operation)
        {
            case ArtifactStateOperation.AddStacks:
                state.stacks = Mathf.Min(Mathf.Max(1, conditions.maximumStacks),
                    state.stacks + Mathf.Max(1, response.amount));
                if (state.stacks >= Mathf.Max(1, conditions.maximumStacks))
                {
                    state.stacks = 0;
                    state.armed = true;
                }
                break;
            case ArtifactStateOperation.ClearStacks:
                state.stacks = 0;
                break;
            case ArtifactStateOperation.Arm:
                state.armed = true;
                state.storedTarget = damageEvent.HasValue ? damageEvent.Value.target : null;
                break;
            case ArtifactStateOperation.Disarm:
            case ArtifactStateOperation.ConsumeMark:
                state.armed = false;
                state.storedTarget = null;
                break;
            case ArtifactStateOperation.StartCooldown:
                state.cooldownReadyAt = Time.time + Mathf.Max(0f, conditions.cooldownSeconds);
                break;
        }
    }

    private static bool CanActivate(ArtifactRuleRuntimeState state, ArtifactPassiveConditionSet conditions)
    {
        if (Time.time < state.cooldownReadyAt) return false;
        if (conditions.requireArmed && !state.armed) return false;
        if (conditions.requireDisarmed && state.armed) return false;
        return true;
    }

    private static bool MatchesDamage(CombatEventFilter filter, in DamageResolvedEvent combatEvent)
    {
        if (filter.requirePositiveDamage && combatEvent.damageDealt <= 0f) return false;
        if (filter.requireCriticalHit && !combatEvent.isCriticalHit) return false;
        if (filter.filterByCategory && filter.category != combatEvent.category) return false;
        if (filter.filterByDelivery && filter.delivery != combatEvent.descriptor.delivery) return false;
        if ((combatEvent.provenance.tags & filter.requiredDamageTags) != filter.requiredDamageTags) return false;
        return (combatEvent.provenance.tags & filter.forbiddenDamageTags) == 0;
    }

    private static bool MatchesSwitch(CombatEventFilter filter, CharacterObject wearer,
        in CharacterSwitchCompletedEvent combatEvent)
    {
        return filter.switchRelation switch
        {
            ArtifactSwitchRelation.IncomingIsWearer => ReferenceEquals(combatEvent.incomingCharacter, wearer),
            ArtifactSwitchRelation.OutgoingIsWearer => ReferenceEquals(combatEvent.outgoingCharacter, wearer),
            _ => true
        };
    }

    private static bool MatchesHealth(ArtifactPassiveConditionSet conditions, float ratio)
    {
        bool minimumMatches = conditions.minimumHealthExclusive
            ? ratio > conditions.minimumHealthRatio
            : ratio >= conditions.minimumHealthRatio;
        bool maximumMatches = conditions.maximumHealthExclusive
            ? ratio < conditions.maximumHealthRatio
            : ratio <= conditions.maximumHealthRatio;
        return minimumMatches && maximumMatches;
    }

    private void AddRuntime(ArtifactPassiveRuntime runtime)
    {
        _runtimes.Add(runtime);
        _runtimesByInstance[runtime.InstanceId] = runtime;
    }

    private static bool HasArmedState(ArtifactPassiveRuntime runtime)
    {
        for (int i = 0; i < runtime.Rules.Length; i++)
        {
            if (runtime.Rules[i].armed) return true;
        }
        return false;
    }

    private void RemoveRuntime(ArtifactPassiveRuntime runtime)
    {
        if (runtime?.Artifact?.Definition?.passiveSet?.rules != null)
        {
            for (int i = 0; i < runtime.Artifact.Definition.passiveSet.rules.Count; i++)
            {
                ArtifactPassiveRuleDefinition rule = runtime.Artifact.Definition.passiveSet.rules[i];
                if (rule.trigger == ArtifactPassiveTrigger.CharacterHealthChanged && runtime.Rules[i].active)
                {
                    runtime.Rules[i].active = false;
                    RemoveRuleRuntimeEffects(runtime, rule);
                }
            }
        }
        _runtimes.Remove(runtime);
        _runtimesByInstance.Remove(runtime.InstanceId);
    }
}
