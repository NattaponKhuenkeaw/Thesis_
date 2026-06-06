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
    private readonly Dictionary<ActionEventEntry, Coroutine> activeAfterImageCoroutines = new Dictionary<ActionEventEntry, Coroutine>();
    protected bool prevFrameGrounded;
    private ActionEventEntry activeActionSequence;
    private GameFreezeManager.FreezeToken activeFreezeToken;
    private bool pauseCurrentNodeForSequence;
    private bool advanceCurrentNodeWithUnscaledTime;
    private Coroutine activeActionSequenceCoroutine;

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

        prevFrameGrounded = player.Movement != null && player.Movement.GroundState.IsEffectivelyGrounded;

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
        GroundStateAuthority authorityOnExit = player.Movement != null
            ? player.Movement.GroundState.Authority
            : GroundStateAuthority.Physical;

        EndActiveActionEventSequence();
        StopAfterImageTrails();
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

            player.Combat.SetAirborneGravityScale(1f); // Restore gravity
        }

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected -= HandleGrabConnected;
        }

        if (player.Movement != null && authorityOnExit == GroundStateAuthority.ForceAirborne)
        {
            player.Movement.GroundState.ClearAuthority();
        }

        base.Exit();
        resolver.Reset();
        hasStartedCooldown = false;
        triggeredActionEvents.Clear();
    }

    public override void Update()
    {
        UpdateGroundAuthorityGuardrail();

        // Check for landing BEFORE base.Update() advances frames, so we can interrupt cleanly
        bool isMovingUp = player.Movement != null && player.Movement.VerticalVelocity > 0.1f;
        if (player.Movement != null && player.Movement.GroundState.IsEffectivelyGrounded && !prevFrameGrounded && !isMovingUp)
        {
            OnLandedDuringCombat();
        }

        base.Update();

        if (fsm.CurrentState != this)
        {
            return;
        }

        // --- Ground State Tracking ---
        if (player.Movement != null)
        {
            prevFrameGrounded = player.Movement.GroundState.IsEffectivelyGrounded;
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
        // Keeps gravity synced with the current action authority after ground/air promotion decisions.
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
            prevFrameGrounded = player.Movement.GroundState.IsEffectivelyGrounded;
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

                float resolvedCooldown = player.Combat.GetFinalCooldown(
                    GetAttackCategory(),
                    skillAction.cooldown,
                    skillAction.actionName);
                AbilityUIManager.Instance.TriggerCooldown(abilityType, resolvedCooldown);
            }

            hasStartedCooldown = true;
        }
    }

    protected virtual InputContext BuildInputContext()
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        bool logicallyGrounded = player.Movement != null && player.Movement.GroundState.IsEffectivelyGrounded;
        
        bool actionGrounded;
        if (player.Combat != null && player.Combat.IsAttacking())
        {
            actionGrounded = logicallyGrounded;
        }
        else
        {
            actionGrounded = player.Movement != null && player.Movement.IsGroundedForActionSelection();
        }

        return new InputContext
        {
            isGrounded = actionGrounded,
            directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
            facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            previousState = transitionContext.previousState,
            hasGrabbedEnemy = player.Combat != null && player.Combat.HasGrabbedEnemy(),
            hasPreviouslyGrabbedEnemy = player.Combat != null && player.Combat.HadGrabAtStepStart,
            isAgainstWall = player.Movement != null && player.Movement.IsAgainstWall(),
            wasAirborne = !prevFrameGrounded && logicallyGrounded,
            isAirCombo = player.Combat != null && player.Combat.IsAirCombo,
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
        StopAfterImageTrails();
        triggeredActionEvents.Clear();

        if (player.Movement != null)
        {
            player.Movement.SetUseUnscaledAttackMotion(false);
            player.Movement.ForceFaceDirectionFromInput(ctx.directionalInput);
        }

        player.Combat?.BeginResolvedAction(
            ctx.isGrounded,
            !ctx.isGrounded,
            resolver.ActiveCombatAction != null && resolver.ActiveCombatAction.allowGroundToAirTransition,
            resolver.ActiveCombatAction);

        player.Combat?.ExecuteStep(
            step,
            resolver.TreeDepth,
            !ctx.isGrounded,
            GetAttackCategory(),
            resolver.ActiveCombatAction?.actionName,
            isAutoChain);
        UpdateGroundAuthorityGuardrail();
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

            case ActionEventType.TeleportToNearestEnemy:
                StartTeleportToNearestEnemySequence(entry);
                break;

            case ActionEventType.ScreenImpulse:
                TriggerScreenImpulse(entry);
                break;

            case ActionEventType.AfterImage:
                StartAfterImageTrail(entry);
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

    private void StartTeleportToNearestEnemySequence(ActionEventEntry entry)
    {
        EndActiveActionEventSequence();

        if (entry == null)
        {
            return;
        }

        entry.teleportSettings ??= new TeleportEventSettings();
        TeleportEventSettings settings = entry.teleportSettings;
        EnemyController target = player.Combat != null
            ? player.Combat.FindNearestEnemyTarget(settings.targetRadius)
            : null;

        if (target == null)
        {
            return;
        }

        activeActionSequence = entry;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;

        if (settings.snapCameraToPlayer && CameraManager.instance != null)
        {
            CameraManager.instance.BeginTemporaryPlayerSnap(settings.cameraSnapDuration);
        }

        TeleportPlayerNearTarget(target, settings);

        if (settings.cameraImpulseStrength > 0f && GameImpulseController.Instance != null)
        {
            GameImpulseController.Instance.PlayOneShot(settings.cameraImpulseStrength);
        }

        if (settings.playScreenFlick
            && settings.screenFlickSettings != null
            && UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.PlaySequence(
                settings.screenFlickSettings,
                null,
                FinishActiveActionSequence);
            return;
        }

        float duration = settings.GetTotalDurationSeconds();
        if (duration > 0f && player != null)
        {
            activeActionSequenceCoroutine = player.StartCoroutine(FinishActionSequenceAfterDelay(duration));
            return;
        }

        FinishActiveActionSequence();
    }

    private void TeleportPlayerNearTarget(EnemyController target, TeleportEventSettings settings)
    {
        if (target == null)
        {
            return;
        }

        Vector3 playerPosition = player.transform.position;
        float targetX = target.transform.position.x;
        float horizontalDirection = Mathf.Abs(targetX - playerPosition.x) > 0.001f
            ? Mathf.Sign(targetX - playerPosition.x)
            : (player.Movement != null ? player.Movement.FacingDirection : 1f);
        float resolvedOffset = Mathf.Max(0f, settings.teleportOffsetX);
        float destinationX = targetX - (horizontalDirection * resolvedOffset);

        if (player.Movement != null)
        {
            Vector3 warpDelta = player.Movement.TeleportHorizontally(destinationX);
            player.Movement.SnapFacingDirection(horizontalDirection >= 0f ? 1 : -1);
            player.Combat?.HandleOwnerTeleported();
            CameraManager.instance?.NotifyPlayerWarp(warpDelta);
        }
        else
        {
            Vector3 destination = new Vector3(destinationX, playerPosition.y, playerPosition.z);
            Vector3 warpDelta = destination - playerPosition;
            player.transform.position = destination;
            Physics2D.SyncTransforms();
            player.Combat?.HandleOwnerTeleported();
            CameraManager.instance?.NotifyPlayerWarp(warpDelta);
        }
    }

    private void HandleUltimateSequenceBlackScreenStarted(ActionEventEntry entry)
    {
        TriggerUltimateSequencePayload(entry);
    }

    private void TriggerScreenImpulse(ActionEventEntry entry)
    {
        if (entry?.screenImpulseSettings?.impulse == null || GameImpulseController.Instance == null)
        {
            return;
        }

        GameImpulseController.Instance.Play(entry.screenImpulseSettings.impulse);
    }

    private void StartAfterImageTrail(ActionEventEntry entry)
    {
        if (entry?.afterImageSettings == null || player?.Movement == null)
        {
            return;
        }

        StopAfterImageTrail(entry);

        Coroutine routine = player.StartCoroutine(RunAfterImageTrail(entry));
        activeAfterImageCoroutines[entry] = routine;
    }

    private System.Collections.IEnumerator RunAfterImageTrail(ActionEventEntry entry)
    {
        AttackStepData sourceStep = player.Combat != null ? player.Combat.GetCurrentStepData() : null;
        AfterImageEventSettings settings = entry.afterImageSettings;
        float interval = Mathf.Max(0.01f, settings.spawnIntervalSeconds);
        float elapsed = interval;

        while (fsm.CurrentState == this
            && player.Combat != null
            && player.Combat.GetCurrentStepData() == sourceStep)
        {
            if (settings.endMode == AfterImageEndMode.EndAtFrame && CurrentFrame > settings.endFrame)
            {
                break;
            }

            if (elapsed >= interval)
            {
                player.Movement.SpawnAfterimage();
                elapsed = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        activeAfterImageCoroutines.Remove(entry);
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
        if (activeActionSequenceCoroutine != null && player != null)
        {
            player.StopCoroutine(activeActionSequenceCoroutine);
            activeActionSequenceCoroutine = null;
        }

        if (GameFreezeManager.Instance != null && activeFreezeToken.IsValid)
        {
            GameFreezeManager.Instance.EndScopedFreeze(activeFreezeToken);
        }

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EndTemporaryPlayerSnap();
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
        if (activeActionSequenceCoroutine != null && player != null)
        {
            player.StopCoroutine(activeActionSequenceCoroutine);
            activeActionSequenceCoroutine = null;
        }

        if (UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.StopSequence(false);
        }

        if (GameFreezeManager.Instance != null && activeFreezeToken.IsValid)
        {
            GameFreezeManager.Instance.EndScopedFreeze(activeFreezeToken);
        }

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EndTemporaryPlayerSnap();
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
        activeActionSequenceCoroutine = null;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;
        player.Movement?.SetUseUnscaledAttackMotion(false);
        StopAfterImageTrails();

        if (CameraManager.instance != null)
        {
            CameraManager.instance.EndTemporaryPlayerSnap();
        }

        // Safety reset for animator update mode
        if (player.Animator != null && player.Animator.anim != null)
        {
            player.Animator.anim.updateMode = AnimatorUpdateMode.Normal;
        }
    }

    private System.Collections.IEnumerator FinishActionSequenceAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        activeActionSequenceCoroutine = null;
        FinishActiveActionSequence();
    }

    private void StopAfterImageTrails()
    {
        if (player == null || activeAfterImageCoroutines.Count == 0)
        {
            activeAfterImageCoroutines.Clear();
            return;
        }

        foreach (Coroutine routine in activeAfterImageCoroutines.Values)
        {
            if (routine != null)
            {
                player.StopCoroutine(routine);
            }
        }

        activeAfterImageCoroutines.Clear();
    }

    private void StopAfterImageTrail(ActionEventEntry entry)
    {
        if (player == null || entry == null)
        {
            return;
        }

        if (activeAfterImageCoroutines.TryGetValue(entry, out Coroutine routine) && routine != null)
        {
            player.StopCoroutine(routine);
        }

        activeAfterImageCoroutines.Remove(entry);
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

    private void UpdateGroundAuthorityGuardrail()
    {
        if (player.Movement == null)
        {
            return;
        }

        if (player.Combat == null || !player.Combat.IsAttacking())
        {
            if (player.Movement.GroundState.Authority == GroundStateAuthority.ForceAirborne)
            {
                player.Movement.GroundState.ClearAuthority();
            }

            return;
        }

        if (player.Combat.IsAirCombo)
        {
            if (player.Movement.IsPhysicallyGrounded() && player.Movement.VerticalVelocity <= 0.1f)
            {
                player.Movement.GroundState.ClearAuthority();
            }
            else
            {
                player.Movement.GroundState.SetAuthority(GroundStateAuthority.ForceAirborne);
            }

            return;
        }

        if (player.Combat.CurrentActionAllowsGroundToAirTransition)
        {
            if (!player.Movement.IsPhysicallyGrounded())
            {
                player.Combat.SetAirCombo(true);
                player.Movement.GroundState.SetAuthority(GroundStateAuthority.ForceAirborne);

                if (player.Animator != null)
                {
                    player.Animator.RestoreAirborneState();
                }
            }
            else
            {
                player.Movement.GroundState.ClearAuthority();
            }

            return;
        }

        if (player.Combat.CurrentActionStartedGrounded)
        {
            player.Movement.GroundState.SetAuthority(GroundStateAuthority.StickyGrounded);
            return;
        }

        player.Movement.GroundState.ClearAuthority();
    }

    // =========================================================
    // Pre-Execution Interrupt Guard
    // =========================================================

    /// <summary>
    /// Centralized guard that checks whether a new combat action should be
    /// aborted BEFORE resolve/execute/cooldown. Call this at the top of
    /// OnSkillInput / OnUltimateInput / OnAttackInput when currentPhase == None.
    ///
    /// Returns true if the action was aborted (state already exited).
    /// </summary>
    protected bool ShouldAbortBeforeExecution()
    {
        if (player.Movement == null) return false;

        // Case 1: We just landed this frame — an airborne action would be
        // immediately interrupted on the next Update(), wasting its cooldown.
        bool isGrounded = player.Movement.GroundState.IsEffectivelyGrounded;
        bool wasAirborneLastFrame = !prevFrameGrounded;
        bool notMovingUp = player.Movement.VerticalVelocity <= 0.1f;

        if (isGrounded && wasAirborneLastFrame && notMovingUp
            && player.Combat != null && player.Combat.ShouldInterruptAttackOnGrounded())
        {
            fsm.Log("FSM", $"[{StateId}] Aborted: landing detected before execution", Color.yellow);
            player.Combat.ResetCombo();
            ExitToAppropriateMovementState();
            return true;
        }

        return false;
    }
}
