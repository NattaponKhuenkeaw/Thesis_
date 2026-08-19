using UnityEngine;

/// <summary>
/// Graph-only labels for Behavior nodes. Not a second enemy FSM.
/// Camera and hit reactions use <see cref="EnemyController.InCombat"/> and
/// <see cref="EnemyReactionState"/>.
/// </summary>
public enum State
{
    Idle,
    Patrol,
    Notice,
    Chase,
    Attack,
    TakeDamage,
    Die,
    Hitstun,
    Knockdown
}
