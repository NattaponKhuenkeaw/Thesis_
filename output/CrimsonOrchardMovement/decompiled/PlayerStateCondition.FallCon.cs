namespace PlayerStateCondition;

public class FallCon : StateCondition
{
	public override int priority { get; protected set; } = 2;

	public FallCon(PlayerController controller)
		: base(controller)
	{
		playerController = controller;
	}

	public override bool TransitionCondition()
	{
		if (playerController.currentState is PState_SlowFalling)
		{
			return playerController.currentSlowfallPressing == 0f;
		}
		return !playerController.isGround;
	}

	public override void Execute()
	{
		playerController.ChangeState(PlayerData.CharState.Falling);
	}
}
