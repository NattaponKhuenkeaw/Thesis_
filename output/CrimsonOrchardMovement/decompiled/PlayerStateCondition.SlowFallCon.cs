namespace PlayerStateCondition;

public class SlowFallCon : StateCondition
{
	public override int priority { get; protected set; } = 3;

	public SlowFallCon(PlayerController controller)
		: base(controller)
	{
		playerController = controller;
	}

	public override bool TransitionCondition()
	{
		return playerController.currentSlowfallPressing >= playerController.minSlowfallPressing;
	}

	public override void Execute()
	{
		playerController.ChangeState(PlayerData.CharState.SlowFalling);
	}
}
