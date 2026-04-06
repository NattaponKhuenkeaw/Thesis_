using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    // =========================================================
    // Dependencies
    // =========================================================
    [Header("Dependencies")]
    public Animator anim;
    public PlayerMovement movement;
    public PlayerBasicAttack combat;

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
            
        if (combat == null)
            combat = GetComponentInParent<PlayerBasicAttack>();
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
        bool suppressFallAnimation = combat != null && combat.IsPlungeInAirPhase();
        bool isFalling = !suppressFallAnimation && !movement.IsGrounded() && rb.linearVelocity.y < -0.1f && !movement.IsLanding;
        
        if (isFalling && !wasFalling)
        {
            anim.Play(fallHash, 0, 0f);
        }
        
        wasFalling = isFalling;
    }

    private void OnEnable()
    {
        if (movement != null)
        {
            movement.OnJumpStart += TriggerJump;
            movement.OnDoubleJump += TriggerJump;
            movement.OnDashStart += TriggerDash;
            movement.OnLand += TriggerLanding;
        }
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            movement.OnJumpStart -= TriggerJump;
            movement.OnDoubleJump -= TriggerJump;
            movement.OnDashStart -= TriggerDash;
            movement.OnLand -= TriggerLanding;
        }
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
        if (combat != null && combat.ShouldSuppressLandingFeedback())
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

        // Snap immediately without transition delay to align perfectly with Logic Frames
        string[] candidates = new string[] { $"Attack_{comboStep}", $"Attack{comboStep}", $"_Attack{comboStep}" };
        if (TryPlayFirstExistingState(candidates)) return;

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
    public void OnImpactStart() {}

    public void OnRecoveryStart() {}
    public void OnAttackEnd() {}

    // =========================================================
    // Sync Logic
    // =========================================================
    public void SyncCombatAnimation(int comboStep, int currentFrame, int totalFrames, bool isAir)
    {
        if (anim == null || totalFrames <= 0) return;

        // Calculate normalized time (0 to 1) based on frame
        // currentFrame is 1-indexed in our logic
        float normalizedTime = Mathf.Clamp01((currentFrame - 1) / (float)totalFrames);

        string[] candidates;
        if (isAir)
        {
            candidates = GetAirAttackStateCandidates(comboStep);
        }
        else
        {
            candidates = new string[] { $"Attack_{comboStep}", $"Attack{comboStep}", $"_Attack{comboStep}" };
        }

        foreach (string stateName in candidates)
        {
            string resolvedStateName = stateName.Contains(".") ? stateName : $"Base Layer.{stateName}";
            int stateHash = Animator.StringToHash(resolvedStateName);
            
            if (anim.HasState(0, stateHash))
            {
                // Force play at the correct normalized time
                anim.Play(stateHash, 0, normalizedTime);
                return;
            }
        }
    }
}
