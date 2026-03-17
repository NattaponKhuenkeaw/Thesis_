using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerMovement movement;
    public BasicAttack basicAttack;
    public TeamLoader teamLoader;
    public SkillController skillController;
    public UltimateController ultimateController;

    private Vector2 moveInput;

    void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponent<BasicAttack>();

        if (teamLoader == null)
            teamLoader = FindObjectOfType<TeamLoader>();

        if (skillController == null)
            skillController = GetComponent<SkillController>();

        if (ultimateController == null)
            ultimateController = GetComponent<UltimateController>();
    }

    void Update()
    {
        if (Project.UI.InputLockManager.IsLocked)
        {
            // If we're locked, maybe ensure movement stops? 
            // Better to let PlayerMovement handle stopping if Move(Vector2.zero) isn't called,
            // or we can explicitly pass zero.
            if (movement != null)
                movement.Move(Vector2.zero);
            return;
        }

        if (movement != null)
        {
            movement.Move(moveInput);
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
    }

    public void OnMove(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked)
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if ((skillController != null && skillController.IsCasting()) ||
            (ultimateController != null && ultimateController.IsCasting()))
            return;

        if (value.isPressed && movement != null)
        {
            movement.Jump();
        }
    }

    public void OnDash(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if ((skillController != null && skillController.IsCasting()) ||
            (ultimateController != null && ultimateController.IsCasting()))
            return;

        if (value.isPressed && movement != null)
        {
            movement.Dash();
        }
    }

    public void OnAttack(InputValue value)
    {
        if (Project.UI.InputLockManager.IsLocked) return;

        if ((skillController != null && skillController.IsCasting()) ||
            (ultimateController != null && ultimateController.IsCasting()))
            return;

        if (value.isPressed && basicAttack != null)
        {
            basicAttack.OnAttackInput();
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