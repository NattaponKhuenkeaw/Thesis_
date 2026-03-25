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
    private float pendingPlungeUntil = -999f;

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
        ResolvePendingPlungeAttack();

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
                AttackRequestType attackRequest = actionController.GetBufferedAttackType();
                bool accepted = basicAttack.OnAttackInput(attackRequest);
                if (accepted)
                {
                    actionController.ConsumeAttackBuffer();
                }
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();

        if (movement != null)
        {
            movement.SetMoveInput(moveInput);
            movement.SetDownInput(moveInput.y < -0.5f);
        }
    }

    public void OnJump(InputValue value)
    {
        if (movement == null)
            return;

        bool isPressed = value.isPressed;

        if (!isPressed)
            return;

        bool canJumpNow = true;

        if (basicAttack != null && basicAttack.IsAttacking() && !basicAttack.CanJumpCancel())
        {
            canJumpNow = false;
        }

        if (canJumpNow)
        {
            if (moveInput.y < -0.5f && movement.TryDropDown())
                return;

            if (TryQueueDownwardAirSpecial())
                return;

            if (actionController != null && actionController.IsState(PlayerActionState.Attack))
                actionController.SetState(PlayerActionState.Normal);

            movement.Jump();
        }
    }

    public void OnDash(InputValue value)
    {
        if (!value.isPressed || movement == null || actionController == null)
            return;

        if (TryQueueFallKick())
            return;

        bool canDashNow = actionController.CanStartDash();

        if (!canDashNow && basicAttack != null && basicAttack.IsAttacking() && basicAttack.CanDashCancel())
        {
            canDashNow = true;
            basicAttack.ResetCombo();
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
        if(value.isPressed && actionController != null)
        {
            pendingPlungeUntil = -999f;
            actionController.BufferAttack(ResolveAttackRequest());
         
        }
    }

    public void OnSkill(InputValue value)
    {
        if (value.isPressed && skillController != null)
        {
            skillController.UseSkill();
        }
    }

    public void OnUltimate(InputValue value)
    {
        if (value.isPressed && ultimateController != null)
        {
            ultimateController.UseUltimate();
        }
    }

    private AttackRequestType ResolveAttackRequest()
    {
        if (movement != null)
        {
            float dashAttackWindow = basicAttack != null ? basicAttack.GetDashAttackInputWindow() : 0.12f;
            if (movement.IsDashing || movement.WasDashStartedRecently(dashAttackWindow))
                return AttackRequestType.DashAttack;

            if (movement.IsJumping || !movement.IsGrounded())
                return AttackRequestType.AirCombo;
        }

        return AttackRequestType.GroundCombo;
    }

    private bool TryQueueDownwardAirSpecial()
    {
        if (movement == null || movement.IsGrounded() || moveInput.y > -0.5f)
            return false;

        float overrideWindow = basicAttack != null ? basicAttack.GetFallKickOverrideWindow() : 0.12f;
        pendingPlungeUntil = Time.time + Mathf.Max(0f, overrideWindow);

        if (pendingPlungeUntil <= Time.time && actionController != null)
        {
            actionController.BufferAttack(AttackRequestType.PlungeAttack);
            pendingPlungeUntil = -999f;
        }

        return true;
    }

    private bool TryQueueFallKick()
    {
        if (movement == null || movement.IsGrounded() || moveInput.y > -0.5f)
            return false;

        if (Time.time > pendingPlungeUntil)
            return false;

        actionController.BufferAttack(AttackRequestType.FallKick);
        pendingPlungeUntil = -999f;
        return true;
    }

    private void ResolvePendingPlungeAttack()
    {
        if (pendingPlungeUntil <= 0f)
            return;

        if (movement == null || movement.IsGrounded())
        {
            pendingPlungeUntil = -999f;
            return;
        }

        if (Time.time < pendingPlungeUntil)
            return;

        if (actionController != null)
            actionController.BufferAttack(AttackRequestType.PlungeAttack);

        pendingPlungeUntil = -999f;
    }
}
