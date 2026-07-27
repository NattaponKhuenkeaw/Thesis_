/// <summary>
/// Interface for combat systems that interact with PlayerMovement.
/// Allows PlayerMovement to query combat state without depending on a concrete class.
/// </summary>
public interface ICharacterCombat
{
    // =========================================================
    // Movement Queries (used by PlayerMovement)
    // =========================================================
    bool IsAttacking();
    bool IsActiveCombat();
    bool CanMoveAfterAttack();
    bool CanSwitchDirection();
    bool ShouldSuppressLandingFeedback();

    // =========================================================
    // Combat Engine (used by PlayerAttackState)
    // =========================================================
    void ProcessFrameFromState(int frame, ActionPhase phase);
    void OnRecoveryPhaseFromState();

    // Step execution
    void ExecuteStep(AttackStepData step, int comboDepth, bool isAirCombo, AttackCategory category, string resolvedActionName = null, string resolvedActionId = null, bool isAutoChain = false);

    // Step queries
    AttackStepData GetCurrentStepData();
    int GetComboStep();
    bool IsExecutingAutoChainStep { get; }
    bool HasActiveHitstop { get; }
    bool HasGrabbedEnemy();

    // Combo management
    void CancelCurrentAttackButKeepComboProgress();
    void ResetCombo();

    // Air Combo Cooldown (Refactored from PlayerMovement)
    bool CanStartAirCombo();
    void TriggerAirComboCooldown();
    void ResetAirComboCooldown();

    void SetAirCombo(bool airCombo);

    // Events
    event System.Action OnComboFinished;
    event System.Action OnGrabConnected;
}
