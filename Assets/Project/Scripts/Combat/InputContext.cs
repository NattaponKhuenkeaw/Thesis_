using UnityEngine;

// =========================================================
// InputContext — Snapshot of game state at the moment attack input occurs.
// Built by PlayerAttackState, consumed by ActionResolver.
// =========================================================

[System.Serializable]
public struct InputContext
{
    public bool isGrounded;
    public Vector2 directionalInput;
    public int facingDirection;         // 1 or -1
    public PlayerStateId previousState;
    public bool hasGrabbedEnemy;
    public bool isAgainstWall;
    public CharacterPassiveController passiveController;
    public CombatRuntimeState runtimeState;

    // =========================================================
    // Computed — resolve directional input to a cardinal direction
    // =========================================================

    private const float THRESHOLD = 0.4f;

    /// <summary>
    /// Converts the raw directional input into a cardinal DirectionRequirement
    /// relative to the character's facing direction.
    /// </summary>
    public DirectionRequirement ResolvedDirection
    {
        get
        {
            if (Mathf.Abs(directionalInput.x) < THRESHOLD
                && Mathf.Abs(directionalInput.y) < THRESHOLD)
                return DirectionRequirement.Neutral;

            // Vertical takes priority over horizontal (typical for 2D fighters)
            if (directionalInput.y > THRESHOLD) return DirectionRequirement.Up;
            if (directionalInput.y < -THRESHOLD) return DirectionRequirement.Down;

            // Horizontal relative to facing
            float facingDot = directionalInput.x * facingDirection;
            if (facingDot > THRESHOLD) return DirectionRequirement.Forward;
            if (facingDot < -THRESHOLD) return DirectionRequirement.Back;

            return DirectionRequirement.Neutral;
        }
    }
}

// =========================================================
// DirectionRequirement — Match condition for directional input.
// =========================================================

public enum DirectionRequirement
{
    Any,        // No direction check (always matches for this field)
    Neutral,    // No directional hold (stick at center)
    Forward,    // Holding toward facing direction
    Back,       // Holding away from facing direction
    Up,         // Holding up
    Down        // Holding down
}
