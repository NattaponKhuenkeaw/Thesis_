using System.Collections.Generic;
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
    public PlayerCombat combat;

    // =========================================================
    // Animator Hashes
    // =========================================================
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int verticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");

    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int dashHash = Animator.StringToHash("Dash");
    private readonly int isDieHash = Animator.StringToHash("IsDie");
    private readonly int takeDamageHash = Animator.StringToHash("TakeDamage");
    private readonly int landingHash = Animator.StringToHash("Landing");
    private readonly int isLandingHash = Animator.StringToHash("IsLanding");
    private readonly int fallHash = Animator.StringToHash("Fall");

    // =========================================================
    // Private Runtime Data
    // =========================================================
    private Rigidbody2D rb;
    private bool wasFalling;
    private HashSet<int> animParamHashes;

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
            combat = GetComponentInParent<PlayerCombat>();

        animParamHashes = new HashSet<int>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (var param in anim.parameters)
            {
                animParamHashes.Add(param.nameHash);
            }
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
        // Suppress falling animation during ANY combat action (Attack, Skill, Ultimate) 
        // to prevent locomotion from overriding the custom action animation.
        bool isAttacking = combat != null && combat.IsAttacking();
        
        // CRITICAL: Strict suppression. If attacking, we skip ALL locomotion updates.
        // We also force IsGrounded to false in the Animator to prevent "Any State -> Idle" 
        // transitions from triggering while we are playing combat animations.
        if (isAttacking)
        {
            anim.SetBool(isGroundedHash, false);
            anim.SetFloat(speedHash, 0f);
            anim.SetFloat(verticalVelocityHash, 0f);
            wasFalling = false;
            return;
        }

        bool suppressFallAnimation = (combat != null && combat.ShouldSuppressLandingFeedback());
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
        // Force immediate snap to Dash state to ensure responsiveness from Cancel frames
        PlayStateByName("Dash", "Dash"); 
    }

    public void TriggerLanding()
    {
        if (combat != null && combat.ShouldSuppressLandingFeedback())
            return;

        SafeSetTrigger(landingHash);
    }

    public void TriggerTakeDamage()
    {
        SafeSetTrigger(takeDamageHash);
    }

    public void SetDie(bool isDead)
    {
        anim.SetBool(isDieHash, isDead);
    }

    public void TriggerAttackCombo(int comboStep, string explicitClipName = null)
    {
        // Prioritize attack animation over lingering jump transitions when attacking mid-air.
        SafeResetTrigger(jumpHash);

        // Snap immediately without transition delay to align perfectly with Logic Frames
        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(explicitClipName))
        {
            candidates.Add(explicitClipName);
        }
        else
        {
            candidates.Add($"Attack_{comboStep}");
            candidates.Add($"Attack{comboStep}");
            candidates.Add($"_Attack{comboStep}");
        }

        if (!TryPlayFirstExistingState(candidates.ToArray()))
        {
            Debug.LogWarning($"[PlayerAnimator] Missing State in Animator! Could not find state for Attack Step {comboStep} or Explicit Clip '{explicitClipName}'.");
        }
    }

    public void TriggerAirAttack(int comboStep, string explicitClipName = null)
    {
        if (anim == null)
            return;

        SafeResetTrigger(jumpHash);
        SafeResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);

        int normalizedStep = Mathf.Max(1, comboStep);

        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(explicitClipName))
        {
            candidates.Add(explicitClipName);
        }
        else
        {
            candidates.AddRange(GetAirAttackStateCandidates(normalizedStep));
        }

        if (!TryPlayFirstExistingState(candidates.ToArray()))
        {
            Debug.LogWarning($"[PlayerAnimator] Missing State in Animator! Could not find state for Air Attack Step {comboStep} or Explicit Clip '{explicitClipName}'.");
        }
    }

    public void TriggerPlungeFall(string stateName)
    {
        if (anim == null)
            return;

        SafeResetTrigger(jumpHash);
        SafeResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);
        PlayStateByName(stateName, "Plunge_Fall");
    }

    public void TriggerPlungeImpact(string stateName)
    {
        if (anim == null)
            return;

        SafeResetTrigger(jumpHash);
        SafeResetTrigger(landingHash);
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
        // Deprecated: Combat states are now entirely Code-Driven through State Names
    }

    public void TriggerSkill(string explicitClipName = null, bool airborne = false)
    {
        if (anim == null)
            return;

        SafeResetTrigger(jumpHash);
        SafeResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);

        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(explicitClipName))
        {
            candidates.Add(explicitClipName);
        }
        else
        {
            candidates.AddRange(GetSkillStateCandidates(airborne));
        }

        if (TryPlayFirstExistingState(candidates.ToArray()))
            return;

        PlayStateByName(airborne ? "Air_Skill" : "Skill", "Skill");
    }

    public void TriggerUltimate(string explicitClipName = null, bool airborne = false)
    {
        if (anim == null)
            return;

        SafeResetTrigger(jumpHash);
        SafeResetTrigger(landingHash);
        anim.SetBool(isLandingHash, false);

        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(explicitClipName))
        {
            candidates.Add(explicitClipName);
        }
        else
        {
            candidates.AddRange(GetUltimateStateCandidates(airborne));
        }

        if (TryPlayFirstExistingState(candidates.ToArray()))
            return;

        PlayStateByName(airborne ? "Air_Ultimate" : "Ultimate", "Ultimate");
    }

    public void TriggerSubStepAnimation(AnimationClip clip)
    {
        if (clip == null || anim == null) return;
        
        // Attempt to play the exact clip name as the State name
        if (!TryPlayFirstExistingState(new string[] { clip.name }))
        {
            Debug.LogWarning($"[Combat Engine] Animator State '{clip.name}' could not be found! Please make sure the State Node inside the Animator is named EXACTLY like the Animation Clip.");
        }
    }

    private void PlayStateByName(string stateName, string fallbackStateName)
    {
        List<string> candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(stateName)) candidates.Add(stateName);
        if (!string.IsNullOrWhiteSpace(fallbackStateName) && fallbackStateName != stateName) candidates.Add(fallbackStateName);

        if (TryPlayFirstExistingState(candidates.ToArray()))
            return;

        string resolvedStateName = !string.IsNullOrWhiteSpace(stateName)
            ? stateName
            : fallbackStateName;

        if (string.IsNullOrWhiteSpace(resolvedStateName))
            return;

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

    private string[] GetSkillStateCandidates(bool airborne)
    {
        return airborne
            ? new[] { "Air_Skill", "_AirSkill", "AirSkill", "Skill_Air", "SkillAir", "Skill" }
            : new[] { "Skill" };
    }

    private string[] GetUltimateStateCandidates(bool airborne)
    {
        return airborne
            ? new[] { "Air_Ultimate", "_AirUltimate", "AirUltimate", "Ultimate_Air", "UltimateAir", "Ultimate" }
            : new[] { "Ultimate" };
    }

    private bool TryPlayFirstExistingState(string[] stateNames)
    {
        if (stateNames == null || stateNames.Length == 0 || anim == null)
            return false;

        int layerCount = anim.layerCount;

        foreach (string stateName in stateNames)
        {
            if (string.IsNullOrEmpty(stateName)) continue;

            int shortHash = Animator.StringToHash(stateName);
            string legacyPath = $"Base Layer.{stateName}";
            int legacyHash = Animator.StringToHash(legacyPath);

            // Check all layers for this state
            for (int i = 0; i < layerCount; i++)
            {
                // Try short name
                if (anim.HasState(i, shortHash))
                {
                    var stateInfo = anim.GetCurrentAnimatorStateInfo(i);
                    var nextStateInfo = anim.GetNextAnimatorStateInfo(i);
                    
                    // Guard: If we are already in this state OR transitioning to it, 
                    // and we just started it (normalizedTime < 0.01), don't play again to avoid frame-0 lock.
                    if ((stateInfo.shortNameHash == shortHash && stateInfo.normalizedTime < 0.05f) || 
                        (nextStateInfo.shortNameHash == shortHash))
                    {
                        return true; 
                    }

                    //Debug.Log($"[PlayerAnimator] Playing State: '{stateName}' on Layer {i}. (Prev: {stateInfo.shortNameHash})");
                    anim.Play(shortHash, i, 0f);
                    return true;
                }

                // Try legacy path (only on layer 0)
                if (i == 0 && anim.HasState(i, legacyHash))
                {
                    if (anim.GetCurrentAnimatorStateInfo(i).shortNameHash == legacyHash) return true;

                    //Debug.Log($"[PlayerAnimator] Playing Legacy State: '{legacyPath}' on Layer 0.");
                    anim.Play(legacyHash, i, 0f);
                    return true;
                }
            }
        }

        // If we reached here, NONE of the candidates were found in any layer.
        if (stateNames.Length > 0 && !string.IsNullOrEmpty(stateNames[0]))
        {
            string tried = string.Join(", ", stateNames);
            Debug.LogError($"[PlayerAnimator] STATE NOT FOUND! Tried candidates: [{tried}] across {layerCount} layers. " +
                           "Please ensure the Animator State Name exactly matches one of these strings.");
        }

        return false;
    }

    private void SafeSetTrigger(int triggerHash)
    {
        if (anim != null && animParamHashes != null && animParamHashes.Contains(triggerHash))
            anim.SetTrigger(triggerHash);
    }

    private void SafeResetTrigger(int triggerHash)
    {
        if (anim != null && animParamHashes != null && animParamHashes.Contains(triggerHash))
            anim.ResetTrigger(triggerHash);
    }

    /// <summary>
    /// Manually syncs the animator's playback time to a specific logical frame.
    /// Used for Looping Active Phases to ensure visual and data sync.
    /// </summary>
    public void UpdateActionNormalizedTime(int logicFrame, int totalFrames)
    {
        if (anim == null || totalFrames <= 0) return;

        // Calculate normalized time (0 to 1)
        // logicFrame is 1-indexed, so we subtract 1 for the timeline position.
        float normalizedTime = Mathf.Clamp01((float)(logicFrame - 1) / totalFrames);

        // We apply this to all layers to ensure any active combat animation is synced.
        for (int i = 0; i < anim.layerCount; i++)
        {
            var state = anim.GetCurrentAnimatorStateInfo(i);
            
            // Optimization: Only play if the normalized time difference is significant enough to notice
            // (prevents micro-stuttering from float precision)
            if (Mathf.Abs(state.normalizedTime - normalizedTime) > 0.001f)
            {
                anim.Play(state.fullPathHash, i, normalizedTime);
            }
        }
    }

    // =========================================================
    // Animation Events
    // =========================================================
    public void OnImpactStart() {}

    public void OnRecoveryStart() {}
    public void OnAttackEnd() {}
}
