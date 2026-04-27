using System.Collections.Generic;
using CombatCore;
using UnityEngine;

/// <summary>
/// Intermediate base class for all combat states (Attack, Skill, Ultimate).
/// Centralizes ActionResolver logic, input context building, action events,
/// and systematic interrupt rules.
/// </summary>
public abstract class CombatPlayerState : FrameDataPlayerState
{
    protected readonly ActionResolver resolver = new ActionResolver();
    protected ActionResolverTable resolverTable;
    protected bool hasStartedCooldown;

    private readonly HashSet<ActionEventEntry> triggeredActionEvents = new HashSet<ActionEventEntry>();
    protected bool prevFrameGrounded;
    private ActionEventEntry activeActionSequence;
    private GameFreezeManager.FreezeToken activeFreezeToken;
    private bool pauseCurrentNodeForSequence;
    private bool advanceCurrentNodeWithUnscaledTime;

    // --- Airborne Gravity Policy ---
    private AirborneGravityPolicy gravityPolicy;
    private int recoveryFrameCount;
    private int framesInRecovery;

    public CombatPlayerState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        resolverTable = player.characterObject != null ? player.characterObject.actionResolverTable : null;
        resolver.SetLogger(fsm);
        hasStartedCooldown = false;
        ResetActionEventTracking();

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected += HandleGrabConnected;
        }

        prevFrameGrounded = player.Movement != null && player.Movement.IsGrounded();

        // --- Gravity Policy ---
        gravityPolicy = player.Movement != null && player.Movement.profile != null
            ? player.Movement.profile.airborneGravityPolicy
            : AirborneGravityPolicy.Default;
        framesInRecovery = 0;
        recoveryFrameCount = 0;
        UpdateGravityScale(ActionPhase.Startup, 0f);
    }

    // Removed FixedUpdate update of prevFrameGrounded to ensure Update() has exclusive control 
    // over the frame-to-frame transition for landing detection.

    public override void Exit()
    {
        EndActiveActionEventSequence();
        TryStartCooldown();

        if (player.Combat != null && player.Combat.HasGrabbedEnemy())
        {
            Vector2 ejectDir = player.Movement != null
                ? new Vector2(player.Movement.FacingDirection, 0.3f).normalized
                : Vector2.right;
            player.Combat.ForceReleaseGrab(ejectDir, 4f);
        }

        if (player.Combat != null && player.Combat.IsAttacking())
        {
            if (resolver.IsActive && !resolver.IsLastStep())
                player.Combat.CancelCurrentAttackButKeepComboProgress();
            else
                player.Combat.ResetCombo();

            player.Combat.SetPlungeFalling(false);
            player.Combat.SetAirborneGravityScale(1f); // Restore gravity
        }

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected -= HandleGrabConnected;
        }

        base.Exit();
        resolver.Reset();
        hasStartedCooldown = false;
        triggeredActionEvents.Clear();
    }

    public override void Update()
    {
        // Check for landing BEFORE base.Update() advances frames, so we can interrupt cleanly
        if (player.Movement != null && player.Movement.IsGrounded() && !prevFrameGrounded)
        {
            OnLandedDuringCombat();
        }

        base.Update();

        if (fsm.CurrentState != this)
        {
            return;
        }

        // --- Recovery Early Exit (Gravity takes over) ---
        if (currentPhase == ActionPhase.Recovery && player.Movement != null && player.Combat != null && player.Combat.IsInAirborneAction())
        {
            float maxFall = player.Movement.profile != null
                ? player.Movement.profile.maxFallSpeed
                : 18f;

            if (gravityPolicy.ShouldExitRecovery(player.Movement.VerticalVelocity, maxFall))
            {
                fsm.Log("FSM", $"[{StateId}] Recovery exit: fall velocity threshold reached", Color.yellow);
                OnActionComplete();
                return;
            }
        }

        // --- Continuous Gravity Update ---
        // Ensures ground-to-air transitions (via motionData) react instantly.
        UpdateGravityScale(currentPhase, GetRecoveryProgress());

        if (player.Combat != null && player.Combat.IsAttacking() && currentPhase == ActionPhase.Active)
        {
            ActionFrameData currentFrameData = GetFrameData();
            if (currentFrameData != null)
            {
                int activeFrame = currentFrame - currentFrameData.ActivePhaseStart + 1;
                StateContext ctx = BuildInputContext();
                ctx.activeFrame = activeFrame;

                AttackStepData triggeredStep = resolver.ResolveTriggers(ctx, activeFrame);
                if (triggeredStep != null)
                {
                    ExecuteCombatStep(triggeredStep, ctx, true);
                }
            }
        }

        if (player.Movement != null)
        {
            prevFrameGrounded = player.Movement.IsGrounded();
        }
    }

    // =========================================================
    // Interrupt Rules
    // =========================================================

    public new bool CanBeInterruptedBy(ActionCategory incoming)
    {
        if (player.Combat?.activeThrowContext != null
            && player.Combat.activeThrowContext.SessionState == GrabSessionState.Held)
        {
            return false;
        }
        return base.CanBeInterruptedBy(incoming);
    }

    public bool TryTriggerCounter(GameObject attacker)
    {
        if (player.Combat != null && player.Combat.IsAttacking() && currentPhase == ActionPhase.Active)
        {
            ActionFrameData currentFrameData = GetFrameData();
            if (currentFrameData != null)
            {
                int activeFrame = currentFrame - currentFrameData.ActivePhaseStart + 1;
                StateContext ctx = BuildInputContext();
                ctx.activeFrame = activeFrame;
                ctx.attacker = attacker;

                AttackStepData currentStep = resolver.GetCurrentStepData();
                if (currentStep == null || currentStep.triggerWindows == null) return false;

                TriggerWindow bestMatch = null;
                foreach (TriggerWindow trigger in currentStep.triggerWindows)
                {
                    if (trigger == null || trigger.targetNode == null) continue;
                    if (!trigger.IsInWindow(activeFrame)) continue;

                    bool isCounterTrigger = false;
                    foreach (ActionConditionBase cond in trigger.triggerConditions)
                    {
                        if (cond is PlayerGotHitCondition)
                        {
                            isCounterTrigger = true;
                            break;
                        }
                    }

                    if (!isCounterTrigger) continue;

                    if (ActionConditionUtility.AllMet(trigger.triggerConditions, ctx))
                    {
                        if (bestMatch == null || trigger.priority > bestMatch.priority)
                            bestMatch = trigger;
                    }
                }

                if (bestMatch != null)
                {
                    foreach (ActionConditionBase cond in bestMatch.triggerConditions)
                    {
                        cond?.OnTriggered(ctx);
                    }

                    AttackStepData triggeredStep = resolver.ForceTransitionToNode(bestMatch.targetNode);
                    if (triggeredStep != null)
                    {
                        ExecuteCombatStep(triggeredStep, ctx, true);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // =========================================================
    // Combat Utilities
    // =========================================================

    protected abstract AttackCategory GetAttackCategory();

    protected override ActionFrameData GetFrameData()
    {
        return player.Combat != null ? player.Combat.GetCurrentStepData() : null;
    }

    protected override void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        player.Combat?.ProcessFrameFromState(frame, phase);
        ProcessActionEvents(frame);

        if (phase == ActionPhase.Recovery)
        {
            framesInRecovery++;
        }
    }

    protected override bool ShouldAdvanceLogicFrames()
    {
        if (pauseCurrentNodeForSequence)
        {
            return false;
        }

        if (advanceCurrentNodeWithUnscaledTime
            && GameFreezeManager.Instance != null
            && GameFreezeManager.Instance.OwnsScopedFreeze(activeFreezeToken))
        {
            return true;
        }

        return player.Combat == null || !player.Combat.HasActiveHitstop;
    }

    protected override bool ShouldAdvanceWithUnscaledTime()
    {
        return advanceCurrentNodeWithUnscaledTime
            && GameFreezeManager.Instance != null
            && GameFreezeManager.Instance.OwnsScopedFreeze(activeFreezeToken);
    }

    protected override void OnRecoveryBegin()
    {
        player.Combat?.OnRecoveryPhaseFromState();
    }

    protected override void OnActionComplete()
    {
        TryStartCooldown();
        resolver.Reset();
        player.Combat?.ResetCombo();
        ExitToAppropriateMovementState();
    }

    protected override void OnCancelBegin()
    {
        TryStartCooldown();
    }

    protected override void OnPhaseChanged(ActionPhase oldPhase, ActionPhase newPhase)
    {
        base.OnPhaseChanged(oldPhase, newPhase);

        if (newPhase == ActionPhase.Startup)
        {
            if (player.Combat != null && player.Combat.IsInAirborneAction() && player.Movement != null)
            {
                Rigidbody2D rb = player.Movement.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * gravityPolicy.startupVelocityRetain);
                }
            }
        }
        else if (newPhase == ActionPhase.Recovery)
        {
            framesInRecovery = 0;
            ActionFrameData frameData = CachedFrameData;
            recoveryFrameCount = frameData != null ? frameData.recoveryFrames : 1;
        }

        UpdateGravityScale(newPhase, 0f);
    }

    protected virtual void TryStartCooldown()
    {
        if (hasStartedCooldown || player.Combat == null) return;

        if (resolver.ActiveCombatAction is SkillAction skillAction && skillAction.cooldown > 0f)
        {
            player.Combat.StartActionCooldown(GetAttackCategory(), skillAction.actionName);

            if (AbilityUIManager.Instance != null)
            {
                AbilityUIManager.AbilityType abilityType =
                    GetAttackCategory() == AttackCategory.Ultimate
                        ? AbilityUIManager.AbilityType.Ultimate
                        : AbilityUIManager.AbilityType.Skill;

                AbilityUIManager.Instance.TriggerCooldown(abilityType, skillAction.cooldown);
            }

            hasStartedCooldown = true;
        }
    }

    protected virtual InputContext BuildInputContext()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        bool physicallyGrounded = player.Movement != null && player.Movement.IsGrounded();
        bool actionGrounded = player.Movement != null && player.Movement.IsGroundedForActionSelection();

        return new InputContext
        {
            isGrounded = actionGrounded,
            directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
            facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            previousState = transitionContext.previousState,
            hasGrabbedEnemy = player.Combat != null && player.Combat.HasGrabbedEnemy(),
            hasPreviouslyGrabbedEnemy = player.Combat != null && player.Combat.HadGrabAtStepStart,
            isAgainstWall = player.Movement != null && player.Movement.IsAgainstWall(),
            wasAirborne = !prevFrameGrounded && physicallyGrounded,
            passiveController = player.PassiveController,
            runtimeState = player.PassiveController != null ? player.PassiveController.RuntimeState : null
        };
    }

    protected bool TryResolveNextStep(bool isAutoChain = false)
    {
        if (!resolver.IsActive) return false;

        InputContext ctx = BuildInputContext();
        AttackStepData nextStep = resolver.ResolveNext(ctx);
        if (nextStep == null) return false;

        ExecuteCombatStep(nextStep, ctx, isAutoChain);
        return true;
    }

    protected void ExecuteCombatStep(AttackStepData step, InputContext ctx, bool isAutoChain)
    {
        EndActiveActionEventSequence();
        triggeredActionEvents.Clear();

        if (player.Movement != null)
        {
            player.Movement.SetUseUnscaledAttackMotion(false);
            player.Movement.ForceFaceDirectionFromInput(ctx.directionalInput);
        }

        player.Combat?.ExecuteStep(step, resolver.TreeDepth, !ctx.isGrounded, GetAttackCategory(), resolver.ActiveCombatAction?.actionName, isAutoChain);
        RestartAction();
    }

    protected bool TryAutoChain()
    {
        if (player.Combat == null) return false;

        AttackStepData step = player.Combat.GetCurrentStepData();
        if (step == null || !step.autoChainToNext) return false;

        return TryResolveNextStep(true);
    }

    protected virtual void HandleGrabConnected()
    {
        InputContext ctx = BuildInputContext();
        AttackStepData grabFollowUp = resolver.ResolveGrabSuccess(ctx);
        if (grabFollowUp != null)
        {
            fsm.InputBuffer?.ClearAll();

            ThrowContext throwCtx = player.Combat.activeThrowContext;
            Transform socket = player.Combat.hitBoxController?.attackAnchor
                             ?? player.transform;
            Vector2 offset = player.Combat.LastGrabOffset;

            foreach (EnemyController enemy in throwCtx.GetGrabbedEnemies())
                throwCtx.BeginSession(enemy, socket, offset);

            int holdFrames = grabFollowUp.startupFrames
                           + grabFollowUp.activeFrames
                           + grabFollowUp.recoveryFrames + 2;
            throwCtx.ExtendHold(holdFrames);

            ExecuteCombatStep(grabFollowUp, ctx, true);
        }
    }

    private void ProcessActionEvents(int frame)
    {
        AttackStepData step = player.Combat != null ? player.Combat.GetCurrentStepData() : null;
        if (step?.eventData?.entries == null)
        {
            return;
        }

        foreach (ActionEventEntry entry in step.eventData.entries)
        {
            if (entry == null || triggeredActionEvents.Contains(entry))
            {
                continue;
            }

            if (frame != entry.triggerFrame)
            {
                continue;
            }

            triggeredActionEvents.Add(entry);
            ExecuteActionEvent(entry);
        }
    }

    private void ExecuteActionEvent(ActionEventEntry entry)
    {
        switch (entry.eventType)
        {
            case ActionEventType.UltimateOverlaySequence:
                StartUltimateOverlaySequence(entry);
                break;
        }
    }

    private void StartUltimateOverlaySequence(ActionEventEntry entry)
    {
        EndActiveActionEventSequence();

        activeActionSequence = entry;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;

        if (entry.freezeWorld && GameFreezeManager.Instance != null)
        {
            activeFreezeToken = GameFreezeManager.Instance.BeginScopedFreeze();

            switch (entry.timelineMode)
            {
                case UltimateFreezeTimelineMode.PauseNodeDuringFreeze:
                    pauseCurrentNodeForSequence = true;
                    player.Movement?.SetUseUnscaledAttackMotion(false);
                    break;

                case UltimateFreezeTimelineMode.AdvanceNodeUnscaledDuringFreeze:
                    advanceCurrentNodeWithUnscaledTime = true;
                    player.Movement?.SetUseUnscaledAttackMotion(true);

                    // Ensure the player's animation also advances while the world is frozen
                    if (player.Animator != null && player.Animator.anim != null)
                    {
                        player.Animator.anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                    }
                    break;
            }
        }

        if (UltimateFlashOverlay.Instance == null)
        {
            TriggerUltimateSequencePayload(entry);
            FinishActiveActionSequence();
            return;
        }

        UltimateFlashOverlay.Instance.PlaySequence(
            entry.overlaySettings,
            () => HandleUltimateSequenceBlackScreenStarted(entry),
            FinishActiveActionSequence);
    }

    private void HandleUltimateSequenceBlackScreenStarted(ActionEventEntry entry)
    {
        TriggerUltimateSequencePayload(entry);
    }

    private void TriggerUltimateSequencePayload(ActionEventEntry entry)
    {
        Transform playerTransform = player.transform;
        Vector3 offset = entry.activationVFXOffset;
        
        // Fix: Use player's FacingDirection instead of scale.x (since player flips via Y rotation)
        float facingDirection = player.Movement != null ? player.Movement.FacingDirection : (playerTransform.localScale.x > 0 ? 1f : -1f);
        
        offset.x *= facingDirection;

        Vector3 spawnPosition = playerTransform.position + offset;

        if (entry.activationVFXType != Project.Combat.VFXType.None && Project.Combat.VFXManager.Instance != null)
        {
            Project.Combat.VFXManager.Instance.Spawn(entry.activationVFXType, spawnPosition, facingDirection < 0);
        }
        else if (entry.activationVFXPrefab != null)
        {
            GameObject vfxInstance = Object.Instantiate(entry.activationVFXPrefab, spawnPosition, playerTransform.rotation);

            ParticleSystem[] particleSystems = vfxInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in particleSystems)
            {
                ParticleSystem.MainModule main = ps.main;
                main.useUnscaledTime = true;
            }

            Animator[] animators = vfxInstance.GetComponentsInChildren<Animator>();
            foreach (Animator animator in animators)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlayUltimateActivation(entry.activationVoice, entry.activationSFX, entry.voiceDelay);
        }
    }

    private void FinishActiveActionSequence()
    {
        if (GameFreezeManager.Instance != null && activeFreezeToken.IsValid)
        {
            GameFreezeManager.Instance.EndScopedFreeze(activeFreezeToken);
        }

        // Restore animator update mode to normal after the sequence finishes
        if (player.Animator != null && player.Animator.anim != null)
        {
            player.Animator.anim.updateMode = AnimatorUpdateMode.Normal;
        }

        activeFreezeToken = default;
        activeActionSequence = null;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        player.Movement?.SetUseUnscaledAttackMotion(false);
    }

    private void EndActiveActionEventSequence()
    {
        if (UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.StopSequence(false);
        }

        if (GameFreezeManager.Instance != null && activeFreezeToken.IsValid)
        {
            GameFreezeManager.Instance.EndScopedFreeze(activeFreezeToken);
        }

        // Restore animator update mode to normal if we were in an unscaled sequence
        if (player.Animator != null && player.Animator.anim != null)
        {
            player.Animator.anim.updateMode = AnimatorUpdateMode.Normal;
        }

        activeFreezeToken = default;
        activeActionSequence = null;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        player.Movement?.SetUseUnscaledAttackMotion(false);
    }

    private void ResetActionEventTracking()
    {
        triggeredActionEvents.Clear();
        activeActionSequence = null;
        activeFreezeToken = default;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        player.Movement?.SetUseUnscaledAttackMotion(false);

        // Safety reset for animator update mode
        if (player.Animator != null && player.Animator.anim != null)
        {
            player.Animator.anim.updateMode = AnimatorUpdateMode.Normal;
        }
    }

    // =========================================================
    // Airborne Gravity & Landing
    // =========================================================

    private void UpdateGravityScale(ActionPhase phase, float recoveryProgress)
    {
        // Only apply airborne gravity policy if we are actually airborne
        if (player.Combat != null && player.Combat.IsInAirborneAction())
        {
            float scale = gravityPolicy.Evaluate(phase, recoveryProgress);
            player.Combat.SetAirborneGravityScale(scale);
        }
        else if (player.Combat != null)
        {
            // Reset to full gravity when grounded or not in airborne action
            player.Combat.SetAirborneGravityScale(1f);
        }
    }

    private float GetRecoveryProgress()
    {
        if (currentPhase != ActionPhase.Recovery) return 0f;
        return recoveryFrameCount > 0 ? (float)framesInRecovery / recoveryFrameCount : 1f;
    }

    private void OnLandedDuringCombat()
    {
        if (player.Combat == null) return;

        if (player.Combat.ShouldInterruptAttackOnGrounded())
        {
            fsm.Log("FSM", $"[{StateId}] Interrupted by landing", Color.yellow);
            player.Combat.ResetCombo();
            ExitToAppropriateMovementState();
        }
    }
}
