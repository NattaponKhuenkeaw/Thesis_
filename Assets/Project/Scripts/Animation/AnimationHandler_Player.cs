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
        bool isFalling = !movement.IsGrounded() && rb.linearVelocity.y < -0.1f && !movement.IsLanding;
        
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
        anim.SetInteger(comboStepHash, comboStep);
        anim.SetTrigger(attackHash);
    }

    public void TriggerPlungeImpact(string stateName)
    {
        if (anim == null)
            return;

        anim.ResetTrigger(jumpHash);
        anim.ResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);

        string resolvedStateName = stateName;
        if (string.IsNullOrWhiteSpace(resolvedStateName))
            resolvedStateName = "Attack_1";

        if (!resolvedStateName.Contains("."))
            resolvedStateName = $"Base Layer.{resolvedStateName}";

        anim.Play(resolvedStateName, 0, 0f);
    }

    public void ResetCombo()
    {
        anim.SetInteger(comboStepHash, 0);
    }

    public void TriggerSkill()
    {
        anim.SetTrigger(skillHash);
    }

    public void TriggerUltimate()
    {
        anim.SetTrigger(ultimateHash);
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
