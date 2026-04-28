using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input handler: forwards raw input events to the PlayerStateMachine's
/// centralized routing methods. Input buffering is now handled by the
/// FSM's ActionInputBuffer — this handler simply writes to it.
/// </summary>
public class InputHandler_Player : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerInput playerInput;
    public PlayerStateMachine stateMachine;
    public TeamLoader teamLoader;
    public SkillTreeController skillTreeController;

    [Header("Buffer Settings")]
    public float attackBufferTime = 0.12f;
    public float dashBufferTime = 0.12f;

    private Vector2 moveInput;

    void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (stateMachine == null)
            stateMachine = GetComponent<PlayerStateMachine>();

        if (teamLoader == null)
            teamLoader = Object.FindFirstObjectByType<TeamLoader>();

        if (skillTreeController == null)
            skillTreeController = GetComponent<SkillTreeController>();
    }

    void Update()
    {
        if (Project.UI.InputLockManager.IsLocked)
        {
            moveInput = Vector2.zero;
            stateMachine?.RouteMoveInput(Vector2.zero);
            return;
        }

        // Always update directional snapshot in the buffer
        if (stateMachine != null && stateMachine.InputBuffer != null)
            stateMachine.InputBuffer.SetDirectionalInput(moveInput);

        // Forward continuous inputs via centralized routing
        if (stateMachine != null)
        {
            stateMachine.RouteMoveInput(moveInput);
        }

        HandleTeamSwitchInput();

        // Skill tree debug shortcuts
        if (Keyboard.current != null && skillTreeController != null)
        {
            if (Keyboard.current.digit7Key.wasPressedThisFrame)
                skillTreeController.UnlockNodeById("AERO_SKILL_01");
            if (Keyboard.current.digit8Key.wasPressedThisFrame)
                skillTreeController.UnlockNodeById("AERO_SKILL_02");
            if (Keyboard.current.digit9Key.wasPressedThisFrame)
                skillTreeController.UnlockNodeById("AERO_ULT_01");
            if (Keyboard.current.digit0Key.wasPressedThisFrame)
                skillTreeController.ResetAllNodes();
        }
    }

    // =========================================================
    // PlayerInput Callbacks (Unity Input System)
    // =========================================================
    public void OnMove(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        moveInput = value.Get<Vector2>();
        stateMachine?.RouteDownInput(moveInput.y < -0.5f);
    }

    public void OnJump(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        moveInput = GetLatestMoveInput();
        stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);
        stateMachine?.RouteDownInput(moveInput.y < -0.5f);

        PlayerMovement movement = stateMachine != null && stateMachine.playerCharacter != null
            ? stateMachine.playerCharacter.Movement
            : null;

        if (movement != null && moveInput.y < -0.5f)
        {
            if (movement.IsDroppingDown || movement.TryDropDown())
            {
                stateMachine?.InputBuffer?.ConsumeJump();
                return;
            }
        }

        // Buffer jump in the centralized buffer
        if (stateMachine?.InputBuffer != null)
            stateMachine.InputBuffer.BufferJump();

        stateMachine?.RouteJumpInput();
    }

    public void OnDash(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        // Route through FSM — it handles buffering internally
        stateMachine?.RouteDashInput(moveInput.x);
    }

    public void OnAttack(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        if (!TryPrioritizeJumpBeforeCombatInput())
            return;

        stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);
        
        // Route through FSM — it handles buffering internally
        stateMachine?.RouteAttackInput();
    }

    public void OnSkill(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (value.isPressed)
        {
            if (!TryPrioritizeJumpBeforeCombatInput())
                return;

            stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);
            stateMachine?.RouteSkillInput();
        }
    }

    public void OnUltimate(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (value.isPressed)
        {
            stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);
            stateMachine?.RouteUltimateInput();
        }
    }

    private bool TryPrioritizeJumpBeforeCombatInput()
    {
        if (playerInput == null || playerInput.actions == null || stateMachine == null)
            return true;

        InputAction jumpAction = playerInput.actions["Jump"];
        if (jumpAction == null || !jumpAction.IsPressed())
            return true;

        PlayerMovement movement = stateMachine.playerCharacter != null
            ? stateMachine.playerCharacter.Movement
            : null;

        if (movement == null)
            return true;

        bool isGroundedLike = movement.IsGrounded() || movement.IsGroundedForActionSelection();
        if (!isGroundedLike)
            return true;

        stateMachine.InputBuffer?.BufferJump();
        stateMachine.RouteJumpInput();

        // If jump is being prioritized from the ground, do not allow the same
        // callback to start a ground combat action before the state fully flips airborne.
        return stateMachine.CurrentStateId == PlayerStateId.JumpStartup || stateMachine.CurrentStateId == PlayerStateId.Airborne;
    }

    private void HandleTeamSwitchInput()
    {
        if (Keyboard.current == null || stateMachine == null || stateMachine.InputBuffer == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            stateMachine.InputBuffer.BufferSwitch(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            stateMachine.InputBuffer.BufferSwitch(1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            stateMachine.InputBuffer.BufferSwitch(2);

        stateMachine.TryConsumeBufferedSwitch();
    }

    private Vector2 GetLatestMoveInput()
    {
        if (playerInput == null || playerInput.actions == null)
            return moveInput;

        InputAction moveAction = playerInput.actions["Move"];
        if (moveAction == null)
            return moveInput;

        return moveAction.ReadValue<Vector2>();
    }
}
