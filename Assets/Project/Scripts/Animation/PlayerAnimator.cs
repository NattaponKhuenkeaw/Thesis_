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

        // Standard locomotion parameters
        anim.SetFloat(speedHash, Mathf.Abs(rb.linearVelocity.x));

        float verticalVelocity = rb.linearVelocity.y;
        // Suppress negative vertical velocity in the animator during jump startup to prevent premature "Fall" transitions.
        if (movement.IsJumping && verticalVelocity < 0.1f)
        {
            verticalVelocity = 0.1f;
        }
        anim.SetFloat(verticalVelocityHash, verticalVelocity);

        anim.SetBool(isGroundedHash, movement.IsGrounded());
        anim.SetBool(isLandingHash, movement.IsLanding);

        // Check if we are in an "Active" combat action (Startup, Active, Recovery, Cancel).
        // If we are in the 'Done' phase, we allow locomotion parameters to update 
        // to ensure a smooth transition back to Idle/Move.
        bool isActivelyAttacking = combat != null && combat.IsActiveCombat();
        
        if (isActivelyAttacking)
        {
            // Only force grounded to false if we are specifically in an air combo.
            // For ground attacks, we want the animator to know we are grounded.
            if (combat.IsAirCombo)
            {
                anim.SetBool(isGroundedHash, false);
            }
            
            anim.SetFloat(speedHash, 0f);
            anim.SetFloat(verticalVelocityHash, 0f);
            wasFalling = false;
            return;
        }

        bool suppressFallAnimation = (combat != null && combat.ShouldSuppressLandingFeedback());
        bool isFalling = !suppressFallAnimation && !movement.IsGrounded() && rb.linearVelocity.y < -0.1f && !movement.IsLanding && !movement.IsJumping;
        
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

    public void TriggerAttackCombo(int comboStep, string explicitClipName = null, string stepName = null)
    {
        // Prioritize attack animation over lingering jump transitions when attacking mid-air.
        SafeResetTrigger(jumpHash);

        // Snap immediately without transition delay to align perfectly with Logic Frames
        List<string> candidates = new List<string>();
        if (!string.IsNullOrEmpty(explicitClipName))
        {
            candidates.Add(explicitClipName);
        }
        
        if (!string.IsNullOrEmpty(stepName))
        {
            candidates.Add(stepName);
            // Common permutations
            candidates.Add(stepName.ToLower());
            candidates.Add(stepName.ToUpper());
        }
        
        // Always add generic candidates as fallbacks
        candidates.Add($"Attack_{comboStep}");
        candidates.Add($"Attack{comboStep}");
        candidates.Add($"_Attack{comboStep}");
        candidates.Add($"Attack_{comboStep:D2}"); // Attack_01 style

        if (!TryPlayFirstExistingState(candidates.ToArray()))
        {
            // If still nothing, try to use a trigger if the parameter exists
            SafeSetTrigger(Animator.StringToHash("Attack"));
            anim.SetInteger(Animator.StringToHash("ComboStep"), comboStep);
        }
    }

    public void TriggerAirAttack(int comboStep, string explicitClipName = null, string stepName = null)
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
        
        if (!string.IsNullOrEmpty(stepName))
        {
            candidates.Add(stepName);
            // Common permutations
            candidates.Add(stepName.ToLower());
            candidates.Add(stepName.ToUpper());
            if (!stepName.Contains("_")) candidates.Add($"Air_{stepName}");
        }
        
        // Always add generic candidates as fallbacks
        candidates.AddRange(GetAirAttackStateCandidates(normalizedStep));

        if (!TryPlayFirstExistingState(candidates.ToArray()))
        {
            // Fallback to integer parameter if it exists
            int airStepHash = Animator.StringToHash("AirComboStep");
            if (animParamHashes.Contains(airStepHash))
            {
                anim.SetInteger(airStepHash, normalizedStep);
                SafeSetTrigger(Animator.StringToHash("AirAttack"));
            }
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

        // If we are already in jump/fall states and not significantly into the animation,
        // don't restart it to avoid the "stuck at start frame" visual.
        var state = anim.GetCurrentAnimatorStateInfo(0);
        bool isAlreadyAirborne = state.IsName("Jump_Start") || state.IsName("Plunge_Fall") || state.IsName("Jump_Fall");
        
        if (isAlreadyAirborne && state.normalizedTime < 0.9f)
            return;

        if (movement.IsGrounded())
        {
            anim.Play(jumpHash, 0, 0f);
            return;
        }

        // Only play Fall if we are actually moving downwards.
        // If we have positive velocity (e.g. state was entered while rising), play Jump or stay as-is.
        if (rb.linearVelocity.y < -0.1f)
        {
            anim.Play(fallHash, 0, 0f);
        }
        else
        {
            anim.Play(jumpHash, 0, 0f);
        }
    }

    public void ResetCombo()
    {
        // Force-clear any combat specific booleans if added in the future
        anim.SetBool(isGroundedHash, movement != null ? movement.IsGrounded() : true);
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
        
        // Always add generic candidates as fallbacks
        candidates.AddRange(GetSkillStateCandidates(airborne));

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
        
        // Always add generic candidates as fallbacks
        candidates.AddRange(GetUltimateStateCandidates(airborne));

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
            //Debug.LogWarning($"[Combat Engine] Animator State '{clip.name}' could not be found! Please make sure the State Node inside the Animator is named EXACTLY like the Animation Clip.");
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
            1 => new[] { "Air_Attack1", "_AirAttack1", "AirAttack1", "Air_Attack_1", "AirAttack_1" },
            2 => new[] { "Air_Attack2", "_AirAttack2", "AirAttack2", "Air_Attack_2", "AirAttack_2" },
            3 => new[] { "Air_Attack3", "_AirAttack3", "AirAttack3", "Air_Attack_3", "AirAttack_3" },
            _ => new[] { $"Air_Attack{normalizedStep}", $"_AirAttack{normalizedStep}", $"AirAttack{normalizedStep}", $"Air_Attack_{normalizedStep}" }
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
            //Debug.LogError($"[PlayerAnimator] STATE NOT FOUND! Tried candidates: [{tried}] across {layerCount} layers. " +
                           //"Please ensure the Animator State Name exactly matches one of these strings.");
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

        // CRITICAL GUARD: If we are near the end of the action (normalizedTime ~1.0),
        // we STOP forcing the frame to allow the Animator to blend into the next state.
        if (normalizedTime > 0.99f) return;

        // We apply this to all layers to ensure any active combat animation is synced.
        for (int i = 0; i < anim.layerCount; i++)
        {
            var state = anim.GetCurrentAnimatorStateInfo(i);
            
            // Optimization: Only play if the normalized time difference is significant enough to notice
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
