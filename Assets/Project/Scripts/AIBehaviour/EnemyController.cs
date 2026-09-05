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

    private bool facingLocked;
    private float facingUnlockAt;

    /// <summary>
    /// True while an attack owns the facing. Every <see cref="EnemyFacingUtil.FaceHorizontal"/> caller
    /// honours this, so locomotion and the Behavior graph cannot re-aim the swing once it is committed.
    /// A timed hold can outlive the attack node so chase cannot snap-turn the instant the node ends.
    /// </summary>
    public bool FacingLocked => facingLocked && Time.time < facingUnlockAt;

    public void LockFacing()
    {
        facingLocked = true;
        facingUnlockAt = float.PositiveInfinity;
    }

    public void HoldCommittedFacing(float seconds)
    {
        facingLocked = true;
        facingUnlockAt = Time.time + Mathf.Max(0f, seconds);
    }

    public void ReleaseFacing()
    {
        facingLocked = false;
        facingUnlockAt = 0f;
    }

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
    [Tooltip("Seconds the corpse stays before Destroy. Must cover the death clip or the enemy pops mid-animation. Enemy_Die.anim runs 0.4s; bosses raise this via ConfigureDeathDespawn.")]
    [SerializeField, Min(0f)] private float destroyDelayOnDeath = 0.4f;

    [Header("Combat Kit")]
    [Tooltip("Optional EnemyKit asset. When assigned it owns the role, range, and ranged shot values below.")]
    [SerializeField] private EnemyKit kit;

    [Header("Combat Role")]
    [Tooltip("Enemy1/2/3 are EnemyCore variants. Change this role, range, and tint — do not duplicate the Behavior Graph.")]
    [SerializeField] private EnemyCombatRole combatRole = EnemyCombatRole.InterruptibleMelee;
    [Tooltip("If > 0, overrides Behavior Graph AttackRange. For ranged this is also laser length and max shot reach.")]
    [SerializeField] private float attackRangeOverride = -1f;

    [Header("Ranged Shot")]
    [Tooltip("Bullet travel speed.")]
    [SerializeField, Min(0.1f)] private float rangedShotSpeed = 8f;
    [Tooltip("Seconds the bullet stays alive. Raise this if you lengthen range or lower speed.")]
    [SerializeField, Min(0.05f)] private float rangedShotLifetime = 2.5f;
    [Tooltip("World-space shot origin. Move this on the prefab to change where the laser and bullet start.")]
    [SerializeField] private Transform rangedMuzzle;
    [Tooltip("Used when Ranged Muzzle is empty. X flips with facing.")]
    [SerializeField] private Vector2 rangedSpawnOffset = new Vector2(0.85f, 0.15f);
    [Tooltip("Optional bullet art. Leave empty for the default square.")]
    [SerializeField] private Sprite rangedShotSprite;
    [SerializeField] private Color rangedShotTint = Color.white;
    [SerializeField, Min(0.05f)] private float rangedShotRadius = 0.18f;
    [SerializeField] private LayerMask rangedLineMask = ~0;

    /// <summary>Kit asset that overrides the local role and ranged values. Null = use prefab values.</summary>
    public EnemyKit Kit
    {
        get => kit;
        set => kit = value;
    }

    public EnemyCombatRole CombatRole
    {
        get => kit != null ? kit.CombatRole : combatRole;
        set => combatRole = value;
    }

    public float AttackRangeOverride
    {
        get => kit != null ? kit.AttackRange : attackRangeOverride;
        set => attackRangeOverride = value;
    }

    private float ShotSpeed => kit != null ? kit.ShotSpeed : rangedShotSpeed;
    private float ShotLifetime => kit != null ? kit.ShotLifetime : rangedShotLifetime;
    private Sprite ShotSprite => kit != null ? kit.ShotSprite : rangedShotSprite;
    private Color ShotTint => kit != null ? kit.ShotTint : rangedShotTint;
    private float ShotRadius => kit != null ? kit.ShotRadius : rangedShotRadius;
    private Vector2 MuzzleOffset => kit != null ? kit.MuzzleOffset : rangedSpawnOffset;
    private LayerMask LineOfFireMask => kit != null ? kit.LineOfFireMask : rangedLineMask;

    public bool UninterruptibleOnceWindup => CombatRole == EnemyCombatRole.CommitMelee;
    public bool UsesRangedAttack => CombatRole == EnemyCombatRole.RangedHold;
    public bool NeverEvade => CombatRole == EnemyCombatRole.RangedHold;
    public Transform RangedMuzzle
    {
        get => rangedMuzzle;
        set => rangedMuzzle = value;
    }
    public bool ArmorDuringCommittedSwing => CombatRole == EnemyCombatRole.CommitMelee;

    public float ResolveAttackRange(float blackboardRange)
    {
        float range = AttackRangeOverride;
        return range > 0f ? range : blackboardRange;
    }

    /// <summary>
    /// Horizontal stand-off used by chase. Separate from <see cref="ResolveAttackRange"/> so a ranged
    /// kit can shoot across the room without walking into the melee pile to "arrive".
    /// </summary>
    public float ResolveHoldRange(float blackboardAttackRange)
    {
        float attackRange = Mathf.Max(0.1f, ResolveAttackRange(blackboardAttackRange));
        float authored = kit != null ? kit.HoldRange : -1f;
        if (authored >= 0f)
            return authored;

        if (UsesRangedAttack)
            return Mathf.Min(attackRange, 7f);

        return attackRange * 0.85f;
    }

    /// <summary>
    /// Kits author a negative number to mean "not set here". Zero is a legitimate beat, so it must
    /// not double as the unset marker the way <see cref="AttackRangeOverride"/> uses it.
    /// </summary>
    private static float ResolveKitTiming(float kitValue, float blackboardValue)
    {
        return kitValue >= 0f ? kitValue : blackboardValue;
    }

    public float ResolveWarningDuration(float blackboardValue)
    {
        return ResolveKitTiming(kit != null ? kit.WarningDuration : -1f, blackboardValue);
    }

    public float ResolveHitWindowDuration(float blackboardValue)
    {
        return ResolveKitTiming(kit != null ? kit.HitWindowDuration : -1f, blackboardValue);
    }

    public float ResolveAttackCooldown(float blackboardValue)
    {
        return ResolveKitTiming(kit != null ? kit.AttackCooldown : -1f, blackboardValue);
    }

    public float ResolveCommitTelegraphFraction(float blackboardValue)
    {
        return ResolveKitTiming(kit != null ? kit.CommitTelegraphFraction : -1f, blackboardValue);
    }

    public float ResolveInterruptCooldown(float blackboardValue)
    {
        return ResolveKitTiming(kit != null ? kit.InterruptCooldown : -1f, blackboardValue);
    }

    public float ResolveRecoveryPlantDuration(float blackboardValue)
    {
        return ResolveKitTiming(kit != null ? kit.RecoveryPlantDuration : -1f, blackboardValue);
    }

    public float ResolveLungeDistance(float blackboardLunge)
    {
        if (UsesRangedAttack)
            return 0f;

        return ResolveKitTiming(kit != null ? kit.LungeDistance : -1f, blackboardLunge);
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
        // A pooled enemy disabled mid-swing would otherwise come back unable to turn.
        ReleaseFacing();
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
        if (statusEffect == StatusEffectType.Stun && enabled)
        {
            ApplyStun(2.5f);
        }
    }

    public void ApplyStun(float durationSeconds)
    {
        if (!isAlive || durationSeconds <= 0f) return;
        int frames = Mathf.RoundToInt(durationSeconds * CombatCore.FrameDataConstants.LOGIC_FPS);
        HitReactionData stunData = new HitReactionData
        {
            damage = 0f,
            hitstunFrames = frames,
            reactionTiming = HitReactionData.CreateTiming(frames, 0, 0),
            reactionKind = EnemyReactionKind.Flinch
        };
        if (hitReactionHandler != null)
        {
            hitReactionHandler.StartReaction(stunData, transform.position);
        }
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
        HideAttackWarning();

        StopAntiStunlockArmorWindow();
        ReleaseFacing();

        // Stop the graph before the animator is told to die, or an attack node still mid-swing
        // retriggers its own animation on top of the death clip.
        SetBehaviorAgentActive(false);

        // Drop the drive but leave the corpse in the simulation. It still needs gravity and the
        // body collider to land, and switching the collider off was what made dying enemies fall
        // through the floor for the length of the death animation. Vertical speed is kept so a
        // kill mid-juggle still arcs down instead of freezing in the air. Nothing can hit or
        // target it once isAlive is false; the whole combat layer gates on that.
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (attackHitbox != null) attackHitbox.enabled = false;

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
        if (UsesRangedAttack)
        {
            worldUI?.HideAttackWarning();
            ShowRangedAimLaser();
            return;
        }

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
        HideRangedAimLaser();
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

    public Vector3 GetRangedMuzzleWorldPosition()
    {
        if (rangedMuzzle != null)
        {
            return rangedMuzzle.position;
        }

        Transform named = transform.Find("RangedMuzzle");
        if (named != null)
        {
            rangedMuzzle = named;
            return rangedMuzzle.position;
        }

        float facing = EnemyFacingUtil.GetFacingSign(transform);
        Vector2 offset = MuzzleOffset;
        return transform.position + new Vector3(offset.x * facing, offset.y, 0f);
    }

    public Vector2 GetRangedShotDirection()
    {
        return Vector2.right * EnemyFacingUtil.GetFacingSign(transform);
    }

    public bool HasClearStraightShot(Transform target, float attackRange)
    {
        if (!UsesRangedAttack || target == null)
        {
            return false;
        }

        float range = ResolveAttackRange(attackRange);
        Vector3 origin = GetRangedMuzzleWorldPosition();
        Vector2 direction = GetRangedShotDirection();
        Collider2D targetBody = ResolveShotTargetCollider(target);
        Vector3 aim = targetBody != null ? targetBody.bounds.center : EnemyCombatRange.GetAimPoint(target);

        float alongMuzzle = Vector2.Dot((Vector2)aim - (Vector2)origin, direction);
        float alongBody = Vector2.Dot((Vector2)aim - (Vector2)transform.position, direction);
        // Fail only when the target is behind the body or beyond shot range. Being behind the muzzle
        // (player hugging the sprite) used to return false and freeze the ranged enemy in place.
        if (alongBody <= 0.05f || alongBody > range)
        {
            return false;
        }

        float beamHalf = Mathf.Max(0.12f, ShotRadius);
        if (targetBody != null)
        {
            Bounds bounds = targetBody.bounds;
            if (origin.y < bounds.min.y - beamHalf || origin.y > bounds.max.y + beamHalf)
            {
                return false;
            }
        }
        else if (Mathf.Abs(aim.y - origin.y) > beamHalf * 2f)
        {
            return false;
        }

        float probe = alongMuzzle > 0.05f ? alongMuzzle : alongBody;
        return !IsStraightShotBlocked(origin, direction, probe, target);
    }

    public void FireRangedShot(Transform target)
    {
        HideRangedAimLaser();

        Vector3 origin = GetRangedMuzzleWorldPosition();
        Vector2 direction = GetRangedShotDirection();

        GameObject shot = new GameObject("EnemyRangedShot");
        shot.transform.position = origin;
        Sprite customSprite = ShotSprite;
        float radius = ShotRadius;
        bool hasCustomSprite = customSprite != null;
        float thickness = Mathf.Max(0.12f, radius * 2f);
        shot.transform.localScale = hasCustomSprite
            ? Vector3.one * Mathf.Max(0.2f, radius / 0.18f)
            : new Vector3(thickness * 1.6f, thickness, 1f);
        shot.layer = 0;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        shot.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sprite = shot.AddComponent<SpriteRenderer>();
        sprite.sprite = hasCustomSprite ? customSprite : GetRangedShotSprite();
        sprite.color = hasCustomSprite ? ShotTint : new Color(0.35f, 0.85f, 1f, 1f);
        sprite.sortingOrder = 520;

        CircleCollider2D col = shot.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        Rigidbody2D shotBody = shot.AddComponent<Rigidbody2D>();
        shotBody.bodyType = RigidbodyType2D.Kinematic;
        shotBody.gravityScale = 0f;
        shotBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        float shotDistance = ResolveAttackRange(7f);
        if (target != null)
            shotDistance = Mathf.Max(shotDistance, Mathf.Abs(target.position.x - origin.x));
        float speed = ShotSpeed;
        float travelTime = shotDistance / Mathf.Max(0.1f, speed);
        float lifetime = Mathf.Max(ShotLifetime, travelTime + 0.1f);
        EnemyRangedShot projectile = shot.AddComponent<EnemyRangedShot>();
        projectile.Launch(this, direction, speed, lifetime);
    }

    private void ShowRangedAimLaser()
    {
        EnemyRangedAimLaser laser = GetComponent<EnemyRangedAimLaser>();
        if (laser == null)
        {
            laser = gameObject.AddComponent<EnemyRangedAimLaser>();
        }

        float laserLength = ResolveAttackRange(7f);
        PlayerCharacter player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
        if (player != null)
        {
            float toPlayer = Mathf.Abs(player.transform.position.x - GetRangedMuzzleWorldPosition().x);
            laserLength = Mathf.Max(laserLength, toPlayer);
        }

        laser.Show(this, laserLength);
    }

    private void HideRangedAimLaser()
    {
        EnemyRangedAimLaser laser = GetComponent<EnemyRangedAimLaser>();
        laser?.Hide();
    }

    private static Collider2D ResolveShotTargetCollider(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerCharacter player = target.GetComponent<PlayerCharacter>()
            ?? target.GetComponentInParent<PlayerCharacter>();
        if (player != null)
        {
            Collider2D playerBody = player.GetComponent<Collider2D>();
            if (playerBody != null)
            {
                return playerBody;
            }
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
            {
                return colliders[i];
            }
        }

        return colliders.Length > 0 ? colliders[0] : null;
    }

    private bool IsStraightShotBlocked(Vector3 origin, Vector2 direction, float distance, Transform target)
    {
        float probe = Mathf.Max(0.1f, distance);
        Vector2 start = (Vector2)origin + direction * 0.04f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(start, direction, probe, LineOfFireMask);
        if (hits.Length > 1)
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i].collider;
            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (IsIntendedShotTarget(hit, target))
            {
                return false;
            }

            if (hit.GetComponentInParent<PlayerCharacter>() != null)
            {
                return false;
            }

            // Other enemies occupy the lane visually but must not own it. A melee pile in front of a
            // ranged kit was the reason standing still looked like "the archer never fires".
            if (hit.GetComponentInParent<EnemyController>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsIntendedShotTarget(Collider2D hit, Transform target)
    {
        if (hit == null || target == null)
            return false;

        Transform hitTransform = hit.transform;
        return hitTransform == target
            || hitTransform.IsChildOf(target)
            || target.IsChildOf(hitTransform);
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
