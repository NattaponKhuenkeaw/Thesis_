using CombatCore;
using UnityEngine;

/// <summary>Executes the referenced intro and its optional authored input-driven combo.</summary>
public sealed class PlayerQteIntroState : PlayerAttackState
{
    private CombatAction _action;
    private AttackStepData _step;
    private EnemyController _target;
    private ComboTree _combo;
    private QtePreparedEntry _preparedEntry;
    private LightningPlungeVFX _plungeVfx;
    private LightningPlungeVFX _plungePrefab;
    private SpriteRenderer[] _plungeRenderers;
    private bool[] _originalRenderFlags;
    private bool _plungeActive, _plungeLanded;
    private MotionKeyframe _plungeMotion;
    private float _plungeVisualFrame;
    private bool _plungeNeedsAbort;

    public PlayerQteIntroState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }
    public override PlayerStateId StateId => PlayerStateId.QteIntro;
    protected override bool IsQteIntroExecution => resolver.TreeDepth <= 1;

    protected override InterruptPolicy GetInterruptPolicy()
    {
        if (resolver.TreeDepth > 1) return base.GetInterruptPolicy();
        InterruptPolicy policy = InterruptPolicy.BasicAttack;
        policy.active = ActionCategory.None;
        policy.recovery = _combo != null ? InterruptPolicy.BasicAttack.recovery : ActionCategory.None;
        policy.recoveryGate = _combo != null ? InterruptPolicy.BasicAttack.recoveryGate : InputGate.None;
        return policy;
    }

    public void Prepare(CombatAction action, AttackStepData step, EnemyController target)
    {
        _preparedEntry = default;
        _action = action;
        _step = step;
        _target = target;
        ComboTree authored = player.characterObject?.qteSwap?.GetIntroComboTree(player.characterObject.actionResolverTable);
        _combo = authored?.rootNode?.stepData == step ? authored : null;
    }

    public void Prepare(QtePreparedEntry entry)
    {
        Prepare(entry.action, entry.step, entry.target);
        _preparedEntry = entry;
    }

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        QtePlungeSettings settings = player.characterObject?.qteSwap?.plunge;
        _plungeActive = _preparedEntry.mode == QteEntryMode.LightningPlunge && settings?.prefab != null;
        _plungeLanded = false;
        _plungeNeedsAbort = false;
        _plungeVisualFrame = 0f;
        if (_plungeActive)
        {
            CachePlungeRenderers();
            SetPlungeActorHidden(true);
            // The outgoing state's Exit may have committed a Ground output after TeamLoader placement.
            player.Movement.ApplyQteEntryPosition(_preparedEntry.placement);
        }
        BeginQteIntro(_action, _step, _target, _combo);
        if (!_plungeActive) return;
        _plungeMotion = null;
        if (_step.motionData?.keyframes != null)
            foreach (var key in _step.motionData.keyframes)
                if (key.taskType == MotionTaskType.CapturedWorldPoint) { _plungeMotion = key; break; }
        player.Movement.PrepareCapturedWorldMotion(_preparedEntry.placement);
        if (_plungeVfx == null || _plungePrefab != settings.prefab)
        {
            if (_plungeVfx != null) Object.Destroy(_plungeVfx.gameObject);
            _plungeVfx = Object.Instantiate(settings.prefab, player.transform);
            _plungeVfx.name = "LightningPlungeVFX";
            _plungePrefab = settings.prefab;
        }
        var placement = _preparedEntry.placement;
        _plungeVfx.Begin(settings.visuals, placement.startBodyPosition, placement.surfacePoint, placement.surfaceNormal,
            execution: player.Combat?.CurrentExecution);
        _plungeVfx.Sample(0f, _step.ActivePhaseStart, _step.TotalFrames);
    }

    protected override void ProcessLogicFrame(int frame, ActionPhase phase)
    {
        if (_plungeNeedsAbort)
        {
            _plungeNeedsAbort = false;
            AbortPlunge();
            return;
        }
        if (_plungeActive && !_plungeLanded)
        {
            if (!player.Movement.SampleCapturedWorldMotion(_plungeMotion, frame))
            { AbortPlunge(); return; }
            if (frame >= _step.ActivePhaseStart)
            {
                if (!player.Movement.TryCommitQteGroundContact(out Vector2 surface))
                { AbortPlunge(); return; }
                _plungeLanded = true;
                player.Movement.ClearCapturedWorldMotion();
                player.Movement.SnapFacingDirection(_preparedEntry.placement.landingFacing);
                SetPlungeActorHidden(false);
                _plungeVfx.SetContactPoint(surface);
            }
        }
        base.ProcessLogicFrame(frame, phase);
        SamplePlunge(frame);
    }

    public override void Update()
    {
        base.Update();
        if (fsm.CurrentState != this || !_plungeActive) return;
        float fraction = Time.timeScale > 0f && player.Combat?.HasActiveHitstop != true
            ? Mathf.Clamp01((Time.time - Time.fixedTime) * FrameDataConstants.LOGIC_FPS) : 0f;
        float frame = Mathf.Max(0f, currentFrame) + fraction;
        if (!_plungeLanded) frame = Mathf.Min(frame, _step.ActivePhaseStart - 0.001f);
        SamplePlunge(frame);
    }

    private void SamplePlunge(float frame)
    {
        if (!_plungeActive) return;
        // Render interpolation must not rewind/emit particles again when pause or hitstop begins.
        frame = Mathf.Max(_plungeVisualFrame, frame);
        _plungeVisualFrame = frame;
        _plungeVfx?.Sample(frame, _step.ActivePhaseStart, _step.TotalFrames);
    }

    private void CachePlungeRenderers()
    {
        if (_plungeRenderers == null)
        {
            _plungeRenderers = player.Animator.GetComponentsInChildren<SpriteRenderer>(true);
            _originalRenderFlags = new bool[_plungeRenderers.Length];
        }
        for (int i = 0; i < _plungeRenderers.Length; i++)
            if (_plungeRenderers[i] != null) _originalRenderFlags[i] = _plungeRenderers[i].forceRenderingOff;
    }

    private void SetPlungeActorHidden(bool hidden)
    {
        for (int i = 0; _plungeRenderers != null && i < _plungeRenderers.Length; i++)
            if (_plungeRenderers[i] != null) _plungeRenderers[i].forceRenderingOff = hidden || _originalRenderFlags[i];
    }

    private void ReleasePlunge(bool finishTail = false)
    {
        if (!_plungeActive) return;
        _plungeActive = false;
        SetPlungeActorHidden(false);
        if (finishTail && _plungeLanded) _plungeVfx?.FinishTail(_step.ActivePhaseStart, player.Combat);
        else _plungeVfx?.Stop();
        player.Movement?.ClearCapturedWorldMotion();
    }

    private void AbortPlunge()
    {
        ReleasePlunge();
        CommitAbortedActionOutput(false);
        ExitToAppropriateMovementState();
    }

    public void OnOwnerDisabled()
    {
        _plungeVfx?.Stop();
        if (fsm.CurrentState != this || !_plungeActive) return;
        ReleasePlunge();
        _plungeNeedsAbort = true;
    }

    protected override void OnActionComplete()
    {
        ReleasePlunge(finishTail: true);
        base.OnActionComplete();
    }

    public override bool HandleAttackInput()
    {
        if (!CanBeInterruptedBy(ActionCategory.BasicAttack)) return true;
        Buffer?.ConsumeAttack();
        if (_combo != null && !resolver.IsLastStep() && IsRecoveryOrCancelPhase)
        {
            ReleasePlunge(finishTail: true);
            // This explicitly authored intro combo remains one sequence even
            // if its dash briefly crosses the motor's ground-contact boundary.
            TryResolveNextStep(preserveAuthoredCombo: true);
            return true;
        }
        fsm.ChangeState(fsm.AttackState);
        fsm.AttackState.OnAttackInput();
        return true;
    }

    public override void OnAttackInput() => HandleAttackInput();

    public override void Exit()
    {
        ReleasePlunge();
        base.Exit();
        _action = null;
        _step = null;
        _target = null;
        _combo = null;
    }
}
