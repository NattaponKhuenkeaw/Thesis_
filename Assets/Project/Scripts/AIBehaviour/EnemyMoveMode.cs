/// <summary>
/// How a Behavior Graph enemy closes into its attack slot.
/// Shared by trash, flyers, and (later) bosses on the same Agent.
/// </summary>
public enum EnemyMoveMode
{
    Ground = 0,
    Fly = 1,
    Warp = 2
}
