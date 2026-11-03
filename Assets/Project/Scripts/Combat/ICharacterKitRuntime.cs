using System;

public interface ICharacterKitRuntime : IDisposable
{
    void Tick(float scaledCombatDelta);
    void Reconcile();
    void OnCastAccepted(CombatActionExecutionToken cast, CombatAction action);
    void OnActionActive(CombatActionExecutionToken cast, CombatAction action);
    void OnPositivePrimaryHit(in DamageRequest request, in DamageResult result, float damageDealt);
    void OnDamageResolved(in DamageResolvedEvent result);
    bool BlocksOrdinaryAuraEnergyGain { get; }
    bool Affects(CharacterObject character);
    float RemainingSeconds { get; }
    bool TryGetEffectRemaining(RuntimeEffectInstance effect, out float remaining);
}
