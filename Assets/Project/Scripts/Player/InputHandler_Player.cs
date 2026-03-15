using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayerMovement movement;
    public BasicAttack basicAttack;

    private Vector2 moveInput;

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (basicAttack == null) basicAttack = GetComponent<BasicAttack>();
    }

    void Update()
    {
        if (movement != null)
        {
            movement.Move(moveInput);
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && movement != null)
        {
            movement.Jump();
        }
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed && movement != null)
        {
            movement.Dash();
        }
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed && basicAttack != null)
        {
            basicAttack.OnAttackInput();
        }
    }
}