using Unity.Behavior;

[BlackboardEnum]
public enum State
{
    Idle,
    Patrol,
    Notice,
    Chase,
    Attack,
    TakeDamage,
    Dead
}
