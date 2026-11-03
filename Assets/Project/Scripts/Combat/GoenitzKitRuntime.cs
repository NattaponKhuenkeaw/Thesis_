using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GoenitzKitRuntime : ICharacterKitRuntime
{
    private readonly CharacterObject _owner;
    private readonly TeamLoader _team;
    private readonly GoenitzKitRuntimeDefinition _data;
    private readonly List<CharacterObject> _recipients = new List<CharacterObject>();
    private readonly Dictionary<EnemyController, ulong> _rewardedLives = new Dictionary<EnemyController, ulong>();
    private readonly Dictionary<RuntimeEffectRecipe, RuntimeEffectInstance> _payloads = new Dictionary<RuntimeEffectRecipe, RuntimeEffectInstance>();
    private bool _active;
    private bool _changing;
    private bool _disposed;
    private uint _epoch = 1;
    private int _ultimateLevel = -1;
    private int _passiveLevel = -1;
    private CharacterObject[] _lastTeam;
    public bool BlocksOrdinaryAuraEnergyGain => _active;
    private float DrainRate => _data.drainAmount / _data.drainInterval;
    public float RemainingSeconds => _active ? _team.GetCharacterCurrentAE(_owner) / DrainRate : 0f;

    public GoenitzKitRuntime(CharacterObject owner, TeamLoader team, GoenitzKitRuntimeDefinition data)
    {
        _owner = owner; _team = team; _data = data;
        _team.OnCharacterAuraEnergyUpdated += OnAuraChanged;
    }

    private int Level(PotentialTrack track) => _team.MetaProgression?.GetPotentialLevel(_owner, track) ?? 0;
    private bool OwnerAlive => _team.RunInventory?.GetRunCharacter(_owner) != null
        && _team.RunInventory.GetRunCharacter(_owner).currentHP != 0f;
    public bool Affects(CharacterObject character) => _active && _recipients.Contains(character);

    public void Reconcile()
    {
        if (_disposed || _changing) return;
        int ultimate = Level(PotentialTrack.Ultimate), passive = Level(PotentialTrack.Passive);
        CharacterObject[] characters = _team.KitRuntimes?.Characters;
        bool changed = ultimate != _ultimateLevel || passive != _passiveLevel || characters != _lastTeam;
        _ultimateLevel = ultimate; _passiveLevel = passive; _lastTeam = characters;
        if (_active && (!OwnerAlive || ultimate < 1 || _team.GetCharacterCurrentAE(_owner) <= 0f)) EndMode();
        else if (_active && changed) RefreshRecipients();
    }

    public void Tick(float delta)
    {
        if (_disposed || delta <= 0f) return;
        Reconcile();
        if (!_active) return;
        _team.AddCharacterAE(_owner, -DrainRate * delta, AuraEnergyChangeReason.ModeDrain);
    }

    public void OnCastAccepted(CombatActionExecutionToken cast, CombatAction action)
    {
        if (_disposed || cast?.owner != _owner || action == null) return;
        Reconcile();
        bool transform = action.actionId == _data.transformationActionId;
        bool consumes = action.actionId == _data.ultimateActionId || action.actionId == _data.finisherActionId;
        if (_ultimateLevel >= 3 && (transform || consumes)) Apply(_owner, _data.blizzard);
        if (_active && consumes)
        {
            cast.rememberedModeOwner = _owner;
            cast.rememberedModeEpoch = _epoch;
            cast.modeEligibilityPending = true;
            if (action.actionId == _data.finisherActionId) EndMode();
        }
    }

    public void OnActionActive(CombatActionExecutionToken cast, CombatAction action)
    {
        if (_disposed || cast?.owner != _owner) return;
        if (action?.actionId == _data.transformationActionId && !_active
            && Level(PotentialTrack.Ultimate) >= 1 && _team.GetCharacterCurrentAE(_owner) > 0f && OwnerAlive)
            BeginMode(true);
    }

    private void BeginMode(bool newEpoch)
    {
        if (_active || _disposed || !OwnerAlive) return;
        if (newEpoch) _epoch++;
        _active = true;
        RefreshRecipients();
    }

    private void RefreshRecipients()
    {
        if (_changing) return;
        _changing = true;
        using (_team.RunInventory.BeginRuntimeEffectMutationBatch())
        {
            RemoveRecipients();
            CharacterObject[] characters = _team.KitRuntimes?.Characters;
            if (characters != null)
                for (int i = 0; i < characters.Length; i++)
                {
                    CharacterObject target = characters[i];
                    if (target == null || (target != _owner && _passiveLevel < 3)
                        || _team.RunInventory.GetRunCharacter(target)?.currentHP == 0f) continue;
                    _recipients.Add(target);
                    _team.GetCharacterCombatRuntimeState(target).SetTag(target == _owner ? _data.modeTag : _data.sharedModeTag);
                    Apply(target, _data.frenzy);
                    if (target != _owner && _passiveLevel >= 4) Apply(target, _data.evasion);
                    CharacterEvents.NotifyStatsChanged(target);
                }
        }
        _changing = false;
    }

    private void RemoveRecipients()
    {
        for (int i = 0; i < _recipients.Count; i++)
        {
            CharacterObject target = _recipients[i];
            _team.GetCharacterCombatRuntimeState(target).RemoveTag(target == _owner ? _data.modeTag : _data.sharedModeTag);
            Remove(target, _data.frenzy);
            Remove(target, _data.evasion);
            // Dash readiness cannot survive its enabling mode.
            if (target == _owner)
            {
                _team.GetCharacterCombatRuntimeState(target).RemoveTag("goenitz_after_dash_bonus_action");
                _team.GetCharacterCombatRuntimeState(target).RemoveTag("goenitz_after_dash_basic_depth4");
            }
            CharacterEvents.NotifyStatsChanged(target);
        }
        _recipients.Clear();
    }

    private void EndMode()
    {
        if (!_active) return;
        _active = false;
        _changing = true;
        using (_team.RunInventory.BeginRuntimeEffectMutationBatch()) RemoveRecipients();
        _changing = false;
    }

    private void OnAuraChanged(CharacterObject owner, float current, float maximum)
    {
        if (owner == _owner && current <= 0f && _active) EndMode();
    }

    public void OnPositivePrimaryHit(in DamageRequest request, in DamageResult result, float damageDealt)
    {
        if (_disposed || damageDealt <= 0f || request.target == null) return;
        bool affected = Affects(request.sourceCharacter);
        bool own = request.sourceCharacter == _owner;
        CombatActionExecutionToken cast = request.execution?.cast;
        bool remembered = own && cast != null && cast.modeEligibilityPending
            && cast.rememberedModeOwner == _owner && cast.rememberedModeEpoch == _epoch;
        if (remembered) cast.modeEligibilityPending = false;
        if (own && (affected || remembered) && !request.target.IsAlive) RewardKill(request.target);
        if (affected && _ultimateLevel >= 2 && request.target.IsAlive)
            request.target.RuntimeEffects?.ApplyEffect(_data.vulnerability, _owner, _team.GetCharacterRuntimeStats(_owner));
        if (own && result.IsCriticalHit && _passiveLevel >= 1)
            request.target.DamageTypeState?.ApplyRuntimeElementModifier(_data.criticalPhysicalShred);
        if (own && result.IsCriticalHit && request.actionId == _data.exSkillActionId)
        {
            Emit(_data.exBleed, request);
            _team.AddCharacterAE(_owner, _data.exCriticalAuraEnergy);
        }
        if (affected) Emit(_data.frenzyBleed, request);
        if (own && request.actionId == _data.finisherActionId && cast != null && cast.ClaimPayload(request.target.LifeIdentity))
            Emit(_data.finisherBleed, request);
    }

    public void OnDamageResolved(in DamageResolvedEvent result)
    {
        if (_disposed || !_active || result.sourceCharacter != _owner || result.damageDealt <= 0f
            || !result.killedTarget || (result.acceptedPolicy & DamageProcPolicy.PublishKillEvent) == 0) return;
        RewardKill(result.target);
    }

    private void RewardKill(EnemyController enemy)
    {
        if (_ultimateLevel < 4 || !OwnerAlive || enemy == null) return;
        if (_rewardedLives.TryGetValue(enemy, out ulong life) && life == enemy.LifeIdentity) return;
        _rewardedLives[enemy] = enemy.LifeIdentity;
        Apply(_owner, _data.killAttack);
        _team.AddCharacterAE(_owner, _data.killExtension, AuraEnergyChangeReason.ModeKillExtension);
        if (!_active && _team.GetCharacterCurrentAE(_owner) > 0f) BeginMode(false);
    }

    private void Emit(RuntimeEffectRecipe recipe, in DamageRequest request)
    {
        if (recipe?.definition == null || !request.target.IsAlive) return;
        if (!_payloads.TryGetValue(recipe, out RuntimeEffectInstance instance))
        {
            instance = new RuntimeEffectInstance();
            instance.Initialize(recipe, request.sourceCharacter, request.sourceStats);
            _payloads.Add(recipe, instance);
        }
        recipe.definition.OnHitDealt(new RuntimeEffectDamageContext
        {
            instance = instance, sourceCharacter = request.sourceCharacter,
            sourceStats = _team.GetCharacterRuntimeStats(request.sourceCharacter), targetEnemy = request.target,
            category = request.category, actionId = request.actionId, actionName = request.actionName,
            stepData = request.stepData, damageDelivery = DamageDelivery.Primary,
            execution = request.execution, provenance = request.provenance,
            attackerPosition = request.attackerPosition, hitDirection = request.hitDirection
        });
    }

    private void Apply(CharacterObject target, RuntimeEffectRecipe recipe)
    {
        if (recipe?.definition != null)
            _team.RunInventory.ApplyRuntimeEffect(target, recipe, _owner, _team.GetCharacterRuntimeStats(_owner));
    }

    private void Remove(CharacterObject target, RuntimeEffectRecipe recipe)
    {
        if (recipe?.definition != null) _team.RunInventory.RemoveRuntimeEffect(target, recipe.definition, recipe, _owner);
    }

    public bool TryGetEffectRemaining(RuntimeEffectInstance effect, out float remaining)
    {
        remaining = RemainingSeconds;
        return _active && effect?.sourceCharacter == _owner
            && (effect.recipe == _data.frenzy || effect.recipe == _data.evasion);
    }

    public void Dispose()
    {
        if (_disposed) return;
        EndMode();
        _epoch++;
        _disposed = true;
        _team.OnCharacterAuraEnergyUpdated -= OnAuraChanged;
        _rewardedLives.Clear();
        _payloads.Clear();
    }
}
