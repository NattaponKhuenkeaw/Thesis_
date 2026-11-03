using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "P_State_New", menuName = "State/PlayerState/Jump")]
public class PState_Jump : PlayerState
{
	[SerializeField]
	protected AudioClip jumpAudio;

	[SerializeField]
	protected float stateDuration = 0.5f;

	[Header("JumpCut")]
	[SerializeField]
	private float decreaseRate = 5f;

	[SerializeField]
	private bool canJumpCut;

	[Header("Cancel Animation Customizer")]
	[FormerlySerializedAs("c_StartFrame")]
	[SerializeField]
	protected float cancleStartFrame = 0.15f;

	[FormerlySerializedAs("c_Duration")]
	[SerializeField]
	protected float cancleDuration = 0.3f;

	protected Coroutine fallDelayCo;

	protected Coroutine jumpCutCou;

	public override void StateStart(PlayerController manager)
	{
		if (animName != null && animName != "")
		{
			manager.SetAnim(animName, 0f, canCancle: true);
		}
		manager.canFlip = true;
		manager.FlipCheck();
		Singleton<AudioManager>.Instance.PlaySound(PlaySoundRequest.SE(jumpAudio).WithVolume(0.8f));
		manager.playerData.rb.linearVelocityY = 0f;
		manager.playerData.rb.AddForce(Vector2.up * manager.playerData.JumpForce, ForceMode2D.Impulse);
		manager.CancelTimer(cancleStartFrame, cancleDuration);
		manager.DisableChangeState(stateDuration);
		manager.playerStateConditionManager.AvailableStateSetUp(base.canTransitionToState);
	}

	public override void StateUpdate(PlayerController manager)
	{
		if (manager.jumpCutCon && canJumpCut)
		{
			jumpCutCou = manager.StartCoroutine(manager.DecreaseYVelocity(decreaseRate));
		}
		if (manager.isGround && manager.canChangeState)
		{
			manager.SetAnim("Falling_OnGround", 0.2f);
		}
		if (manager.wallSlideCon)
		{
			manager.ChangeState(PlayerData.CharState.WallSlide);
		}
	}

	public override void StateFixedUpdate(PlayerController manager)
	{
		manager.Move();
	}

	public override void StateExit(PlayerController manager)
	{
		if (jumpCutCou != null)
		{
			manager.StopCoroutine(jumpCutCou);
		}
	}

	public override void OnEnable()
	{
		base.stateName = PlayerData.CharState.Jump;
	}
}
