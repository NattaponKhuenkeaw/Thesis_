public enum SourceStatReadMode
{
    SnapshotOnApply,
    LiveOnResolve
}

public static class CombatSourceStatResolver
{
    public static CharacterStats Resolve(
        CharacterObject sourceCharacter,
        CharacterStats sourceStatsSnapshot,
        SourceStatReadMode readMode)
    {
        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader != null)
        {
            return teamLoader.ResolveSourceStats(
                sourceCharacter,
                sourceStatsSnapshot,
                readMode);
        }

        return sourceStatsSnapshot;
    }
}
