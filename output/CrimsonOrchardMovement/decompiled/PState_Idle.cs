using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "P_State_New", menuName = "State/PlayerState/Idle")]
public class PState_Idle : PlayerState
{
	[SerializeField]
	private float animDelayDura = 0.2f;

	[Header("Animation Loop Setting")]
	[FormerlySerializedAs("random_Offset")]
	[SerializeField]
	private Vector2 randomOffset;

	[SerializeField]
	private string spAnimName;

	[SerializeField]
	private string endSpAnimName;

	private bool isSpecialIdle;

	private Coroutine animDelayCo;

	private Coroutine mainStateCo;

	public override void StateStart(PlayerController manager)
	{
		isSpecialIdle = false;
		manager.playerStateConditionManager.AvailableStateSetUp(base.canTransitionToState);
		animDelayCo = manager.StartCoroutine(AnimDelay(manager));
		mainStateCo = manager.StartCoroutine(MainState(manager));
		manager.canFlip = true;
		manager.FlipCheck();
		manager.OnGroundReset();
		manager.playerData.rb.linearVelocityX = 0f;
	}

	public override void StateUpdate(PlayerController manager)
	{
	}

	public override void StateFixedUpdate(PlayerController manager)
	{
	}

	public override void StateExit(PlayerController manager)
	{
		manager.StopCoroutine(animDelayCo);
		manager.StopCoroutine(mainStateCo);
	}

	public override void OnEnable()
	{
		base.stateName = PlayerData.CharState.Idle;
	}

	private IEnumerator AnimDelay(PlayerController manager)
	{
		yield return new WaitForSeconds(animDelayDura);
		if (animName != null && animName != "")
		{
			manager.SetAnim(animName);
		}
	}

	private IEnumerator MainState(PlayerController manager)
	{
		while (true)
		{
			yield return new WaitForSeconds(Random.Range(randomOffset.x, randomOffset.y));
			if (isSpecialIdle)
			{
				isSpecialIdle = false;
				manager.SetAnim(endSpAnimName);
			}
			else
			{
				isSpecialIdle = true;
				manager.SetAnim(spAnimName);
			}
		}
	}
}
