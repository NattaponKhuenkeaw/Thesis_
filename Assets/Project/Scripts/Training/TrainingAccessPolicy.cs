using Project.Core.ZoneState;

namespace Project.Training
{
    /// <summary>Editing is a Training capability; it does not change the room's combat zone.</summary>
    public static class TrainingAccessPolicy
    {
        public static ZoneMode LoadoutMode(ZoneMode roomMode) =>
            TrainingSession.Instance != null && TrainingSession.Instance.IsActive ? ZoneMode.SafeZone : roomMode;
    }
}
