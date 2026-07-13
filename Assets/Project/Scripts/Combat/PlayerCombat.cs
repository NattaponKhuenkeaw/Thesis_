using System.Collections;
using System.Collections.Generic;
using CombatCore;
using UnityEngine;
using UnityEngine.Serialization;
using Project.Combat;
using Project.ShopSystem;

public sealed class AuraEnergyRewardClaim
{
    public float Amount { get; }
    public bool IsClaimed { get; private set; }

    public AuraEnergyRewardClaim(float amount)
    {
        Amount = Mathf.Max(0f, amount);
    }

    public bool TryClaim()
    {
        if (IsClaimed || Amount <= 0f)
            return false;

        IsClaimed = true;
        return true;
    }
}

/// <summary>
/// Central combat executor for player-authored actions.
/// The FSM and ActionResolver decide when/what to execute; this component
/// executes hitboxes, projectiles, motion, cooldown bookkeeping, and
/// passive/runtime state side effects.
/// </summary>
public class PlayerCombat : MonoBehaviour, ICharacterCombat
{
    private const float AuraEnergyChanceReward = 10f;

    [Header("Dependencies")]
    public PlayerCharacter playerCharacter;
    public PlayerAnimator animHandler;
    public PlayerMovement movement;

    [FormerlySerializedAs("meleeHitbox")]
    public GameObject hitboxObject;

    [FormerlySerializedAs("meleeHitboxController")]
    public HitBoxController hitBoxController;

    [FormerlySerializedAs("meleeHitboxDamage")]
    public HitBoxDamage hitBoxDamage;

    public CharacterPassiveController passiveController;

    [Header("Audio & VFX")]
    public VFXSpawner vfxSpawner;
    public HitFeedbackResolver hitFeedbackResolver;

    [Header("Detection")]
    public LayerMask enemyLayerMask;
    [SerializeField] private Vector2 dashThroughDamageBoxSize = new Vector2(1.8f, 2.4f);

    [Header("Air Combo")]
    public float airAttackComboResetCooldown = 0.5f;
    private float airAttackComboCooldownEndTime;

    [Header("Aura Energy Chance Gain Gate")]
    [SerializeField, Min(1)] private int auraEnergyChanceGrantLimitPerWindow = 5;
    [SerializeField, Min(0.01f)] private float auraEnergyChanceGrantWindowSeconds = 1f;

    private readonly Dictionary<CooldownKey, float> lastCategoryUseTime = new Dictionary<CooldownKey, float>();
    private readonly Dictionary<CooldownKey, float> lastActionUseTime = new Dictionary<CooldownKey, float>();
    private readonly Dictionary<CooldownKey, float> pendingCooldownReductions = new Dictionary<CooldownKey, float>();
    private readonly HashSet<EnemyController> hitEnemiesThisStep = new HashSet<EnemyController>();
    private readonly HashSet<EnemyController> grabTargetsThisStep = new HashSet<EnemyController>();

    private AttackStepData currentStepData;
    private AttackCategory currentAttackCategory = AttackCategory.Normal;
    private string currentActionId;
    private string currentActionName;
    private CombatAction currentResolvedCombatAction;
    private float currentActionDamageMultiplier;
    private ActionDamageSourceStat currentActionDamageSourceStat = ActionDamageSourceStat.Attack;
    private float currentAuraEnergyReward;
    private AuraEnergyRewardClaim currentAuraEnergyRewardClaim;
    private AdditionalDamageEffectDefinition[] currentAdditionalDamageEffects =
        System.Array.Empty<AdditionalDamageEffectDefinition>();
    private int comboDepth;
    private int currentAttackFrame;
    private int cachedStartupFrames;
    // Removed hitstopFramesLeft as it is now handled by GameFreezeManager
    private int hitboxActiveCount;
    private ActionPhase currentPhase = ActionPhase.None;
    private bool isAirCombo;
    private Coroutine dashThroughDamageRoutine;

    public PlayerCharacter PlayerCharacter => playerCharacter;
    public Vector2 FacingDirection => GetFacingDirection();
    private bool currentActionStartedGrounded;
    private bool currentActionAllowsGroundToAirTransition;
    private bool isExecutingAutoChainStep;
    private GrabHitbox currentGrabHitbox;
    private bool hadGrabAtStepStart;
    private Animator cachedAnimator;
    private Coroutine priorityResetCoroutine;
    private TeamLoader teamLoader;
    private float auraEnergyChanceGrantWindowStartedAt = -999f;
    private int auraEnergyChanceGrantsThisWindow;

    [System.Serializable]
    public struct GrabParams
    {
        public Vector2 snapOffset;
        public int recoveryFrames;
    }
    private GrabParams lastGrabParams;
    public Vector2 LastGrabOffset => lastGrabParams.snapOffset;

    public ThrowContext activeThrowContext;

    public event System.Action OnComboFinished;
    public event System.Action OnGrabConnected;

    public bool HasActiveHitstop => GameFreezeManager.Instance != null && GameFreezeManager.Instance.IsTimeFrozen;
    public CombatRuntimeState RuntimeState => passiveController != null ? passiveController.RuntimeState : null;
    public string CurrentActionId => currentActionId;
    public string CurrentActionName => currentActionName;
    public AttackCategory CurrentAttackCategory => currentAttackCategory;
    public bool IsExecutingAutoChainStep => isExecutingAutoChainStep;
    public bool HadGrabAtStepStart => hadGrabAtStepStart;
    public bool IsAirCombo => isAirCombo;
    public bool CurrentActionStartedGrounded => currentActionStartedGrounded;
    public bool CurrentActionAllowsGroundToAirTransition => currentActionAllowsGroundToAirTransition;
    public float CurrentActionDamageMultiplier => currentActionDamageMultiplier;
    public ActionDamageSourceStat CurrentActionDamageSourceStat => currentActionDamageSourceStat;
    public float CurrentActionDamageSourceValue => GetCurrentActionDamageSourceValue();
    public float CurrentAuraEnergyReward => currentAuraEnergyReward;
    public AuraEnergyRewardClaim CurrentAuraEnergyRewardClaim => currentAuraEnergyRewardClaim;
    public AdditionalDamageEffectDefinition[] CurrentAdditionalDamageEffects => currentAdditionalDamageEffects;
    public float CurrentSourceAttack => GetModifiedAttackStat();

    void Awake()
    {
        playerCharacter = playerCharacter ?? GetComponent<PlayerCharacter>();
        animHandler = animHandler ?? GetComponentInChildren<PlayerAnimator>();
        movement = movement ?? GetComponent<PlayerMovement>();
        passiveController = passiveController ?? GetComponent<CharacterPassiveController>();

        if (hitboxObject != null)
        {
            hitBoxController = hitBoxController ?? hitboxObject.GetComponent<HitBoxController>();
            hitBoxDamage = hitBoxDamage ?? hitboxObject.GetComponent<HitBoxDamage>();
        }

        activeThrowContext = new ThrowContext(transform.root.GetInstanceID());

        if (animHandler != null)
        {
            cachedAnimator = animHandler.GetComponent<Animator>();
        }
    }

    void Start()
    {
        if (hitboxObject != null)
        {
            hitboxObject.SetActive(false);
        }
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
        // Physics logic handled here. 
        // Hitstop is now handled globally via Time.timeScale in GameFreezeManager.
    }

    public void ExecuteStep(
        AttackStepData step,
        int depth,
        bool airCombo,
        AttackCategory category,
        string resolvedActionName = null,
        string resolvedActionId = null,
        bool isAutoChain = false)
    {
        if (step == null)
        {
            return;
        }

        movement?.CancelAttackMotion();

        hadGrabAtStepStart = HasGrabbedEnemy();
        currentStepData = step;
        comboDepth = depth;
        isAirCombo = airCombo;
        isExecutingAutoChainStep = isAutoChain;
        currentAttackCategory = category;
        currentActionId = resolvedActionId;
        currentActionName = resolvedActionName ?? step.stepName;
        currentAdditionalDamageEffects = passiveController != null
            ? passiveController.BuildAdditionalDamageEffectSnapshot(category, currentActionId, currentActionName, step)
            : System.Array.Empty<AdditionalDamageEffectDefinition>();
        currentPhase = ActionPhase.Startup;
        currentAttackFrame = 0;
        hitboxActiveCount = 0;
        cachedStartupFrames = step.startupFrames;

        if (playerCharacter != null && playerCharacter.StateMachine != null && playerCharacter.StateMachine.showCombatLogs)
        {
            Debug.Log($"[Combat] ExecuteStep: {step.stepName}, isAutoChain={isAutoChain}");
        }

        hitEnemiesThisStep.Clear();
        triggeredEntriesSet.Clear();
        triggeredVFXEntriesSet.Clear();
        CleanupStepVFX(); // --- NEW: Cleanup VFX from previous step ---
        currentGrabHitbox = null; // Clear stale grab state from previous step
        hitEnemiesThisStep.Clear();
        grabTargetsThisStep.Clear();
        DisableHitbox(true);

        // --- NEW: Priority Rendering ---
        SetPriority(true);

        // --- GrabSession: Auto-extend hold through this step ---
        if (activeThrowContext != null && activeThrowContext.IsActive)
        {
            int totalStepFrames = step.startupFrames + step.activeFrames
                                + step.recoveryFrames + 2;
            activeThrowContext.ExtendHold(totalStepFrames, lastGrabParams.recoveryFrames);
        }

        CombatLog($"Execute: '{step.stepName}' (Depth {depth}, Air={airCombo}, Category={category}, Action={currentActionName})");

        string clipName = step.animationClip != null ? step.animationClip.name : null;
        if (animHandler != null)
        {
            switch (category)
            {
                case AttackCategory.Skill:
                    animHandler.TriggerSkill(clipName, airCombo);
                    break;

                case AttackCategory.Ultimate:
                    animHandler.TriggerUltimate(clipName, airCombo);
                    break;

                default:
                    if (airCombo)
                        animHandler.TriggerAirAttack(depth, clipName, currentActionName);
                    else
                        animHandler.TriggerAttackCombo(depth, clipName, currentActionName);
                    break;
            }
        }

        if (depth <= 1 && category == AttackCategory.Normal)
        {
            RecordActionUse(category, currentActionName, currentActionId);

            if (airCombo && !IsStandaloneContextAction(currentActionName))
            {
                TriggerAirComboCooldown();
            }
        }

        passiveController?.NotifyActionStarted(category, currentActionId, currentActionName, step);
    }

    public void ProcessFrameFromState(int frame, ActionPhase phase)
    {
        if (HasActiveHitstop)
        {
            return;
        }

        // --- NEW: Wrap-around detection for Looping Active Phase ---
        // If the current step is looping and we are in the active phase,
        // detect if the frame index has jumped backwards (the wrap).
        if (currentStepData != null && currentStepData.loopActivePhase && phase == ActionPhase.Active)
        {
            bool isWrap = frame < currentAttackFrame;
            bool isSingleFrameLoop = currentStepData.activeFrames == 1;

            if (isWrap || isSingleFrameLoop)
            {
                // Reset all "already triggered" states so they re-trigger on the new loop
                triggeredEntriesSet.Clear();
                // Only clear VFX entries that are marked to loop during active phase
                triggeredVFXEntriesSet.RemoveWhere(entry => entry.loopDuringActive);
                hitEnemiesThisStep.Clear();
                
                // Explicitly reset the HitBoxDamage component so it can hit the same targets again
                hitBoxDamage?.ResetActivationState();

                // GrabSession: Extend hold for the duration of the next loop cycle
                if (activeThrowContext != null && activeThrowContext.IsActive)
                {
                    activeThrowContext.ExtendHold(currentStepData.activeFrames + 1);
                }

                // --- NEW: Re-trigger Motion on Wrap ---
                // Since the logical frame index has jumped backwards (e.g. from 20 to 11),
                // we need to find the "active" motion keyframe for this logical frame and restart it.
                if (currentStepData.motionData != null && currentStepData.motionData.keyframes != null)
                {
                    MotionKeyframe currentMotion = null;
                    foreach (var kf in currentStepData.motionData.keyframes)
                    {
                        // Find the keyframe that started closest to (or at) the current wrapped frame
                        if (kf.startFrame <= frame)
                        {
                            if (currentMotion == null || kf.startFrame > currentMotion.startFrame)
                                currentMotion = kf;
                        }
                    }
                    
                    if (currentMotion != null)
                    {
                        movement?.ApplyAttackMovement(currentMotion);
                    }
                }
            }
        }

        currentAttackFrame = frame;
        currentPhase = phase;

        RestoreAnimatorSpeedIfNeeded();

        switch (phase)
        {
            case ActionPhase.Active:
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

        ProcessMotion(frame);
        ProcessAudio(frame);
        ProcessVFX(frame);

        // Sync visual animation with logic frame (handles visual looping).
        // CRITICAL: We skip this in the 'Done' phase to allow the Animator to 
        // begin its natural exit transitions without logic conflict.
        if (currentStepData != null && animHandler != null && phase != ActionPhase.Done)
        {
            animHandler.UpdateActionNormalizedTime(frame, currentStepData.TotalFrames);
        }

        if (hitboxActiveCount > 0 && hitboxObject != null && hitboxObject.activeSelf)
        {
            UpdateActiveHitboxPosition();
            PerformHitboxOverlapCheck();
        }
    }

    public void OnRecoveryPhaseFromState()
    {
        DisableHitbox(true);
    }

    public bool IsAttacking() => currentPhase != ActionPhase.None && currentStepData != null;
    public bool IsActiveCombat() => IsAttacking() && currentPhase != ActionPhase.Done;

    public bool ShouldInterruptAttackOnGrounded()
    {
        if (!IsAttacking())
        {
            return false;
        }

        return isAirCombo;
    }

    public bool ShouldSuppressLandingFeedback() => false;

    /// <summary>
    /// Whether the player is currently in an airborne combat action.
    /// Used by PlayerMovement to decide whether to apply airborne gravity policy.
    /// Returns true for ANY active combat action (Attack, Skill, Ultimate) while airborne.
    /// </summary>
    public bool IsInAirborneAction() => (currentPhase != ActionPhase.None) && isAirCombo;

    public bool ShouldPreserveGroundAuthority()
    {
        return IsAttacking()
            && currentActionStartedGrounded
            && !currentActionAllowsGroundToAirTransition
            && !isAirCombo;
    }

    /// <summary>
    /// Current gravity scale factor for airborne actions (0..1).
    /// Set by CombatPlayerState based on the current action phase and AirborneGravityPolicy.
    /// </summary>
    private float airborneGravityScaleOverride = 1f;

    public float GetAirborneGravityScale() => airborneGravityScaleOverride;

    /// <summary>
    /// Called by CombatPlayerState to communicate the current phase's gravity scale.
    /// </summary>
    public void SetAirborneGravityScale(float scale) => airborneGravityScaleOverride = Mathf.Clamp01(scale);
    public int GetComboStep() => comboDepth;
    public bool CanSwitchDirection() => currentPhase == ActionPhase.None || currentPhase == ActionPhase.Startup;
    public bool CanMoveAfterAttack() => !IsAttacking();
    public bool HasGrabbedEnemy() => activeThrowContext != null && activeThrowContext.HasAnyTargets();
    public AttackStepData GetCurrentStepData() => currentStepData;

    public EnemyController FindNearestEnemyTarget(float maxRadius)
    {
        Vector2 origin = transform.position;
        EnemyController nearestEnemy = CameraManager.instance != null
            ? CameraManager.instance.GetNearestEngagedEnemyInRadius(origin, maxRadius)
            : null;

        float nearestDistanceSqr = nearestEnemy != null
            ? ((Vector2)nearestEnemy.transform.position - origin).sqrMagnitude
            : float.MaxValue;

        if (maxRadius > 0f)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, maxRadius, enemyLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                EnemyController enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float distanceSqr = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                {
                    continue;
                }

                nearestDistanceSqr = distanceSqr;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    public void HandleOwnerTeleported()
    {
        if (activeThrowContext != null && activeThrowContext.IsActive)
        {
            Transform socket = hitBoxController != null && hitBoxController.attackAnchor != null
                ? hitBoxController.attackAnchor
                : transform;
            activeThrowContext.UpdateSession(socket, LastGrabOffset);
            activeThrowContext.ExtendHold(1, lastGrabParams.recoveryFrames);
        }

        if (hitboxActiveCount > 0 && hitboxObject != null && hitboxObject.activeSelf)
        {
            UpdateActiveHitboxPosition();
            Physics2D.SyncTransforms();
            PerformHitboxOverlapCheck();
            return;
        }

        Physics2D.SyncTransforms();
    }

    public bool IsOnCooldown(AttackCategory category, float cooldown, string actionName = null, string actionId = null)
    {
        if (cooldown <= 0f)
        {
            return false;
        }

        CharacterObject activeChar = playerCharacter != null ? playerCharacter.characterObject : null;
        bool hasSpecificAction = !string.IsNullOrWhiteSpace(actionName) || !string.IsNullOrWhiteSpace(actionId);

        string normalizedActionName = !string.IsNullOrWhiteSpace(actionId)
            ? actionId
            : string.IsNullOrWhiteSpace(actionName) ? "Default" : actionName;
        CooldownKey actionKey = new CooldownKey(activeChar, category, normalizedActionName);

        if (lastActionUseTime.TryGetValue(actionKey, out float actionUseTime))
        {
            return Time.time < actionUseTime + GetFinalCooldown(category, cooldown, actionName, actionId);
        }

        if (hasSpecificAction)
        {
            return false;
        }

        CooldownKey categoryKey = new CooldownKey(activeChar, category);
        if (!lastCategoryUseTime.TryGetValue(categoryKey, out float useTime))
        {
            return false;
        }

        return Time.time < useTime + GetFinalCooldown(category, cooldown, actionName, actionId);
    }

    public float GetRemainingCooldown(AttackCategory category, float cooldown, string actionName = null, string actionId = null)
    {
        CharacterObject activeChar = playerCharacter != null ? playerCharacter.characterObject : null;
        return GetRemainingCooldownForCharacter(activeChar, category, cooldown, actionName, actionId);
    }

    public float GetRemainingCooldownForCharacter(CharacterObject character, AttackCategory category, float cooldown, string actionName = null, string actionId = null)
    {
        bool hasSpecificAction = !string.IsNullOrWhiteSpace(actionName) || !string.IsNullOrWhiteSpace(actionId);

        string normalizedActionName = !string.IsNullOrWhiteSpace(actionId)
            ? actionId
            : string.IsNullOrWhiteSpace(actionName) ? "Default" : actionName;
        CooldownKey actionKey = new CooldownKey(character, category, normalizedActionName);

        if (lastActionUseTime.TryGetValue(actionKey, out float actionUseTime))
        {
            return Mathf.Max(actionUseTime + GetFinalCooldown(category, cooldown, actionName, actionId) - Time.time, 0f);
        }

        if (hasSpecificAction)
        {
            return 0f;
        }

        CooldownKey categoryKey = new CooldownKey(character, category);
        if (!lastCategoryUseTime.TryGetValue(categoryKey, out float useTime))
        {
            return 0f;
        }

        return Mathf.Max(useTime + GetFinalCooldown(category, cooldown, actionName, actionId) - Time.time, 0f);
    }

    public void StartActionCooldown(AttackCategory category, string actionName = null, string actionId = null)
    {
        RecordActionUse(category, actionName ?? currentActionName, actionId ?? currentActionId);
    }

    public void ResetActionCooldown(AttackCategory category, string actionName = null, string actionId = null)
    {
        CharacterObject activeChar = playerCharacter != null ? playerCharacter.characterObject : null;
        if (string.IsNullOrWhiteSpace(actionName) && string.IsNullOrWhiteSpace(actionId))
        {
            CooldownKey categoryKey = new CooldownKey(activeChar, category);
            lastCategoryUseTime.Remove(categoryKey);

            List<CooldownKey> actionKeysToRemove = null;
            foreach (CooldownKey key in lastActionUseTime.Keys)
            {
                if (ReferenceEquals(key.character, activeChar) && key.category == category)
                {
                    actionKeysToRemove ??= new List<CooldownKey>();
                    actionKeysToRemove.Add(key);
                }
            }

            if (actionKeysToRemove != null)
            {
                for (int i = 0; i < actionKeysToRemove.Count; i++)
                {
                    lastActionUseTime.Remove(actionKeysToRemove[i]);
                    pendingCooldownReductions.Remove(actionKeysToRemove[i]);
                }
            }

            pendingCooldownReductions.Remove(categoryKey);
            RemovePendingCooldownReductions(activeChar, category);
            return;
        }

        string normalizedActionName = !string.IsNullOrWhiteSpace(actionId)
            ? actionId
            : string.IsNullOrWhiteSpace(actionName) ? "Default" : actionName;
        CooldownKey actionKey = new CooldownKey(activeChar, category, normalizedActionName);
        lastActionUseTime.Remove(actionKey);
        pendingCooldownReductions.Remove(actionKey);
        pendingCooldownReductions.Remove(new CooldownKey(activeChar, category));
    }

    public void ReduceActionCooldown(AttackCategory category, string actionName, string actionId, float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        CharacterObject activeChar = playerCharacter != null ? playerCharacter.characterObject : null;
        if (!string.IsNullOrWhiteSpace(actionName) || !string.IsNullOrWhiteSpace(actionId))
        {
            string normalizedActionName = !string.IsNullOrWhiteSpace(actionId)
                ? actionId
                : string.IsNullOrWhiteSpace(actionName) ? "Default" : actionName;
            CooldownKey actionKey = new CooldownKey(activeChar, category, normalizedActionName);
            if (lastActionUseTime.ContainsKey(actionKey))
            {
                lastActionUseTime[actionKey] -= seconds;
                return;
            }

            AddPendingCooldownReduction(actionKey, seconds);
            return;
        }

        CooldownKey categoryKey = new CooldownKey(activeChar, category);
        if (lastCategoryUseTime.ContainsKey(categoryKey))
        {
            lastCategoryUseTime[categoryKey] -= seconds;
            return;
        }

        AddPendingCooldownReduction(categoryKey, seconds);
    }

    public void BeginResolvedAction(
        bool startedGrounded,
        bool airCombo,
        bool allowGroundToAirTransition,
        CombatAction action)
    {
        currentActionStartedGrounded = startedGrounded;
        currentActionAllowsGroundToAirTransition = allowGroundToAirTransition;
        isAirCombo = airCombo;
        bool beginsNewAction = action == null
            || currentResolvedCombatAction != action
            || currentAuraEnergyRewardClaim == null;

        currentResolvedCombatAction = action;
        currentActionId = action != null ? action.actionId : null;
        currentActionDamageSourceStat = action != null
            ? action.damageSourceStat
            : ActionDamageSourceStat.Attack;
        currentActionDamageMultiplier = action != null
            ? Mathf.Max(0f, action.actionDamageMultiplier)
            : 0f;
        currentAuraEnergyReward = action != null
            ? Mathf.Max(0f, action.auraEnergyReward)
            : 0f;

        if (passiveController != null && action != null)
        {
            currentActionDamageMultiplier = passiveController.ModifyActionDamageMultiplier(
                action.Category,
                action.actionId,
                action.actionName,
                currentActionDamageMultiplier);
            currentAuraEnergyReward = passiveController.ModifyAuraEnergyReward(
                action.Category,
                action.actionId,
                action.actionName,
                currentAuraEnergyReward);
        }

        if (beginsNewAction)
        {
            currentAuraEnergyRewardClaim = new AuraEnergyRewardClaim(currentAuraEnergyReward);
        }
    }

    public void SetAirCombo(bool airCombo) => isAirCombo = airCombo;

    public bool CanStartAirCombo() => Time.time >= airAttackComboCooldownEndTime;
    public void TriggerAirComboCooldown() => airAttackComboCooldownEndTime = Time.time + airAttackComboResetCooldown;
    public void ResetAirComboCooldown() => airAttackComboCooldownEndTime = 0f;

    public void ResetCombo()
    {
        CleanupStepVFX(); // --- NEW: Cleanup VFX on combo reset ---
        if (currentPhase == ActionPhase.None && comboDepth == 0) return;

        passiveController?.NotifyActionEnded(
            currentAttackCategory,
            currentActionId,
            currentActionName,
            currentStepData);

        comboDepth = 0;
        currentPhase = ActionPhase.None;
        currentStepData = null;
        currentActionId = null;
        currentActionName = null;
        currentResolvedCombatAction = null;
        currentActionDamageSourceStat = ActionDamageSourceStat.Attack;
        currentActionDamageMultiplier = 0f;
        currentAuraEnergyReward = 0f;
        currentAuraEnergyRewardClaim = null;
        currentAdditionalDamageEffects = System.Array.Empty<AdditionalDamageEffectDefinition>();
        hitboxActiveCount = 0;
        // hitstop is now global
        currentAttackFrame = 0;
        isAirCombo = false;
        currentActionStartedGrounded = false;
        currentActionAllowsGroundToAirTransition = false;
        airborneGravityScaleOverride = 1f;
        isExecutingAutoChainStep = false;
        currentGrabHitbox = null;
        lastGrabParams = default;
        grabTargetsThisStep.Clear();

        // End any active grab session on combo reset
        EndGrabSession();

        DisableHitbox(true);
        movement?.CancelAttackMotion();
        animHandler?.ResetCombo();

        // --- NEW: Priority Reset ---
        SetPriority(false, 0.3f);

        if (cachedAnimator != null)
        {
            cachedAnimator.speed = 1f;
        }

        CombatLog("Combo: Reset");
        OnComboFinished?.Invoke();
    }

    public void CancelCurrentAttackButKeepComboProgress()
    {
        CleanupStepVFX(); // --- NEW: Cleanup VFX on attack cancel ---
        passiveController?.NotifyActionEnded(
            currentAttackCategory,
            currentActionId,
            currentActionName,
            currentStepData);
        currentPhase = ActionPhase.None;
        currentAttackFrame = 0;
        // hitstop is now global
        hitboxActiveCount = 0;
        currentGrabHitbox = null;
        grabTargetsThisStep.Clear();
        DisableHitbox(true);

        if (cachedAnimator != null)
        {
            cachedAnimator.speed = 1f;
        }

        currentActionStartedGrounded = false;
        currentActionAllowsGroundToAirTransition = false;
        movement?.CancelAttackMotion();
        OnComboFinished?.Invoke();
    }

    public void ApplyHitstop(int frames)
    {
        if (GameFreezeManager.Instance != null)
        {
            GameFreezeManager.Instance.FreezeFrame(frames);
        }
    }

    public void EnableHitbox()
    {
        hitboxActiveCount++;
        if (hitboxObject != null)
        {
            hitboxObject.SetActive(true);
        }
    }

    public void DisableHitbox(bool force = false)
    {
        if (force)
        {
            hitboxActiveCount = 0;
        }
        else
        {
            hitboxActiveCount--;
        }

        if (hitboxActiveCount <= 0)
        {
            hitboxActiveCount = 0;
            currentGrabHitbox = null;
            if (hitboxObject != null)
            {
                hitboxObject.SetActive(false);
            }
        }
    }

    public void RegisterGrabbedEnemy(EnemyController enemy, Transform socketOverride = null, Vector2 offsetOverride = default)

    {
        if (enemy == null)
        {
            return;
        }

        if (activeThrowContext == null)
        {
            activeThrowContext = new ThrowContext(transform.root.GetInstanceID());
        }

        activeThrowContext.AddGrabbedTarget(enemy);

        // --- Seamless Multi-Step Grab Re-binding ---
        // If the session is active (e.g. hitting an already grabbed enemy with another GrabHitbox),
        // update it with the new grab socket and offset so ExtendHold uses the correct new positions.
        if (activeThrowContext.IsActive)
        {
            Transform socket = socketOverride != null ? socketOverride : hitBoxController != null && hitBoxController.attackAnchor != null 
                             ? hitBoxController.attackAnchor 
                             : transform.root;
            activeThrowContext.UpdateSession(socket, (socketOverride != null) ? offsetOverride : LastGrabOffset);
        }
    }

    public void ClearGrabbedEnemy(EnemyController enemy = null)
    {
        activeThrowContext?.ClearTargets();
    }

    public bool IsHoldingEnemy(EnemyController enemy)
    {
        return activeThrowContext != null && activeThrowContext.HasTarget(enemy);
    }

    public void ReleaseGrabbedEnemy()
    {
        activeThrowContext?.BreakGrab();
    }

    /// <summary>
    /// Ends the grab session normally (clean release).
    /// Called when the throw's final step reaches Recovery, allowing
    /// the slam hitbox's reaction to handle the enemy's transition.
    /// </summary>
    public void EndGrabSession()
    {
        if (activeThrowContext == null || !activeThrowContext.IsActive) return;
        activeThrowContext.ReleaseClean();
        lastGrabParams = default;
    }

    /// <summary>
    /// Force-releases all grabbed enemies with a safe eject direction.
    /// Used when the player is interrupted (hitstun, cancel, state exit).
    /// Applies a short knockdown to the enemy so they don't freeze in place.
    /// </summary>
    public void ForceReleaseGrab(Vector2 ejectDirection, float ejectSpeed = 4f)
    {
        if (activeThrowContext == null || !activeThrowContext.IsActive) return;
        activeThrowContext.ForceRelease(ejectDirection, ejectSpeed);
        lastGrabParams = default;
    }

    public bool ValidateGrabTarget(EnemyController enemy, GrabHitbox grabHitbox = null)
    {
        if (grabHitbox == null)
        {
            grabHitbox = currentGrabHitbox;
        }

        if (enemy == null || grabHitbox == null || !enemy.IsAlive)
        {
            return false;
        }

        // --- Seamless Multi-Step Grab ---
        // If we are ALREADY holding the enemy in a grab session, they are explicitly
        // a valid target for our combo's subsequent grab hitboxes, regardless of their
        // exact velocity or physics state at this microsecond.
        if (IsHoldingEnemy(enemy))
        {
            return true;
        }

        if (!grabHitbox.canGrabAirborne && IsEnemyAirborneForGrab(enemy))
        {
            return false;
        }

        return true;
    }

    public void MaintainGrabThroughFollowUp(int framesUntilRelease)
    {
        if (activeThrowContext == null || !activeThrowContext.HasAnyTargets())
        {
            return;
        }

        if (framesUntilRelease <= 0)
        {
            return;
        }

        Transform grabSocket = hitBoxController != null && hitBoxController.attackAnchor != null
            ? hitBoxController.attackAnchor
            : transform.root;

        foreach (EnemyController enemyToMaintain in activeThrowContext.GetGrabbedEnemies())
        {
            if (enemyToMaintain != null)
            {
                enemyToMaintain.MaintainGrab(
                    transform.root.GetInstanceID(),
                    grabSocket,
                    lastGrabParams.snapOffset,
                    framesUntilRelease,
                    lastGrabParams.recoveryFrames);
            }
        }
    }

    public void NotifyResolvedEnemyHit(
        CharacterObject character,
        AuraEnergyRewardClaim actionRewardClaim)
    {
        if (character == null)
            return;

        if (teamLoader == null)
            teamLoader = TeamLoader.Instance;
        if (teamLoader == null)
            return;

        CharacterStats stats = teamLoader.GetCharacterRuntimeStats(character);
        AuraEnergyRate auraEnergyRate = character.auraEnergyRate;
        float statMultiplier = stats != null
            ? Mathf.Max(0f, stats.aeRecharge)
            : 1f;

        if (actionRewardClaim != null && actionRewardClaim.TryClaim())
        {
            float guaranteedReward = actionRewardClaim.Amount * statMultiplier;
            if (guaranteedReward > 0f)
            {
                teamLoader.AddCharacterAE(character, guaranteedReward);
            }
        }

        // The fixed chance reward is independent from the action-authored reward.
        if (UnityEngine.Random.value > ResolveAuraEnergyGainChance(auraEnergyRate))
            return;

        if (!TryConsumeAuraEnergyChanceGrantSlot())
            return;

        float energyToGain = AuraEnergyChanceReward * statMultiplier;

        if (energyToGain <= 0f)
            return;

        teamLoader.AddCharacterAE(character, energyToGain);
    }

    public float ModifyRuntimeOutgoingDamage(
        CharacterObject character,
        RuntimeEffectDamageContext context,
        float currentDamage)
    {
        PlayerRunInventory inventory = ResolveRunInventory();
        float result = inventory != null
            ? inventory.ModifyOutgoingDamage(character, context, currentDamage)
            : currentDamage;

        if (playerCharacter != null
            && playerCharacter.characterObject == character
            && playerCharacter.RuntimeEffects != null)
        {
            result = playerCharacter.RuntimeEffects.ModifyOutgoingDamage(context, result);
        }

        return result;
    }

    public void NotifyRuntimeHitDealt(CharacterObject character, RuntimeEffectDamageContext context)
    {
        PlayerRunInventory inventory = ResolveRunInventory();
        inventory?.NotifyRuntimeHitDealt(character, context);
        if (playerCharacter != null
            && playerCharacter.characterObject == character
            && playerCharacter.RuntimeEffects != null)
        {
            playerCharacter.RuntimeEffects.NotifyHitDealt(context);
        }
    }

    public PlayerRunInventory ResolveRunInventoryForEffects()
    {
        return ResolveRunInventory();
    }

    private PlayerRunInventory ResolveRunInventory()
    {
        if (teamLoader == null)
        {
            teamLoader = TeamLoader.Instance;
        }

        return teamLoader != null ? teamLoader.RunInventory : null;
    }

    private bool TryConsumeAuraEnergyChanceGrantSlot()
    {
        float now = Time.time;
        float windowDuration = Mathf.Max(0.01f, auraEnergyChanceGrantWindowSeconds);
        if (now - auraEnergyChanceGrantWindowStartedAt >= windowDuration)
        {
            auraEnergyChanceGrantWindowStartedAt = now;
            auraEnergyChanceGrantsThisWindow = 0;
        }

        int grantLimit = Mathf.Max(1, auraEnergyChanceGrantLimitPerWindow);
        if (auraEnergyChanceGrantsThisWindow >= grantLimit)
            return false;

        auraEnergyChanceGrantsThisWindow++;
        return true;
    }

    private static float ResolveAuraEnergyGainChance(AuraEnergyRate auraEnergyRate)
    {
        return auraEnergyRate switch
        {
            AuraEnergyRate.Low => 0.05f,
            AuraEnergyRate.High => 0.15f,
            _ => 0.10f
        };
    }

    public ProjectileSpawnContext BuildProjectileSpawnContext(ProjectileSpawnHitbox projectileHitbox)
    {
        ProjectileData data = projectileHitbox != null ? projectileHitbox.projectileData : null;
        ProjectileType projectileType = projectileHitbox != null ? projectileHitbox.ResolveProjectileType() : ProjectileType.None;
        if (passiveController != null
            && passiveController.TryGetProjectileReplacement(
                currentAttackCategory,
                currentActionId,
                currentActionName,
                currentStepData != null ? currentStepData.stepName : null,
                out ProjectileType replacementProjectileType,
                out ProjectileData replacementProjectileData))
        {
            if (replacementProjectileData != null)
            {
                data = replacementProjectileData;
            }

            if (replacementProjectileType != ProjectileType.None)
            {
                projectileType = replacementProjectileType;
            }
            else if (data != null)
            {
                projectileType = data.projectileType;
            }
        }

        Vector2 resolvedSpawnOffset = projectileHitbox != null && projectileHitbox.HasAuthoredSpawnOffset
            ? projectileHitbox.ResolveSpawnOffset()
            : data != null ? data.spawnOffset : Vector2.zero;
        Vector2 origin = GetSpawnPosition(resolvedSpawnOffset);
        Vector2 direction = GetFacingDirection();
        int remainingHits = data != null ? Mathf.Max(1, data.maxHits) : 1;
        bool needsTarget = data != null && data.RequiresTarget;

        float sourceAttack = GetModifiedAttackStat();
        float actionDamageSourceValue = GetCurrentActionDamageSourceValue();
        CharacterObject sourceCharacter = playerCharacter != null ? playerCharacter.characterObject : null;
        CharacterStats stats = playerCharacter != null ? playerCharacter.Stats : null;

        return new ProjectileSpawnContext
        {
            projectileType = projectileType,
            projectileData = data,
            casterTransform = transform,
            projectileParent = null,
            targetTransform = needsTarget ? FindProjectileTarget(data, direction.x) : null,
            passiveController = passiveController,
            sourceStep = currentStepData,
            sourceActionId = currentActionId,
            sourceActionName = currentActionName,
            category = currentAttackCategory,
            origin = origin,
            direction = direction,
            resolvedSpawnOffset = resolvedSpawnOffset,
            sourceAttack = sourceAttack,
            actionDamageSourceValue = actionDamageSourceValue,
            attackerStats = stats,
            actionDamageMultiplier = currentActionDamageMultiplier,
            actionDamageSourceStat = currentActionDamageSourceStat,
            auraEnergyReward = currentAuraEnergyReward,
            auraEnergyRewardClaim = currentAuraEnergyRewardClaim,
            additionalDamageEffects = currentAdditionalDamageEffects,
            isSkillOrUltimate = currentAttackCategory == AttackCategory.Skill || currentAttackCategory == AttackCategory.Ultimate,
            remainingHits = remainingHits,
            sourceCombat = this,
            sourceCharacter = sourceCharacter
        };
    }

    private void ProcessHitFrames(int frame)
    {
        if (currentStepData == null)
        {
            return;
        }

        int frameInActivePhase = frame - cachedStartupFrames - 1;
        if (currentStepData.hitData == null || currentStepData.hitData.hitboxes == null)
        {
            return;
        }

        for (int i = 0; i < currentStepData.hitData.hitboxes.Count; i++)
        {
            HitboxBase hitbox = currentStepData.hitData.hitboxes[i];
            if (hitbox == null)
            {
                continue;
            }

            if (frameInActivePhase == hitbox.startFrameOffset)
            {
                TriggerHitbox(hitbox);
            }

            if (hitbox is NormalHitbox or GrabHitbox)
            {
                if (frameInActivePhase == hitbox.startFrameOffset + hitbox.activeFrames)
                {
                    DisableHitbox();
                }
            }
        }
    }

    private void TriggerHitbox(HitboxBase hitbox)
    {
        switch (hitbox)
        {
            case NormalHitbox normalHitbox:
                TriggerHitFrame(normalHitbox);
                break;

            case GrabHitbox grabHitbox:
                TriggerGrabFrame(grabHitbox);
                break;

            case ProjectileSpawnHitbox projectileHitbox:
                TriggerProjectileFrame(projectileHitbox);
                break;

            default:
                CombatLog($"Unsupported hitbox type '{hitbox.HitboxTypeName}'.");
                break;
        }
    }

    private void TriggerHitFrame(NormalHitbox normalHitbox)
    {
        currentGrabHitbox = null;

        if (normalHitbox != null && hitBoxController != null)
        {
            hitBoxController.ApplyHitbox(normalHitbox);
        }

        float baseDamage = GetBaseActionDamage(normalHitbox != null ? normalHitbox.damageRatio : 1f);
        hitBoxDamage?.SetHitData(
            baseDamage,
            false,
            false,
            0f,
            0f,
            normalHitbox,
            currentAttackCategory,
            currentActionName,
            currentStepData,
            passiveController,
            currentActionId);
        ConfigurePerEnemyDamageCalculation(hitBoxDamage);
        hitBoxDamage?.ResetActivationState();

        CombatLog($"Active: (Frame {currentAttackFrame})");
        EnableHitbox();
        hitEnemiesThisStep.Clear();
    }

    private void TriggerGrabFrame(GrabHitbox grabHitbox)
    {
        if (grabHitbox == null)
        {
            return;
        }

        currentGrabHitbox = grabHitbox;
        lastGrabParams = new GrabParams 
        { 
            snapOffset = Vector2.zero, 
            recoveryFrames = grabHitbox.recoveryFrames 
        };
        hitBoxController?.ApplyHitbox(grabHitbox);

        float baseDamage = GetBaseActionDamage(grabHitbox.damageRatio);
        hitBoxDamage?.SetGrabData(
            baseDamage,
            false,
            grabHitbox,
            false,
            currentAttackCategory,
            currentActionName,
            currentStepData,
            passiveController,
            currentActionId);
        ConfigurePerEnemyDamageCalculation(hitBoxDamage);
        hitBoxDamage?.ResetActivationState();

        CombatLog($"Grab Active: (Frame {currentAttackFrame})");
        EnableHitbox();
        hitEnemiesThisStep.Clear();
    }

    private void TriggerProjectileFrame(ProjectileSpawnHitbox projectileHitbox)
    {
        if (projectileHitbox == null || projectileHitbox.projectileData == null)
        {
            return;
        }

        if (ProjectileManager.Instance != null)
        {
            ProjectileManager.Instance.SpawnProjectile(BuildProjectileSpawnContext(projectileHitbox));
        }
        else
        {
            Debug.LogError("ProjectileManager instance not found in scene!");
        }
        CombatLog($"Projectile Spawned: {projectileHitbox.projectileData.name}");
    }

    /// <summary>
    /// Re-positions the active hitbox to follow the player's current root position.
    /// Without this, the hitbox stays at the stale position from TriggerHitFrame/TriggerGrabFrame
    /// and won't track the player during dashes or fast movement.
    /// </summary>
    private void UpdateActiveHitboxPosition()
    {
        if (hitBoxController == null || currentStepData == null)
            return;

        if (currentStepData.hitData == null || currentStepData.hitData.hitboxes == null)
            return;

        // Find the currently active hitbox to get its offset
        HitboxBase activeHitbox = null;
        if (currentGrabHitbox != null)
            activeHitbox = currentGrabHitbox;
        else
        {
            // Find the last triggered normal hitbox
            int frameInActivePhase = currentAttackFrame - cachedStartupFrames - 1;
            for (int i = currentStepData.hitData.hitboxes.Count - 1; i >= 0; i--)
            {
                var hb = currentStepData.hitData.hitboxes[i];
                if (hb != null && frameInActivePhase >= hb.startFrameOffset
                    && frameInActivePhase < hb.startFrameOffset + hb.activeFrames)
                {
                    activeHitbox = hb;
                    break;
                }
            }
        }

        if (activeHitbox != null)
            hitBoxController.ApplyHitbox(activeHitbox);
    }

    private void ProcessMotion(int frame)
    {
        if (currentStepData == null || currentStepData.motionData?.keyframes == null)
        {
            return;
        }

        foreach (MotionKeyframe keyframe in currentStepData.motionData.keyframes)
        {
            if (frame == keyframe.startFrame)
            {
                movement?.ApplyAttackMovement(keyframe);
            }
        }
    }

    // =========================================================
    // Audio & VFX Frame Processing
    // =========================================================

    private HashSet<AudioEntry> triggeredEntriesSet = new HashSet<AudioEntry>();
    private HashSet<VFXEntry> triggeredVFXEntriesSet = new HashSet<VFXEntry>();
    private Dictionary<VFXEntry, Project.Combat.GenericVFXPlayer> activeStepVFX = new Dictionary<VFXEntry, Project.Combat.GenericVFXPlayer>();

    private void CleanupStepVFX()
    {
        foreach (var vfx in activeStepVFX.Values)
        {
            if (vfx != null)
            {
                vfx.Stop();
            }
        }
        activeStepVFX.Clear();
    }

    private void ProcessAudio(int frame)
    {
        if (currentStepData == null || currentStepData.audioData?.entries == null) return;

        if (GameAudioManager.Instance == null)
        {
            // Only log once to avoid spamming
            if (Time.frameCount % 60 == 0)
                Debug.LogWarning("PlayerCombat: GameAudioManager.Instance is null! Audio will not play. Please ensure a GameAudioManager component is in your scene.", this);
            return;
        }

        foreach (var entry in currentStepData.audioData.entries)
        {
            if (frame == entry.triggerFrame && !triggeredEntriesSet.Contains(entry))
            {
                triggeredEntriesSet.Add(entry);
                GameAudioManager.Instance.PlayTimelineAudio(entry);
            }
        }
    }

    private void ProcessVFX(int frame)
    {
        if (currentStepData == null || currentStepData.vfxData?.entries == null) return;
        if (vfxSpawner == null) return;

        foreach (var entry in currentStepData.vfxData.entries)
        {
            if (frame == entry.triggerFrame && !triggeredVFXEntriesSet.Contains(entry))
            {
                triggeredVFXEntriesSet.Add(entry);
                
                // --- NEW: Selective Stop for Looping VFX ---
                // If we are re-triggering this entry (e.g. during a loop), stop the previous instance first.
                if (activeStepVFX.TryGetValue(entry, out var oldVfx) && oldVfx != null)
                {
                    oldVfx.Stop();
                }

                var vfx = vfxSpawner.SpawnVFX(entry);
                if (vfx != null)
                {
                    activeStepVFX[entry] = vfx;
                }
            }
        }
    }

    private void HandleAnimatorHitstop()
    {
        // No longer needed as Time.timeScale = 0 pauses all animators.
    }

    private void RestoreAnimatorSpeedIfNeeded()
    {
        // No longer needed as Time.timeScale = 1 restores speed.
    }

    private void PerformHitboxOverlapCheck()
    {
        if (hitboxObject == null || hitBoxController == null)
        {
            return;
        }

        BoxCollider2D collider = hitBoxController.hitboxCollider;
        if (collider == null)
        {
            return;
        }

        Vector2 worldPos = collider.transform.TransformPoint(collider.offset);
        Vector2 size = collider.size;
        float angle = collider.transform.eulerAngles.z;

        Collider2D[] results = Physics2D.OverlapBoxAll(worldPos, size, angle, enemyLayerMask);

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
        EnemyController grabbedEnemy = null;
        foreach (Collider2D result in results)
        {
            EnemyController enemy = result.GetComponentInParent<EnemyController>();
            if (enemy == null || hitEnemiesThisStep.Contains(enemy) || !CanProcessHitboxTarget(enemy))
            {
                continue;
            }

            // --- Multi-Step Grab Logic ---
            // If it's a GrabHitbox, we only allow it to hit each enemy ONCE per step.
            // This prevents the "re-grab" glitch during looping active phases.
            if (currentGrabHitbox != null && grabTargetsThisStep.Contains(enemy))
            {
                continue;
            }

            hitEnemiesThisStep.Add(enemy);
            if (currentGrabHitbox != null)
            {
                grabTargetsThisStep.Add(enemy);
            }

            CombatLog($"HIT: {enemy.gameObject.name} (Damage: {hitBoxDamage?.DamageValue ?? 0f:F1})");

            bool wasAlreadyHeld = IsHoldingEnemy(enemy);
            hitBoxDamage?.ProcessHit(enemy);

            if (currentGrabHitbox != null && !wasAlreadyHeld && IsHoldingEnemy(enemy))
            {
                didGrabAny = true;
                grabbedEnemy ??= enemy;
            }
        }

        if (didGrabAny)
        {
            if (playerCharacter != null && playerCharacter.StateMachine != null && playerCharacter.StateMachine.showCombatLogs)
                Debug.Log($"[Combat] ON_GRAB_SUCCESS Detected on frame {currentAttackFrame}. Invoking success transition.");

            OnGrabConnected?.Invoke();
            passiveController?.NotifyGrabSuccess(
                currentAttackCategory,
                currentActionId,
                currentActionName,
                currentStepData,
                grabbedEnemy);
        }
    }

    private void HandleDashStart()
    {
        passiveController?.NotifyAfterDash();
        StartDashThroughDamageRoutine();

        if (isAirCombo && comboDepth > 0 && movement != null && !movement.IsGrounded())
            CancelCurrentAttackButKeepComboProgress();
        else
            ResetCombo();
    }

    private void StartDashThroughDamageRoutine()
    {
        if (dashThroughDamageRoutine != null)
        {
            StopCoroutine(dashThroughDamageRoutine);
            dashThroughDamageRoutine = null;
        }

        PassiveDashThroughDamageDefinition[] effects = passiveController != null
            ? passiveController.BuildDashThroughDamageEffectSnapshot()
            : System.Array.Empty<PassiveDashThroughDamageDefinition>();
        if (effects.Length == 0)
        {
            return;
        }

        dashThroughDamageRoutine = StartCoroutine(ResolveDashThroughDamage(effects));
    }

    private IEnumerator ResolveDashThroughDamage(PassiveDashThroughDamageDefinition[] effects)
    {
        yield return null;

        HashSet<EnemyController> damagedEnemies = new HashSet<EnemyController>();
        while (movement != null && movement.IsDashing && movement.IsInvincible())
        {
            Vector2 origin = transform.position;
            Collider2D[] hits = Physics2D.OverlapBoxAll(origin, dashThroughDamageBoxSize, 0f, enemyLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                EnemyController enemy = hits[i] != null ? hits[i].GetComponentInParent<EnemyController>() : null;
                if (enemy == null || !enemy.IsAlive || damagedEnemies.Contains(enemy))
                {
                    continue;
                }

                if (TryApplyDashThroughDamage(enemy, effects))
                {
                    damagedEnemies.Add(enemy);
                }
            }

            yield return null;
        }

        dashThroughDamageRoutine = null;
    }

    private bool TryApplyDashThroughDamage(EnemyController enemy, PassiveDashThroughDamageDefinition[] effects)
    {
        bool dealtDamage = false;
        float sourceAttack = GetModifiedAttackStat();
        CharacterStats stats = playerCharacter != null ? playerCharacter.Stats : null;
        CharacterObject character = playerCharacter != null ? playerCharacter.characterObject : null;
        PassiveEventContext context = new PassiveEventContext
        {
            category = AttackCategory.Normal,
            actionId = "dash",
            actionName = "Dash",
            runtimeState = passiveController != null ? passiveController.RuntimeState : null,
            enemy = enemy,
            enemyMaxHp = enemy != null ? enemy.maxHP : 0f
        };

        for (int i = 0; i < effects.Length; i++)
        {
            PassiveDashThroughDamageDefinition effect = effects[i];
            if (effect == null || !effect.CanTrigger(context))
            {
                continue;
            }

            float baseDamage = effect.CalculateBaseDamage(sourceAttack, context, stats);
            RuntimeEffectDamageContext damageContext = new RuntimeEffectDamageContext
            {
                sourceCharacter = character,
                sourceStats = stats,
                targetEnemy = enemy,
                category = AttackCategory.Normal,
                actionId = "dash",
                actionName = "Dash",
                elementType = effect.elementType,
                damageRoutine = DamageRoutine.Additional,
                damage = baseDamage
            };
            RuntimeEffectDamageInput damageInput = new RuntimeEffectDamageInput
            {
                context = damageContext,
                baseDamage = baseDamage,
                defenderDefense = enemy.defense,
                defenderDamageResistance = enemy.damageRes
            };
            PlayerRunInventory inventory = ResolveRunInventory();
            inventory?.ModifyDamageInput(character, damageInput);
            if (playerCharacter != null && playerCharacter.RuntimeEffects != null)
            {
                playerCharacter.RuntimeEffects.ModifyDamageInput(damageInput);
            }

            float resolvedDamage = CombatFormula.CalculateResolvedDamage(
                damageInput.baseDamage,
                DamageRoutine.Additional,
                effect.elementType,
                stats,
                damageInput.defenderDefense,
                AttackCategory.Normal,
                out _,
                damageInput.defenderDamageResistance);
            if (resolvedDamage <= 0f)
            {
                continue;
            }

            Vector2 direction = GetFacingDirection();
            HitReactionData reactionData = HitReactionData.Default(resolvedDamage, direction, 0f);
            reactionData.damageIsFinal = true;
            reactionData.damageRoutine = DamageRoutine.Additional;
            reactionData.elementType = effect.elementType;
            reactionData.isCriticalHit = false;
            reactionData.ignoreElementalIncomingModifiers = damageInput.ignoreElementalIncomingModifiers;
            reactionData.sourceCharacter = character;
            reactionData.sourceStats = stats;
            reactionData.sourceCategory = AttackCategory.Normal;
            reactionData.sourceActionId = "dash";
            reactionData.sourceActionName = "Dash";

            float damageDealt = enemy.TakeHit(reactionData, transform.position);
            dealtDamage |= damageDealt > 0f;
        }

        return dealtDamage;
    }

    private void HandleJumpStart()
    {
        ResetCombo();
    }

    private void HandleLandingComboReset()
    {
        if (isAirCombo && comboDepth > 0)
        {
            ResetCombo();
        }
    }

    private float GetBaseActionDamage(float hitFrameMultiplier)
    {
        float sourceStat = GetCurrentActionDamageSourceValue();
        float resolvedMultiplier = ResolveHitDamageMultiplier(hitFrameMultiplier);
        return Mathf.Max(0f, sourceStat * resolvedMultiplier);
    }

    private float GetModifiedAttackStat()
    {
        CharacterStats stats = playerCharacter != null ? playerCharacter.Stats : null;
        float attackStat = stats != null ? stats.attack : 1f;
        if (passiveController != null)
        {
            attackStat = passiveController.ModifyRuntimeValue(PassiveModifierTarget.AttackStat, attackStat);
        }

        return Mathf.Max(0f, attackStat);
    }

    private float GetCurrentActionDamageSourceValue()
    {
        return GetModifiedActionDamageSourceValue(currentActionDamageSourceStat);
    }

    private float GetModifiedActionDamageSourceValue(ActionDamageSourceStat sourceStat)
    {
        CharacterStats stats = playerCharacter != null ? playerCharacter.Stats : null;
        if (stats == null)
        {
            return sourceStat == ActionDamageSourceStat.Attack ? GetModifiedAttackStat() : 0f;
        }

        float value = sourceStat switch
        {
            ActionDamageSourceStat.MaxHP => stats.maxHP,
            ActionDamageSourceStat.Defense => stats.defense,
            _ => stats.attack
        };

        if (passiveController != null)
        {
            PassiveModifierTarget target = sourceStat switch
            {
                ActionDamageSourceStat.MaxHP => PassiveModifierTarget.MaxHP,
                ActionDamageSourceStat.Defense => PassiveModifierTarget.Defense,
                _ => PassiveModifierTarget.AttackStat
            };
            value = passiveController.ModifyRuntimeValue(target, value);
        }

        return Mathf.Max(0f, value);
    }

    public void RefreshProjectileDamageContext(ref ProjectileSpawnContext context)
    {
        if (playerCharacter == null)
        {
            return;
        }

        context.attackerStats = playerCharacter.Stats;
        context.sourceAttack = GetModifiedAttackStat();
        context.actionDamageSourceValue = GetModifiedActionDamageSourceValue(context.actionDamageSourceStat);
    }

    private float ResolveHitDamageMultiplier(float damageRatio)
    {
        if (currentActionDamageMultiplier <= 0f)
        {
            return Mathf.Max(0f, damageRatio);
        }

        return currentActionDamageMultiplier * Mathf.Clamp01(damageRatio);
    }

    private void ConfigurePerEnemyDamageCalculation(HitBoxDamage targetHitBoxDamage)
    {
        if (targetHitBoxDamage == null || playerCharacter == null)
        {
            return;
        }

        CharacterStats stats = playerCharacter.Stats;
        targetHitBoxDamage.ConfigurePerEnemyDamageCalculation(
            stats,
            GetModifiedAttackStat(),
            currentAttackCategory == AttackCategory.Skill || currentAttackCategory == AttackCategory.Ultimate,
            currentAdditionalDamageEffects,
            this,
            playerCharacter.characterObject,
            currentAuraEnergyRewardClaim);
    }

    public float GetFinalCooldown(AttackCategory category, float baseValue, string actionName = null, string actionId = null)
    {
        float cooldown = baseValue;

        if ((category == AttackCategory.Skill || category == AttackCategory.Ultimate)
            && playerCharacter != null
            && playerCharacter.characterObject != null
            && playerCharacter.Stats != null)
        {
            cooldown = CombatFormula.CalculateCooldownDuration(
                baseValue,
                playerCharacter.Stats.cooldownRate);
        }

        if (passiveController != null)
        {
            cooldown = passiveController.ModifyCooldown(
                category,
                actionId ?? currentActionId,
                actionName ?? currentActionName,
                cooldown);
        }

        return Mathf.Max(0.05f, cooldown);
    }

    public float GetFinalUltimateDuration(float baseValue)
    {
        return passiveController != null
            ? passiveController.ModifyValue(PassiveModifierTarget.UltimateDuration, baseValue)
            : baseValue;
    }

    private Vector2 GetGrabOrigin()
    {
        if (hitBoxController != null && hitBoxController.attackAnchor != null)
            return hitBoxController.attackAnchor.position;

        if (movement != null)
            return movement.transform.position;

        return transform.position;
    }

    private bool IsEnemyAirborneForGrab(EnemyController enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        EnemyReactionState reactionState = enemy.CurrentReactionState;
        if (reactionState == EnemyReactionState.Knockup || reactionState == EnemyReactionState.Knockfall)
        {
            return true;
        }

        // Increased threshold to prevent physics micro-bounces during fast dashes
        // from incorrectly categorizing the enemy as airborne.
        return enemy.rb != null && Mathf.Abs(enemy.rb.linearVelocity.y) > 2.0f;
    }

    private bool CanProcessHitboxTarget(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsAlive)
        {
            return false;
        }

        if (currentGrabHitbox == null)
        {
            return true;
        }

        // --- Seamless Multi-Step Grab ---
        // We removed the block that returned false if the enemy was already in activeThrowContext.
        // Returning false here prevented subsequent GrabHitboxes in a combo chain from hitting 
        // the enemy, which broke multi-step grabs that needed to re-evaluate or update offsets.
        // Instead, we let ValidateGrabTarget handle it, which returns true for already held enemies.

        return ValidateGrabTarget(enemy);
    }

    private Vector2 GetSpawnPosition(Vector2 offset)
    {
        Transform origin = transform;
        Vector2 finalOffset = ProjectileSpatialUtility.ResolveFacingAwareOffset(
            offset,
            movement != null ? movement.FacingDirection : 1f);

        return (Vector2)origin.position + finalOffset;
    }

    private Vector2 GetFacingDirection()
    {
        return movement != null && movement.FacingDirection == -1 ? Vector2.left : Vector2.right;
    }

    private Transform FindProjectileTarget(ProjectileData data, float facingSign)
    {
        if (CameraManager.instance == null)
        {
            return null;
        }

        EnemyController target = CameraManager.instance.GetProjectileTrackingTarget(
            transform.position,
            facingSign,
            data != null ? data.trackingRadius : 0f);
        return target != null ? target.transform : null;
    }

    private void CombatLog(string message)
    {
        if (playerCharacter != null && playerCharacter.StateMachine != null && playerCharacter.StateMachine.showCombatLogs)
        {
            playerCharacter.StateMachine.Log("Combat", message, new Color(1f, 0.55f, 0f));
        }
    }

    private void RecordActionUse(AttackCategory category, string actionName, string actionId = null)
    {
        float useTime = Time.time;
        CharacterObject activeChar = playerCharacter != null ? playerCharacter.characterObject : null;

        CooldownKey categoryKey = new CooldownKey(activeChar, category);
        lastCategoryUseTime[categoryKey] = useTime;
        ApplyPendingCooldownReduction(categoryKey, lastCategoryUseTime);

        string normalizedActionName = !string.IsNullOrWhiteSpace(actionId)
            ? actionId
            : string.IsNullOrWhiteSpace(actionName) ? "Default" : actionName;
        CooldownKey actionKey = new CooldownKey(activeChar, category, normalizedActionName);
        lastActionUseTime[actionKey] = useTime;
        ApplyPendingCooldownReduction(actionKey, lastActionUseTime);
    }

    private void AddPendingCooldownReduction(CooldownKey key, float seconds)
    {
        pendingCooldownReductions.TryGetValue(key, out float existing);
        pendingCooldownReductions[key] = existing + seconds;
    }

    private void ApplyPendingCooldownReduction(CooldownKey key, Dictionary<CooldownKey, float> cooldowns)
    {
        if (!pendingCooldownReductions.TryGetValue(key, out float seconds) || seconds <= 0f)
        {
            return;
        }

        cooldowns[key] -= seconds;
        pendingCooldownReductions.Remove(key);
    }

    private void RemovePendingCooldownReductions(CharacterObject character, AttackCategory category)
    {
        List<CooldownKey> keysToRemove = null;
        foreach (CooldownKey key in pendingCooldownReductions.Keys)
        {
            if (ReferenceEquals(key.character, character) && key.category == category)
            {
                keysToRemove ??= new List<CooldownKey>();
                keysToRemove.Add(key);
            }
        }

        if (keysToRemove == null)
        {
            return;
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            pendingCooldownReductions.Remove(keysToRemove[i]);
        }
    }

    private bool IsStandaloneContextAction(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)
            || playerCharacter == null
            || playerCharacter.characterObject == null
            || playerCharacter.characterObject.actionResolverTable == null
            || playerCharacter.characterObject.actionResolverTable.standaloneActions == null)
        {
            return false;
        }

        List<StandaloneAction> standaloneActions = playerCharacter.characterObject.actionResolverTable.standaloneActions;
        for (int i = 0; i < standaloneActions.Count; i++)
        {
            StandaloneAction standaloneAction = standaloneActions[i];
            if (standaloneAction == null)
            {
                continue;
            }

            if (string.Equals(standaloneAction.actionName, actionName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Exclude common plunge-related node names even if they aren't standalone entry points
        if (actionName == "Strike" || actionName == "Impact" || actionName == "Dive")
        {
            return true;
        }

        return false;
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
            {
                return accumulatedFrames + releaseFrame;
            }

            accumulatedFrames += Mathf.Max(0, step.startupFrames)
                + Mathf.Max(0, step.activeFrames)
                + Mathf.Max(0, step.recoveryFrames);
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
                if (hitbox == null || hitbox is GrabHitbox)
                {
                    continue;
                }

                earliestFrame = Mathf.Min(
                    earliestFrame,
                    step.startupFrames + 1 + Mathf.Max(0, hitbox.startFrameOffset));
            }
        }

        return earliestFrame == int.MaxValue ? -1 : earliestFrame;
    }

    public void SetPriority(bool isPriority, float delay = 0f)
    {
        if (priorityResetCoroutine != null)
        {
            StopCoroutine(priorityResetCoroutine);
            priorityResetCoroutine = null;
        }

        if (delay > 0 && !isPriority)
        {
            priorityResetCoroutine = StartCoroutine(PriorityResetRoutine(delay));
        }
        else
        {
            ApplyPriority(isPriority);
        }
    }

    private void ApplyPriority(bool isPriority)
    {
        if (movement == null || movement.spriteRenderer == null) return;
        
        // Using a simpler approach for player since it's usually one renderer
        // but let's be safe and use current order + offset
        int baseOrder = 0; // Default player order is usually 0
        movement.spriteRenderer.sortingOrder = baseOrder + (isPriority ? Project.Systems.EntitySortingManager.PRIORITY_SORTING_OFFSET : 0);
    }

    private System.Collections.IEnumerator PriorityResetRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyPriority(false);
        priorityResetCoroutine = null;
    }

    private struct CooldownKey : System.IEquatable<CooldownKey>
    {
        public readonly CharacterObject character;
        public readonly AttackCategory category;
        public readonly string actionIdOrName;

        public CooldownKey(CharacterObject character, AttackCategory category, string actionIdOrName = null)
        {
            this.character = character;
            this.category = category;
            this.actionIdOrName = actionIdOrName;
        }

        public bool Equals(CooldownKey other)
        {
            return ReferenceEquals(character, other.character)
                && category == other.category
                && string.Equals(actionIdOrName, other.actionIdOrName, System.StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CooldownKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (character != null ? character.GetHashCode() : 0);
                hash = hash * 23 + (int)category;
                hash = hash * 23 + (actionIdOrName != null ? actionIdOrName.GetHashCode(System.StringComparison.Ordinal) : 0);
                return hash;
            }
        }
    }
}
