namespace PlayerStateCondition;

public class AirJumpCon : StateCondition
{
	public override int priority { get; protected set; } = 3;

	public AirJumpCon(PlayerController controller)
		: base(controller)
	{
		playerController = controller;
	}

	public override bool TransitionCondition()
	{
		if (playerController.GetMovementInputReader().JumpPressed && playerController.airJumpCounter > 0)
		{
			return !playerController.isGround;
		}
		return false;
	}

	public override void Execute()
	{
		playerController.ChangeState(PlayerData.CharState.AirJump);
	}
}
