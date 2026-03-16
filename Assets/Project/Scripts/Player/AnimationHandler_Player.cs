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

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;

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
            movement.OnDashStart += TriggerDash;
        }
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.OnJumpStart -= TriggerJump;
            movement.OnDashStart -= TriggerDash;
        }
    }

    void Update()
    {
        if (movement == null || anim == null || rb == null)
            return;

        anim.SetFloat(speedHash, Mathf.Abs(rb.linearVelocity.x));
        anim.SetFloat(verticalVelocityHash, rb.linearVelocity.y);
        anim.SetBool(isGroundedHash, movement.IsGrounded());
    }

    // =========================================================
    // Public Animation Triggers
    // =========================================================
    public void TriggerJump()
    {
        anim.SetTrigger(jumpHash);
    }

    public void TriggerDash()
    {
        anim.SetTrigger(dashHash);
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
        anim.SetInteger(comboStepHash, comboStep);
        anim.SetTrigger(attackHash);
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
}