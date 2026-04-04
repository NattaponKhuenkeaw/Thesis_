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
}
