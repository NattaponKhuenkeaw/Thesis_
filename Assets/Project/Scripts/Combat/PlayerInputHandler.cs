using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler_Player : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerInput playerInput;
    public PlayerMovement movement;
    public PlayerBasicAttack basicAttack;
    public TeamLoader teamLoader;
    public SkillController skillController;
    public UltimateController ultimateController;
    public SkillTreeController skillTreeController;
    public PlayerActionController actionController;

    private Vector2 moveInput;

    void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (actionController == null)
            actionController = GetComponent<PlayerActionController>();

        if (movement == null)
            movement = GetComponent < PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponent<PlayerBasicAttack>();

        if (teamLoader == null)
            teamLoader = FindObjectOfType<TeamLoader>();

        if (skillController == null)
            skillController = GetComponent<SkillController>();

        if (ultimateController == null)
            ultimateController = GetComponent<UltimateController>();

        if (skillTreeController == null)
            skillTreeController = GetComponent<SkillTreeController>();
    }

    void Update()
    {
        if (Project.UI.InputLockManager.IsLocked)
        {
            moveInput = Vector2.zero;
            if (movement != null)
            {
                movement.Move(Vector2.zero);
                movement.SetJumpHeld(false);
            }
            return;
        }

        if (movement != null)
        {
            movement.Move(moveInput);

            if (playerInput != null && playerInput.actions != null)
            {
                InputAction jumpAction = playerInput.actions["Jump"];
                if (jumpAction != null)
                    movement.SetJumpHeld(jumpAction.IsPressed());
            }
        }

        if (teamLoader != null && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                teamLoader.SwitchToIndex(0);

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                teamLoader.SwitchToIndex(1);

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
                teamLoader.SwitchToIndex(2);
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (skillController != null)
                skillController.UseSkill();
        }

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (ultimateController != null)
                ultimateController.UseUltimate();
        }

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

        if (actionController != null)
        {
            if (actionController.HasBufferedDash() && movement != null && actionController.CanStartDash())
            {
                actionController.ConsumeDashBuffer();
                movement.Dash(moveInput.x);
            }
            else if (actionController.HasBufferedAttack() && basicAttack != null)
            {
                bool accepted = basicAttack.OnAttackInput();
                if (accepted)
                {
                    actionController.ConsumeAttackBuffer();
                }
            }
        }
    }

    public void OnMove(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        moveInput = value.Get<Vector2>();

        if (movement != null)
        {
            movement.SetMoveInput(moveInput);
            movement.SetDownInput(moveInput.y < -0.5f);
        }
    }

    public void OnJump(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if (movement == null)
            return;

        bool isPressed = value.isPressed;

        if (!isPressed)
            return;

        bool canJumpNow = true;

        if (canJumpNow)
        {
            if (moveInput.y < -0.5f && movement.TryDropDown())
                return;

            if (moveInput.y < -0.5f && !movement.IsGrounded() && basicAttack != null && basicAttack.TryStartPlungeAttack())
                return;

            if (actionController != null && actionController.IsState(PlayerActionState.Attack))
                actionController.SetState(PlayerActionState.Normal);

            movement.Jump();
        }
    }

    public void OnDash(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if (!value.isPressed || movement == null || actionController == null)
            return;

        bool canDashNow = actionController.CanStartDash();

        if (!canDashNow && basicAttack != null && basicAttack.IsAttacking())
        {
            canDashNow = true;
            actionController.SetState(PlayerActionState.Normal);
        }

        if (canDashNow)
        {
            movement.Dash(moveInput.x);
        }
        else
        {
            actionController.BufferDash();
        }
    }

    public void OnAttack(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if(value.isPressed && actionController != null)
        {
            actionController.BufferAttack();
        
        }
    }

    public void OnSkill(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if (value.isPressed && skillController != null)
        {
            skillController.UseSkill();
        }
    }

    public void OnUltimate(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if (value.isPressed && ultimateController != null)
        {
            ultimateController.UseUltimate();
        }
    }
}
