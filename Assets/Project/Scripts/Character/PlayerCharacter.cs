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
    public BasicAttack basicAttack;
    public AnimationHandler animationHandler;
    public CharacterRuntime runtime;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (basicAttack == null)
            basicAttack = GetComponent<BasicAttack>();

        if (animationHandler == null)
            animationHandler = GetComponentInChildren<AnimationHandler>();

        if (runtime == null)
            runtime = GetComponent<CharacterRuntime>();
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

        ApplyRuntimeData();
        ApplyMovementData();
        ApplyAttackData();
        ApplyAnimationData();
    }

    public void SetCharacter(CharacterObject newCharacter)
    {
        characterObject = newCharacter;
        ApplyCharacterData();
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private void ApplyRuntimeData()
    {
        if (runtime != null && characterObject != null)
        {
            runtime.SetCharacter(characterObject);
        }
    }

    private void ApplyMovementData()
    {
        if (movement == null || characterObject == null)
            return;

        CharacterStats stats = characterObject.stats;

        movement.speed = stats.moveSpeed;
        movement.jumpForce = stats.jumpForce;
        movement.dashSpeed = stats.dashSpeed;
        movement.dashDuration = stats.dashDuration;
        movement.dashCooldown = stats.dashCooldown;
        movement.jumpStartupTime = stats.jumpStartupTime;
        movement.landingLagTime = stats.landingLagTime;
    }

    private void ApplyAttackData()
    {
        if (basicAttack == null || characterObject == null)
            return;

        CharacterStats stats = characterObject.stats;

        basicAttack.SetAttackData(
            stats.maxCombo,
            stats.comboResetDelay,
            stats.comboQueueOpenTime,
            stats.attack1Duration,
            stats.attack2Duration,
            stats.attack3Duration
        );
    }

    private void ApplyAnimationData()
    {
        if (animationHandler == null || characterObject == null)
            return;

        if (animationHandler.anim != null && characterObject.metaData.animatorController != null)
        {
            animationHandler.anim.runtimeAnimatorController = characterObject.metaData.animatorController;
        }
    }
}