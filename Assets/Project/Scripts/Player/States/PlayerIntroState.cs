using UnityEngine;
using Project.Core.ZoneState;

/// <summary>
/// Intro state: cinematic state during room transition. The player is fully locked
/// and forced to run to the EntrancePoint.StopPosition.
/// </summary>
public class PlayerIntroState : PlayerState
{
    private const float ArrivalTolerance = 0.2f;
    private const float MaxIntroDurationSeconds = 10f;

    private float introStartedAt;

    public PlayerIntroState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Intro;
    public override PlayerStatePriority Priority => PlayerStatePriority.System;

    public override bool AcceptsMoveInput => false;
    public override bool AcceptsDashInput => false;
    public override bool AcceptsJumpInput => false;
    public override bool AcceptsAttackInput => false;
    public override bool AcceptsSkillInput => false;
    public override bool AcceptsUltimateInput => false;

    public EntrancePoint TargetEntrance { get; set; }

    public override void Enter()
    {
        base.Enter();
        introStartedAt = Time.unscaledTime;
        BeginCinematicTraversal();
        if (player.Movement != null)
        {
            player.Movement.SetHorizontalInput(0f);
        }
    }

    public override void Update()
    {
        if (player.Movement == null || TargetEntrance == null)
        {
            CompleteIntro(snapToStopX: false);
            return;
        }

        Vector3 currentPos = player.transform.position;
        Vector3 stopPos = TargetEntrance.StopPosition;
        float directionToTarget = stopPos.x - currentPos.x;
        float distance = Mathf.Abs(directionToTarget);

        if (distance < ArrivalTolerance)
        {
            CompleteIntro(snapToStopX: false);
            return;
        }

        if (Time.unscaledTime - introStartedAt > MaxIntroDurationSeconds)
        {
            Debug.LogWarning($"[PlayerIntroState] Timed out before reaching entrance stop point. Snapping from {currentPos.x:F2} to {stopPos.x:F2} and returning control.");
            CompleteIntro(snapToStopX: true);
            return;
        }

        float sign = Mathf.Sign(directionToTarget);
        player.Movement.SnapFacingDirection((int)sign);
        player.Movement.SetHorizontalInput(sign);
    }

    public override void Exit()
    {
        base.Exit();
        TargetEntrance = null;
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

    private void CompleteIntro(bool snapToStopX)
    {
        fsm.InputBuffer?.ClearAll();
        if (player.Movement != null)
        {
            player.Movement.SetHorizontalInput(0f);
            player.Movement.CancelAttackMotion();
            if (snapToStopX && TargetEntrance != null)
            {
                player.Movement.TeleportHorizontally(TargetEntrance.StopPosition.x);
            }
        }

        Object.FindFirstObjectByType<SceneBoundaryController>()?.CompleteIntro();
        TargetEntrance = null;
        fsm.ChangeState(fsm.IdleState);
    }
}
