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
    private readonly List<EnemyController> _lastSplashTargets = new List<EnemyController>();
    private readonly List<(GameObject prefab, ArtifactAreaVisual view)> _explosionVisuals =
        new List<(GameObject, ArtifactAreaVisual)>();
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
        DamageDescriptor descriptor, CombatActionExecutionToken cast = null)
    {
        if (descriptor.delivery == DamageDelivery.Primary && cast?.ArtifactInfusion is ElementType castElement)
        {
            descriptor.elementType = castElement;
            return descriptor;
        }

        ArtifactPassiveRuntimeSystem system = _instance;
        if (system == null || sourceCharacter == null) return descriptor;

        // Check recipe-based infusion buff in RunInventory
        if (system._teamLoader?.RunInventory != null)
        {
            var runChar = system._teamLoader.RunInventory.GetRunCharacter(sourceCharacter);
            if (runChar != null)
            {
                var effects = runChar.ActiveEffects;
                for (int i = 0; i < effects.Count; i++)
                {
                    RuntimeEffectInstance effect = effects[i];
                    if (effect == null || effect.IsExpired) continue;
                    if (effect.definition is InfusionRuntimeEffectDefinition infusionEffect)
                    {
                        descriptor.elementType = infusionEffect.infusedElementType;
                        return descriptor;
                    }
                }
            }
        }

        // Fallback to legacy dictionary
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
        // A multicast event can still invoke a delegate already copied for dispatch while its
        // Unity owner is being destroyed. OnDestroy unsubscribes, and this guard covers that race.
        if (this == null || !isActiveAndEnabled) return;
        if ((combatEvent.acceptedPolicy & DamageProcPolicy.TriggerArtifactRules) == 0) return;
        for (int runtimeIndex = 0; runtimeIndex < _runtimes.Count; runtimeIndex++)
        {
            ArtifactPassiveRuntime runtime = _runtimes[runtimeIndex];
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
                    || !CanActivate(runtime.Rules[ruleIndex], rule.conditions, Time.time)) continue;
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
                || !CanActivate(runtime.Rules[ruleIndex], rule.conditions, Time.time)) continue;
            Activate(runtime, rule, ruleIndex, null, null, combatEvent.execution?.cast);
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
            if (rule.trigger != trigger || !MatchesDamage(rule.filter, combatEvent, runtime.Wearer)
                || !CanActivate(state, rule.conditions, Time.time)) continue;
            if (rule.conditions.requireStoredTarget
                && !ReferenceEquals(state.storedTarget, combatEvent.target)) continue;
            if (rule.conditions.requireArmed)
            {
                // The hit that just armed the mark cannot immediately consume it on the same hit or same action
                ulong rootId = combatEvent.provenance.rootEventId;
                if (rootId != 0 && state.lastRootDamageEventId == rootId) continue;
                if (!string.IsNullOrEmpty(state.storedActionId) && string.Equals(combatEvent.actionId, state.storedActionId, StringComparison.Ordinal))
                {
                    // Accumulate damage from subsequent hits belonging to the same initiating Skill action
                    state.snapshottedSkillDamage += combatEvent.damageDealt;
                    continue;
                }
            }
            if (rule.ruleId == "claw-bleed")
            {
                bool sourceHasClaw = combatEvent.sourceCharacter != null && HasActiveRuntimeEffect(combatEvent.sourceCharacter, "P1-A04-CLAW");
                bool wearerHasClaw = HasActiveRuntimeEffect(runtime.Wearer, "P1-A04-CLAW");
                if (!sourceHasClaw && !wearerHasClaw)
                {
                    continue;
                }
            }
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
        DamageResolvedEvent? damageEvent, CharacterObject incomingCharacter, CombatActionExecutionToken cast = null)
    {
        ArtifactRuleRuntimeState state = runtime.Rules[ruleIndex];
        if (rule.responses.Exists(response => response is InfuseDamageTypeResponseDefinition infusion && infusion.onlyCurrentCast)
            && (cast == null || !cast.ClaimArtifactInfusionRoll(runtime.Artifact.InstanceId))) return;
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
                if (infusionResponse.onlyCurrentCast) cast.ArtifactInfusion = infusionResponse.elementType;
                else ApplyInfusion(runtime, infusionResponse);
            }
            else if (response is SpawnParticleResponseDefinition particleResponse)
            {
                _particlePool?.Spawn(runtime, particleResponse.definition, damageEvent);
            }
            else if (response is ExplosionSplashResponseDefinition splashResponse)
            {
                ApplyExplosionSplash(runtime, splashResponse, damageEvent, state);
            }
            else if (response is ApplyDotResponseDefinition dotResponse)
            {
                ApplyDot(runtime, dotResponse, damageEvent, state);
            }
            else if (response is HealTeamFromResolvedDamageResponseDefinition healTeamResponse)
            {
                ApplyHealTeamFromDamage(runtime, healTeamResponse, damageEvent);
            }
        }

        if (!wasArmed && state.armed)
        {
            RemoveRuleRuntimeEffects(runtime, rule);
            EvaluateArmedRulesImmediately(runtime, damageEvent);
        }
        state.activationSequence++;
        ArtifactPassiveFeedbackEvents.Publish(new ArtifactPassiveActivatedEvent(runtime, rule, ruleIndex));

        if (IsLoggingEnabled)
        {
            string wearerName = runtime.Wearer != null ? runtime.Wearer.name : "None";
            string artifactName = runtime.Artifact.Definition != null ? runtime.Artifact.Definition.itemName : "Unknown Artifact";
            Debug.Log($"[ArtifactPassive] Activated: '{artifactName}' on '{wearerName}' (Rule #{ruleIndex}, Trigger: {rule.trigger})");
        }
    }

    private void EvaluateArmedRulesImmediately(ArtifactPassiveRuntime runtime, DamageResolvedEvent? damageEvent)
    {
        if (runtime?.Artifact?.Definition?.passiveSet?.rules == null) return;
        List<ArtifactPassiveRuleDefinition> rules = runtime.Artifact.Definition.passiveSet.rules;
        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            ArtifactPassiveRuleDefinition rule = rules[ruleIndex];
            if (!rule.conditions.requireArmed) continue;
            if (rule.conditions.requireStoredTarget) continue;
            ArtifactRuleRuntimeState state = runtime.Rules[ruleIndex];
            if (!state.armed || !CanActivate(state, rule.conditions, Time.time)) continue;

            Activate(runtime, rule, ruleIndex, damageEvent, null);
        }
    }

    private void ApplyExplosionSplash(ArtifactPassiveRuntime runtime,
        ExplosionSplashResponseDefinition response, DamageResolvedEvent? damageEvent, ArtifactRuleRuntimeState state)
    {
        _lastSplashTargets.Clear();
        if (response.definition == null || !damageEvent.HasValue) return;
        Vector2 center = ArtifactAreaGeometry.TargetCenter(damageEvent.Value.target,
            damageEvent.Value.hitPosition);
        center += response.definition.centerOffset;

        int layerMask = response.definition.targetLayers.value;
        if (layerMask == 0) layerMask = Physics2D.AllLayers;
        Vector2 radii = ArtifactAreaGeometry.Radii(response.definition.radius, response.definition.verticalRadiusMultiplier);
        bool stretched = radii.y > radii.x;
        Collider2D[] colliders = stretched
            ? Physics2D.OverlapBoxAll(center, radii * 2f, 0f, layerMask)
            : Physics2D.OverlapCircleAll(center, radii.x, layerMask);
        int cap = response.definition.targetCap > 0 ? response.definition.targetCap : int.MaxValue;
        int hitCount = 0;

        for (int i = 0; i < colliders.Length && hitCount < cap; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;
            if (stretched && !ArtifactAreaGeometry.Overlaps(col, center, radii)) continue;
            EnemyController enemy = col.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.IsTargetable) continue;
            if (response.definition.excludeOriginalTarget && ReferenceEquals(enemy, damageEvent.Value.target)) continue;

            if (!_lastSplashTargets.Contains(enemy))
            {
                _lastSplashTargets.Add(enemy);
            }
            hitCount++;
        }

        if (_lastSplashTargets.Count == 0 && damageEvent.Value.target != null && !response.definition.excludeOriginalTarget
            && (damageEvent.Value.target.bodyCollider == null
                || ArtifactAreaGeometry.Overlaps(damageEvent.Value.target.bodyCollider, center, radii)))
        {
            _lastSplashTargets.Add(damageEvent.Value.target);
        }

        if (response.damageRatio > 0f)
        {
            float baseDamage = response.damageBasis switch
            {
                ArtifactDamageBasis.Fixed => response.damageRatio,
                ArtifactDamageBasis.TriggeringBaseDamage => (damageEvent.HasValue ? damageEvent.Value.baseDamage : 0f) * response.damageRatio,
                ArtifactDamageBasis.ConfirmedDamage => (state != null && state.snapshottedSkillDamage > 0f
                    ? state.snapshottedSkillDamage
                    : (damageEvent.HasValue ? damageEvent.Value.damageDealt : 0f)) * response.damageRatio,
                _ => (ResolveSourceStats(runtime) != null ? ResolveSourceStats(runtime).attack : 0f) * response.damageRatio
            };

            if (baseDamage > 0f)
            {
                ElementType element = response.elementType != ElementType.None
                    ? response.elementType
                    : (response.preserveParentElement && damageEvent.HasValue ? damageEvent.Value.descriptor.elementType : ElementType.None);
                DamageDelivery delivery = response.preserveParentDelivery && damageEvent.HasValue
                    ? damageEvent.Value.descriptor.delivery
                    : DamageDelivery.Additional;

                DamageDescriptor descriptor = new DamageDescriptor(
                    delivery,
                    element,
                    DamageCriticalPolicy.Prevent);

                SpawnExplosionVisual(response.definition, center, element);

                for (int i = 0; i < _lastSplashTargets.Count; i++)
                {
                    EnemyController enemy = _lastSplashTargets[i];
                    if (enemy == null || !enemy.IsTargetable) continue;

                    DamageRequest request = new DamageRequest
                    {
                        target = enemy,
                        sourceCharacter = damageEvent.Value.sourceCharacter ?? runtime.Wearer,
                        sourceArtifact = runtime.Artifact,
                        sourceStats = ResolveSourceStats(runtime, damageEvent.Value.sourceCharacter),
                        baseDamage = baseDamage,
                        ignoreDefense = true,
                        defenderDefense = enemy.defense,
                        defenderDamageResistance = enemy.damageRes,
                        descriptor = descriptor,
                        category = AttackCategory.Normal,
                        actionId = response.definition.stableId,
                        actionName = "Explosion Splash",
                        damageRatio = response.damageRatio,
                        sourceStatValue = ResolveSourceStats(runtime, damageEvent.Value.sourceCharacter) != null ? ResolveSourceStats(runtime, damageEvent.Value.sourceCharacter).attack : 0f,
                        attackerPosition = center,
                        hitDirection = ((Vector2)enemy.transform.position - center).normalized,
                        provenance = new DamageProvenance(0, damageEvent.Value.provenance.rootEventId, damageEvent.Value.provenance.rootEventId, 1,
                            runtime.InstanceId, CombatDamageTag.Artifact | CombatDamageTag.Explosion | CombatDamageTag.Splash),
                        hasExplicitProcPolicy = true,
                        procPolicy = DamageProcPolicy.PublishResolvedEvent
                            | DamageProcPolicy.PublishKillEvent
                            | DamageProcPolicy.ApplyHitReaction
                            | DamageProcPolicy.ApplyElementDot
                    };
                    CombatDamageProcessor.Apply(request);
                }
            }
        }
    }

    private void ApplyDot(ArtifactPassiveRuntime runtime,
        ApplyDotResponseDefinition response, DamageResolvedEvent? damageEvent, ArtifactRuleRuntimeState state)
    {
        if (response.definition == null) return;

        CharacterObject attacker = damageEvent.HasValue && damageEvent.Value.sourceCharacter != null
            ? damageEvent.Value.sourceCharacter
            : runtime.Wearer;
        CharacterStats sourceStats = ResolveSourceStats(runtime, attacker) ?? ResolveSourceStats(runtime, runtime.Wearer);
        float baseDamage = ResolveDotBaseDamage(response, damageEvent, sourceStats, state);
        if (baseDamage <= 0f) return;

        if (response.target == ArtifactResponseTarget.TriggerTarget && damageEvent.HasValue && damageEvent.Value.target != null)
        {
            ApplyDotToEnemy(damageEvent.Value.target, response.definition, baseDamage, sourceStats, attacker);
            return;
        }

        if (response.target == ArtifactResponseTarget.SplashTargets)
        {
            if (_lastSplashTargets.Count > 0)
            {
                for (int i = 0; i < _lastSplashTargets.Count; i++)
                {
                    ApplyDotToEnemy(_lastSplashTargets[i], response.definition, baseDamage, sourceStats, attacker);
                }
            }
            else if (damageEvent.HasValue && damageEvent.Value.target != null)
            {
                ApplyDotToEnemy(damageEvent.Value.target, response.definition, baseDamage, sourceStats, attacker);
            }
            return;
        }

        if (response.target == ArtifactResponseTarget.RoomEnemies)
        {
            RoomEnemyRegistry.FillTargetable(_roomEnemies);
            for (int i = 0; i < _roomEnemies.Count; i++)
            {
                ApplyDotToEnemy(_roomEnemies[i], response.definition, baseDamage, sourceStats, attacker);
            }
        }
    }

    private static void ApplyDotToEnemy(EnemyController enemy, DotEffectDefinition definition,
        float baseDamage, CharacterStats sourceStats, CharacterObject attacker)
    {
        if (enemy == null) return;
        EnemyDamageTypeState damageTypeState = enemy.DamageTypeState;
        if (damageTypeState == null)
        {
            damageTypeState = enemy.GetComponent<EnemyDamageTypeState>() ?? enemy.gameObject.AddComponent<EnemyDamageTypeState>();
        }
        damageTypeState?.ApplyCustomDot(definition, baseDamage, sourceStats, attacker);
    }

    private static float ResolveDotBaseDamage(ApplyDotResponseDefinition response,
        DamageResolvedEvent? damageEvent, CharacterStats sourceStats, ArtifactRuleRuntimeState state)
    {
        return response.damageBasis switch
        {
            ArtifactDamageBasis.Fixed => response.ratio,
            ArtifactDamageBasis.TriggeringBaseDamage => (damageEvent.HasValue ? damageEvent.Value.baseDamage : 0f) * response.ratio,
            ArtifactDamageBasis.ConfirmedDamage => (state != null && state.snapshottedSkillDamage > 0f
                ? state.snapshottedSkillDamage
                : (damageEvent.HasValue ? damageEvent.Value.damageDealt : 0f)) * response.ratio,
            _ => (sourceStats != null ? sourceStats.attack : 0f) * response.ratio
        };
    }

    private bool HasActiveRuntimeEffect(CharacterObject character, string stableId)
    {
        if (character == null || _teamLoader == null || _teamLoader.RunInventory == null) return false;
        var runChar = _teamLoader.RunInventory.GetRunCharacter(character);
        if (runChar == null || runChar.ActiveEffects == null) return false;
        for (int i = 0; i < runChar.ActiveEffects.Count; i++)
        {
            var effect = runChar.ActiveEffects[i];
            if (effect == null) continue;
            if (string.Equals(effect.recipe?.recipeId, stableId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(effect.recipe?.presentation?.effectId, stableId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(effect.definition?.effectId, stableId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(RuntimeEffectRuntimeUtility.GetStableId(effect.definition, effect.recipe), stableId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private void ApplyRuntimeEffect(ArtifactPassiveRuntime runtime,
        ApplyRuntimeEffectResponseDefinition response, DamageResolvedEvent? damageEvent,
        CharacterObject incomingCharacter)
    {
        if (response.recipe?.definition == null) return;
        CharacterObject[] team = _teamLoader != null ? _teamLoader.GetTeamSnapshot() : Array.Empty<CharacterObject>();
        if (response.target == ArtifactResponseTarget.Wearer)
        {
            if (response.removeFromWearer)
                _teamLoader?.RunInventory?.RemoveRuntimeEffect(runtime.Wearer, response.recipe.definition,
                    response.recipe, runtime.Wearer);
            else
                ApplyCharacterEffect(runtime, runtime.Wearer, response.recipe);
            return;
        }
        if (response.target == ArtifactResponseTarget.TriggerSource && damageEvent.HasValue && damageEvent.Value.sourceCharacter != null)
        {
            ApplyCharacterEffect(runtime, damageEvent.Value.sourceCharacter, response.recipe);
            return;
        }
        if (response.target == ArtifactResponseTarget.IncomingCharacter)
        {
            ApplyCharacterEffect(runtime, incomingCharacter, response.recipe);
            return;
        }
        if (response.target == ArtifactResponseTarget.Allies || response.target == ArtifactResponseTarget.Teammates)
        {
            for (int i = 0; i < team.Length; i++)
            {
                CharacterObject target = team[i];
                if (target == null || (response.target == ArtifactResponseTarget.Teammates
                    && ReferenceEquals(target, runtime.Wearer))) continue;
                if (response.livingAlliesOnly && !_teamLoader.IsCharacterAlive(target)) continue;
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
        if (response.target == ArtifactResponseTarget.SplashTargets)
        {
            if (_lastSplashTargets.Count > 0)
            {
                for (int i = 0; i < _lastSplashTargets.Count; i++)
                {
                    _lastSplashTargets[i]?.RuntimeEffects?.ApplyEffect(response.recipe, runtime.Wearer,
                        ResolveSourceStats(runtime));
                }
            }
            else if (damageEvent.HasValue && damageEvent.Value.target != null)
            {
                damageEvent.Value.target.RuntimeEffects?.ApplyEffect(response.recipe, runtime.Wearer,
                    ResolveSourceStats(runtime));
            }
            return;
        }
        if (response.target == ArtifactResponseTarget.RoomEnemies)
        {
            RoomEnemyRegistry.FillTargetable(_roomEnemies);
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

    private CharacterStats ResolveSourceStats(ArtifactPassiveRuntime runtime, CharacterObject fallbackSource = null)
    {
        CharacterObject source = fallbackSource != null ? fallbackSource : (runtime != null ? runtime.Wearer : null);
        return source != null && _teamLoader != null ? _teamLoader.GetCharacterRuntimeStats(source) : null;
    }

    private void ApplyInfusion(ArtifactPassiveRuntime runtime, InfuseDamageTypeResponseDefinition response)
    {
        CharacterObject[] team = _teamLoader.GetTeamSnapshot();
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null || (response.target == ArtifactResponseTarget.Teammates
                && ReferenceEquals(target, runtime.Wearer))) continue;
            _infusions[target] = new InfusionState(response.elementType,
                Time.time + Mathf.Max(0f, response.durationSeconds));
        }
    }

    private void ApplyHealTeamFromDamage(ArtifactPassiveRuntime runtime,
        HealTeamFromResolvedDamageResponseDefinition response, DamageResolvedEvent? damageEvent)
    {
        if (!damageEvent.HasValue || damageEvent.Value.damageDealt <= 0f || response == null) return;
        float healAmount = damageEvent.Value.damageDealt * Mathf.Max(0f, response.confirmedDamageRatio);
        if (healAmount <= 0f) return;

        CharacterObject[] team = _teamLoader != null ? _teamLoader.GetTeamSnapshot() : Array.Empty<CharacterObject>();
        for (int i = 0; i < team.Length; i++)
        {
            CharacterObject target = team[i];
            if (target == null) continue;
            if (response.target == ArtifactResponseTarget.Teammates && ReferenceEquals(target, runtime.Wearer)) continue;
            if (response.target == ArtifactResponseTarget.Wearer && !ReferenceEquals(target, runtime.Wearer)) continue;
            RuntimeEffectHealUtility.ApplyHeal(target, null, healAmount);
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
                state.snapshottedSkillDamage = damageEvent.HasValue ? damageEvent.Value.damageDealt : 0f;
                state.storedActionId = damageEvent.HasValue ? damageEvent.Value.actionId : null;
                if (conditions.durationSeconds > 0f)
                {
                    state.expiresAt = Time.time + conditions.durationSeconds;
                }
                if (damageEvent.HasValue)
                {
                    state.lastRootDamageEventId = damageEvent.Value.provenance.rootEventId;
                }
                break;
            case ArtifactStateOperation.Disarm:
            case ArtifactStateOperation.ConsumeMark:
                if (state.storedTarget != null && state.storedTarget.RuntimeEffects != null &&
                    !string.IsNullOrEmpty(response.storedTargetEffectKey))
                {
                    state.storedTarget.RuntimeEffects.RemoveEffectStacks(response.storedTargetEffectKey, int.MaxValue);
                }
                state.armed = false;
                state.storedTarget = null;
                state.snapshottedSkillDamage = 0f;
                state.storedActionId = null;
                state.expiresAt = 0f;
                break;
            case ArtifactStateOperation.StartCooldown:
                state.cooldownReadyAt = Time.time + Mathf.Max(0f, conditions.cooldownSeconds);
                break;
        }
    }

    private static bool CanActivate(ArtifactRuleRuntimeState state, ArtifactPassiveConditionSet conditions, float now)
    {
        if (conditions.cooldownSeconds > 0f && now < state.cooldownReadyAt) return false;
        if (state.expiresAt > 0f && now >= state.expiresAt)
        {
            state.armed = false;
            state.storedTarget = null;
            state.storedActionId = null;
            state.snapshottedSkillDamage = 0f;
            state.expiresAt = 0f;
        }
        if (conditions.requireArmed && !state.armed) return false;
        if (conditions.requireDisarmed && state.armed) return false;
        return true;
    }

    private static bool MatchesDamage(CombatEventFilter filter, in DamageResolvedEvent combatEvent,
        CharacterObject wearer)
    {
        if (filter.requireWearerAsSource && !ReferenceEquals(wearer, combatEvent.sourceCharacter)) return false;
        if (filter.requirePositiveDamage && combatEvent.damageDealt <= 0f) return false;
        if (filter.requireCriticalHit && !combatEvent.isCriticalHit) return false;
        if (filter.requireNonCriticalHit && combatEvent.isCriticalHit) return false;
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

    private static Sprite s_explosionSprite;

    private static Sprite GetOrCreateExplosionSprite()
    {
        if (s_explosionSprite != null) return s_explosionSprite;
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float radius = center;
        Color[] colors = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / radius));
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        s_explosionSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return s_explosionSprite;
    }

    private void SpawnExplosionVisual(ExplosionSplashDefinition definition, Vector2 center, ElementType element)
    {
        // Guard again at the Unity API boundary in case the owner is destroyed during a re-entrant proc.
        if (this == null || !isActiveAndEnabled || definition == null) return;
        if (definition.visualPrefab == null)
        {
            SpawnExplosionVisual(center, definition.radius, element);
            return;
        }
        foreach (var entry in _explosionVisuals)
            if (entry.prefab == definition.visualPrefab && entry.view != null && entry.view.IsFinished)
            {
                entry.view.Begin(center, definition.radius, verticalMultiplier: definition.verticalRadiusMultiplier);
                return;
            }
        GameObject host = Instantiate(definition.visualPrefab, transform);
        ArtifactAreaVisual view = host.GetComponent<ArtifactAreaVisual>();
        if (view != null)
        {
            _explosionVisuals.Add((definition.visualPrefab, view));
            view.Begin(center, definition.radius, verticalMultiplier: definition.verticalRadiusMultiplier);
        }
        else Destroy(host);
    }

    private void OnDisable()
    {
        foreach (var entry in _explosionVisuals)
            if (entry.view != null) entry.view.ClearImmediately();
    }

    private static void SpawnExplosionVisual(Vector2 center, float radius, ElementType element)
    {
        GameObject go = new GameObject("[DebugExplosionSplash]");
        go.transform.position = center;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreateExplosionSprite();
        Color col = element == ElementType.Bleed
            ? new Color(0.9f, 0.15f, 0.2f, 0.45f)
            : new Color(1.0f, 0.6f, 0.15f, 0.45f);
        sr.color = col;
        go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        int segments = 32;
        lr.positionCount = segments;
        lr.startWidth = 0.06f;
        lr.endWidth = 0.06f;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            lr.material = new Material(shader);
        }
        Color lineCol = col;
        lineCol.a = 0.9f;
        lr.startColor = lineCol;
        lr.endColor = lineCol;

        Vector3[] positions = new Vector3[segments];
        float angleStep = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            positions[i] = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
        }
        lr.SetPositions(positions);

        UnityEngine.Object.Destroy(go, 0.35f);
    }
}
