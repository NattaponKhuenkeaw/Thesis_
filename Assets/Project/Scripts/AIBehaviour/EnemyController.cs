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
    /// <summary>Fired when the enemy's behavior state changes (via SetEnemyStateAction).</summary>
    public event System.Action<State> OnStateChanged;

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

    [Header("Combo State")]
    [SerializeField] private int currentComboHits = 0;
    public int CurrentComboHits => currentComboHits;
    public bool LastHitAppliedReaction { get; private set; } = true;

    [Header("Anti-Stunlock")]
    [SerializeField] private int antiStunlockComboThreshold = 4;
    [SerializeField] [Range(0f, 1f)] private float antiStunlockResistChance = 0.3f;

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
    private SpriteRenderer attackHitboxSR;
    private LineRenderer attackHitboxLR;

    /// <summary>Child hitbox collider used by AttackAction.</summary>
    public Collider2D attackHitbox;

    /// <summary>Main body collider (disabled on death).</summary>
    public Collider2D bodyCollider;

    [SerializeField] private EnemyWorldUI worldUI;

    [Header("Hit Reaction")]
    public EnemyHitReactionHandler hitReactionHandler;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Start()
    {
        currentHP = maxHP;
    }

    void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        isAlive   = true;

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
        if (damageTypeState == null)
            damageTypeState = gameObject.AddComponent<EnemyDamageTypeState>();

        // Start with hitbox disabled and cache debug renderers
        if (attackHitbox != null)
        {
            EnemyAttackHitbox damageHitbox = attackHitbox.GetComponent<EnemyAttackHitbox>();
            if (damageHitbox == null)
                damageHitbox = attackHitbox.gameObject.AddComponent<EnemyAttackHitbox>();

            damageHitbox.SetHitboxWindowActive(false);
            attackHitbox.enabled = false;

            attackHitboxSR = attackHitbox.GetComponent<SpriteRenderer>();
            attackHitboxLR = attackHitbox.GetComponent<LineRenderer>();
        }

        // Auto-attach hurtbox drawer to main body
        if (bodyCollider != null && bodyCollider.GetComponent<ColliderDebugDrawer>() == null)
        {
            var drawer = bodyCollider.gameObject.AddComponent<ColliderDebugDrawer>();
            drawer.debugColor = Color.yellow; // Yellow for enemies
        }
    }

    void Update()
    {
        bool isDebugActive = CombatCore.TrainingDebugManager.IsActive;

        // Runtime visualization for Game View
        if (attackHitbox != null && attackHitbox.enabled)
        {
            // Toggle visibility based on debug mode (using cached references)
            if (attackHitboxSR != null) attackHitboxSR.enabled = isDebugActive;
            if (attackHitboxLR != null) attackHitboxLR.enabled = isDebugActive;

            if (isDebugActive)
            {
                // 1. Auto-scale SpriteRenderer to match Collider size
                if (attackHitboxSR != null)
                {
                    if (attackHitbox is BoxCollider2D box)
                    {
                        attackHitboxSR.drawMode = SpriteDrawMode.Sliced;
                        attackHitboxSR.size = box.size;
                    }
                }

                // 2. Beautiful outline using LineRenderer
                if (attackHitboxLR != null)
                {
                    DrawLineRenderer(attackHitboxLR);
                }
                else
                {
#if UNITY_EDITOR
                    DrawHitboxRuntime();
#endif
                }
            }
        }
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
        reactionData.damage = finalIncomingDamage;

        currentHP -= finalIncomingDamage;
        currentHP  = Mathf.Max(currentHP, 0f);

        if (CombatCore.TrainingDebugStats.Instance != null && CombatCore.TrainingDebugManager.IsActive)
        {
            CombatCore.TrainingDebugStats.Instance.RecordHit(this, reactionData.sourceCharacter, finalIncomingDamage);
        }

        bool shouldApplyReaction = !isSeparateDamage && ShouldApplyHitReaction();
        LastHitAppliedReaction = shouldApplyReaction;

        if (!shouldApplyReaction && !isSeparateDamage)
        {
            // Once the enemy successfully resists stagger, break the combo counter so
            // it does not immediately keep armoring through every following hit.
            currentComboHits = 0;
        }

        // Fire event (AnimationHandler_Enemy triggers TakeDamage anim via this)
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
            {
                damageTypeState = GetComponent<EnemyDamageTypeState>();
                if (damageTypeState == null)
                {
                    damageTypeState = gameObject.AddComponent<EnemyDamageTypeState>();
                }
            }

            return damageTypeState;
        }
    }

    /// <summary>Trigger death sequence (also callable from DieAction node).</summary>
    public void Die()
    {
        if (!isAlive) return;
        isAlive = false;

        if (bodyCollider  != null) bodyCollider.enabled  = false;
        if (attackHitbox  != null) attackHitbox.enabled  = false;

        OnDied?.Invoke();

        // Remove from combat camera when dead
        if (CameraManager.instance != null)
            CameraManager.instance.LeaveCombat(this);

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

        // Restore original colors
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && i < originalColors.Length)
                spriteRenderers[i].color = originalColors[i];
        }

        flashCoroutine = null;
    }

    public void SetBehaviorAgentActive(bool active)
    {
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
    /// Called by SetEnemyStateAction when the behavior graph transitions to a new state.
    /// Broadcasts to listeners (e.g., CameraManager for Notice/Chase → combat cam).
    /// </summary>
    public void NotifyStateChanged(State newState)
    {
        OnStateChanged?.Invoke(newState);
    }

    public void TriggerAttackWarning(float duration = -1f)
    {
        worldUI?.ShowAttackWarning(duration);
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

            // Toggle cached debug renderers
            bool isDebugActive = CombatCore.TrainingDebugManager.IsActive;
            if (attackHitboxSR != null) attackHitboxSR.enabled = active && isDebugActive;
            if (attackHitboxLR != null) attackHitboxLR.enabled = active && isDebugActive;
        }
    }

    // =========================================================
    // Gizmos
    // =========================================================
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (attackHitbox != null && attackHitbox.enabled)
        {
            Gizmos.color = Color.red;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = attackHitbox.transform.localToWorldMatrix;

            if (attackHitbox is BoxCollider2D box)
            {
                Gizmos.DrawWireCube((Vector3)box.offset, (Vector3)box.size);
            }
            else if (attackHitbox is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere((Vector3)circle.offset, circle.radius);
            }

            Gizmos.matrix = oldMatrix;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Also draw a faint version when selected but not attacking
        if (attackHitbox != null && !attackHitbox.enabled)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = attackHitbox.transform.localToWorldMatrix;

            if (attackHitbox is BoxCollider2D box)
            {
                Gizmos.DrawWireCube((Vector3)box.offset, (Vector3)box.size);
            }
            else if (attackHitbox is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere((Vector3)circle.offset, circle.radius);
            }

            Gizmos.matrix = oldMatrix;
        }
    }

    private void DrawHitboxRuntime()
    {
        Matrix4x4 matrix = attackHitbox.transform.localToWorldMatrix;
        Color color = Color.red;

        if (attackHitbox is BoxCollider2D box)
        {
            Vector3 size = box.size;
            Vector3 offset = box.offset;

            Vector3 p1 = matrix.MultiplyPoint3x4(offset + new Vector3(-size.x, -size.y) * 0.5f);
            Vector3 p2 = matrix.MultiplyPoint3x4(offset + new Vector3(size.x, -size.y) * 0.5f);
            Vector3 p3 = matrix.MultiplyPoint3x4(offset + new Vector3(size.x, size.y) * 0.5f);
            Vector3 p4 = matrix.MultiplyPoint3x4(offset + new Vector3(-size.x, size.y) * 0.5f);

            Debug.DrawLine(p1, p2, color);
            Debug.DrawLine(p2, p3, color);
            Debug.DrawLine(p3, p4, color);
            Debug.DrawLine(p4, p1, color);
        }
        else if (attackHitbox is CircleCollider2D circle)
        {
            int segments = 20;
            float angleStep = 360f / segments;
            Vector3 prevPt = Vector3.zero;

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 pt = (Vector3)circle.offset + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius;
                Vector3 worldPt = matrix.MultiplyPoint3x4(pt);

                if (i > 0) Debug.DrawLine(prevPt, worldPt, color);
                prevPt = worldPt;
            }
        }
    }
#endif

    private void DrawLineRenderer(LineRenderer lr)
    {
        if (attackHitbox is BoxCollider2D box)
        {
            lr.positionCount = 4;
            lr.loop = true;

            Vector2 size = box.size;
            Vector2 offset = box.offset;

            Vector3 p1 = offset + new Vector2(-size.x, -size.y) * 0.5f;
            Vector3 p2 = offset + new Vector2(size.x, -size.y) * 0.5f;
            Vector3 p3 = offset + new Vector2(size.x, size.y) * 0.5f;
            Vector3 p4 = offset + new Vector2(-size.x, size.y) * 0.5f;

            Transform t = attackHitbox.transform;
            lr.SetPosition(0, t.TransformPoint(p1));
            lr.SetPosition(1, t.TransformPoint(p2));
            lr.SetPosition(2, t.TransformPoint(p3));
            lr.SetPosition(3, t.TransformPoint(p4));
        }
        else if (attackHitbox is CircleCollider2D circle)
        {
            int segments = 36;
            lr.positionCount = segments;
            lr.loop = true;
            float angleStep = 360f / segments;

            Transform t = attackHitbox.transform;
            Vector2 offset = circle.offset;
            float radius = circle.radius;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 localPoint = offset + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                lr.SetPosition(i, t.TransformPoint(localPoint));
            }
        }
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
