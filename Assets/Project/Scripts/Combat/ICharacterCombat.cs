/// <summary>
/// Interface for combat systems that interact with PlayerMovement.
/// Allows PlayerMovement to query combat state without depending on a concrete class.
/// </summary>
public interface ICharacterCombat
{
    bool IsAttacking();
    bool CanMoveAfterAttack();
    bool CanSwitchDirection();
    bool ShouldSuppressLandingFeedback();
    bool ShouldLockAirComboInPlace();
    
    // State-driven control hooks
    void SetDrivenByState(bool driven);
    void ProcessFrameFromState(int frame, ActionPhase phase);
    void OnRecoveryPhaseFromState();
    
    // Combo management
    AttackStepData GetCurrentStepData();
    bool TryChainCombo();
    bool ShouldPreserveAirComboOnComplete();
    void CancelCurrentAttackButKeepComboProgress();
    void ResetCombo();
    bool IsLastComboStep();
    int GetComboStep();
    bool ShouldInterruptAttackOnGrounded();
    bool OnAttackInput();
    
    // Events
    event System.Action OnComboFinished;
    event System.Action<AttackStepData> OnBranchToSubStep;
    
    bool IsExecutingSubStep();
}
