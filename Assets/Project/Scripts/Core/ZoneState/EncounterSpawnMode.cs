namespace Project.Core.ZoneState
{
    /// <summary>
    /// How a combat room releases its enemy quota.
    /// </summary>
    public enum EncounterSpawnMode
    {
        /// <summary>Cap concurrent living enemies; spawn one-at-a-time; refill on death.</summary>
        Pressure = 0,
        /// <summary>Spawn a whole wave at once, wait for wipe, then the next wave.</summary>
        Burst = 1
    }
}
