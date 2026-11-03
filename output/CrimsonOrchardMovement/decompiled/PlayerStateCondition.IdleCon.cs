using Unity.Mathematics;

namespace PlayerStateCondition;

public class IdleCon : StateCondition
{
	public override int priority { get; protected set; }

	public IdleCon(PlayerController controller)
		: base(controller)
	{
		playerController = controller;
	}

	public override bool TransitionCondition()
	{
		if (playerController.currentState is PState_Falling)
		{
			return playerController.isGround;
		}
		if (playerController.currentState is PState_Crouch)
		{
			return !playerController.GetMovementInputReader().CrouchHold;
		}
		if (playerController.isGround)
		{
			return math.abs(playerController.playerMoveInput) <= 0f;
		}
		return false;
	}

	public override void Execute()
	{
		playerController.ChangeState(PlayerData.CharState.Idle);
	}
}
