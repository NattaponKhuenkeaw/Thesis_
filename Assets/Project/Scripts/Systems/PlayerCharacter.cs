using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    // =========================================================
    // Character Data
    // =========================================================
    [Header("Character Data")]
    public CharacterObject characterObject;

    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public PlayerMovement movement;
    public PlayerBasicAttack basicAttack;
    public AnimationHandler animationHandler;
    public CharacterRuntime runtime;

    [Header("Extra Controllers")]
    public SkillController skillController;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponent<PlayerBasicAttack>();

        if (animationHandler == null)
            animationHandler = GetComponentInChildren<AnimationHandler>();

        if (runtime == null)
            runtime = GetComponent<CharacterRuntime>();

        if (skillController == null)
            skillController = GetComponent<SkillController>();
    }

    void Start()
    {
        ApplyCharacterData();
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void ApplyCharacterData()
    {
        if (characterObject == null)
            return;

        ApplyMovementData();
        ApplyAnimationData();

        if (runtime != null)
        {
            runtime.SetCharacter(characterObject);
        }
    }

    public void SetCharacter(CharacterObject newCharacter)
    {
        characterObject = newCharacter;
        ApplyCharacterData();
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void ApplyMovementData()
    {
        if (movement == null || characterObject == null)
            return;

        CharacterStats stats = characterObject.stats;

        movement.speed = stats.moveSpeed;
        movement.jumpForce = stats.jumpForce;
        movement.maxJumpCount = stats.maxJumpCount;
        movement.maxAirDashCount = stats.maxAirDashCount;
        movement.groundAcceleration = stats.groundAcceleration;
        movement.groundDeceleration = stats.groundDeceleration;
        movement.airAcceleration = stats.airAcceleration;
        movement.airDeceleration = stats.airDeceleration;
        movement.airSpeedMultiplier = stats.airSpeedMultiplier;
        movement.dashSpeed = stats.dashSpeed;
        movement.dashDuration = stats.dashDuration;
        movement.dashCooldown = stats.dashCooldown;
    }

    private void ApplyAnimationData()
    {
        if (animationHandler == null || characterObject == null)
            return;

        if (animationHandler.anim == null)
            return;

        if (characterObject.metaData == null)
            return;

        if (characterObject.metaData.animatorController == null)
            return;

        animationHandler.anim.runtimeAnimatorController = characterObject.metaData.animatorController;
    }
}
