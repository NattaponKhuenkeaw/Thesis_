using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationHandler : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public Animator anim;
    public PlayerMovement movement;

    // =========================================================
    // Animator Hashes
    // =========================================================
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int verticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");

    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int dashHash = Animator.StringToHash("Dash");
    private readonly int attackHash = Animator.StringToHash("Attack");
    private readonly int comboStepHash = Animator.StringToHash("ComboStep");
    private readonly int airComboStepHash = Animator.StringToHash("AirComboStep");
    private readonly int isDieHash = Animator.StringToHash("IsDie");
    private readonly int takeDamageHash = Animator.StringToHash("TakeDamage");
    private readonly int skillHash = Animator.StringToHash("Skill");
    private readonly int ultimateHash = Animator.StringToHash("Ultimate");
    private readonly int landingHash = Animator.StringToHash("Landing");
    private readonly int isLandingHash = Animator.StringToHash("IsLanding");
    private readonly int fallHash = Animator.StringToHash("Fall");

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;
    private bool wasFalling;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (movement != null)
            rb = movement.GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (movement != null)
        {
            movement.OnJumpStart += TriggerJump;
            movement.OnDoubleJump += TriggerJump;
            movement.OnDashStart += TriggerDash;
            movement.OnLand += TriggerLanding;
        }
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.OnJumpStart -= TriggerJump;
            movement.OnDoubleJump -= TriggerJump;
            movement.OnDashStart -= TriggerDash;
            movement.OnLand -= TriggerLanding;
        }
    }

    void Update()
    {
        if (movement == null || anim == null || rb == null)
            return;

        anim.SetFloat(speedHash, Mathf.Abs(rb.linearVelocity.x));
        anim.SetFloat(verticalVelocityHash, rb.linearVelocity.y);
        anim.SetBool(isGroundedHash, movement.IsGrounded());
        anim.SetBool(isLandingHash, movement.IsLanding);

        // Detect falling state (airborne and moving downwards)
        bool suppressFallAnimation = movement.basicAttack != null && movement.basicAttack.IsPlungeInAirPhase();
        bool isFalling = !suppressFallAnimation && !movement.IsGrounded() && rb.linearVelocity.y < -0.1f && !movement.IsLanding;
        
        if (isFalling && !wasFalling)
        {
            anim.Play(fallHash, 0, 0f);
        }
        
        wasFalling = isFalling;
    }

    // =========================================================
    // Public Animation Triggers
    // =========================================================
    public void TriggerJump()
    {
        // Using Play(state, layer, time) to force the animation to restart from beginning
        // even if we are already in the Jump state (essential for double jumps).
        anim.Play(jumpHash, 0, 0f);
    }

    public void TriggerDash()
    {
        anim.SetTrigger(dashHash);
    }

    public void TriggerLanding()
    {
        if (movement != null && movement.basicAttack != null && movement.basicAttack.ShouldSuppressLandingFeedback())
            return;

        anim.SetTrigger(landingHash);
    }

    public void TriggerTakeDamage()
    {
        anim.SetTrigger(takeDamageHash);
    }

    public void SetDie(bool isDead)
    {
        anim.SetBool(isDieHash, isDead);
    }

    public void TriggerAttackCombo(int comboStep)
    {
        // Prioritize attack animation over lingering jump transitions when attacking mid-air.
        anim.ResetTrigger(jumpHash);
        anim.SetInteger(airComboStepHash, 0);
        anim.SetInteger(comboStepHash, comboStep);
        anim.SetTrigger(attackHash);
    }

    public void TriggerAirAttack(int comboStep)
    {
        if (anim == null)
            return;

        anim.ResetTrigger(attackHash);
        anim.ResetTrigger(jumpHash);
        anim.ResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);
        anim.SetInteger(comboStepHash, 0);
        int normalizedStep = Mathf.Max(1, comboStep);
        anim.SetInteger(airComboStepHash, normalizedStep);

        if (TryPlayFirstExistingState(GetAirAttackStateCandidates(normalizedStep)))
            return;

        anim.SetTrigger(attackHash);
    }

    public void TriggerPlungeFall(string stateName)
    {
        if (anim == null)
            return;

        anim.ResetTrigger(jumpHash);
        anim.ResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);
        PlayStateByName(stateName, "Plunge_Fall");
    }

    public void TriggerPlungeImpact(string stateName)
    {
        if (anim == null)
            return;

        anim.ResetTrigger(jumpHash);
        anim.ResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);

        PlayStateByName(stateName, "Attack_1");
    }

    public void RestoreAirborneState()
    {
        if (anim == null || movement == null || rb == null)
            return;

        if (movement.IsGrounded())
            return;

        if (rb.linearVelocity.y > 0.1f)
        {
            anim.Play(jumpHash, 0, 0f);
            return;
        }

        anim.Play(fallHash, 0, 0f);
    }

    public void ResetCombo()
    {
        anim.SetInteger(comboStepHash, 0);
        anim.SetInteger(airComboStepHash, 0);
    }

    public void TriggerSkill()
    {
        anim.SetTrigger(skillHash);
    }

    public void TriggerUltimate()
    {
        anim.SetTrigger(ultimateHash);
    }

    private void PlayStateByName(string stateName, string fallbackStateName)
    {
        string resolvedStateName = stateName;
        if (string.IsNullOrWhiteSpace(resolvedStateName))
            resolvedStateName = fallbackStateName;

        if (!resolvedStateName.Contains("."))
            resolvedStateName = $"Base Layer.{resolvedStateName}";

        anim.Play(resolvedStateName, 0, 0f);
    }

    private string[] GetAirAttackStateCandidates(int comboStep)
    {
        int normalizedStep = Mathf.Max(1, comboStep);
        return normalizedStep switch
        {
            1 => new[] { "Air_Attack1", "_AirAttack1", "AirAttack1" },
            2 => new[] { "Air_Attack2", "_AirAttack2", "AirAttack2" },
            3 => new[] { "Air_Attack3", "_AirAttack3", "AirAttack3" },
            _ => new[] { $"Air_Attack{normalizedStep}", $"_AirAttack{normalizedStep}", $"AirAttack{normalizedStep}" }
        };
    }

    private bool TryPlayFirstExistingState(string[] stateNames)
    {
        if (stateNames == null || stateNames.Length == 0)
            return false;

        foreach (string stateName in stateNames)
        {
            string resolvedStateName = stateName.Contains(".")
                ? stateName
                : $"Base Layer.{stateName}";

            if (!anim.HasState(0, Animator.StringToHash(resolvedStateName)))
                continue;

            anim.Play(resolvedStateName, 0, 0f);
            return true;
        }

        return false;
    }

    // =========================================================
    // Animation Events
    // =========================================================
    public void OnImpactStart()
    {
        if (movement != null && movement.basicAttack != null)
            movement.basicAttack.OnImpactStart();
    }

    public void OnRecoveryStart()
    {
        if (movement != null && movement.basicAttack != null)
            movement.basicAttack.OnRecoveryStart();
    }

    public void OnAttackEnd()
    {
        if (movement != null && movement.basicAttack != null)
            movement.basicAttack.OnAttackEnd();
    }
}
