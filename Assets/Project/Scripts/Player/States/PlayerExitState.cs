using UnityEngine;

/// <summary>
/// Exit walk-out cinematic paired with <see cref="PlayerIntroState"/>.
/// Locks input and runs the player toward an authored exit target before the fade/load.
/// </summary>
public class PlayerExitState : PlayerState
{
    private const float ArrivalTolerance = 0.35f;
    private const float MaxExitDurationSeconds = 6f;

    private float exitStartedAt;

    public PlayerExitState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Exit;

    public override bool AcceptsMoveInput => false;
    public override bool AcceptsDashInput => false;
    public override bool AcceptsJumpInput => false;
    public override bool AcceptsAttackInput => false;
    public override bool AcceptsSkillInput => false;
    public override bool AcceptsUltimateInput => false;

    public Vector3 TargetPosition { get; set; }

    /// <summary>True when Exit reached its target or timed out cleanly (not interrupted by Hitstun/Dead).</summary>
    public bool FinishedNormally { get; private set; }

    public override void Enter()
    {
        base.Enter();
        FinishedNormally = false;
        exitStartedAt = Time.unscaledTime;
        if (player.Movement != null)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            player.Movement.SetHorizontalInput(0f);
        }
    }

    public override void Update()
    {
        if (player.Movement == null)
        {
            fsm.ChangeState(fsm.IdleState);
            return;
        }

        Vector3 currentPos = player.transform.position;
        float directionToTarget = TargetPosition.x - currentPos.x;
        float distance = Mathf.Abs(directionToTarget);

        if (distance < ArrivalTolerance)
        {
            CompleteExit(snapToTargetX: false);
            return;
        }

        if (Time.unscaledTime - exitStartedAt > MaxExitDurationSeconds)
        {
            Debug.LogWarning(
                $"[PlayerExitState] Timed out before reaching exit target. Snapping from {currentPos.x:F2} to {TargetPosition.x:F2}.");
            CompleteExit(snapToTargetX: true);
            return;
        }

        float sign = Mathf.Sign(directionToTarget);
        player.Movement.SnapFacingDirection((int)sign);
        player.Movement.SetHorizontalInput(sign);
    }

    public override void Exit()
    {
        base.Exit();
        if (player.Movement != null)
        {
            player.Movement.SetHorizontalInput(0f);

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// Snaps to the exit target and returns to Idle. Used when Exit was interrupted mid-walk.
    /// </summary>
    public void ForceComplete()
    {
        CompleteExit(snapToTargetX: true);
    }

    private void CompleteExit(bool snapToTargetX)
    {
        if (player.Movement != null)
        {
            player.Movement.SetHorizontalInput(0f);
            if (snapToTargetX)
            {
                player.Movement.TeleportHorizontally(TargetPosition.x);
            }
        }

        FinishedNormally = true;
        fsm.ChangeState(fsm.IdleState);
    }
}
