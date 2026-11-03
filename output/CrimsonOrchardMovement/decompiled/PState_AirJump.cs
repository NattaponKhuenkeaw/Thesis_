using UnityEngine;

[CreateAssetMenu(fileName = "P_State_New", menuName = "State/PlayerState/AirJump")]
public class PState_AirJump : PState_Jump
{
	[SerializeField]
	private float airJumpReducer = 0.9f;

	public override void StateStart(PlayerController manager)
	{
		manager.AirJumpCounterReduce();
		manager.ResetAirAttackCounter();
		if (animName != null && animName != "")
		{
			manager.SetAnim(animName);
		}
		manager.playerData.rb.gravityScale = manager.playerData.baseGravityScale;
		Singleton<AudioManager>.Instance.PlaySound(PlaySoundRequest.SE(jumpAudio).WithVolume(0.8f));
		manager.playerData.rb.linearVelocityY = 0f;
		manager.playerData.rb.AddForce(Vector2.up * manager.playerData.JumpForce * airJumpReducer, ForceMode2D.Impulse);
		manager.CancelTimer(cancleStartFrame, cancleDuration);
		manager.DisableChangeState(stateDuration);
		manager.playerStateConditionManager.AvailableStateSetUp(base.canTransitionToState);
	}

	public override void StateUpdate(PlayerController manager)
	{
		if (manager.isGround && manager.canChangeState)
		{
			manager.SetAnim("Falling_OnGround", 0.2f);
		}
	}

	public override void StateFixedUpdate(PlayerController manager)
	{
		base.StateFixedUpdate(manager);
	}

	public override void StateExit(PlayerController manager)
	{
		base.StateExit(manager);
	}

	public override void OnEnable()
	{
		base.stateName = PlayerData.CharState.AirJump;
	}
}
