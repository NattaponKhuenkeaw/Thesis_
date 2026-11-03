using Project.Progression;

public sealed class KitInfoContext
{
    public CharacterProgressionState Progression { get; }
    public CombatRuntimeState RuntimeState { get; }

    public KitInfoContext(CharacterProgressionState progression, CombatRuntimeState runtimeState = null)
    {
        Progression = progression;
        RuntimeState = runtimeState;
    }
}
