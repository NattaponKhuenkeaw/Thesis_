using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler_Player : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerMovement movement;
    public PlayerBasicAttack basicAttack;
    public TeamLoader teamLoader;
    public SkillController skillController;
    public UltimateController ultimateController;
    public SkillTreeController skillTreeController;

    private Vector2 moveInput;

    void Awake()
    {
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
        if ((skillController != null && skillController.IsCasting()) ||
            (ultimateController != null && ultimateController.IsCasting()))
            return;

        if (value.isPressed && movement != null)
        {
            movement.Dash(moveInput.x);
        }
    }

    public void OnAttack(InputValue value)
    {
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
}