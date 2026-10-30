using UnityEngine;
using Project.InventorySystem.Items;

/// <summary>
/// Central player facade. Holds CharacterObject data, runtime HP,
/// and references to all player sub-systems.
/// </summary>
public class PlayerCharacter : MonoBehaviour, ICombatActionAvailabilityProvider
{
    public static PlayerCharacter Instance { get; private set; }

    // =========================================================
    // Character Data
    // =========================================================
    [Header("Character Data")]
    public CharacterObject characterObject;

    // =========================================================
    // Sub-system References (auto-cached)
    // =========================================================
    [Header("Sub-systems")]
    public PlayerStateMachine StateMachine;
    public PlayerMovement Movement;
    public PlayerCombat Combat;
    public PlayerAnimator Animator;
    public PlayerAudio Audio;
    public CharacterPassiveController PassiveController;
    public RuntimeEffectReceiver RuntimeEffects;

    // =========================================================
    // Runtime HP
    // =========================================================
    [Header("Runtime Stats")]
    [SerializeField] private float currentHP;
    [SerializeField] private CharacterStats runtimeStats;

    [Header("Damage Settings")]
    [SerializeField] private float postHitInvulnerabilityDuration = 0.4f;

    public float CurrentHP => currentHP;
    public float MaxHP => Stats != null ? Stats.maxHP : currentHP;
    public bool IsDead => currentHP <= 0f;
    public CharacterStats Stats => runtimeStats ?? (characterObject != null ? characterObject.stats : null);
    public bool IsDamageInvulnerable => Time.time < damageInvulnerableUntil;

    private bool deathEventRaised;
    private float damageInvulnerableUntil = -999f;

    public bool IsUltimateInvulnerable { get; set; } = false;
    public bool HasSuperArmor { get; set; } = false;

    // =========================================================
    // HP Events
    // =========================================================
    public event System.Action<float, float> OnHealthChanged;
    public event System.Action<float> OnDamaged;
    public event System.Action<float> OnHealed;
    public event System.Action<float> OnShieldGained;
    public event System.Action<float> OnShieldAbsorbed;
    public event System.Action OnDamageMissed;
    public event System.Action OnDied;
    public event System.Action<CharacterObject> OnCharacterChanged;

    // =========================================================
    // Convenience (read by animation/movement)
    // =========================================================
    public float HorizontalInput => Movement != null ? Movement.HorizontalInput : 0f;

    // =========================================================
    // Unity Methods
    // =========================================================
    void Awake()
    {
        Instance = this;
        CacheSubSystems();

        // Auto-attach hurtbox drawer to player body
        var bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider != null && GetComponent<ColliderDebugDrawer>() == null)
        {
            var drawer = gameObject.AddComponent<ColliderDebugDrawer>();
            drawer.debugColor = Color.green; // Green for player
        }

        if (GetComponentInChildren<PlayerWorldUI>(true) == null)
        {
            gameObject.AddComponent<PlayerWorldUI>();
        }
    }

    void Start()
    {
        if (TeamLoader.Instance == null)
        {
            ApplyCharacterData();
        }
    }

    // =========================================================
    // Public Methods
    // =========================================================
    public void ApplyCharacterData(float? initialHp = null)
    {
        if (characterObject == null)
            return;

        runtimeStats ??= characterObject != null ? characterObject.GetLeveledStats() : null;
        ApplyMovementData();
        ApplyAnimationData();
        InitializeHP(initialHp);
        InitializeAudio();
        PassiveController?.InitializeFromCharacterData();
    }

    public void RefreshRuntimeStats(CharacterStats effectiveStats)
    {
        runtimeStats = effectiveStats ?? (characterObject != null ? characterObject.GetLeveledStats() : null);
        ApplyMovementData();

        if (characterObject != null)
        {
            currentHP = Mathf.Clamp(currentHP, 0f, MaxHP);
            NotifyHealthChanged();
        }
    }

    public void SetCharacter(
        CharacterObject newCharacter,
        float? initialHp = null,
        CharacterStats effectiveStats = null)
    {
        PrepareForCharacterSwitch();
        characterObject = newCharacter;
        runtimeStats = effectiveStats ?? (newCharacter != null
            ? newCharacter.GetLeveledStats()
            : null);
        ApplyCharacterData(initialHp);
        PassiveController?.InitializeFromCharacterData();
        OnCharacterChanged?.Invoke(characterObject);
    }

    // =========================================================
    // HP Methods
    // =========================================================
    public void InitializeHP(float? initialHp = null)
    {
        if (characterObject == null)
            return;

        float maxHp = MaxHP;
        currentHP = Mathf.Clamp(initialHp ?? maxHp, 0f, maxHp);
        deathEventRaised = currentHP <= 0f;
        damageInvulnerableUntil = -999f;
        NotifyHealthChanged();
    }

    public void SetCurrentHP(float value)
    {
        if (characterObject == null)
            return;

        float previousHP = currentHP;
        currentHP = Mathf.Clamp(value, 0f, MaxHP);
        deathEventRaised = currentHP <= 0f;
        if (currentHP > previousHP)
        {
            OnHealed?.Invoke(currentHP - previousHP);
        }
        NotifyHealthChanged();
    }

    public void ForceDeath()
    {
        if (characterObject == null || (IsDead && deathEventRaised))
            return;

        currentHP = 0f;
        NotifyHealthChanged();

        if (!deathEventRaised)
        {
            deathEventRaised = true;
            ClearUltimateGaugeOnDeath();
            HandleCharacterDefeated();
        }
    }

    public bool TakeDamage(float value)
    {
        return TakeDamage(value, default);
    }

    public bool TakeDamage(float value, IncomingAttackContext attackContext)
    {
        if (value <= 0f || IsDead)
            return false;

        if (IsImmuneToIncomingAttack(attackContext))
        {
            OnDamageMissed?.Invoke();
            return false;
        }

        if (PassiveController != null && PassiveController.TryEvadeIncomingDamage(value))
        {
            OnDamageMissed?.Invoke();
            return false;
        }

        float finalDamage = value;
        if (Stats != null)
        {
            finalDamage = CombatFormula.CalculateIncomingDamage(value, Stats.defense);
        }

        TeamLoader teamLoader = TeamLoader.Instance;
        float shieldAbsorbedAmount = 0f;
        if ((teamLoader != null && teamLoader.RunInventory != null && characterObject != null) || RuntimeEffects != null)
        {
            RuntimeEffectDamageContext context = new RuntimeEffectDamageContext
            {
                sourceCharacter = characterObject,
                sourceStats = Stats,
                category = AttackCategory.Normal,
                damage = finalDamage
            };

            if (teamLoader != null && teamLoader.RunInventory != null && characterObject != null)
            {
                finalDamage = teamLoader.RunInventory.ModifyIncomingDamage(characterObject, context, finalDamage);
                context.damage = finalDamage;
            }

            if (RuntimeEffects != null)
            {
                finalDamage = RuntimeEffects.ModifyIncomingDamage(context, finalDamage);
            }

            shieldAbsorbedAmount = context.shieldAbsorbedAmount;
            if (shieldAbsorbedAmount > 0f)
            {
                OnShieldAbsorbed?.Invoke(shieldAbsorbedAmount);
            }
        }

        float previousHP = currentHP;
        currentHP -= finalDamage;
        currentHP = Mathf.Max(currentHP, 0f);

        float actualDamage = previousHP - currentHP;
        if (actualDamage <= 0f)
        {
            if (shieldAbsorbedAmount <= 0f)
            {
                OnDamageMissed?.Invoke();
            }
            return false;
        }

        damageInvulnerableUntil = Time.time + Mathf.Max(0f, postHitInvulnerabilityDuration);

        OnDamaged?.Invoke(actualDamage);
        NotifyHealthChanged();
        PassiveController?.NotifyDamageTaken(actualDamage);
        RuntimeEffects?.RemoveEffectsOnOwnerDamaged();
        teamLoader?.RunInventory?.RemoveRuntimeEffectsOnOwnerDamaged(characterObject);

        if (currentHP <= 0f && !deathEventRaised)
        {
            if (Combat != null && Combat.activeThrowContext != null)
            {
                Combat.activeThrowContext.BreakGrab();
            }

            deathEventRaised = true;
            ClearUltimateGaugeOnDeath();
            HandleCharacterDefeated();
        }
        else if (StateMachine != null && !IsDead)
        {
            bool canInterrupt = StateMachine.CurrentState == null
                || StateMachine.CurrentState.CanBeInterruptedByDamage(attackContext);

            if (canInterrupt && attackContext.canTriggerHitReaction)
            {
                if (Combat != null && Combat.activeThrowContext != null)
                {
                    Combat.activeThrowContext.BreakGrab();
                }

                StateMachine.ChangeState(StateMachine.HitstunState);
            }
        }

        return true;
    }

    /// <summary>
    /// True invulnerability beats every attack property.
    /// Dash/dodge frames only beat <see cref="EnemyAttackProperty.Normal"/>.
    /// </summary>
    public bool IsImmuneToIncomingAttack(IncomingAttackContext attackContext)
    {
        if (IsDamageInvulnerable || IsUltimateInvulnerable)
            return true;

        bool hasDodgeFrames = Movement != null && Movement.IsInvincible();
        if (attackContext.property == EnemyAttackProperty.Normal)
            return hasDodgeFrames;

        // Undodgeable / Grab ignore dash i-frames.
        return false;
    }

    /// <summary>
    /// Active character knockout: play death, remove from run team/backpack, then
    /// recover onto a compacted survivor at index 0. Only raise OnDied (RunEnd /
    /// team wipe) when nobody left can continue the run.
    /// </summary>
    private void HandleCharacterDefeated()
    {
        TeamLoader teamLoader = TeamLoader.Instance ?? UnityEngine.Object.FindFirstObjectByType<TeamLoader>();
        if (teamLoader != null && teamLoader.TryBeginActiveDefeatRecovery())
        {
            // Death anim + remove + switch runs asynchronously on TeamLoader.
            return;
        }

        RaiseTeamWipeAfterDefeat();
    }

    /// <summary>
    /// Terminal team-wipe path after the active character dies with no survivors.
    /// </summary>
    internal void RaiseTeamWipeAfterDefeat()
    {
        OnDied?.Invoke();

        if (StateMachine != null && IsDead)
            StateMachine.ChangeState(StateMachine.DeadState);
    }

    /// <summary>
    /// Resets this character's Ultimate Aura Energy to 0 and notifies AE UI listeners.
    /// </summary>
    private void ClearUltimateGaugeOnDeath()
    {
        if (characterObject == null)
            return;

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader == null)
            return;

        teamLoader.SetCharacterCurrentAE(characterObject, 0f);
    }

    public bool TryCounter(GameObject attacker)
    {
        if (IsDead) return false;

        if (StateMachine != null && StateMachine.CurrentState is CombatPlayerState combatState)
        {
            return combatState.TryTriggerCounter(attacker);
        }

        return false;
    }

    public void Heal(float value)
    {
        if (characterObject == null || value <= 0f || IsDead)
            return;

        currentHP += value;
        currentHP = Mathf.Min(currentHP, MaxHP);

        TeamLoader teamLoader = TeamLoader.Instance;
        if (teamLoader != null && teamLoader.RunInventory != null)
        {
            var state = teamLoader.RunInventory.GetRunCharacter(characterObject);
            if (state != null)
            {
                state.currentHP = currentHP;
            }
        }

        OnHealed?.Invoke(value);
        NotifyHealthChanged();
    }

    public void NotifyShieldGained(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        OnShieldGained?.Invoke(amount);
    }

    public void ApplyStatus(StatusEffectType statusEffect, bool enabled = true)
    {
        if (enabled && PassiveController != null && PassiveController.HasDebuffImmunity())
            return;

        RuntimeEffects?.ApplyStatus(statusEffect, enabled);
    }

    /// <summary>
    /// Checks if a valid combat action exists for the current context (grounded/airborne, input, etc.).
    /// Used by the FSM to prevent locking movement when no action is available.
    /// </summary>
    public bool HasActionForContext(AttackCategory category)
    {
        return TryResolveActionForContext(category, out _, out _);
    }

    public bool TryResolveActionForContext(AttackCategory category, out CombatAction action, out AttackStepData step, bool requireAvailability = false, bool? ultimateInputHeld = null)
    {
        action = null;
        step = null;

        if (characterObject == null || characterObject.actionResolverTable == null)
            return false;

        InputContext ctx = BuildActionInputContext(requireAvailability);
        if (ultimateInputHeld.HasValue) ctx.ultimateInputHeld = ultimateInputHeld.Value;

        // Standard ActionResolver used for one-off validation
        ActionResolver resolver = new ActionResolver();
        step = resolver.ResolveInitial(ctx, characterObject.actionResolverTable, category);
        action = resolver.ActiveCombatAction;
        return step != null;
    }

    public bool IsActionAvailable(CombatAction action)
        => IsActionAvailable(action, null);

    internal bool IsActionAvailable(CombatAction action, CombatAction sourceCooldownException)
    {
        if (action == null)
        {
            return false;
        }

        if (action.resourceCost > 0
            && (PassiveController == null || string.IsNullOrWhiteSpace(action.resourceCounterId)
                || PassiveController.RuntimeState.GetCounter(action.resourceCounterId) < action.resourceCost))
            return false;
        if (action.commitsDash && (Movement == null || !Movement.CanDash())) return false;
        if (action.requiresEnemyTarget
            && (Combat == null || Combat.FindNearestEnemyTarget(action.GetTargetSearchRadius()) == null)) return false;

        if (action is SkillAction skillAction
            && skillAction.cooldown > 0f
            && IsAbilitySlotOnCooldown(action.Category, sourceCooldownException))
        {
            return false;
        }

        if (action is UltimateAction ultimateAction)
        {
            TeamLoader loader = TeamLoader.Instance;
            float currentAE = loader != null && characterObject != null
                ? loader.GetCharacterCurrentAE(characterObject)
                : 0f;
            float requiredAE = loader != null && characterObject != null
                ? loader.GetUltimateCost(characterObject, ultimateAction)
                : Mathf.Max(0f, ultimateAction.energyCost);

            return requiredAE <= 0.01f || currentAE + 0.01f >= requiredAE;
        }

        return true;
    }

    public bool TryCommitActionCounterCost(CombatAction action, ICombatActionAvailabilityProvider availability = null)
    {
        if (action == null || !(availability ?? this).IsActionAvailable(action)) return false;
        return action.resourceCost <= 0 || PassiveController.RuntimeState.ConsumeCounter(action.resourceCounterId, action.resourceCost);
    }

    private bool IsAbilitySlotOnCooldown(AttackCategory category, CombatAction sourceCooldownException = null)
    {
        if (Combat == null || characterObject == null || characterObject.actionResolverTable == null)
        {
            return false;
        }

        ActionResolverTable table = characterObject.actionResolverTable;
        System.Collections.Generic.IReadOnlyList<SkillAction> actions = category switch
        {
            AttackCategory.Skill => table.skillActions,
            AttackCategory.Ultimate => table.ultimateActions,
            _ => null
        };

        if (actions == null)
        {
            return false;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            SkillAction slotAction = actions[i];
            if (slotAction == sourceCooldownException) continue;
            if (slotAction == null || slotAction.cooldown <= 0f)
            {
                continue;
            }

            if (Combat.IsOnCooldown(
                    category,
                    slotAction.cooldown,
                    slotAction.actionName,
                    slotAction.actionId))
            {
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // Private Methods
    // =========================================================
    private InputContext BuildActionInputContext(bool includeAvailability)
    {
        bool actionGrounded = StateMachine == null || StateMachine.IsGroundedContext;
        bool justLanded = Movement != null && (Movement.GroundState.JustLanded || Movement.IsLanding);
        return new InputContext
        {
            isGrounded = actionGrounded,
            ultimateInputHeld = StateMachine?.InputBuffer?.UltimateInputHeld == true,
            directionalInput = StateMachine != null && StateMachine.InputBuffer != null
                ? StateMachine.InputBuffer.DirectionalInput
                : Vector2.zero,
            facingDirection = Movement != null ? Movement.FacingDirection : 1,
            hasGrabbedEnemy = Combat != null && Combat.HasGrabbedEnemy(),
            isAgainstWall = Movement != null && Movement.IsAgainstWall(),
            wasAirborne = justLanded,
            justLanded = justLanded,
            passiveController = PassiveController,
            runtimeState = PassiveController != null ? PassiveController.RuntimeState : null,
            actionAvailabilityProvider = includeAvailability ? this : null
        };
    }

    private void CacheSubSystems()
    {
        if (StateMachine == null)
            StateMachine = GetComponent<PlayerStateMachine>();

        if (Movement == null)
            Movement = GetComponent<PlayerMovement>();

        if (Combat == null)
            Combat = GetComponent<PlayerCombat>();

        if (Animator == null)
            Animator = GetComponentInChildren<PlayerAnimator>();

        if (Audio == null)
            Audio = GetComponent<PlayerAudio>();

        if (PassiveController == null)
            PassiveController = GetComponent<CharacterPassiveController>();

        if (RuntimeEffects == null)
            RuntimeEffects = GetComponent<RuntimeEffectReceiver>();

        if (RuntimeEffects == null)
            RuntimeEffects = gameObject.AddComponent<RuntimeEffectReceiver>();
    }

    private void PrepareForCharacterSwitch()
    {
        Combat?.InvalidateFieldExecutions();
        Combat?.ResetCombo();
        Movement?.CancelAttackMotion();
        StateMachine?.ResetForCharacterSwitch();
    }

    private void ApplyMovementData()
    {
        if (Movement == null || characterObject == null)
            return;

        Movement.profile = characterObject.movementProfile;
        Movement.dashData = characterObject.DashData;
        Movement.maxJumpOverride = characterObject.maxJumpOverride;
        Movement.maxAirDashOverride = characterObject.maxAirDashOverride;

        if (Stats != null && Stats.speed > 0)
        {
            Movement.moveSpeedOverride = Stats.speed;
        }
        else
        {
            Movement.moveSpeedOverride = -1f;
        }
    }

    private void ApplyAnimationData()
    {
        if (Animator == null || characterObject == null)
            return;

        if (Animator.anim == null)
            return;

        if (characterObject.metaData == null)
            return;

        if (characterObject.metaData.animatorController == null ||
            Animator.anim.runtimeAnimatorController == characterObject.metaData.animatorController)
            return;

        Animator.anim.runtimeAnimatorController = characterObject.metaData.animatorController;
    }

    private void NotifyHealthChanged()
    {
        if (OnHealthChanged != null)
            OnHealthChanged(currentHP, MaxHP);
    }

    private void InitializeAudio()
    {
        if (Audio == null)
            Audio = gameObject.AddComponent<PlayerAudio>();

        if (characterObject != null)
        {
            Audio.Initialize(this, characterObject.audioData);
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
