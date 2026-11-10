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
    private bool skillPressed;
    private bool skillPending;
    private float skillPressedAt;
    private float skillHoldDuration;
    private CharacterObject skillInputCharacter;
    private bool ultimatePressed;
    private bool ultimatePending;
    private float ultimatePressedAt;
    private float ultimateHoldDuration;
    private CharacterObject ultimateInputCharacter;
    private static int lastAdminConsoleToggleFrame = -1;
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
        CancelSkillGesture();
        CancelUltimateGesture();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        EnsurePlayerActionMap();
        Debug.Log($"[AdminConsoleShortcut] Player input polling is active on '{name}'. Press Ctrl+Alt+C.");
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
        HandleAdminConsoleShortcut();

        if (Project.UI.InputLockManager.IsLocked)
        {
            CancelUltimateGesture();
            CancelSkillGesture();
            moveInput = Vector2.zero;
            stateMachine?.RouteMoveInput(Vector2.zero);
            return;
        }

        UpdateUltimateGesture();
        UpdateSkillGesture();

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

    private static void HandleAdminConsoleShortcut()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        bool altPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

        if (!keyboard.cKey.wasPressedThisFrame)
        {
            return;
        }

        Debug.Log($"[AdminConsoleShortcut] Game input received C. Ctrl={ctrlPressed}, Alt={altPressed}.");
        if (!ctrlPressed || !altPressed || lastAdminConsoleToggleFrame == Time.frameCount)
        {
            return;
        }

        lastAdminConsoleToggleFrame = Time.frameCount;
        AdminConsole adminConsole = Object.FindFirstObjectByType<AdminConsole>(FindObjectsInactive.Include);
        if (adminConsole == null)
        {
            Debug.LogWarning("[AdminConsoleShortcut] Runtime received Ctrl+Alt+C, but no AdminConsole was found in the loaded scenes.");
            return;
        }

        Debug.Log($"[AdminConsoleShortcut] Game input matched Ctrl+Alt+C; toggling '{adminConsole.name}' in scene '{adminConsole.gameObject.scene.name}', activeSelf={adminConsole.gameObject.activeSelf}.");
        adminConsole.ToggleAdminControl();
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
        if (Project.UI.InputLockManager.IsLocked || !value.isPressed) return;
        moveInput = GetLatestMoveInput();
        stateMachine?.InputBuffer?.SetDirectionalInput(moveInput);
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
        if (Project.UI.InputLockManager.IsLocked) { CancelSkillGesture(); return; }
        if (!value.isPressed) { ReleaseSkillGesture(); return; }
        if (IsPointerOverClickBlockedUI() || !TryPrioritizeJumpBeforeCombatInput()) return;
        BeginSkillGesture();
    }

    public void OnUltimate(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked || InteractableWorldUI.IsPlayerInInteractableArea)
        {
            CancelUltimateGesture();
            return;
        }
        if (!value.isPressed)
        {
            ReleaseUltimateGesture();
            return;
        }
        BeginUltimateGesture();
    }

    private void BeginUltimateGesture()
    {
        if (ultimatePressed) return;
        ultimatePressed = true;

        PlayerCharacter actor = stateMachine != null ? stateMachine.playerCharacter : null;
        ultimateInputCharacter = actor != null ? actor.characterObject : null;
        // Resolve the hold candidate through the same authored conditions and
        // locomotion context. Characters without one keep immediate press input.
        if (actor != null && actor.TryResolveActionForContext(AttackCategory.Ultimate,
                out CombatAction action, out _, ultimateInputHeld: true)
            && action is UltimateAction ultimate && ultimate.inputTrigger == UltimateInputTrigger.Hold)
        {
            ultimatePending = true;
            ultimatePressedAt = Time.unscaledTime;
            ultimateHoldDuration = Mathf.Max(0.01f, ultimate.holdDuration);
            return;
        }
        DispatchUltimateGesture(held: false);
    }

    private void UpdateUltimateGesture()
    {
        if (!ultimatePressed) return;
        if (!IsUltimateGestureValid())
        {
            CancelUltimateGesture();
            return;
        }
        InputAction action = playerInput != null && playerInput.actions != null
            ? playerInput.actions.FindAction("Ultimate", throwIfNotFound: false) : null;
        if (action == null || !action.enabled)
        {
            CancelUltimateGesture();
            return;
        }
        if (!action.IsPressed())
        {
            ReleaseUltimateGesture();
            return;
        }
        if (ultimatePending && Time.unscaledTime - ultimatePressedAt >= ultimateHoldDuration)
            DispatchUltimateGesture(held: true);
    }

    private bool IsUltimateGestureValid() => !Project.UI.InputLockManager.IsLocked
        && !Project.UI.PauseMenuController.IsOpen && !InteractableWorldUI.IsPlayerInInteractableArea
        && stateMachine != null && stateMachine.playerCharacter != null
        && playerInput != null && playerInput.currentActionMap != null && playerInput.currentActionMap.enabled
        && stateMachine.playerCharacter.characterObject == ultimateInputCharacter;

    private void ReleaseUltimateGesture()
    {
        if (!IsUltimateGestureValid())
        {
            CancelUltimateGesture();
            return;
        }
        if (ultimatePending)
            DispatchUltimateGesture(Time.unscaledTime - ultimatePressedAt >= ultimateHoldDuration);
        stateMachine?.InputBuffer?.ReleaseUltimateHold();
        CancelUltimateGesture(clearBuffer: false);
    }

    private void DispatchUltimateGesture(bool held)
    {
        ultimatePending = false;
        stateMachine?.InputBuffer?.SetDirectionalInput(GetLatestMoveInput());
        stateMachine?.RouteUltimateInput(held);
        if (held && ultimatePressed) stateMachine?.InputBuffer?.SustainUltimateHold();
    }

    private void CancelUltimateGesture(bool clearBuffer = true)
    {
        if (clearBuffer && ultimatePressed) stateMachine?.InputBuffer?.ClearUltimate();
        ultimatePressed = false;
        ultimatePending = false;
        ultimateInputCharacter = null;
    }

    private void BeginSkillGesture()
    {
        if (skillPressed) return;
        skillPressed = true;

        PlayerCharacter actor = stateMachine != null ? stateMachine.playerCharacter : null;
        skillInputCharacter = actor != null ? actor.characterObject : null;
        // Resolve the hold candidate through the same authored conditions and
        // locomotion context. Characters without one keep immediate press input.
        if (actor != null && actor.TryResolveActionForContext(AttackCategory.Skill,
                out CombatAction action, out _, skillInputHeld: true)
            && action is SkillAction skill && skill.requiresSkillHold)
        {
            skillPending = true;
            skillPressedAt = Time.unscaledTime;
            skillHoldDuration = Mathf.Max(0.01f, skill.skillHoldDuration);
            return;
        }
        DispatchSkillGesture(held: false);
    }

    private void UpdateSkillGesture()
    {
        if (!skillPressed) return;
        if (!IsSkillGestureValid())
        {
            CancelSkillGesture();
            return;
        }
        InputAction action = playerInput != null && playerInput.actions != null
            ? playerInput.actions.FindAction("Skill", throwIfNotFound: false) : null;
        if (action == null || !action.enabled)
        {
            CancelSkillGesture();
            return;
        }
        if (!action.IsPressed())
        {
            ReleaseSkillGesture();
            return;
        }
        if (skillPending && Time.unscaledTime - skillPressedAt >= skillHoldDuration)
            DispatchSkillGesture(held: true);
    }

    private bool IsSkillGestureValid() => !Project.UI.InputLockManager.IsLocked
        && !Project.UI.PauseMenuController.IsOpen && !InteractableWorldUI.IsPlayerInInteractableArea
        && stateMachine != null && stateMachine.playerCharacter != null
        && playerInput != null && playerInput.currentActionMap != null && playerInput.currentActionMap.enabled
        && stateMachine.playerCharacter.characterObject == skillInputCharacter;

    private void ReleaseSkillGesture()
    {
        if (!IsSkillGestureValid())
        {
            CancelSkillGesture();
            return;
        }
        if (skillPending)
            DispatchSkillGesture(Time.unscaledTime - skillPressedAt >= skillHoldDuration);
        stateMachine?.InputBuffer?.ReleaseSkillHold();
        CancelSkillGesture(clearBuffer: false);
    }

    private void DispatchSkillGesture(bool held)
    {
        skillPending = false;
        stateMachine?.InputBuffer?.SetDirectionalInput(GetLatestMoveInput());
        stateMachine?.RouteSkillInput(held);
        if (held && skillPressed) stateMachine?.InputBuffer?.SustainSkillHold();
    }

    private void CancelSkillGesture(bool clearBuffer = true)
    {
        if (clearBuffer && skillPressed) stateMachine?.InputBuffer?.ClearSkill();
        skillPressed = false;
        skillPending = false;
        skillInputCharacter = null;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) { CancelUltimateGesture(); CancelSkillGesture(); }
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

        if (Keyboard.current.digit1Key.wasPressedThisFrame
            || Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            int targetIndex = FindNextLivingTeamSwitchTarget();
            if (targetIndex >= 0)
                stateMachine.InputBuffer.BufferSwitch(targetIndex);
        }

        stateMachine.TryConsumeBufferedSwitch();
    }

    private int FindNextLivingTeamSwitchTarget()
    {
        if (teamLoader == null)
            teamLoader = stateMachine != null ? stateMachine.teamLoader : null;
        if (teamLoader == null)
            return -1;

        CharacterObject[] roster = teamLoader.GetTeamSnapshot();
        if (roster == null || roster.Length == 0)
            return -1;

        int currentIndex = Mathf.Clamp(teamLoader.CurrentIndex, 0, roster.Length - 1);
        for (int offset = 1; offset < roster.Length; offset++)
        {
            int candidateIndex = (currentIndex + offset) % roster.Length;
            CharacterObject candidate = roster[candidateIndex];
            if (candidate != null && teamLoader.IsCharacterAlive(candidate))
                return candidateIndex;
        }

        return -1;
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

        if (isMouseActive && CombatCore.TrainingDebugHUD.IsPointerOverHud(Mouse.current.position.ReadValue()))
            return true;

        if (isTouchActive)
        {
            var touches = Touchscreen.current.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                if (touch.press.isPressed)
                {
                    if (CombatCore.TrainingDebugHUD.IsPointerOverHud(touch.position.ReadValue())) return true;
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
