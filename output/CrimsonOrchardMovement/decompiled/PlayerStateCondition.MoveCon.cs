using Unity.Mathematics;

namespace PlayerStateCondition;

public class MoveCon : StateCondition
{
	public override int priority { get; protected set; } = 1;

	public MoveCon(PlayerController controller)
		: base(controller)
	{
		playerController = controller;
	}

	public override bool TransitionCondition()
	{
		if (math.abs(playerController.playerMoveInput) > 0f)
		{
			return playerController.isGround;
		}
		return false;
	}

	public override void Execute()
	{
		playerController.ChangeState(PlayerData.CharState.Move);
	}
}
