namespace Project.Core.ZoneState
{
    public enum ZoneMode
    {
        SafeZone,
        CombatZone
    }

    /// <summary>
    /// BlazBlue-style exit portal category shown after a room clear.
    /// </summary>
    public enum RoomKind
    {
        Combat = 0,
        Elite = 1,
        Shop = 2,
        Reward = 3,
        Boss = 4,
        Level = 5,
        Rest = 6
    }

    public interface IZoneStateListener
    {
        void OnZoneChanged(ZoneMode newMode);
    }
}
