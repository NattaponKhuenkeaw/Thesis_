using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerState : ScriptableObject
{
	[Header("Generic State Modifier")]
	public bool canRepeat;

	[SerializeField]
	protected string animName;

	[SerializeField]
	protected float animExitTime;

	[SerializeField]
	protected bool canCancleOtherAnimation;

	[field: SerializeField]
	public PlayerData.CharState stateName { get; protected set; }

	[field: SerializeField]
	public List<PlayerData.CharState> canTransitionToState { get; protected set; }

	public virtual void StateStart(PlayerController manager)
	{
		manager.playerStateConditionManager.AvailableStateSetUp(canTransitionToState);
		if (animName != null && animName != "")
		{
			manager.SetAnim(animName, animExitTime, canCancleOtherAnimation);
		}
	}

	public abstract void StateUpdate(PlayerController manager);

	public abstract void StateFixedUpdate(PlayerController manager);

	public abstract void StateExit(PlayerController manager);

	public abstract void OnEnable();
}
