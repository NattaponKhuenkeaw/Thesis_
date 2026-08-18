namespace Project.Core.ZoneState
{
    /// <summary>
    /// How a combat chamber picks types from the Encounter weighted pool.
    /// </summary>
    public enum EncounterTypePickMode
    {
        /// <summary>Build a bag from weights, shuffle, deal; reshuffle when empty.</summary>
        ShuffleBag = 0,
        /// <summary>Each spawn rolls independently using weights.</summary>
        WeightedRandom = 1
    }
}
