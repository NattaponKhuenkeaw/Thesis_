using System;

public interface ICombatEventHub
{
    event Action<ActionStartedEvent> ActionStarted;
    event Action<DamageResolvedEvent> DamageResolved;
    event Action<CharacterSwitchCompletedEvent> CharacterSwitchCompleted;
    event Action<CharacterHealthChangedEvent> CharacterHealthChanged;
}

public sealed class CombatEventHub : ICombatEventHub
{
    public const byte MaximumProcDepth = 8;

    private static readonly CombatEventHub _shared = new CombatEventHub();
    private ulong _nextDamageEventId = 1;

    public static CombatEventHub Shared => _shared;

    public event Action<ActionStartedEvent> ActionStarted;
    public event Action<DamageResolvedEvent> DamageResolved;
    public event Action<CharacterSwitchCompletedEvent> CharacterSwitchCompleted;
    public event Action<CharacterHealthChangedEvent> CharacterHealthChanged;

    private CombatEventHub() { }

    public void Publish(in ActionStartedEvent combatEvent) => ActionStarted?.Invoke(combatEvent);
    public void Publish(in CharacterSwitchCompletedEvent combatEvent) => CharacterSwitchCompleted?.Invoke(combatEvent);
    public void Publish(in CharacterHealthChangedEvent combatEvent) => CharacterHealthChanged?.Invoke(combatEvent);
    public void Publish(in DamageResolvedEvent combatEvent) => DamageResolved?.Invoke(combatEvent);

    public DamageProvenance AssignDamageIdentity(in DamageProvenance provenance)
    {
        ulong eventId = _nextDamageEventId++;
        if (_nextDamageEventId == 0) _nextDamageEventId = 1;
        return provenance.WithIdentity(eventId);
    }

    public ulong ReserveEventId()
    {
        ulong eventId = _nextDamageEventId++;
        if (_nextDamageEventId == 0) _nextDamageEventId = 1;
        return eventId;
    }

    public void ResetRunSequence() => _nextDamageEventId = 1;
}
