using UnityEngine;
using CombatCore;

/// <summary>
/// Jump startup state: the brief window between pressing jump and actually
/// leaving the ground. Only movement and facing are allowed — no actions,
/// no input buffering. On the final startup frame, applies the jump impulse
/// and transitions to AirborneState.
///
/// Owns the jump-startup frame counter that was previously inside PlayerMovement.
/// </summary>
public class PlayerJumpStartupState : PlayerState
{
    private int startupFrame;
    private int totalStartupFrames;

    public PlayerJumpStartupState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.JumpStartup;
    public override PlayerStatePriority Priority => PlayerStatePriority.Ability;

    // =========================================================
    // Capabilities — only movement and facing during startup
    // =========================================================
    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => false;
    public override bool AcceptsJumpInput => false;
    public override bool AcceptsAttackInput => false;
    public override bool AcceptsSkillInput => false;
    public override bool AcceptsUltimateInput => false;

    public override void Enter()
    {
        base.Enter();

        if (player.Movement == null)
        {
            fsm.ChangeState(fsm.IdleState);
            return;
        }

        // Consume the jump resource
        player.Movement.ConsumeJumpCount();

        // Calculate startup frames from profile
        float startupTime = player.Movement.profile != null
            ? player.Movement.profile.jumpStartupTime
            : 0.05f;
        totalStartupFrames = Mathf.Max(1, Mathf.RoundToInt(startupTime * FrameDataConstants.LOGIC_FPS));
        startupFrame = 0;
    }

    public override void FixedUpdate()
    {
        if (player.Movement == null) return;

        startupFrame++;

        if (startupFrame >= totalStartupFrames)
        {
            // Apply jump impulse
            player.Movement.ApplyJumpImpulse();

            // Transition to Airborne
            fsm.ChangeState(fsm.AirborneState);
        }
    }

    public override void OnMoveInput(Vector2 input)
    {
        if (player.Movement != null)
            player.Movement.SetMoveInput(input);
    }
}
