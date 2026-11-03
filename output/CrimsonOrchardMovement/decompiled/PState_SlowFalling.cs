using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "P_State_New", menuName = "State/PlayerState/SlowFalling")]
public class PState_SlowFalling : PlayerState
{
	[SerializeField]
	protected AudioClip slowFallingAudio;

	[SerializeField]
	private float delay = 0.5f;

	[SerializeField]
	private float slowedGravityScale = 0.5f;

	[SerializeField]
	private float slowMoveMulti = 0.3f;

	private bool canChangeState;

	private float baseDecel;

	private Rigidbody2D rb;

	private Coroutine delayCou;

	public override void StateStart(PlayerController manager)
	{
		base.StateStart(manager);
		rb = manager.playerData.rb;
		canChangeState = false;
		Singleton<AudioManager>.Instance.PlaySound(PlaySoundRequest.SE(slowFallingAudio).WithVolume(0.5f));
		manager.ForceResetVelocity();
		rb.gravityScale = 0f;
		baseDecel = manager.playerData.deccelInAir;
		manager.playerData.deccelInAir = 0.3f;
		delayCou = manager.StartCoroutine(Delay());
		IEnumerator Delay()
		{
			yield return new WaitForSeconds(delay);
			rb.gravityScale = slowedGravityScale;
			canChangeState = true;
		}
	}

	public override void StateUpdate(PlayerController manager)
	{
		_ = canChangeState;
	}

	public override void StateFixedUpdate(PlayerController manager)
	{
		manager.Move(slowMoveMulti);
	}

	public override void StateExit(PlayerController manager)
	{
		if (delayCou != null)
		{
			manager.StopCoroutine(delayCou);
		}
		rb.gravityScale = manager.playerData.baseGravityScale;
		manager.playerData.deccelInAir = baseDecel;
	}

	public override void OnEnable()
	{
		base.stateName = PlayerData.CharState.SlowFalling;
	}
}
