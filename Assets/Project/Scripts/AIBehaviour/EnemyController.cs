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
    [SerializeField] private EnemyLifecycleStateId lifecycleState = EnemyLifecycleStateId.Spawn;
    private EnemyLifecycleState lifecycleStateInstance;
    private bool lifecycleStarted;
    private bool hasStarted;
    private bool spawnPhysicsPaused;
    private bool simulatedBeforeSpawn;
    private float deathDespawnAt;
    private bool disabledDeathDespawnScheduled;

    public bool IsAlive => isAlive;
    public EnemyLifecycleStateId LifecycleState => lifecycleState;
    public bool IsGameplayActive => isActiveAndEnabled && isAlive && lifecycleState == EnemyLifecycleStateId.Active;
    public bool IsCombatReady => IsGameplayActive && !IsRoomTraversalBlockingCombat;
    public static bool IsRoomTraversalBlockingCombat
    {
        get
        {
            PlayerCharacter player = PlayerCharacter.Instance;
            if (player == null || !player.isActiveAndEnabled) return false;
            PlayerStateMachine state = player.StateMachine;
            return state == null || state.CurrentState == null || state.IsCinematicTraversal;
        }
    }
    private bool resumeBehaviorAfterRoomTraversal;
    private bool roomTraversalPauseApplied;
    private bool restartBehaviorAfterRoomTraversal;
    public bool IsTargetable => IsGameplayActive;
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

    /// <summary>Main body collider; remains enabled during DieState so corpses keep their physics.</summary>
    public Collider2D bodyCollider;

    [SerializeField] private EnemyWorldUI worldUI;
    private AudioSource attackWarningAudioSource;
    private readonly EnemyAttackAudioCue attackAudioCue = new EnemyAttackAudioCue();

    [Header("Hit Reaction")]
    public EnemyHitReactionHandler hitReactionHandler;
    private EnemyLocomotion locomotion;
    public EnemyShieldGuard ShieldGuard { get; private set; }
    private Transform rangedAimTarget;
    private Collider2D rangedAimTargetBody;
    private Vector2 committedShotDirection;
    private bool rangedAimCommitted;

    [Header("Death")]
    [Tooltip("Seconds the corpse stays before Destroy. Must cover the death clip or the enemy pops mid-animation. Enemy_Die.anim runs 0.4s; bosses raise this via ConfigureDeathDespawn.")]
    [SerializeField, Min(0f)] private float destroyDelayOnDeath = 0.4f;

    [Header("Spawn Presentation")]
    [Tooltip("Detect ground at the authored spawn position. Airborne enemies fall without AI until grounded.")]
    [SerializeField] private bool useSpawnPresentation;
    [SerializeField] private LayerMask spawnGroundLayers;
    [SerializeField, Min(0.005f)] private float spawnGroundDistance = 0.06f;
    [SerializeField] private bool snapSpawnToGround;
    [SerializeField, Min(1f)] private float spawnGroundSearchDistance = 30f;
    private bool spawnPlacementFailed;
    [Tooltip("Safety limit for missing ground or misconfigured animations. Does not activate airborne enemies.")]
    [SerializeField, Min(1f)] private float lifecyclePresentationTimeout = 30f;
    private AnimationHandler_Enemy lifecycleAnimation;
    private EnemyLifecycleVFX lifecycleVFX;
    private bool spawnFalling;
    private bool spawnAnimationPlaying;
    private bool deathAnimationPlaying;
    private bool deathAnimationWarned;
    private readonly RaycastHit2D[] spawnGroundHits = new RaycastHit2D[16];

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
    public bool UsesRangedAttack => CombatRole == EnemyCombatRole.RangedHold || CombatRole == EnemyCombatRole.AimedRanged;
    public bool NeverEvade => UsesRangedAttack || CombatRole == EnemyCombatRole.ShieldGuard;
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
        hasStarted = true;

        // A controller can be killed before its first frame (for example by a scripted setup).
        // Do not let Start resurrect that instance.
        if (!isAlive || lifecycleState == EnemyLifecycleStateId.Despawn)
        {
            return;
        }

        currentHP = maxHP;
        if (startArmored)
            EnterArmored(resetDurability: true);

        BeginSpawnLifecycle();
    }

    void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        isAlive   = true;
        lifecycleState = EnemyLifecycleStateId.Spawn;
        lifecycleStateInstance = null;
        lifecycleStarted = false;
        hasStarted = false;

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
        locomotion = GetComponent<EnemyLocomotion>();
        ShieldGuard = GetComponent<EnemyShieldGuard>();

        if (worldUI == null)
            worldUI = GetComponentInChildren<EnemyWorldUI>(true);

        runtimeEffects = GetComponent<RuntimeEffectReceiver>();
        if (runtimeEffects == null)
            runtimeEffects = gameObject.AddComponent<RuntimeEffectReceiver>();

        damageTypeState = GetComponent<EnemyDamageTypeState>();
        if (damageTypeState == null)
            damageTypeState = gameObject.AddComponent<EnemyDamageTypeState>();

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

    private void Update()
    {
        if (!lifecycleStarted || lifecycleStateInstance == null)
            return;

        lifecycleStateInstance.Tick(Time.deltaTime);
        UpdateRoomTraversalPause();
        TickAttackSound(Time.deltaTime);
    }

    private void UpdateRoomTraversalPause()
    {
        if (!IsGameplayActive) return;
        if (IsRoomTraversalBlockingCombat)
        {
            if (!roomTraversalPauseApplied)
            {
                var agent = GetComponent<BehaviorGraphAgent>();
                bool resume = resumeBehaviorAfterRoomTraversal || (agent != null && agent.enabled);
                if (agent != null && agent.enabled && agent.Graph != null && agent.Graph.IsRunning)
                {
                    agent.End();
                    restartBehaviorAfterRoomTraversal = true;
                }
                SetBehaviorAgentActive(false);
                resumeBehaviorAfterRoomTraversal = resume;
                roomTraversalPauseApplied = true;
                ShieldGuard?.EndGuard();
                SetAttackHitboxActive(false);
                HideAttackWarning();
                EndRangedAim();
            }
            if (rb != null && CurrentReactionState == EnemyReactionState.None)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        roomTraversalPauseApplied = false;
        if (resumeBehaviorAfterRoomTraversal && CurrentReactionState == EnemyReactionState.None)
        {
            resumeBehaviorAfterRoomTraversal = false;
            SetBehaviorAgentActive(true);
            if (restartBehaviorAfterRoomTraversal)
            {
                var agent = GetComponent<BehaviorGraphAgent>();
                if (agent != null && agent.enabled && agent.Graph != null) agent.Restart();
                restartBehaviorAfterRoomTraversal = false;
            }
        }
    }

    /// <summary>
    /// Places a newly spawned instance at its final location and keeps it out of gameplay for the
    /// spawn setup frame. Room spawners call this before applying difficulty and before Active.
    /// </summary>
    public void InitializeSpawn(Vector3 position, Quaternion rotation)
    {
        // New instances only: a corpse may already have destruction scheduled.
        if (!isAlive)
            return;

        if (lifecycleStateInstance != null)
            lifecycleStateInstance.Exit();

        transform.SetPositionAndRotation(position, rotation);
        lifecycleState = EnemyLifecycleStateId.Spawn;
        lifecycleStateInstance = null;
        lifecycleStarted = false;
        isAlive = true;
        currentHP = maxHP;
        currentComboHits = 0;
        LastHitAppliedReaction = true;

        SetInCombat(false);
        SetAttackHitboxActive(false);
        SetBehaviorAgentActive(false);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (hasStarted)
            BeginSpawnLifecycle();
    }

    private void BeginSpawnLifecycle()
    {
        lifecycleStarted = true;
        TransitionToLifecycleState(new EnemySpawnState(this));
    }

    private void TransitionToLifecycleState(EnemyLifecycleState nextState)
    {
        if (nextState == null)
            return;

        if (lifecycleStateInstance != null && lifecycleStateInstance.Id == nextState.Id)
            return;

        lifecycleStateInstance?.Exit();
        lifecycleState = nextState.Id;
        lifecycleStateInstance = nextState;
        lifecycleStateInstance.Enter();
    }

    internal void TransitionToActiveLifecycle()
    {
        if (!isAlive || lifecycleState != EnemyLifecycleStateId.Spawn)
            return;

        TransitionToLifecycleState(new EnemyActiveState(this));
    }

    internal void TransitionToDespawnLifecycle()
    {
        if (lifecycleState != EnemyLifecycleStateId.Die)
            return;

        TransitionToLifecycleState(new EnemyDespawnState(this));
    }

    internal float DeathDespawnDelay => destroyDelayOnDeath;

    internal bool HasSpawnGroundSupport()
    {
        if (bodyCollider == null || !bodyCollider.enabled)
            return false;
        var bounds = bodyCollider.bounds;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(spawnGroundLayers.value != 0 ? spawnGroundLayers : LayerMask.GetMask("Ground", "OneWayPlatform"));
        filter.useTriggers = false;
        // Probe the feet, not the pivot. A side wall or another actor is not support.
        int count = Physics2D.BoxCast(new Vector2(bounds.center.x, bounds.min.y + 0.025f),
            new Vector2(Mathf.Max(0.01f, bounds.size.x * 0.8f), 0.02f), 0f,
            Vector2.down, filter, spawnGroundHits, spawnGroundDistance + 0.025f);
        for (int i = 0; i < count; i++)
        {
            var hit = spawnGroundHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)
                || hit.collider.GetComponentInParent<EnemyController>() != null
                || hit.collider.GetComponentInParent<PlayerCombat>() != null)
                continue;
            if (hit.normal.y >= 0.5f)
                return true;
        }
        return false;
    }

    public bool TryPlaceSpawnOnGround()
    {
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider == null || !bodyCollider.enabled || bodyCollider.isTrigger) return false;
        Physics2D.SyncTransforms();
        Bounds bounds = bodyCollider.bounds;
        var filter = new ContactFilter2D();
        filter.SetLayerMask(spawnGroundLayers.value != 0 ? spawnGroundLayers : LayerMask.GetMask("Ground", "OneWayPlatform"));
        filter.useTriggers = false;
        Vector2 origin = new Vector2(bounds.center.x, Mathf.Max(transform.position.y, bounds.min.y) + .5f);
        int count = Physics2D.Raycast(origin, Vector2.down, filter, spawnGroundHits, spawnGroundSearchDistance);
        float nearest = float.PositiveInfinity;
        float groundY = 0f;
        for (int i = 0; i < count; i++)
        {
            var hit = spawnGroundHits[i];
            if (hit.collider == null || hit.normal.y < .5f || hit.distance >= nearest
                || hit.collider.transform.IsChildOf(transform)
                || hit.collider.GetComponentInParent<EnemyController>() != null
                || hit.collider.GetComponentInParent<PlayerCharacter>() != null) continue;
            nearest = hit.distance;
            groundY = hit.point.y;
        }
        if (float.IsPositiveInfinity(nearest)) return false;
        Vector3 position = transform.position;
        position.y += groundY + .01f - bounds.min.y;
        transform.position = position;
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
        }
        Physics2D.SyncTransforms();
        return true;
    }

    internal bool TickSpawnPresentation(float elapsed)
    {
        if (spawnPlacementFailed)
        {
            Die();
            return false;
        }
        bool vfxFinished = lifecycleVFX == null || lifecycleVFX.TickSpawn(elapsed);
        if (!useSpawnPresentation)
            return vfxFinished;
        if (spawnFalling)
        {
            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (HasSpawnGroundSupport() && (rb == null || rb.linearVelocity.y <= 0.01f))
                return vfxFinished;
        }
        else if (!spawnAnimationPlaying || lifecycleAnimation == null || lifecycleAnimation.IsLifecycleAnimationComplete())
            return vfxFinished;

        if (elapsed >= lifecyclePresentationTimeout)
        {
            Debug.LogWarning($"{name}: Spawn presentation timed out. Check ground layers, body collider and Animator state. Removing enemy instead of activating mid-air.", this);
            Die();
        }
        return false;
    }

    internal bool IsDeathPresentationComplete(float elapsed)
    {
        if (elapsed < DeathDespawnDelay)
            return false;
        bool animationFinished = !deathAnimationPlaying || lifecycleAnimation == null
            || lifecycleAnimation.IsLifecycleAnimationComplete();
        if (!animationFinished && elapsed >= Mathf.Max(DeathDespawnDelay, lifecyclePresentationTimeout))
        {
            if (!deathAnimationWarned)
            {
                deathAnimationWarned = true;
                Debug.LogWarning($"{name}: Death animation timed out. Check non-looping state and transitions.", this);
            }
            animationFinished = true;
        }
        bool vfxFinished = lifecycleVFX == null || lifecycleVFX.TickDeath(elapsed, animationFinished);
        return animationFinished && vfxFinished;
    }

    private static ulong _nextLifeIdentity;
    private ulong _lifeIdentity;
    public ulong LifeIdentity => _lifeIdentity != 0 ? _lifeIdentity : (_lifeIdentity = ++_nextLifeIdentity);

    internal void OnSpawnLifecycleEntered()
    {
        _lifeIdentity = ++_nextLifeIdentity;
        if (locomotion == null)
            locomotion = GetComponent<EnemyLocomotion>();
        ShieldGuard?.EndGuard();
        EndRangedAim();
        SetBehaviorAgentActive(false);
        SetAttackHitboxActive(false);
        HideAttackWarning();
        HideRangedAimLaser();
        SetInCombat(false);

        lifecycleAnimation = GetComponentInChildren<AnimationHandler_Enemy>(true);
        lifecycleAnimation?.SetLifecyclePhase(EnemyLifecycleStateId.Spawn);
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
        bool wantsFlight = locomotion != null && locomotion.UsesFixedHeightFlight;
        spawnPlacementFailed = snapSpawnToGround && !wantsFlight && !TryPlaceSpawnOnGround();
        if (useSpawnPresentation)
            Physics2D.SyncTransforms();
        bool flightReady = locomotion != null && locomotion.PrepareSpawnFlight();
        if (snapSpawnToGround && wantsFlight && !flightReady) spawnPlacementFailed = true;
        if (spawnPlacementFailed)
            Debug.LogWarning($"{name}: No supporting ground at spawn. Check Spawn_N position and ground layers; enemy will not activate in empty space.", this);
        spawnFalling = useSpawnPresentation && !flightReady && !HasSpawnGroundSupport();
        if (spawnFalling && (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || rb.gravityScale <= 0f || bodyCollider == null))
            Debug.LogWarning($"{name}: Air spawn needs a Dynamic Rigidbody2D with positive gravity and a body collider. Check prefab setup.", this);
        spawnAnimationPlaying = useSpawnPresentation && lifecycleAnimation != null
            && lifecycleAnimation.PlayLifecycleAnimation(spawnFalling ? lifecycleAnimation.spawnFallState : lifecycleAnimation.spawnGroundState);

        if (rb != null)
        {
            simulatedBeforeSpawn = rb.simulated;
            spawnPhysicsPaused = true;
            rb.simulated = spawnFalling;
            rb.linearVelocity = Vector2.zero;
        }
        lifecycleVFX = GetComponent<EnemyLifecycleVFX>();
        lifecycleVFX?.BeginSpawn();
    }

    internal void OnSpawnLifecycleExited()
    {
        lifecycleVFX?.FinishSpawn();
        if (rb != null && spawnPhysicsPaused)
            rb.simulated = simulatedBeforeSpawn;
        spawnPhysicsPaused = false;
        if (isAlive)
            lifecycleAnimation?.ReturnFromSpawn();
        else
            lifecycleAnimation?.EndLifecycleAnimation();
    }

    internal void OnActiveLifecycleEntered()
    {
        if (!isAlive)
            return;

        lifecycleAnimation?.SetLifecyclePhase(EnemyLifecycleStateId.Active);
        SetBehaviorAgentActive(true);
    }

    internal void OnDieLifecycleEntered()
    {
        ShieldGuard?.EndGuard();
        locomotion?.ReleaseFlightConstraints();
        EndRangedAim();
        if (lifecycleAnimation == null)
            lifecycleAnimation = GetComponentInChildren<AnimationHandler_Enemy>(true);
        lifecycleAnimation?.SetLifecyclePhase(EnemyLifecycleStateId.Die);
        deathDespawnAt = Time.time + DeathDespawnDelay;
        HideAttackWarning();
        HideRangedAimLaser();

        StopAntiStunlockArmorWindow();
        ReleaseFacing();

        // Stop AI before the death event reaches animation/boss listeners.
        SetBehaviorAgentActive(false);

        // Preserve terrain support but stop the corpse blocking the player (including swaps).
        int playerLayer = LayerMask.GetMask("Player");
        foreach (var collider in GetComponentsInChildren<Collider2D>(true))
        {
            collider.excludeLayers = collider.excludeLayers.value | playerLayer;
            collider.layerOverridePriority = int.MaxValue;
        }

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        SetAttackHitboxActive(false);

        OnDied?.Invoke();
        if (lifecycleAnimation == null)
            lifecycleAnimation = GetComponentInChildren<AnimationHandler_Enemy>(true);
        deathAnimationPlaying = lifecycleAnimation != null && lifecycleAnimation.PlayLifecycleAnimation(lifecycleAnimation.deathState);
        if (lifecycleAnimation != null && lifecycleAnimation.HasLifecycleDeath && !deathAnimationPlaying)
            lifecycleAnimation.TriggerDie();
        if (lifecycleVFX == null) lifecycleVFX = GetComponent<EnemyLifecycleVFX>();
        lifecycleVFX?.BeginDeath();
        SetInCombat(false);
        ScheduleDespawnIfDisabled();
    }

    private void ScheduleDespawnIfDisabled()
    {
        if (!Application.isPlaying || isActiveAndEnabled || lifecycleState != EnemyLifecycleStateId.Die || disabledDeathDespawnScheduled)
            return;

        disabledDeathDespawnScheduled = true;
        Destroy(gameObject, Mathf.Max(0f, deathDespawnAt - Time.time));
    }

    internal void OnDespawnLifecycleEntered()
    {
        SetBehaviorAgentActive(false);
        SetAttackHitboxActive(false);
        SetInCombat(false);

        // No pooling exists yet. Despawn destroys the whole enemy instance only after DieState's
        // animation window; no individual component is removed or recreated.
        Destroy(gameObject);
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
        attackAudioCue.Cancel();
        StopAttackWarningAudio();
        ScheduleDespawnIfDisabled();
        SetInCombat(false);
        RoomEnemyRegistry.Unregister(this);
        // A pooled enemy disabled mid-swing would otherwise come back unable to turn.
        ReleaseFacing();
    }

    private float TakeHitInternal(
        HitReactionData reactionData,
        Vector2 attackerPosition)
    {
        if (!isAlive || !IsGameplayActive) return 0f;

        if (reactionData.breaksArmor && !IsSecondaryDamage(reactionData.damageDelivery))
            ShieldGuard?.BreakGuard();

        // Grabs counter directional blocking; explicit reaction/armor immunity still applies below.
        if (reactionData.reactionKind != EnemyReactionKind.Grab
            && ShieldGuard != null && ShieldGuard.BlocksHit(attackerPosition, reactionData.damageDelivery))
        {
            LastHitAppliedReaction = false;
            if (Application.isPlaying && kit != null && kit.BlockSfx != null)
                GameAudioManager.EnsureInstance().PlayHitSFX(kit.BlockSfx, kit.BlockSfxVolume);
            return 0f;
        }

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

        if (CombatCore.TrainingDebugStats.Instance != null && CombatCore.TrainingDebugManager.CollectStats)
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
        if (shouldApplyReaction)
        {
            // Check guard and reaction immunity as a normal hit before binding to the dash anchor.
            if (reactionData.actionAnchor != null && finalIncomingDamage > 0f && currentHP > 0f
                && (!IsGrabbed || CurrentReactionState == EnemyReactionState.None))
            {
                reactionData.reactionKind = EnemyReactionKind.Grab;
                reactionData.attackerTransform = reactionData.grabSocket = reactionData.actionAnchor;
                reactionData.grabOffset = reactionData.snapOffset = Vector2.zero;
                reactionData.knockfallFrames = 0;
                reactionData.reactionTiming = HitReactionData.CreateTiming(0, reactionData.actionAnchorHoldFrames, 0);
            }
            attackAudioCue.Cancel();
            StopAttackWarningAudio();
            ShieldGuard?.EndGuard();
            locomotion?.ConstrainFlightReaction(ref reactionData);
        }
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
            if (damageTypeState == null)
                damageTypeState = gameObject.AddComponent<EnemyDamageTypeState>();

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
        if (!isAlive
            || lifecycleState == EnemyLifecycleStateId.Die
            || lifecycleState == EnemyLifecycleStateId.Despawn)
        {
            return;
        }

        isAlive = false;
        lifecycleStarted = true;
        TransitionToLifecycleState(new EnemyDieState(this));
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
        // External reaction systems may ask to resume AI after a grab. Lifecycle still owns the
        // final answer so a dying/spawning enemy cannot be reactivated by a late callback.
        if (active && !IsGameplayActive)
            return;
        if (active && IsRoomTraversalBlockingCombat)
        {
            resumeBehaviorAfterRoomTraversal = true;
            return;
        }
        if (!active) resumeBehaviorAfterRoomTraversal = false;

        EnemyBehaviorBrain brain = GetComponent<EnemyBehaviorBrain>();
        if (brain != null)
        {
            brain.SetAgentActiveForGameplay(active && brain.AllowsGameplayAI);
            return;
        }

        var behaviorAgent = GetComponent<BehaviorGraphAgent>();
        if (behaviorAgent != null)
        {
            bool hasBossDriver = GetComponent<Project.AIBehaviour.Boss.BossPatternController>() != null;
            if (!active || !hasBossDriver)
                behaviorAgent.enabled = active;
        }
    }

    public bool ReleaseGrab(int ownerId, Transform anchor = null)
    {
        return hitReactionHandler != null && hitReactionHandler.ReleaseGrab(ownerId, anchor);
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
        if (inCombat && !IsTargetable)
            return;

        if (InCombat == inCombat)
        {
            // Camera may have been missing on the first edge, or membership may have been
            // dropped while the flag stayed true. Rebind without changing combat state.
            if (inCombat)
                CameraManager.instance?.EnterCombat(this);
            return;
        }

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
        if (!IsCombatReady)
            return;

        PrepareAttackSound(duration >= 0f ? duration : ResolveWarningDuration(.45f));
        StartAttackWarningAudio();
        TickAttackSound(0f);
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
        attackAudioCue.Cancel();
        FinishAttackWarning();
    }

    // The strike transition hides the warning without cancelling an anticipated/delayed cue.
    public void FinishAttackWarning()
    {
        StopAttackWarningAudio();
        HideRangedAimLaser();
        worldUI?.HideAttackWarning();
    }

    public void PrepareAttackSound(float windup)
    {
        attackAudioCue.Begin(Mathf.Max(0f, windup), kit != null ? kit.AttackSfxOffset : 0f);
    }

    public void PlayAttackSound()
    {
        if (attackAudioCue.Confirm(kit != null ? kit.AttackSfxOffset : 0f)) EmitAttackSound();
    }

    private void TickAttackSound(float deltaTime)
    {
        if (!IsCombatReady || CurrentReactionState != EnemyReactionState.None)
        {
            attackAudioCue.Cancel();
            return;
        }
        if (attackAudioCue.Tick(deltaTime)) EmitAttackSound();
    }

    private void EmitAttackSound()
    {
        if (!Application.isPlaying || !IsCombatReady || kit == null || kit.AttackSfx == null) return;
        StopAttackWarningAudio();
        GameAudioManager.EnsureInstance().PlayHitSFX(kit.AttackSfx, kit.AttackSfxVolume);
    }

    private void StartAttackWarningAudio()
    {
        if (!Application.isPlaying || kit == null || kit.AttackWarningSfx == null) return;
        if (attackWarningAudioSource != null && attackWarningAudioSource.isPlaying
            && attackWarningAudioSource.clip == kit.AttackWarningSfx) return;
        if (attackWarningAudioSource == null)
        {
            var host = new GameObject("AttackWarningAudio");
            host.transform.SetParent(transform, false);
            attackWarningAudioSource = host.AddComponent<AudioSource>();
            attackWarningAudioSource.playOnAwake = false;
        }
        GameAudioManager.EnsureInstance().PlayControlledSFX(
            attackWarningAudioSource, kit.AttackWarningSfx, kit.AttackWarningSfxVolume);
    }

    private void StopAttackWarningAudio()
    {
        if (attackWarningAudioSource == null) return;
        attackWarningAudioSource.Stop();
        attackWarningAudioSource.clip = null;
    }

    // =========================================================
    // Collider Helpers (called by Action nodes)
    // =========================================================

    /// <summary>Enable or disable the attack hitbox collider.</summary>
    public void SetAttackHitboxActive(bool active)
    {
        if (active && !IsCombatReady)
            active = false;

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
        if (CombatRole == EnemyCombatRole.AimedRanged)
        {
            if (rangedAimCommitted)
                return committedShotDirection;
            if (rangedAimTarget != null)
                return ResolveAimedShotDirection(rangedAimTarget, rangedAimTargetBody);
        }
        return Vector2.right * EnemyFacingUtil.GetFacingSign(transform);
    }

    public bool IsRangedAiming => rangedAimTarget != null && !rangedAimCommitted;

    public void BeginRangedAim(Transform target)
    {
        rangedAimTarget = target;
        rangedAimTargetBody = CombatRole == EnemyCombatRole.AimedRanged ? ResolveShotTargetCollider(target) : null;
        rangedAimCommitted = false;
    }

    public void CommitRangedAim()
    {
        if (CombatRole != EnemyCombatRole.AimedRanged)
            return;
        committedShotDirection = GetRangedShotDirection();
        rangedAimCommitted = true;
    }

    public void EndRangedAim()
    {
        rangedAimTarget = null;
        rangedAimTargetBody = null;
        rangedAimCommitted = false;
    }

    private Vector2 ResolveAimedShotDirection(Transform target, Collider2D targetBody)
    {
        Vector2 aim = targetBody != null ? (Vector2)targetBody.bounds.center : (Vector2)target.position;
        Vector2 delta = aim - (Vector2)GetRangedMuzzleWorldPosition();
        return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right * EnemyFacingUtil.GetFacingSign(transform);
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

        if (CombatRole == EnemyCombatRole.AimedRanged)
        {
            Vector2 delta = (Vector2)(aim - origin);
            return delta.sqrMagnitude <= range * range
                && !IsStraightShotBlocked(origin, ResolveAimedShotDirection(target, targetBody), delta.magnitude, target);
        }

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
        if (!IsCombatReady) return;
        HideRangedAimLaser();

        Vector3 origin = GetRangedMuzzleWorldPosition();
        if (CombatRole == EnemyCombatRole.AimedRanged && rangedAimTarget == null && target != null)
            BeginRangedAim(target);
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
        EnemyColorVariants appearance = GetComponent<EnemyColorVariants>();
        if (appearance != null)
            sprite.sprite = appearance.ResolveProjectileSprite(sprite.sprite);
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
        if (kit != null && kit.ShotAnimation != null && kit.ShotAnimation.Length > 0)
        {
            Sprite[] frames = appearance != null ? appearance.ResolveProjectileAnimation(kit.ShotAnimation) : kit.ShotAnimation;
            projectile.ConfigureAnimation(frames, kit.ShotAnimationFPS);
            col.radius = radius / Mathf.Max(0.0001f, Mathf.Abs(shot.transform.localScale.x));
        }
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
