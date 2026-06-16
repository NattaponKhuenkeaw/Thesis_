namespace Project.Core.ZoneState
{
    public enum ZoneMode
    {
        SafeZone,
        CombatZone
    }

    public interface IZoneStateListener
    {
        void OnZoneChanged(ZoneMode newMode);
    }
}
