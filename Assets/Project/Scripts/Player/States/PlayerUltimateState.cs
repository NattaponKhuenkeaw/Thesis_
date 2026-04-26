using UnityEngine;

/// <summary>
/// Frame-data-driven ultimate state with buffered follow-up support.
/// Inherits CombatPlayerState for shared resolution and interrupt logic.
/// Handles ultimate activation effects like flashes and time freezes.
/// </summary>
public class PlayerUltimateState : CombatPlayerState
{
    private UltimateAction activeUltimateAction;
    private bool isWaitingForActivation;

    public PlayerUltimateState(PlayerCharacter player, PlayerStateMachine fsm) : base(player, fsm) { }

    public override PlayerStateId StateId => PlayerStateId.Ultimate;
    public override PlayerStatePriority Priority => PlayerStatePriority.SkillUltimate;

    public override bool AcceptsMoveInput => true;
    public override bool AcceptsDashInput => true;
    public override bool AcceptsJumpInput => true;
    public override bool AcceptsAttackInput => true;
    public override bool AcceptsSkillInput => true;
    public override bool AcceptsUltimateInput => true;
    public override bool LocksMovement => true;
    public override bool LocksFacing => true;

    protected override AttackCategory GetAttackCategory() => AttackCategory.Ultimate;

    protected override InterruptPolicy GetInterruptPolicy() => InterruptPolicy.Ultimate;

    public override void Enter(TransitionContext context)
    {
        base.Enter(context);
        activeUltimateAction = null;

        if (UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.OnBlackScreenStarted += HandleBlackScreenStarted;
            UltimateFlashOverlay.Instance.OnSequenceEnded += HandleSequenceEnded;
        }
    }

    public override void Exit()
    {
        base.Exit();
        activeUltimateAction = null;

        if (UltimateFlashOverlay.Instance != null)
        {
            UltimateFlashOverlay.Instance.OnBlackScreenStarted -= HandleBlackScreenStarted;
            UltimateFlashOverlay.Instance.OnSequenceEnded -= HandleSequenceEnded;
        }

        isWaitingForActivation = false;
    }

    private void HandleBlackScreenStarted()
    {
        if (activeUltimateAction == null) return;

        Transform playerTransform = player.transform;
        Vector3 offset = activeUltimateAction.activationVFXOffset;

        // Flip X offset based on facing direction
        float facingDirection = playerTransform.localScale.x > 0 ? 1f : -1f;
        offset.x *= facingDirection;

        Vector3 spawnPosition = playerTransform.position + offset;

        // 1. Try Library-driven VFX (Optimized)
        if (activeUltimateAction.activationVFXType != Project.Combat.VFXType.None && Project.Combat.VFXManager.Instance != null)
        {
            Project.Combat.VFXManager.Instance.Spawn(activeUltimateAction.activationVFXType, spawnPosition, facingDirection < 0);
        }
        // 2. Fallback to Prefab (Legacy)
        else if (activeUltimateAction.activationVFXPrefab != null)
        {
            GameObject vfxInstance = Object.Instantiate(activeUltimateAction.activationVFXPrefab, spawnPosition, playerTransform.rotation);

            // FORCE UNSCALED TIME
            var pss = vfxInstance.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in pss)
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }

            var anims = vfxInstance.GetComponentsInChildren<Animator>();
            foreach (var a in anims)
            {
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        // Audio
        if (GameAudioManager.Instance != null)
        {
            GameAudioManager.Instance.PlayUltimateActivation(activeUltimateAction);
        }
    }

    private void HandleSequenceEnded()
    {
        if (!isWaitingForActivation) return;

        if (GameFreezeManager.Instance != null)
        {
            GameFreezeManager.Instance.RestoreTime();
        }

        isWaitingForActivation = false;
    }

    public override void Update()
    {
        if (isWaitingForActivation) return;
        base.Update();
    }

    protected override void HandleInputDuringRecovery()
    {
        if (TryConsumeAllowedBuffers()) return;
        if (TryAutoChain()) return;
    }

    protected override void HandleInputDuringCancel()
    {
        if (TryConsumeAllowedBuffers()) return;
        if (TryAutoChain()) return;
    }

    public override bool HandleUltimateInput()
    {
        OnUltimateInput();
        return true;
    }

    public override void OnUltimateInput()
    {
        Buffer?.ConsumeUltimate();

        if (player.Combat == null || resolverTable == null)
        {
            ExitToAppropriateMovementState();
            return;
        }

        if (currentPhase == ActionPhase.None)
        {
            InputContext ctx = BuildInputContext();
            AttackStepData step = resolver.ResolveInitial(ctx, resolverTable, AttackCategory.Ultimate);
            
            if (step == null)
            {
                ExitToAppropriateMovementState();
                return;
            }

            if (resolver.ActiveCombatAction is SkillAction skillAction
                && player.Combat.IsOnCooldown(AttackCategory.Ultimate, skillAction.cooldown, skillAction.actionName))
            {
                fsm.Log("Ultimate", "Ultimate is on cooldown", Color.yellow);
                resolver.Reset();
                ExitToAppropriateMovementState();
                return;
            }

            activeUltimateAction = resolver.ActiveCombatAction as UltimateAction;
            TryStartCooldown();
            ExecuteCombatStep(step, ctx, false);
            // --------------------------

            if (activeUltimateAction != null && activeUltimateAction.useDashAfterImage && player.Movement != null)

                // Burst Visuals
                if (activeUltimateAction != null && activeUltimateAction.useDashAfterImage && player.Movement != null)
            {
                player.Movement.SpawnUltimateBurst(3, 0.08f);
            }

            isWaitingForActivation = true;

            // Activation Effects
            if (GameFreezeManager.Instance != null)
            {
                GameFreezeManager.Instance.UltimateFreezeManual(); 
            }
            if (UltimateFlashOverlay.Instance != null)
            {
                UltimateFlashOverlay.Instance.PlayActivationFlash();
            }

            return; 
        }

        if (currentPhase == ActionPhase.Cancel && TryResolveNextUltimateStep())
        {
            return;
        }
    }

    private bool TryResolveNextUltimateStep(bool isAutoChain = false)
    {
        bool success = TryResolveNextStep(isAutoChain);
        
        if (success && activeUltimateAction != null && activeUltimateAction.useDashAfterImage && player.Movement != null)
        {
            player.Movement.SpawnUltimateBurst(3, 0.08f);
        }

        return success;
    }
}
