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
            stateMachine?.RouteJumpHeld(false);
            return;
        }

        // Always update directional snapshot in the buffer
        if (stateMachine != null && stateMachine.InputBuffer != null)
            stateMachine.InputBuffer.SetDirectionalInput(moveInput);

        // Forward continuous inputs via centralized routing
        if (stateMachine != null)
        {
            stateMachine.RouteMoveInput(moveInput);

            if (playerInput != null && playerInput.actions != null)
            {
                InputAction jumpAction = playerInput.actions["Jump"];
                if (jumpAction != null)
                    stateMachine.RouteJumpHeld(jumpAction.IsPressed());
            }
        }

        // Team switching
        if (teamLoader != null && Keyboard.current != null)
        {
            
            if (stateMachine != null && stateMachine.CanSwitchCharacter())
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    teamLoader.SwitchToIndex(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    teamLoader.SwitchToIndex(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    teamLoader.SwitchToIndex(2);
            }
        }

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
        return stateMachine.CurrentStateId == PlayerStateId.Jumping;
    }
}
