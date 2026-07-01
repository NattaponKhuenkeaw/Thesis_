using System;

public static class CharacterEvents
{
    /// <summary>
    /// Invoked whenever a character's core stats or level progression changes.
    /// Listeners (like TeamLoader) can use this to instantly refresh runtime stats.
    /// </summary>
    public static event Action<CharacterObject> OnCharacterStatsChanged;

    /// <summary>
    /// Notifies the game that this character's stats have changed and need to be refreshed at runtime.
    /// </summary>
    public static void NotifyStatsChanged(CharacterObject character)
    {
        if (character != null)
        {
            OnCharacterStatsChanged?.Invoke(character);
        }
    }
}
