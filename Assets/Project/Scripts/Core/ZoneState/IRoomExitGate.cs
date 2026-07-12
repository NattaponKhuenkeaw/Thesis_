namespace Project.Core.ZoneState
{
    public interface IRoomExitGate
    {
        bool IsExitAvailable { get; }
        void SetExitAvailable(bool available, bool playFeedback = true);
    }
}
