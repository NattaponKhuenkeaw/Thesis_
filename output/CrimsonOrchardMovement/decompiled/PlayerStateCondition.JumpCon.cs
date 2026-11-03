namespace PlayerStateCondition;

public class JumpCon : StateCondition
{
	public override int priority { get; protected set; } = 4;

	public JumpCon(PlayerController controller)
		: base(controller)
	{
		playerController = controller;
	}

	public override bool TransitionCondition()
	{
		if (playerController.GetMovementInputReader().JumpPressed)
		{
			if (!playerController.isGround)
			{
				return playerController.isCoyoteTime;
			}
			return true;
		}
		return false;
	}

	public override void Execute()
	{
		playerController.ChangeState(PlayerData.CharState.Jump);
	}
}
