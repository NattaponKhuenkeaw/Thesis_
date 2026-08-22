using System;
using System.Collections;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Core enemy component. Holds stats, HP events, and helpers used by
/// the Unity Behavior action/condition nodes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    // =========================================================
    // Events
    // =========================================================
    /// <summary>Fired when the enemy takes damage. Payload = full reaction data.</summary>
    public event System.Action<HitReactionData> OnDamaged;
    /// <summary>Fired when HP reaches 0.</summary>
    public event System.Action OnDied;
    /// <summary>Fired when camera-facing combat presence changes.</summary>
    public event System.Action<bool> OnCombatChanged;

    // =========================================================
    // Enemy Stats
    // =========================================================
    [Header("Enemy Stats")]
    public float maxHP = 100f;
    public float currentHP = 100f;
    public float attackDamage = 10f;
    public float defense = 0f;
    public float damageRes = 0f;
    public bool isEliteOrBoss = false;

    /// <summary>
    /// Scales basic combat stats (maxHP / attackDamage / defense) and syncs currentHP to the new max.
    /// Used by biome difficulty curve after spawn.
    /// </summary>
    public void ApplyBasicStatMultiplier(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        maxHP *= multiplier;
        attackDamage *= multiplier;
        defense *= multiplier;
        currentHP = maxHP;
        NotifyHealthPresentationRefreshed();
    }

    /// <summary>
    /// Refresh world/boss HP UI after maxHP changes outside the normal damage path (e.g. biome scale).
    /// </summary>
    public void NotifyHealthPresentationRefreshed()
    {
        if (worldUI == null)
        {
            worldUI = GetComponentInChildren<EnemyWorldUI>(true);
        }

        worldUI?.RefreshHealthDisplay();

        Project.UI.BossHealthHUD bossHud = Project.UI.BossHealthHUD.Instance;
        if (bossHud != null)
        {
            bossHud.RefreshIfBound(this);
        }
    }

    [Header("Super Armor (Trash)")]
    [Tooltip("If true, this enemy starts in orange Super Armor (BBEE trash wall).")]
    [SerializeField] private bool startArmored = false;
    [Tooltip("Incoming damage multiplier while Armored.")]
    [SerializeField, Range(0.05f, 1f)] private float armoredDamageMultiplier = 0.35f;
    [Tooltip("Armor durability depleted by non-break hits. 0 = only breaksArmor hits clear armor.")]
    [SerializeField, Min(0f)] private float armorDurability = 40f;
    [SerializeField] private Color armoredTint = new Color(1f, 0.55f, 0.15f, 1f);
    [SerializeField, Min(0f)] private float brokenStateDuration = 0.35f;

    [Header("Runtime Defense")]
    [SerializeField] private EnemyDefenseState defenseState = EnemyDefenseState.Normal;
    [SerializeField] private float currentArmorDurability;

    public EnemyDefenseState DefenseState => defenseState;
    public bool IsArmored => defenseState == EnemyDefenseState.Armored;
    public bool StartArmored => startArmored;

    // =========================================================
    // State
    // =========================================================
    [Header("Runtime State (read-only)")]
    [SerializeField] private bool isAlive = true;
    public bool IsAlive => isAlive;
    public RuntimeEffectReceiver RuntimeEffects
    {
        get
        {
            if (runtimeEffects == null)
            {
                runtimeEffects = GetComponent<RuntimeEffectReceiver>();
                if (runtimeEffects == null)
                {
                    runtimeEffects = gameObject.AddComponent<RuntimeEffectReceiver>();
                }
            }

            return runtimeEffects;
        }
    }
    public EnemyReactionState CurrentReactionState =>
        hitReactionHandler != null ? hitReactionHandler.CurrentState : EnemyReactionState.None;
    public int CurrentReactionOwnerId =>
        hitReactionHandler != null ? hitReactionHandler.CurrentReactionOwnerId : 0;
    public bool IsGrabbed => hitReactionHandler != null && hitReactionHandler.IsGrabbed;

    /// <summary>True while this enemy should stay in the combat camera.</summary>
    public bool InCombat { get; private set; }

    [Header("Combo State")]
    [SerializeField] private int currentComboHits = 0;
    public int CurrentComboHits => currentComboHits;
    public bool LastHitAppliedReaction { get; private set; } = true;

    [Header("Anti-Stunlock")]
    [SerializeField] private int antiStunlockComboThreshold = 4;
    [SerializeField] [Range(0f, 1f)] private float antiStunlockResistChance = 0.3f;
    [Tooltip("After a successful anti-stunlock resist, skip hit-reactions for this many seconds so AI can swing. 0 = old one-hit resist only.")]
    [SerializeField, Min(0f)] private float antiStunlockArmorDuration = 0f;

    /// <summary>
    /// When true, incoming hits still deal damage but skip hit-reactions / Behavior interruption.
    /// Bosses keep this true permanently (stagger/juggle resist); trash enemies may toggle it.
    /// </summary>
    [System.NonSerialized] public bool ignoreHitReactions;

    private Coroutine antiStunlockArmorCoroutine;
    private bool antiStunlockArmorActive;
    private Coroutine brokenStateCoroutine;
    private bool trashArmorOwnsIgnoreHitReactions;

    // =========================================================
    // References
    // =========================================================
    [Header("References")]
    public Rigidbody2D rb;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private int[] originalSortingOrders;
    private Coroutine flashCoroutine;
    private Coroutine priorityResetCoroutine;
    private RuntimeEffectReceiver runtimeEffects;
    private EnemyDamageTypeState damageTypeState;

    /// <summary>Child hitbox collider used by AttackAction.</summary>
    public Collider2D attackHitbox;

    /// <summary>Main body collider (disabled on death).</summary>
    public Collider2D bodyCollider;

    [SerializeField] private EnemyWorldUI worldUI;

    [Header("Hit Reaction")]
    public EnemyHitReactionHandler hitReactionHandler;

    [Header("Death")]
    [Tooltip("Seconds before Destroy after Die(). Bosses set this so death anim can play.")]
    [SerializeField] private float destroyDelayOnDeath = 0f;

    [Header("Combat Role")]
    [Tooltip("Enemy1/2/3 are EnemyCore variants. Change this role, range, and tint — do not duplicate the Behavior Graph.")]
    [SerializeField] private EnemyCombatRole combatRole = EnemyCombatRole.InterruptibleMelee;
    [Tooltip("If > 0, overrides Behavior Graph AttackRange (use ~6-8 for ranged).")]
    [SerializeField] private float attackRangeOverride = -1f;
    [SerializeField] private float rangedShotSpeed = 8f;
    [SerializeField] private float rangedShotLifetime = 2.5f;
    [SerializeField] private Vector2 rangedSpawnOffset = new Vector2(0.85f, 0.15f);
    [SerializeField, Min(0.05f)] private float rangedShotRadius = 0.18f;

    public EnemyCombatRole CombatRole
    {
        get => combatRole;
        set => combatRole = value;
    }

    public float AttackRangeOverride
    {
        get => attackRangeOverride;
        set => attackRangeOverride = value;
    }

    public bool UninterruptibleOnceWindup => combatRole == EnemyCombatRole.CommitMelee;
    public bool UsesRangedAttack => combatRole == EnemyCombatRole.RangedHold;
    public bool NeverEvade => combatRole == EnemyCombatRole.RangedHold;
    public bool ArmorDuringCommittedSwing => combatRole == EnemyCombatRole.CommitMelee;

    public float ResolveAttackRange(float blackboardRange)
    {
        return attackRangeOverride > 0f ? attackRangeOverride : blackboardRange;
    }

    public float ResolveLungeDistance(float blackboardLunge)
    {
        return UsesRangedAttack ? 0f : blackboardLunge;
    }

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        currentHP = maxHP;
        if (startArmored)
            EnterArmored(resetDurability: true);
    }

    void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        isAlive   = true;
        EnemyFacingUtil.SanitizeScale(transform);
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        CacheOriginalColors();
        CacheOriginalSortingOrders();

        if (hitReactionHandler == null)
            hitReactionHandler = GetComponent<EnemyHitReactionHandler>();

        if (worldUI == null)
            worldUI = GetComponentInChildren<EnemyWorldUI>(true);

        runtimeEffects = GetComponent<RuntimeEffectReceiver>();
        if (runtimeEffects == null)
            runtimeEffects = gameObject.AddComponent<RuntimeEffectReceiver>();

        damageTypeState = GetComponent<EnemyDamageTypeState>();

        if (attackHitbox != null)
        {
            EnemyAttackHitbox damageHitbox = attackHitbox.GetComponent<EnemyAttackHitbox>();
            if (damageHitbox == null)
                damageHitbox = attackHitbox.gameObject.AddComponent<EnemyAttackHitbox>();

            damageHitbox.SetHitboxWindowActive(false);
            attackHitbox.enabled = false;

            SpriteRenderer attackHitboxSprite = attackHitbox.GetComponent<SpriteRenderer>();
            if (attackHitboxSprite != null)
                attackHitboxSprite.enabled = false;

            EnsureColliderDebugDrawer(attackHitbox.gameObject, new Color(1f, 0.15f, 0.1f, 1f));
        }

        if (bodyCollider != null)
            EnsureColliderDebugDrawer(bodyCollider.gameObject, Color.yellow);
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>
    /// Full hit-reaction variant. Called by HitBoxDamage with frame-data-driven reaction.
    /// </summary>
    public float TakeResolvedHit(HitReactionData reactionData, Vector2 attackerPosition)
    {
        return TakeHitInternal(reactionData, attackerPosition);
    }

    private void OnEnable()
    {
        RoomEnemyRegistry.Register(this);
    }

    private void OnDisable()
    {
        RoomEnemyRegistry.Unregister(this);
    }

    private float TakeHitInternal(
        HitReactionData reactionData,
        Vector2 attackerPosition)
    {
        if (!isAlive) return 0f;

        bool isSeparateDamage = IsSecondaryDamage(reactionData.damageDelivery);

        if (!isSeparateDamage)
        {
            // Separate damage ticks are numbers only, not another combo impact.
            currentComboHits++;

            if (currentComboHits >= 6 && reactionData.impactTier < DamageImpactTier.Heavy)
                reactionData.impactTier = DamageImpactTier.Heavy;
            else if (currentComboHits >= 3 && reactionData.impactTier < DamageImpactTier.Medium)
                reactionData.impactTier = DamageImpactTier.Medium;
        }

        float finalIncomingDamage = reactionData.damage;

        if (runtimeEffects != null)
        {
            RuntimeEffectDamageContext effectContext = new RuntimeEffectDamageContext
            {
                sourceCharacter = reactionData.sourceCharacter,
                sourceStats = reactionData.sourceStats,
                targetEnemy = this,
                category = reactionData.sourceCategory,
                actionId = reactionData.sourceActionId,
                actionName = reactionData.sourceActionName,
                stepData = reactionData.sourceStepData,
                elementType = reactionData.elementType,
                damageDelivery = reactionData.damageDelivery,
                isCriticalHit = reactionData.isCriticalHit,
                damage = finalIncomingDamage
            };
            finalIncomingDamage = runtimeEffects.ModifyIncomingDamage(effectContext, finalIncomingDamage);
        }

        bool armoredHit = IsArmored && !isSeparateDamage;
        if (armoredHit)
        {
            finalIncomingDamage *= armoredDamageMultiplier;
        }

        reactionData.damage = finalIncomingDamage;

        currentHP -= finalIncomingDamage;
        currentHP  = Mathf.Max(currentHP, 0f);

        if (CombatCore.TrainingDebugStats.Instance != null && CombatCore.TrainingDebugManager.IsActive)
        {
            CombatCore.TrainingDebugStats.Instance.RecordHit(this, reactionData.sourceCharacter, finalIncomingDamage);
        }

        if (armoredHit)
        {
            ProcessArmorHit(reactionData.breaksArmor, finalIncomingDamage);
        }

        bool alreadyIgnoringReactions = ignoreHitReactions;
        bool shouldApplyReaction = !isSeparateDamage && ShouldApplyHitReaction();
        LastHitAppliedReaction = shouldApplyReaction;

        if (!shouldApplyReaction && !isSeparateDamage)
        {
            // Grant / refresh a short armor window so mash cannot re-stagger before AI attacks.
            // Duration 0 keeps legacy behavior: single resist then reset combo.
            if (antiStunlockArmorDuration > 0f && (!alreadyIgnoringReactions || antiStunlockArmorActive))
            {
                BeginAntiStunlockArmorWindow(antiStunlockArmorDuration);
            }
            else if (!alreadyIgnoringReactions)
            {
                currentComboHits = 0;
            }
        }

        // Fire event (reaction pose is driven by EnemyHitReactionHandler → AnimationHandler_Enemy)
        OnDamaged?.Invoke(reactionData);

        // Separate damage intentionally skips reaction, flash, and behavior interruption.
        if (isSeparateDamage)
        {
            if (currentHP <= 0f)
                Die();

            return finalIncomingDamage;
        }

        // Notify Blackboard for BehaviorGraph
        var behaviorAgent = GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent != null && shouldApplyReaction)
        {
            behaviorAgent.SetVariableValue("IsTakingDamage", true);
        }

        // Delegate to the hit reaction handler (freeze, knockback, flash, etc.)
        if (hitReactionHandler != null && shouldApplyReaction)
        {
            hitReactionHandler.StartReaction(reactionData, attackerPosition);
        }

        // --- NEW: Priority Rendering ---
        SetPriority(true);

        // Trigger sprite flash
        if (reactionData.flashFrames > 0)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine(reactionData.flashColor, reactionData.flashFrames));
        }

        if (currentHP <= 0f)
            Die();

        return finalIncomingDamage;
    }

    private bool ShouldApplyHitReaction()
    {
        if (ignoreHitReactions)
            return false;

        if (IsArmored)
            return false;

        if (hitReactionHandler == null)
            return true;

        if (antiStunlockComboThreshold <= 0 || antiStunlockResistChance <= 0f)
            return true;

        bool isGrounded = hitReactionHandler.IsGroundedForCombat;
        if (!isGrounded)
            return true;

        bool isAirborneReaction =
            CurrentReactionState == EnemyReactionState.Knockup ||
            CurrentReactionState == EnemyReactionState.Knockfall;
        if (isAirborneReaction)
            return true;

        if (currentComboHits < antiStunlockComboThreshold)
            return true;

        return UnityEngine.Random.value > antiStunlockResistChance;
    }

    public void EnterArmored(bool resetDurability = true)
    {
        if (brokenStateCoroutine != null)
        {
            StopCoroutine(brokenStateCoroutine);
            brokenStateCoroutine = null;
        }

        defenseState = EnemyDefenseState.Armored;
        if (resetDurability)
            currentArmorDurability = Mathf.Max(0f, armorDurability);

        if (!ignoreHitReactions)
        {
            ignoreHitReactions = true;
            trashArmorOwnsIgnoreHitReactions = true;
        }

        ApplyArmorTint(true);
    }

    public void BreakArmor()
    {
        if (!IsArmored)
            return;

        defenseState = EnemyDefenseState.Broken;
        currentArmorDurability = 0f;

        if (trashArmorOwnsIgnoreHitReactions)
        {
            ignoreHitReactions = false;
            trashArmorOwnsIgnoreHitReactions = false;
        }

        ApplyArmorTint(false);
        currentComboHits = 0;

        if (brokenStateDuration > 0f)
        {
            if (brokenStateCoroutine != null)
                StopCoroutine(brokenStateCoroutine);
            brokenStateCoroutine = StartCoroutine(BrokenStateRoutine());
        }
        else
        {
            defenseState = EnemyDefenseState.Normal;
        }
    }

    private void ProcessArmorHit(bool breaksArmor, float appliedDamage)
    {
        if (!IsArmored)
            return;

        if (breaksArmor)
        {
            BreakArmor();
            return;
        }

        if (armorDurability <= 0f)
            return;

        currentArmorDurability -= Mathf.Max(0f, appliedDamage);
        if (currentArmorDurability <= 0f)
            BreakArmor();
    }

    private IEnumerator BrokenStateRoutine()
    {
        yield return new WaitForSeconds(brokenStateDuration);
        defenseState = EnemyDefenseState.Normal;
        brokenStateCoroutine = null;
    }

    private void ApplyArmorTint(bool armored)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        if (originalColors == null || originalColors.Length != spriteRenderers.Length)
            CacheOriginalColors();

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            spriteRenderers[i].color = armored
                ? Color.Lerp(originalColors[i], armoredTint, 0.65f)
                : originalColors[i];
        }
    }

    private void BeginAntiStunlockArmorWindow(float duration)
    {
        if (duration <= 0f || !isAlive || IsArmored)
            return;

        if (antiStunlockArmorCoroutine != null)
            StopCoroutine(antiStunlockArmorCoroutine);

        antiStunlockArmorCoroutine = StartCoroutine(AntiStunlockArmorRoutine(duration));
    }

    private IEnumerator AntiStunlockArmorRoutine(float duration)
    {
        antiStunlockArmorActive = true;
        ignoreHitReactions = true;

        // Break out of the previous stagger so Behavior can start an attack this window.
        if (hitReactionHandler != null && hitReactionHandler.IsInReaction)
        {
            hitReactionHandler.ForceClearReaction(zeroVelocity: false);
        }

        var behaviorAgent = GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent != null)
        {
            behaviorAgent.SetVariableValue("IsTakingDamage", false);
        }

        yield return new WaitForSeconds(duration);

        antiStunlockArmorActive = false;
        if (!IsArmored && !trashArmorOwnsIgnoreHitReactions)
            ignoreHitReactions = false;
        currentComboHits = 0;
        antiStunlockArmorCoroutine = null;
    }

    private void StopAntiStunlockArmorWindow()
    {
        if (antiStunlockArmorCoroutine != null)
        {
            StopCoroutine(antiStunlockArmorCoroutine);
            antiStunlockArmorCoroutine = null;
        }

        if (antiStunlockArmorActive)
        {
            antiStunlockArmorActive = false;
            if (!IsArmored && !trashArmorOwnsIgnoreHitReactions)
                ignoreHitReactions = false;
        }
    }

    private static bool IsSecondaryDamage(DamageDelivery delivery)
    {
        return delivery == DamageDelivery.Additional || delivery == DamageDelivery.Dot;
    }

    public void ResetComboCount()
    {
        currentComboHits = 0;
        SetPriority(false, 0.5f); // 0.5s delay requested by user
    }

    public void ApplyStatus(StatusEffectType statusEffect, bool enabled = true)
    {
        runtimeEffects?.ApplyStatus(statusEffect, enabled);
    }

    public EnemyDamageTypeState DamageTypeState
    {
        get
        {
            if (damageTypeState == null)
                damageTypeState = GetComponent<EnemyDamageTypeState>();

            return damageTypeState;
        }
    }

    /// <summary>
    /// Legacy overload for backward compatibility. Builds a default HitReactionData.
    /// </summary>
    public void TakeHit(float damage, Vector2 knockbackDirection, float knockbackForce)
    {
        HitReactionData data = HitReactionData.Default(damage, knockbackDirection, knockbackForce);
        TakeResolvedHit(data, transform.position);
    }

    /// <summary>
    /// Delay Destroy after Die so death animation can play (bosses use this).
    /// </summary>
    public void ConfigureDeathDespawn(float delaySeconds)
    {
        destroyDelayOnDeath = Mathf.Max(0f, delaySeconds);
    }

    /// <summary>Trigger death sequence (also callable from DieAction node).</summary>
    public void Die()
    {
        if (!isAlive) return;
        isAlive = false;

        StopAntiStunlockArmorWindow();

        if (bodyCollider  != null) bodyCollider.enabled  = false;
        if (attackHitbox  != null) attackHitbox.enabled  = false;

        OnDied?.Invoke();

        SetInCombat(false);

        if (destroyDelayOnDeath > 0f)
            Destroy(gameObject, destroyDelayOnDeath);
        else
            Destroy(gameObject);
    }

    private void CacheOriginalColors()
    {
        if (spriteRenderers == null) return;
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void CacheOriginalSortingOrders()
    {
        if (spriteRenderers == null) return;
        originalSortingOrders = new int[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                originalSortingOrders[i] = spriteRenderers[i].sortingOrder;
        }
    }

    public void SetPriority(bool isPriority, float delay = 0f)
    {
        if (priorityResetCoroutine != null)
        {
            StopCoroutine(priorityResetCoroutine);
            priorityResetCoroutine = null;
        }

        if (delay > 0 && !isPriority && gameObject.activeInHierarchy && enabled)
        {
            priorityResetCoroutine = StartCoroutine(PriorityResetRoutine(delay));
        }
        else
        {
            Project.Systems.EntitySortingManager.SetCharacterPriority(spriteRenderers, originalSortingOrders, isPriority);
        }
    }

    private IEnumerator PriorityResetRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        Project.Systems.EntitySortingManager.SetCharacterPriority(spriteRenderers, originalSortingOrders, false);
        priorityResetCoroutine = null;
    }

    private IEnumerator FlashRoutine(Color flashColor, int frames)
    {
        // Apply flash colors
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = flashColor;
        }

        // Wait for frames
        int remainingFrames = frames;
        while (remainingFrames > 0)
        {
            yield return new WaitForFixedUpdate();

            if (hitReactionHandler != null
                && hitReactionHandler.CurrentState == EnemyReactionState.ImpactFreeze)
            {
                continue;
            }

            remainingFrames--;
        }

        // Restore original colors (or armored tint if still armored)
        ApplyArmorTint(IsArmored);

        flashCoroutine = null;
    }

    public void SetBehaviorAgentActive(bool active)
    {
        EnemyBehaviorBrain brain = GetComponent<EnemyBehaviorBrain>();
        if (brain != null)
        {
            brain.SetAgentActive(active);
            return;
        }

        var behaviorAgent = GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent != null)
        {
            behaviorAgent.enabled = active;
        }
    }

    public bool ReleaseGrab(int ownerId)
    {
        return hitReactionHandler != null && hitReactionHandler.ReleaseGrab(ownerId);
    }

    public bool MaintainGrab(int ownerId, Transform grabSocket, Vector2 grabOffset, int holdFrames, int recoveryFrames = 0)
    {
        return hitReactionHandler != null
            && hitReactionHandler.MaintainGrab(ownerId, grabSocket, grabOffset, holdFrames, recoveryFrames);
    }

    /// <summary>
    /// Graph nodes still label Idle/Chase/etc. Camera and gameplay only care about combat presence.
    /// </summary>
    public void SetInCombat(bool inCombat)
    {
        if (!isAlive && inCombat)
            return;

        if (InCombat == inCombat)
            return;

        InCombat = inCombat;
        OnCombatChanged?.Invoke(InCombat);

        if (CameraManager.instance == null)
            return;

        if (InCombat)
            CameraManager.instance.EnterCombat(this);
        else
            CameraManager.instance.LeaveCombat(this);
    }

    /// <summary>
    /// Graph-label hook. Maps to <see cref="SetInCombat"/> so Behavior Graph does not become a second FSM.
    /// </summary>
    public void NotifyStateChanged(State newState)
    {
        SetInCombat(IsCombatGraphLabel(newState));
    }

    public static bool IsCombatGraphLabel(State state)
    {
        switch (state)
        {
            case State.Notice:
            case State.Chase:
            case State.Attack:
            case State.TakeDamage:
            case State.Hitstun:
            case State.Knockdown:
                return true;
            default:
                return false;
        }
    }

    public void TriggerAttackWarning(float duration = -1f)
    {
        AttackWarningStyle style = AttackWarningStyle.Normal;
        if (attackHitbox != null)
        {
            EnemyAttackHitbox damageHitbox = attackHitbox.GetComponent<EnemyAttackHitbox>();
            if (damageHitbox != null &&
                (damageHitbox.AttackProperty == EnemyAttackProperty.Undodgeable ||
                 damageHitbox.AttackProperty == EnemyAttackProperty.Grab))
            {
                style = AttackWarningStyle.Undodgeable;
            }
        }

        worldUI?.ShowAttackWarning(duration, style);
    }

    public void HideAttackWarning()
    {
        worldUI?.HideAttackWarning();
    }

    // =========================================================
    // Collider Helpers (called by Action nodes)
    // =========================================================

    /// <summary>Enable or disable the attack hitbox collider.</summary>
    public void SetAttackHitboxActive(bool active)
    {
        if (attackHitbox != null)
        {
            attackHitbox.enabled = active;

            EnemyAttackHitbox damageHitbox = attackHitbox.GetComponent<EnemyAttackHitbox>();
            if (damageHitbox != null)
                damageHitbox.SetHitboxWindowActive(active);
        }
    }

    private static Sprite s_rangedShotSprite;

    public void FireRangedShot(Transform target)
    {
        float facing = EnemyFacingUtil.GetFacingSign(transform);
        Vector3 origin = transform.position + new Vector3(rangedSpawnOffset.x * facing, rangedSpawnOffset.y, 0f);
        Vector2 direction = Vector2.right * facing;
        if (target != null)
        {
            Vector2 toTarget = (Vector2)(target.position - origin);
            if (toTarget.sqrMagnitude > 0.01f)
                direction = toTarget.normalized;
        }

        GameObject shot = new GameObject("EnemyRangedShot");
        shot.transform.position = origin;
        shot.transform.localScale = Vector3.one * Mathf.Max(0.12f, rangedShotRadius * 2f);
        shot.layer = 0;

        SpriteRenderer sprite = shot.AddComponent<SpriteRenderer>();
        sprite.sprite = GetRangedShotSprite();
        sprite.color = new Color(0.35f, 0.85f, 1f, 1f);
        sprite.sortingOrder = 520;

        CircleCollider2D col = shot.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        Rigidbody2D shotBody = shot.AddComponent<Rigidbody2D>();
        shotBody.bodyType = RigidbodyType2D.Kinematic;
        shotBody.gravityScale = 0f;
        shotBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        EnemyRangedShot projectile = shot.AddComponent<EnemyRangedShot>();
        projectile.Launch(this, direction, rangedShotSpeed, rangedShotLifetime);
    }

    private static Sprite GetRangedShotSprite()
    {
        if (s_rangedShotSprite != null)
            return s_rangedShotSprite;

        Texture2D tex = Texture2D.whiteTexture;
        s_rangedShotSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            4f);
        s_rangedShotSprite.hideFlags = HideFlags.HideAndDontSave;
        return s_rangedShotSprite;
    }

    private static void EnsureColliderDebugDrawer(GameObject target, Color color)
    {
        if (target == null)
            return;

        ColliderDebugDrawer drawer = target.GetComponent<ColliderDebugDrawer>();
        if (drawer == null)
            drawer = target.AddComponent<ColliderDebugDrawer>();

        drawer.debugColor = color;
    }

    private Collider2D[] cachedColliders;
    public Collider2D[] GetCachedColliders()
    {
        if (cachedColliders == null)
        {
            cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }
        return cachedColliders;
    }
}
