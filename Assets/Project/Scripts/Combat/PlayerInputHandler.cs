using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simplified input handler: forwards raw input events to the PlayerStateMachine.
/// Input buffering for attacks and dashes is handled via frame-based queuing.
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
    private float attackBufferedUntil = -999f;
    private float dashBufferedUntil = -999f;

    void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (stateMachine == null)
            stateMachine = GetComponent<PlayerStateMachine>();

        if (teamLoader == null)
            teamLoader = FindObjectOfType<TeamLoader>();

        if (skillTreeController == null)
            skillTreeController = GetComponent<SkillTreeController>();
    }

    void Update()
    {
        if (Project.UI.InputLockManager.IsLocked)
        {
            moveInput = Vector2.zero;
            stateMachine?.CurrentState?.OnMoveInput(Vector2.zero);
            stateMachine?.CurrentState?.OnJumpHeld(false);
            return;
        }

        // Forward continuous inputs
        if (stateMachine != null && stateMachine.CurrentState != null)
        {
            stateMachine.CurrentState.OnMoveInput(moveInput);

            if (playerInput != null && playerInput.actions != null)
            {
                InputAction jumpAction = playerInput.actions["Jump"];
                if (jumpAction != null)
                    stateMachine.CurrentState.OnJumpHeld(jumpAction.IsPressed());
            }
        }

        // Team switching
        if (teamLoader != null && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                teamLoader.SwitchToIndex(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                teamLoader.SwitchToIndex(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                teamLoader.SwitchToIndex(2);
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

        // Consume buffered inputs
        ConsumeBufferedInputs();
    }

    // =========================================================
    // PlayerInput Callbacks (Unity Input System)
    // =========================================================
    public void OnMove(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        moveInput = value.Get<Vector2>();
        stateMachine?.CurrentState?.OnDownInput(moveInput.y < -0.5f);
    }

    public void OnJump(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        stateMachine?.CurrentState?.OnJumpInput();
    }

    public void OnDash(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        // Try immediately, otherwise buffer
        dashBufferedUntil = Time.time + dashBufferTime;
    }

    public void OnAttack(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        attackBufferedUntil = Time.time + attackBufferTime;
    }

    public void OnSkill(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (value.isPressed)
            stateMachine?.CurrentState?.OnSkillInput();
    }

    public void OnUltimate(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (value.isPressed)
            stateMachine?.CurrentState?.OnUltimateInput();
    }

    // =========================================================
    // Input Buffering
    // =========================================================
    private void ConsumeBufferedInputs()
    {
        if (stateMachine == null || stateMachine.CurrentState == null)
            return;

        // Try to consume dash buffer
        if (Time.time <= dashBufferedUntil)
        {
            stateMachine.CurrentState.OnDashInput(moveInput.x);
            dashBufferedUntil = -999f;
        }

        // Try to consume attack buffer
        if (Time.time <= attackBufferedUntil)
        {
            stateMachine.CurrentState.OnAttackInput();
            attackBufferedUntil = -999f;
        }
    }
}
