using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerMovement movement;
    public BasicAttack basicAttack;
    public TeamLoader teamLoader;

    // =========================================================
    // Input Cache
    // =========================================================
    private Vector2 moveInput;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponent<BasicAttack>();

        if (teamLoader == null)
            teamLoader = FindObjectOfType<TeamLoader>();
    }

    void Update()
    {
        if (movement != null)
        {
            movement.Move(moveInput);
        }

        // =====================================================
        // Prototype Team Slot Switch Input
        // =====================================================
        if (teamLoader != null && Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                teamLoader.SwitchToIndex(0);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                teamLoader.SwitchToIndex(1);
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                teamLoader.SwitchToIndex(2);
            }
        }
    }

    // =========================================================
    // Input Callbacks
    // =========================================================
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