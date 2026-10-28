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
    private CombatAction counterCostCommittedAction;
    private ActionCameraToken _actionCameraToken;
    private CameraManager _actionCameraOwner;
    private ProceduralSlashField _slashField;
    private int _slashFieldStartFrame;

    private void ReleaseSlashField()
    {
        if (_slashField != null) _slashField.Stop();
    }

    private void ReleaseActionCamera(bool allowTimedCompletion = false)
    {
        if (_actionCameraOwner != null) _actionCameraOwner.ReleaseActionCamera(_actionCameraToken, allowTimedCompletion);
        _actionCameraOwner = null;
        _actionCameraToken = default;
    }

    protected void ResetActionCounterCommitment() => counterCostCommittedAction = null;

    private readonly HashSet<ActionEventEntry> triggeredActionEvents = new HashSet<ActionEventEntry>();
    private readonly Dictionary<ActionEventEntry, Coroutine> activeAfterImageCoroutines = new Dictionary<ActionEventEntry, Coroutine>();
    private ActionEventEntry activeActionSequence;
    private GameFreezeManager.FreezeToken activeFreezeToken;
    private bool pauseCurrentNodeForSequence;
    private bool advanceCurrentNodeWithUnscaledTime;
    private Coroutine activeActionSequenceCoroutine;
    private CombatAction capturedOutputAction;
    private bool capturedActionStartedGrounded;
    private bool hasCommittedActionOutput;

    public CombatPlayerState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        resolverTable = player.characterObject != null ? player.characterObject.actionResolverTable : null;
        resolver.SetLogger(fsm);
        hasStartedCooldown = false;
        ResetActionCounterCommitment();
        capturedOutputAction = null;
        capturedActionStartedGrounded = fsm.IsGroundedContext;
        hasCommittedActionOutput = false;
        ResetActionEventTracking();

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected += HandleGrabConnected;
        }
    }

    public override void Exit()
    {
        ReleaseSlashField();
        ReleaseActionCamera(currentPhase == ActionPhase.Done);
        CommitResolvedActionOutput();
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
        }

        if (player.Combat != null)
        {
            player.Combat.OnGrabConnected -= HandleGrabConnected;
        }

        base.Exit();
        resolver.Reset();
        capturedOutputAction = null;
        hasStartedCooldown = false;
        triggeredActionEvents.Clear();
    }

    public override void Update()
    {
        base.Update();

        if (fsm.CurrentState != this)
        {
            return;
        }

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

    protected virtual bool IsQteIntroExecution => false;

    protected void BeginQteIntro(CombatAction action, AttackStepData step, EnemyController target, ComboTree combo = null)
    {
        resolver.BeginExplicitStep(action, step, true, combo);
        counterCostCommittedAction = action;
        InputContext context = BuildInputContext();
        context.directionalInput = Vector2.zero;
        ExecuteCombatStep(step, context, false);
        player.Movement?.SetActionMotionTarget(target);
        // Evaluate the new controller immediately so replacement never renders an idle pose.
        player.Animator?.anim?.Update(0f);
    }

    protected abstract AttackCategory GetAttackCategory();

    protected override ActionFrameData GetFrameData()
    {
        return player.Combat != null ? player.Combat.GetCurrentStepData() : null;
    }

    protected override void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        player.Combat?.ProcessFrameFromState(frame, phase);
        ProcessActionEvents(frame);
        if (_slashField != null && _slashField.IsPlaying) _slashField.SetLogicFrame(frame - _slashFieldStartFrame);
        if (_actionCameraOwner != null) _actionCameraOwner.SampleActionCamera(_actionCameraToken, frame);
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
        TryStartCooldown();
    }

    protected override void HandleInputDuringCancel()
    {
        // Auto-chain is a structural step transition, so it must still win
        // when an authored Cancel window begins on the first Recovery frame.
        if (TryAutoChain()) return;
        base.HandleInputDuringCancel();
    }

    protected override void OnActionComplete()
    {
        CaptureCompletedStepBoundaryContext();
        TryStartCooldown();
        player.Combat?.ResetCombo();
        ExitToAppropriateMovementState();
    }

    protected virtual void TryStartCooldown()
    {
        if (hasStartedCooldown || player.Combat == null) return;

        if (resolver.ActiveCombatAction is SkillAction skillAction && skillAction.cooldown > 0f)
        {
            player.Combat.StartActionCooldown(GetAttackCategory(), skillAction.actionName, skillAction.actionId);

            if (AbilityUIManager.Instance != null)
            {
                AbilityUIManager.AbilityType abilityType =
                    GetAttackCategory() == AttackCategory.Ultimate
                        ? AbilityUIManager.AbilityType.Ultimate
                        : AbilityUIManager.AbilityType.Skill;

                float resolvedCooldown = player.Combat.GetRemainingCooldown(
                    GetAttackCategory(),
                    skillAction.cooldown,
                    skillAction.actionName,
                    skillAction.actionId);
                AbilityUIManager.Instance.TriggerCooldown(abilityType, resolvedCooldown);
            }

            hasStartedCooldown = true;
        }
    }

    protected virtual InputContext BuildInputContext(bool preserveActiveActionInput = true)
    {
        ActionInputBuffer buffer = fsm.InputBuffer;
        bool logicallyGrounded = preserveActiveActionInput && resolver.IsActive
            ? resolver.StartedGrounded
            : fsm.IsGroundedContext;
        bool justLanded = player.Movement != null && (player.Movement.GroundState.JustLanded || player.Movement.IsLanding);
        bool wasPhysicallyAirborne = player.Movement != null
            ? !player.Movement.GroundState.WasGrounded
            : !logicallyGrounded;

        return new InputContext
        {
            isGrounded = logicallyGrounded,
            directionalInput = buffer != null ? buffer.DirectionalInput : Vector2.zero,
            facingDirection = player.Movement != null ? player.Movement.FacingDirection : 1,
            previousState = transitionContext.previousState,
            hasGrabbedEnemy = player.Combat != null && player.Combat.HasGrabbedEnemy(),
            hasPreviouslyGrabbedEnemy = player.Combat != null && player.Combat.HadGrabAtStepStart,
            isAgainstWall = player.Movement != null && player.Movement.IsAgainstWall(),
            wasAirborne = wasPhysicallyAirborne || justLanded,
            justLanded = justLanded,
            isAirCombo = player.Combat != null && player.Combat.IsAirCombo,
            passiveController = player.PassiveController,
            runtimeState = player.PassiveController != null ? player.PassiveController.RuntimeState : null,
            actionAvailabilityProvider = player
        };
    }

    protected bool TryResolveNextStep(bool isAutoChain = false, bool preserveAuthoredCombo = false)
    {
        if (!resolver.IsActive) return false;

        InputContext ctx = preserveAuthoredCombo ? BuildInputContext() : BuildStepBoundaryInputContext();
        bool contextChanged = ctx.isGrounded != resolver.StartedGrounded;
        AttackStepData nextStep;

        if (contextChanged)
        {
            // A completed step that crossed a ground boundary must not keep
            // walking the old tree. Re-resolve the same input category from
            // the matching Ground/Air action root.
            ctx.actionAvailabilityProvider = null;
            ActionResolver previewResolver = new ActionResolver();
            nextStep = previewResolver.ResolveInitial(
                ctx,
                resolverTable,
                GetAttackCategory(),
                forceRootStep: true);
            if (nextStep == null)
            {
                return false;
            }

            player.Combat?.ResetCombo();
            resolver.Reset();
            nextStep = resolver.ResolveInitial(
                ctx,
                resolverTable,
                GetAttackCategory(),
                forceRootStep: true);
            if (nextStep == null)
            {
                return false;
            }

            OnStepBoundaryActionResolved(resolver.ActiveCombatAction);
        }
        else
        {
            nextStep = resolver.ResolveNext(ctx);
        }

        if (nextStep == null) return false;

        ExecuteCombatStep(nextStep, ctx, isAutoChain);
        return true;
    }

    private InputContext BuildStepBoundaryInputContext()
    {
        bool boundaryGrounded = fsm.SyncCombatContextAtStepBoundary(
            resolver.StartedGrounded);
        InputContext ctx = BuildInputContext(preserveActiveActionInput: false);

        // Input routing may temporarily expose an authored output override.
        // The step-boundary resolver uses cause-aware physical contact instead.
        ctx.isGrounded = boundaryGrounded;
        return ctx;
    }

    protected virtual void OnStepBoundaryActionResolved(CombatAction action) { }

    protected bool ExecuteCombatStep(AttackStepData step, InputContext ctx, bool isAutoChain)
    {
        CombatAction action = resolver.ActiveCombatAction;
        if (step == null || action == null || player.Combat == null) return false;
        bool beginsNewCounterAction = action != counterCostCommittedAction;
        if (beginsNewCounterAction && action != null && action.resourceCost > 0
            && !player.TryCommitActionCounterCost(action)) return false;
        counterCostCommittedAction = action;
        EnemyController target = beginsNewCounterAction && action != null && action.requiresEnemyTarget
            ? player.Combat?.FindNearestEnemyTarget(action.GetTargetSearchRadius()) : null;
        ReleaseActionCamera(isAutoChain || currentPhase == ActionPhase.Done);
        ReleaseSlashField();
        EndActiveActionEventSequence();
        StopAfterImageTrails();
        triggeredActionEvents.Clear();

        if (player.Movement != null)
        {
            player.Movement.SetUseUnscaledAttackMotion(false);
            player.Movement.ForceFaceDirectionFromInput(ctx.directionalInput);
        }

        CombatAction resolvedAction = resolver.ActiveCombatAction;

        // Output ownership is step-scoped. This matters when a Both action
        // re-selects itself from the opposite physical context: the action
        // reference is unchanged, but MatchInput must follow the new root step.
        capturedOutputAction = resolvedAction;
        capturedActionStartedGrounded = ctx.isGrounded;
        hasCommittedActionOutput = false;

        player.Combat?.BeginResolvedAction(!ctx.isGrounded, resolvedAction);

        player.Combat?.ExecuteStep(
            step,
            resolver.TreeDepth,
            !ctx.isGrounded,
            GetAttackCategory(),
            resolver.ActiveCombatAction?.actionName,
            resolver.ActiveCombatAction?.actionId,
            isAutoChain,
            IsQteIntroExecution);
        if (beginsNewCounterAction && target != null) player.Movement?.SetActionMotionTarget(target);
        RestartAction();
        return true;
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

            case ActionEventType.SetFacingDirection:
                ApplyFacingDirectionEvent(entry);
                break;

            case ActionEventType.BackgroundOverlayFlash:
                PlayBackgroundImpactFlash(entry);
                break;

            case ActionEventType.CameraAnimationControl:
                ReleaseActionCamera();
                _actionCameraOwner = CameraManager.instance;
                if (_actionCameraOwner != null)
                    _actionCameraToken = _actionCameraOwner.BeginCameraAnimation(player.Combat?.CurrentExecution,
                        entry.cameraAnimation, entry.triggerFrame, player.Movement != null ? player.Movement.FacingDirection : 1);
                break;

            case ActionEventType.ActionCameraMotion:
                ReleaseActionCamera();
                _actionCameraOwner = CameraManager.instance;
                if (_actionCameraOwner != null)
                {
                    int facing = player.Movement != null ? player.Movement.FacingDirection : 1;
                    _actionCameraToken = entry.cameraMotionSettings?.zoomControl != null
                        ? _actionCameraOwner.BeginScreenImpulseCamera(player.Combat?.CurrentExecution,
                            entry.cameraMotionSettings.zoomControl, entry.triggerFrame, facing)
                        : _actionCameraOwner.BeginActionCamera(player.Combat?.CurrentExecution,
                            entry.cameraMotionSettings, entry.triggerFrame, facing);
                }
                break;

            case ActionEventType.ProceduralSlashField:
                ReleaseSlashField();
                if (_slashField == null)
                {
                    var host = new GameObject("Action Slash Presentation");
                    host.transform.SetParent(player.transform, false);
                    _slashField = host.AddComponent<ProceduralSlashField>();
                }
                _slashFieldStartFrame = entry.triggerFrame;
                _slashField.Begin(player.Combat?.CurrentExecution, entry.slashFieldSettings, player.transform.position,
                    player.Movement != null ? player.Movement.FacingDirection : 1,
                    player.Animator != null ? player.Animator.GetComponent<SpriteRenderer>() : null);
                if (entry.slashFieldSettings?.fullScreenSlash == true)
                    PlayBackgroundImpactFlash(entry);
                break;
        }
    }

    private static void PlayBackgroundImpactFlash(ActionEventEntry entry)
    {
        if (entry == null || UltimateFlashOverlay.Instance == null)
        {
            return;
        }

        entry.backgroundOverlayFlashSettings ??= new BackgroundOverlayFlashSettings();
        UltimateFlashOverlay.Instance.PlayImpactFlash(entry.backgroundOverlayFlashSettings);
    }

    private void ApplyFacingDirectionEvent(ActionEventEntry entry)
    {
        if (entry == null || player?.Movement == null)
        {
            return;
        }

        entry.facingDirectionSettings ??= new FacingDirectionEventSettings();
        int direction = entry.facingDirectionSettings.ResolveDirection(
            player.Movement.FacingDirection);
        player.Movement.SnapFacingDirection(direction);
    }

    private void StartUltimateOverlaySequence(ActionEventEntry entry)
    {
        EndActiveActionEventSequence();

        activeActionSequence = entry;
        pauseCurrentNodeForSequence = false;
        advanceCurrentNodeWithUnscaledTime = false;

        if (entry.enableUltimateZoom && CameraManager.instance != null)
        {
            float zoomSize = entry.ultimateZoomSize > 0.1f ? entry.ultimateZoomSize : 5f;
            CameraManager.instance.SetUltimateZoom(true, zoomSize);
        }

        AbilityUIManager.FadeGameplayUI(0f, 0.25f);

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
        ScreenImpulseEventSettings settings = entry?.screenImpulseSettings;
        if (settings == null) return;
        if (settings.impulse != null && GameImpulseController.Instance != null)
            GameImpulseController.Instance.Play(settings.impulse);

        if (settings.cameraControl != null && settings.cameraControl.enabled && CameraManager.instance != null)
        {
            ReleaseActionCamera();
            _actionCameraOwner = CameraManager.instance;
            _actionCameraToken = _actionCameraOwner.BeginScreenImpulseCamera(player.Combat?.CurrentExecution,
                settings.cameraControl, entry.triggerFrame, player.Movement != null ? player.Movement.FacingDirection : 1);
        }
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
            float activationVolume = Mathf.Clamp01(entry.activationSFXVolume);
            if (resolver.ActiveCombatAction is UltimateAction ultimate)
                activationVolume *= Mathf.Clamp01(ultimate.activationSFXVolume);
            GameAudioManager.Instance.PlayUltimateActivation(entry.activationVoice, entry.activationSFX, entry.voiceDelay, activationVolume);
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
            CameraManager.instance.SetUltimateZoom(false);
        }

        AbilityUIManager.FadeGameplayUI(1f, 0.25f);

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
            CameraManager.instance.SetUltimateZoom(false);
        }

        AbilityUIManager.FadeGameplayUI(1f, 0.25f);

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
            CameraManager.instance.SetUltimateZoom(false);
        }

        AbilityUIManager.FadeGameplayUI(1f, 0.25f);

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
    // Authored Action Output Context
    // =========================================================

    /// <summary>
    /// Returns the state that a cancel or natural completion must commit before
    /// the next action or locomotion state resolves.
    /// </summary>
    public bool TryGetProjectedOutputContext(
        out CombatActionOutputContext outputContext,
        out bool startedGrounded)
    {
        CombatAction action = capturedOutputAction ?? resolver.ActiveCombatAction;
        outputContext = action != null
            ? action.outputContext
            : CombatActionOutputContext.MatchInput;
        startedGrounded = capturedOutputAction != null
            ? capturedActionStartedGrounded
            : resolver.StartedGrounded;
        return action != null;
    }

    protected void CommitResolvedActionOutput()
    {
        if (hasCommittedActionOutput
            || !TryGetProjectedOutputContext(
                out CombatActionOutputContext outputContext,
                out bool startedGrounded))
        {
            return;
        }

        hasCommittedActionOutput = true;
        fsm.CommitActionOutputContext(outputContext, startedGrounded);
    }

    // A blocked prepared path must resume from physical support, not snap through terrain.
    protected void CommitAbortedActionOutput(bool physicallyGrounded)
    {
        hasCommittedActionOutput = true;
        fsm.CommitActionOutputContext(physicallyGrounded ? CombatActionOutputContext.Ground
            : CombatActionOutputContext.Air, capturedActionStartedGrounded);
    }

    protected void CaptureCompletedStepBoundaryContext()
    {
        // Completion may update physical routing state for the outgoing action,
        // but MatchInput remains owned by the context captured on step entry.
        // Re-contact during authored MotionData must not rewrite that contract.
        fsm.SyncCombatContextAtStepBoundary(capturedActionStartedGrounded);
    }

    protected override void ExitToAppropriateMovementState()
    {
        CommitResolvedActionOutput();
        base.ExitToAppropriateMovementState();
    }
}
