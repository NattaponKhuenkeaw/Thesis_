// Force Unity compilation refresh
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
            teamLoader = TeamLoader.Instance;
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        EnsurePlayerActionMap();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsurePlayerActionMap();
    }

    private void EnsurePlayerActionMap()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            var map = playerInput.actions.FindActionMap("Player");
            if (map != null)
            {
                playerInput.SwitchCurrentActionMap("Player");
            }
        }
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
    }

    // =========================================================
    // PlayerInput Callbacks (Unity Input System)
    // =========================================================
    public void OnMove(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (!value.isPressed) return;

        moveInput = GetLatestMoveInput();
        stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);

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
        if (IsPointerOverClickBlockedUI()) return;

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
            if (IsPointerOverClickBlockedUI()) return;

            if (!TryPrioritizeJumpBeforeCombatInput())
                return;

            stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);
            stateMachine?.RouteSkillInput();
        }
    }

    public void OnUltimate(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;
        if (InteractableWorldUI.IsPlayerInInteractableArea) return;
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

        InputAction jumpAction = playerInput.actions.FindAction("Jump", throwIfNotFound: false);
        if (jumpAction == null || !jumpAction.IsPressed())
            return true;

        PlayerMovement movement = stateMachine.playerCharacter != null
            ? stateMachine.playerCharacter.Movement
            : null;

        if (movement == null)
            return true;

        if (!stateMachine.IsGroundedContext)
            return true;

        stateMachine.InputBuffer?.BufferJump();
        stateMachine.RouteJumpInput();

        // If jump is being prioritized from the ground, do not allow the same
        // callback to start a ground combat action before the state fully flips airborne.
        return stateMachine.CurrentState == stateMachine.JumpState;
    }

    private void HandleTeamSwitchInput()
    {
        if (Keyboard.current == null || stateMachine == null || stateMachine.InputBuffer == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            stateMachine.InputBuffer.BufferSwitch(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            stateMachine.InputBuffer.BufferSwitch(1);

        stateMachine.TryConsumeBufferedSwitch();
    }

    private Vector2 GetLatestMoveInput()
    {
        if (playerInput == null || playerInput.actions == null)
            return moveInput;

        InputAction moveAction = playerInput.actions.FindAction("Move", throwIfNotFound: false);
        if (moveAction == null)
            return moveInput;

        return moveAction.ReadValue<Vector2>();
    }

    private static readonly List<RaycastResult> s_UIRaycastResults = new List<RaycastResult>();

    private bool IsPointerOverClickBlockedUI()
    {
        if (EventSystem.current == null)
            return false;

        bool isMouseActive = Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);
        bool isTouchActive = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;

        // If neither mouse nor touch triggered this, don't block gamepad/keyboard inputs
        if (!isMouseActive && !isTouchActive)
            return false;

        if (isTouchActive)
        {
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.press.isPressed)
                {
                    PointerEventData touchData = new PointerEventData(EventSystem.current)
                    {
                        position = touch.position.ReadValue()
                    };
                    s_UIRaycastResults.Clear();
                    EventSystem.current.RaycastAll(touchData, s_UIRaycastResults);
                    if (ContainsClickBlockedUI(s_UIRaycastResults))
                        return true;
                }
            }
            return false;
        }

        // Mouse pointer raycast
        PointerEventData mouseData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        s_UIRaycastResults.Clear();
        EventSystem.current.RaycastAll(mouseData, s_UIRaycastResults);

        return ContainsClickBlockedUI(s_UIRaycastResults);
    }

    private static bool ContainsClickBlockedUI(List<RaycastResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            GameObject go = results[i].gameObject;
            if (go == null) continue;

            // 1. Explicit marker component attached to any individual UI (Navigation, Admin, RouteOffer, etc.)
            if (go.GetComponentInParent<Project.UI.UIBlockPlayerClick>() != null)
                return true;

            // 2. Known individual interactive UI components
            if (go.GetComponentInParent<AdminConsole>() != null)
                return true;

            if (go.GetComponentInParent<Project.UI.RoomChoiceUI>() != null)
                return true;

            // 3. Known UI hierarchies by name (Navigation bar, Admin, RouteOffer)
            Transform current = go.transform;
            while (current != null)
            {
                string name = current.name;
                if (name.IndexOf("Navigation", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Admin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("RouteOffer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                current = current.parent;
            }
        }
        return false;
    }

    private bool IsPointerOverUI() => IsPointerOverClickBlockedUI();
}
