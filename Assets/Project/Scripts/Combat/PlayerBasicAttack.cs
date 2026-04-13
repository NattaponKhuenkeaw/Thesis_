using UnityEngine;
using CombatCore;
using System.Collections.Generic;

/// <summary>
/// Combat engine for basic attacks. Handles hitbox activation, motion processing,
/// damage application, and hit detection.
///
/// Phase tracking (Startup→Active→Recovery→Cancel→Done) is driven by
/// PlayerAttackState via FrameDataPlayerState. Combo resolution is driven by
/// ActionResolver. This class focuses ONLY on:
///   - Executing a resolved AttackStepData (animation, hitbox setup)
///   - Hitbox activation/deactivation based on frame index
///   - Motion curve processing
///   - Hit detection via OverlapBox
///   - Hitstop handling
///   - Grab management
/// </summary>
public class PlayerBasicAttack : MonoBehaviour, ICharacterCombat
{
    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerAnimator animHandler;
    public PlayerMovement movement;
    public GameObject meleeHitbox;
    public MeleeHitboxController meleeHitboxController;
    public MeleeHitboxDamage meleeHitboxDamage;

    // Runtime state
    private int comboDepth = 0;
    private int currentAttackFrame = 0;
    private int hitstopFramesLeft = 0;
    private ActionPhase currentPhase = ActionPhase.None;
    private Dictionary<AttackCategory, float> lastCategoryUseTime = new Dictionary<AttackCategory, float>();

    // Cached step data
    private AttackStepData currentStepData;
    private int cachedStartupFrames;

    // Track Hitbox state
    private int hitboxActiveCount = 0;

    // Layer mask for enemy detection via OverlapBox
    [Header("Detection")]
    public LayerMask enemyLayerMask;
    private HashSet<EnemyController> hitEnemiesThisStep = new HashSet<EnemyController>();

    // State flags (set by PlayerAttackState)
    private bool isPlungeFalling = false;
    private bool isAirCombo = false;
    private GrabHitbox currentGrabHitbox;
    public ThrowContext activeThrowContext;

    // Cached Animator reference
    private Animator cachedAnimator;

    // Events
    public event System.Action OnComboFinished;
    public event System.Action OnGrabConnected;

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    void Awake()
    {
        playerCharacter = playerCharacter ?? GetComponent<PlayerCharacter>();
        animHandler = animHandler ?? GetComponentInChildren<PlayerAnimator>();
        movement = movement ?? GetComponent<PlayerMovement>();
        if (meleeHitbox != null)
        {
            meleeHitboxController = meleeHitboxController ?? meleeHitbox.GetComponent<MeleeHitboxController>();
            meleeHitboxDamage = meleeHitboxDamage ?? meleeHitbox.GetComponent<MeleeHitboxDamage>();
        }
        activeThrowContext = new ThrowContext(transform.root.GetInstanceID());

        // Cache Animator reference
        if (animHandler != null)
            cachedAnimator = animHandler.GetComponent<Animator>();
    }

    void Start()
    {
        if (meleeHitbox != null) meleeHitbox.SetActive(false);
    }

    void OnEnable()
    {
        if (movement != null)
        {
            movement.OnDashStart += HandleDashStart;
            movement.OnJumpStart += HandleJumpStart;
            movement.OnLand += HandleLandingComboReset;
        }
    }

    void OnDisable()
    {
        if (movement != null)
        {
            movement.OnDashStart -= HandleDashStart;
            movement.OnJumpStart -= HandleJumpStart;
            movement.OnLand -= HandleLandingComboReset;
        }

        ReleaseGrabbedEnemy();
    }

    void FixedUpdate()
    {
        // Hitstop tick-down (still needed for non-state-driven hitstop countdown)
        if (hitstopFramesLeft > 0)
        {
            hitstopFramesLeft--;
            HandleAnimatorHitstop();
        }
    }

    private void CombatLog(string message)
    {
        if (playerCharacter != null && playerCharacter.StateMachine != null && playerCharacter.StateMachine.showCombatLogs)
        {
            playerCharacter.StateMachine.Log("Combat", message, new Color(1f, 0.55f, 0f));
        }
    }

    // =========================================================
    // ICharacterCombat — Step Execution (called by PlayerAttackState)
    // =========================================================

    /// <summary>
    /// Executes a resolved AttackStepData. Sets up animation, hitbox caches,
    /// and resets frame tracking. The ActionResolver determines WHAT to execute;
    /// this method handles HOW to execute it.
    /// </summary>
    public void ExecuteStep(AttackStepData step, int depth, bool airCombo, AttackCategory category)
    {
        if (step == null) return;

        // Cancel any previous motion
        if (movement != null) movement.CancelAttackMotion();

        // Set state
        currentStepData = step;
        comboDepth = depth;
        isAirCombo = airCombo;
        currentPhase = ActionPhase.Startup;
        currentAttackFrame = 0;
        hitstopFramesLeft = 0;
        hitboxActiveCount = 0;

        // Cache phase boundaries
        cachedStartupFrames = step.startupFrames;

        // Clear hit tracking
        hitEnemiesThisStep.Clear();
        DisableHitbox(true);

        CombatLog($"Execute: '{step.stepName}' (Depth {depth}, Air={airCombo}, Category={category})");

        // Trigger animation
        string clipName = step.animationClip != null ? step.animationClip.name : null;
        if (animHandler != null)
        {
            if (airCombo)
                animHandler.TriggerAirAttack(depth, clipName);
            else
                animHandler.TriggerAttackCombo(depth, clipName);
        }

        // Record use time for cooldowns
        lastCategoryUseTime[category] = Time.time;
    }

    // =========================================================
    // ICharacterCombat — Frame Processing (called by PlayerAttackState)
    // =========================================================

    /// <summary>
    /// Called by PlayerAttackState each logic frame to process hitbox/motion/VFX.
    /// The phase is determined by FrameDataPlayerState, not internally.
    /// </summary>
    public void ProcessFrameFromState(int frame, ActionPhase phase)
    {
        if (HasActiveHitstop)
        {
            HandleAnimatorHitstop();
            return;
        }

        currentAttackFrame = frame;
        currentPhase = phase;

        RestoreAnimatorSpeedIfNeeded();

        switch (phase)
        {
            case ActionPhase.Active:
                // Process hitframes within active phase
                ProcessHitFrames(frame);
                break;

            case ActionPhase.Recovery:
            case ActionPhase.Cancel:
                if (hitboxActiveCount > 0)
                {
                    DisableHitbox(true);
                }
                break;
        }

        // Motion Processing (MotionFrameData sub-component)
        ProcessMotion(frame);

        // Perform physics overlap check while any hitbox is active
        if (hitboxActiveCount > 0 && meleeHitbox != null && meleeHitbox.activeSelf)
        {
            PerformHitboxOverlapCheck();
        }
    }

    /// <summary>
    /// Called by PlayerAttackState when entering Recovery phase.
    /// </summary>
    public void OnRecoveryPhaseFromState()
    {
        DisableHitbox(true);
    }

    // =========================================================
    // ICharacterCombat — Queries (used by PlayerMovement + PlayerAttackState)
    // =========================================================

    public bool IsAttacking() { return currentPhase != ActionPhase.None && currentStepData != null; }
    public bool HasActiveHitstop => hitstopFramesLeft > 0;

    public bool ShouldInterruptAttackOnGrounded()
    {
        if (!IsAttacking()) return false;
        if (isPlungeFalling) return false; // Plunge fall should land, not interrupt
        return isAirCombo;
    }

    public bool ShouldSuppressLandingFeedback() { return isPlungeFalling; }
    public bool ShouldLockAirComboInPlace() { return IsAttacking() && !isPlungeFalling && isAirCombo && movement != null && !movement.IsGrounded(); }
    public int GetComboStep() { return comboDepth; }
    public bool CanSwitchDirection() { return currentPhase == ActionPhase.None || currentPhase == ActionPhase.Startup; }
    public bool CanMoveAfterAttack() { return !IsAttacking(); }
    public bool HasGrabbedEnemy() { return activeThrowContext != null && activeThrowContext.HasAnyTargets(); }

    public bool IsOnCooldown(AttackCategory category, float cooldown)
    {
        if (cooldown <= 0) return false;
        if (!lastCategoryUseTime.ContainsKey(category)) return false;

        float finalCooldown = GetFinalSkillCooldown(category, cooldown);
        return Time.time < lastCategoryUseTime[category] + finalCooldown;
    }

    public float GetRemainingCooldown(AttackCategory category, float cooldown)
    {
        if (!lastCategoryUseTime.ContainsKey(category)) return 0f;
        float finalCooldown = GetFinalSkillCooldown(category, cooldown);
        float remain = (lastCategoryUseTime[category] + finalCooldown) - Time.time;
        return Mathf.Max(remain, 0f);
    }

    public AttackStepData GetCurrentStepData() { return currentStepData; }

    // =========================================================
    // ICharacterCombat — State Flags (set by PlayerAttackState)
    // =========================================================

    public void SetPlungeFalling(bool falling) { isPlungeFalling = falling; }
    public void SetAirCombo(bool airCombo) { this.isAirCombo = airCombo; }

    // =========================================================
    // ICharacterCombat — Combo Management
    // =========================================================

    public void ResetCombo()
    {
        bool wasAirCombo = isAirCombo && comboDepth > 0 && !isPlungeFalling;

        if (wasAirCombo && movement != null && !movement.IsGrounded())
            movement.TriggerAirComboCooldown();

        comboDepth = 0;
        currentPhase = ActionPhase.None;
        currentStepData = null;

        hitboxActiveCount = 0;
        hitstopFramesLeft = 0;
        currentAttackFrame = 0;
        isPlungeFalling = false;
        isAirCombo = false;
        DisableHitbox(true);

        if (movement != null) movement.CancelAttackMotion();
        if (animHandler != null) animHandler.ResetCombo();

        if (cachedAnimator != null)
            cachedAnimator.speed = 1f;

        CombatLog("Combo: Reset");
        OnComboFinished?.Invoke();
    }

    public void CancelCurrentAttackButKeepComboProgress()
    {
        currentPhase = ActionPhase.None;
        currentAttackFrame = 0;
        hitstopFramesLeft = 0;
        hitboxActiveCount = 0;
        DisableHitbox(true);

        if (cachedAnimator != null)
            cachedAnimator.speed = 1f;

        if (movement != null) movement.CancelAttackMotion();
        OnComboFinished?.Invoke();
    }

    // =========================================================
    // Hitframe Processing
    // =========================================================

    private void ProcessHitFrames(int frame)
    {
        if (currentStepData == null) return;

        int frameInActivePhase = frame - cachedStartupFrames - 1;

        if (HasStructuredHitboxes(currentStepData))
        {
            ProcessStructuredHitboxes(frameInActivePhase);
            return;
        }
    }

    private void ProcessStructuredHitboxes(int frameInActivePhase)
    {
        List<HitboxBase> hitboxes = currentStepData.hitData.hitboxes;
        for (int i = 0; i < hitboxes.Count; i++)
        {
            HitboxBase hitbox = hitboxes[i];
            if (hitbox == null) continue;

            if (frameInActivePhase == hitbox.startFrameOffset) TriggerHitbox(hitbox);
            if (frameInActivePhase == hitbox.startFrameOffset + hitbox.activeFrames) DisableHitbox();
        }
    }

    private void TriggerHitbox(HitboxBase hitbox)
    {
        if (hitbox is NormalHitbox normalHitbox)
        {
            TriggerHitFrame(normalHitbox: normalHitbox);
            return;
        }

        if (hitbox is GrabHitbox grabHitbox)
        {
            TriggerGrabFrame(grabHitbox);
            return;
        }

        CombatLog($"Hitbox '{hitbox.HitboxTypeName}' is not yet supported by the runtime attack processor.");
    }

    private bool HasStructuredHitboxes(AttackStepData step)
    {
        return step != null
            && step.hitData != null
            && step.hitData.hitboxes != null
            && step.hitData.hitboxes.Count > 0;
    }

    // =========================================================
    // Motion Processing
    // =========================================================

    private void ProcessMotion(int frame)
    {
        if (currentStepData == null || currentStepData.motionData == null
            || currentStepData.motionData.keyframes == null) return;

        foreach (var kf in currentStepData.motionData.keyframes)
        {
            if (frame == kf.startFrame && movement != null)
            {
                movement.ApplyAttackMovement(kf);
            }
        }
    }

    // =========================================================
    // Hitstop
    // =========================================================

    public void ApplyHitstop(int frames)
    {
        if (frames > hitstopFramesLeft)
        {
            hitstopFramesLeft = frames;
            HandleAnimatorHitstop();
        }
    }

    private void HandleAnimatorHitstop()
    {
        if (cachedAnimator != null)
        {
            cachedAnimator.speed = hitstopFramesLeft > 0 ? 0f : 1f;
        }
    }

    private void RestoreAnimatorSpeedIfNeeded()
    {
        if (HasActiveHitstop) return;

        if (cachedAnimator != null)
            cachedAnimator.speed = 1f;
    }

    // =========================================================
    // Hitbox Enable/Disable
    // =========================================================

    public void EnableHitbox()
    {
        hitboxActiveCount++;
        if (meleeHitbox != null) meleeHitbox.SetActive(true);
    }

    public void DisableHitbox(bool force = false)
    {
        if (force) hitboxActiveCount = 0;
        else hitboxActiveCount--;

        if (hitboxActiveCount <= 0)
        {
            hitboxActiveCount = 0;
            currentGrabHitbox = null;
            if (meleeHitbox != null) meleeHitbox.SetActive(false);
        }
    }

    // =========================================================
    // Hit Frame Triggering
    // =========================================================

    private void TriggerHitFrame(NormalHitbox normalHitbox)
    {
        float damageMult = 1f;
        currentGrabHitbox = null;

        if (normalHitbox != null)
        {
            damageMult = normalHitbox.damageMultiplier;
            if (meleeHitboxController != null) meleeHitboxController.ApplyHitbox(normalHitbox);
        }

        float finalDamage = GetFinalAttackDamage(damageMult);

        if (meleeHitboxDamage != null)
            meleeHitboxDamage.SetHitData(finalDamage, false, 0f, 0f, hitbox: normalHitbox);

        CombatLog($"Active: (Frame {currentAttackFrame})");
        EnableHitbox();

        // Clear per-step hit tracking so each distinct hitframe window can hit targets
        hitEnemiesThisStep.Clear();
    }

    private void TriggerGrabFrame(GrabHitbox grabHitbox)
    {
        if (grabHitbox == null) return;

        currentGrabHitbox = grabHitbox;

        if (meleeHitboxController != null)
            meleeHitboxController.ApplyHitbox(grabHitbox);

        float finalDamage = GetFinalAttackDamage(1f);

        if (meleeHitboxDamage != null)
            meleeHitboxDamage.SetGrabData(finalDamage, grabHitbox, false);

        CombatLog($"Grab Active: (Frame {currentAttackFrame})");
        EnableHitbox();
        hitEnemiesThisStep.Clear();
    }

    // =========================================================
    // Hit Detection (OverlapBox)
    // =========================================================

    private void PerformHitboxOverlapCheck()
    {
        if (meleeHitbox == null || meleeHitboxController == null) return;

        BoxCollider2D col = meleeHitboxController.hitboxCollider;
        if (col == null) return;

        Vector2 worldPos = col.transform.TransformPoint(col.offset);
        Vector2 size = col.size;
        float angle = col.transform.eulerAngles.z;

        Collider2D[] results = Physics2D.OverlapBoxAll(worldPos, size, angle, enemyLayerMask);

        // Sort by distance for grabs
        if (currentGrabHitbox != null && results.Length > 1)
        {
            Vector2 origin = GetGrabOrigin();
            System.Array.Sort(results, (a, b) =>
            {
                float distA = Vector2.SqrMagnitude((Vector2)a.transform.position - origin);
                float distB = Vector2.SqrMagnitude((Vector2)b.transform.position - origin);
                return distA.CompareTo(distB);
            });
        }

        bool didGrabAny = false;

        foreach (var hit in results)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null && !hitEnemiesThisStep.Contains(enemy) && CanProcessHitboxTarget(enemy))
            {
                hitEnemiesThisStep.Add(enemy);
                CombatLog($"HIT: {enemy.gameObject.name} (Damage: {meleeHitboxDamage.DamageValue:F1})");

                if (meleeHitboxDamage != null)
                    meleeHitboxDamage.ProcessHit(enemy);

                if (currentGrabHitbox != null)
                    didGrabAny = true;
            }
        }

        // Notify PlayerAttackState that a grab connected (for tree branching)
        if (didGrabAny && HasGrabbedEnemy())
        {
            OnGrabConnected?.Invoke();
        }
    }

    // =========================================================
    // Grab Management
    // =========================================================

    public void RegisterGrabbedEnemy(EnemyController enemy)
    {
        if (enemy == null) return;
        if (activeThrowContext == null)
            activeThrowContext = new ThrowContext(transform.root.GetInstanceID());

        activeThrowContext.AddGrabbedTarget(enemy);
    }

    public void ClearGrabbedEnemy(EnemyController enemy = null)
    {
        if (activeThrowContext != null)
            activeThrowContext.ClearTargets();
    }

    public bool IsHoldingEnemy(EnemyController enemy)
    {
        return activeThrowContext != null && activeThrowContext.HasTarget(enemy);
    }

    public void ReleaseGrabbedEnemy()
    {
        if (activeThrowContext != null)
            activeThrowContext.BreakGrab();
    }

    public bool ValidateGrabTarget(EnemyController enemy, GrabHitbox grabHitbox = null)
    {
        if (grabHitbox == null) grabHitbox = currentGrabHitbox;
        if (enemy == null || grabHitbox == null || !enemy.IsAlive) return false;

        if (!grabHitbox.canGrabAirborne && IsEnemyAirborneForGrab(enemy))
            return false;

        return true;
    }

    /// <summary>
    /// Maintain grabbed enemies through a tree-based grab follow-up chain.
    /// Called by PlayerAttackState when branching into grab follow-up nodes.
    /// </summary>
    public void MaintainGrabThroughFollowUp(int framesUntilRelease)
    {
        if (activeThrowContext == null || !activeThrowContext.HasAnyTargets() || currentGrabHitbox == null)
            return;

        if (framesUntilRelease <= 0) return;

        Transform grabSocket = meleeHitboxController != null && meleeHitboxController.attackAnchor != null
            ? meleeHitboxController.attackAnchor
            : transform.root;

        foreach (var enemyToMaintain in activeThrowContext.GetGrabbedEnemies())
        {
            if (enemyToMaintain != null)
            {
                enemyToMaintain.MaintainGrab(
                    transform.root.GetInstanceID(),
                    grabSocket,
                    currentGrabHitbox.snapOffset,
                    framesUntilRelease,
                    currentGrabHitbox.recoveryFrames
                );
            }
        }
    }

    // =========================================================
    // Internal Event Handlers
    // =========================================================

    private void HandleDashStart()
    {
        if (isAirCombo && comboDepth > 0 && !isPlungeFalling && movement != null && !movement.IsGrounded())
            CancelCurrentAttackButKeepComboProgress();
        else
            ResetCombo();
    }

    private void HandleJumpStart()
    {
        ResetCombo();
    }

    private void HandleLandingComboReset()
    {
        if (isAirCombo && comboDepth > 0) ResetCombo();
    }

    // =========================================================
    // Utility
    // =========================================================

    private float GetFinalAttackDamage(float hitFrameMultiplier)
    {
        if (playerCharacter == null || playerCharacter.characterObject == null) return hitFrameMultiplier;
        
        float baseDamage = playerCharacter.characterObject.stats.attack * hitFrameMultiplier;
        return baseDamage;
    }

    private Vector3 GetSpawnPosition(Vector2 offset)
    {
        Transform origin = transform; // Default to character root
        Vector3 finalOffset = new Vector3(offset.x, offset.y, 0f);

        if (movement != null && movement.FacingDirection == -1)
            finalOffset.x = -Mathf.Abs(finalOffset.x);
        else
            finalOffset.x = Mathf.Abs(finalOffset.x);

        return origin.position + finalOffset;
    }

    private Vector2 GetFacingDirection()
    {
        if (movement != null && movement.FacingDirection == -1)
            return Vector2.left;
        return Vector2.right;
    }

    // =========================================================
    // Skill Tree Buff Logic (Moved from Controllers)
    // =========================================================

    private float GetFinalSkillDamage(float baseValue)
    {
        float result = baseValue;
        SkillTreeData tree = playerCharacter.characterObject.skillTreeData;
        if (tree == null || tree.nodes == null) return result;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            var node = tree.nodes[i];
            if (node != null && node.unlocked && node.effectType == SkillTreeEffectType.SkillDamageBonus)
                result += node.effectValue;
        }
        return result;
    }

    private float GetFinalUltimateDamage(float baseValue)
    {
        float result = baseValue;
        SkillTreeData tree = playerCharacter.characterObject.skillTreeData;
        if (tree == null || tree.nodes == null) return result;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            var node = tree.nodes[i];
            if (node != null && node.unlocked && node.effectType == SkillTreeEffectType.UltimateDamageBonus)
                result += node.effectValue;
        }
        return result;
    }

    public float GetFinalSkillCooldown(AttackCategory category, float baseValue)
    {
        if (category != AttackCategory.Skill) return baseValue;

        float result = baseValue;
        SkillTreeData tree = playerCharacter.characterObject.skillTreeData;
        if (tree == null || tree.nodes == null) return result;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            var node = tree.nodes[i];
            if (node != null && node.unlocked && node.effectType == SkillTreeEffectType.SkillCooldownReduction)
                result -= node.effectValue;
        }
        return Mathf.Max(result, 0.1f);
    }

    public float GetFinalUltimateDuration(float baseValue)
    {
        float result = baseValue;
        SkillTreeData tree = playerCharacter.characterObject.skillTreeData;
        if (tree == null || tree.nodes == null) return result;

        for (int i = 0; i < tree.nodes.Length; i++)
        {
            var node = tree.nodes[i];
            if (node != null && node.unlocked && node.effectType == SkillTreeEffectType.UltimateDurationBonus)
                result += node.effectValue;
        }
        return result;
    }

    private Vector2 GetGrabOrigin()
    {
        if (meleeHitboxController != null && meleeHitboxController.attackAnchor != null)
            return meleeHitboxController.attackAnchor.position;

        if (movement != null)
            return movement.transform.position;

        return transform.position;
    }

    private int GetFacingDirectionForGrab()
    {
        if (movement != null)
            return movement.FacingDirection;

        float yRotation = transform.eulerAngles.y;
        return (yRotation > 90f && yRotation < 270f) ? -1 : 1;
    }

    private bool IsEnemyAirborneForGrab(EnemyController enemy)
    {
        if (enemy == null) return false;

        EnemyReactionState reactionState = enemy.CurrentReactionState;
        if (reactionState == EnemyReactionState.Knockup || reactionState == EnemyReactionState.Knockfall)
            return true;

        return enemy.rb != null && Mathf.Abs(enemy.rb.linearVelocity.y) > 0.05f;
    }

    private bool CanProcessHitboxTarget(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsAlive) return false;
        if (currentGrabHitbox == null) return true;

        if (activeThrowContext != null && activeThrowContext.HasTarget(enemy))
            return false;

        return ValidateGrabTarget(enemy);
    }

    /// <summary>
    /// Calculate how many frames until the first non-grab hitbox across a chain of steps.
    /// Used for grab maintenance timing when walking the combo tree.
    /// </summary>
    public static int CalculateFramesUntilRelease(ComboNode node)
    {
        int accumulatedFrames = 0;
        ComboNode current = node;

        while (current != null && current.stepData != null)
        {
            AttackStepData step = current.stepData;
            int releaseFrame = GetFirstReleaseFrame(step);
            if (releaseFrame >= 0)
                return accumulatedFrames + releaseFrame;

            accumulatedFrames += Mathf.Max(0, step.startupFrames) + Mathf.Max(0, step.activeFrames) + Mathf.Max(0, step.recoveryFrames);
            current = current.defaultNext;
        }

        return accumulatedFrames;
    }

    private static int GetFirstReleaseFrame(AttackStepData step)
    {
        int earliestFrame = int.MaxValue;

        if (step.hitData != null && step.hitData.hitboxes != null)
        {
            for (int i = 0; i < step.hitData.hitboxes.Count; i++)
            {
                HitboxBase hitbox = step.hitData.hitboxes[i];
                if (hitbox == null || hitbox is GrabHitbox) continue;

                earliestFrame = Mathf.Min(
                    earliestFrame,
                    step.startupFrames + 1 + Mathf.Max(0, hitbox.startFrameOffset));
            }
        }

        return earliestFrame == int.MaxValue ? -1 : earliestFrame;
    }
}
