using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateCondition;

public class PlayerStateConditionManager : MonoBehaviour
{
	private PlayerController playerController;

	private StateCondition currentState;

	[SerializeField]
	private PlayerData.CharState currentStateID;

	[SerializeField]
	private string debugString;

	private Dictionary<PlayerData.CharState, StateCondition> stateConditionsDict;

	private Dictionary<PlayerData.CharState, StateCondition> cancleAblestate_ConditionsDict;

	private Dictionary<PlayerData.CharState, StateCondition> otherInput_ConditionsDict;

	private List<PlayerData.CharState> availableState;

	public void Init(PlayerController playerController)
	{
		this.playerController = playerController;
		stateConditionsDict = new Dictionary<PlayerData.CharState, StateCondition>
		{
			{
				PlayerData.CharState.Idle,
				new IdleCon(playerController)
			},
			{
				PlayerData.CharState.Jump,
				new JumpCon(playerController)
			},
			{
				PlayerData.CharState.AirJump,
				new AirJumpCon(playerController)
			},
			{
				PlayerData.CharState.Falling,
				new FallCon(playerController)
			},
			{
				PlayerData.CharState.InstantDrop,
				new InstantDropCon(playerController)
			},
			{
				PlayerData.CharState.SlowFalling,
				new SlowFallCon(playerController)
			},
			{
				PlayerData.CharState.Move,
				new MoveCon(playerController)
			},
			{
				PlayerData.CharState.Dash,
				new DashCon(playerController)
			},
			{
				PlayerData.CharState.Crouch,
				new CrouchCon(playerController)
			},
			{
				PlayerData.CharState.BasicAttack1,
				new BasicAttackCon(playerController)
			},
			{
				PlayerData.CharState.BasicAttack2,
				new BasicAttackCon(playerController)
			},
			{
				PlayerData.CharState.BasicAttack3,
				new BasicAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirAttack1,
				new AirAttackCon(playerController)
			},
			{
				PlayerData.CharState.DownAttack,
				new DownAttackCon(playerController)
			},
			{
				PlayerData.CharState.UpperAttack,
				new UpperAttackCon(playerController)
			},
			{
				PlayerData.CharState.ChargeAttack,
				new ChargeAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirPlunge,
				new PlungeAttackCon(playerController)
			},
			{
				PlayerData.CharState.DashAttack,
				new DashAttackCon(playerController)
			},
			{
				PlayerData.CharState.DrinkPotion,
				new UsePotionCon(playerController)
			},
			{
				PlayerData.CharState.UseSkill,
				new UseSKill(playerController)
			},
			{
				PlayerData.CharState.VeinStackConsuming,
				new StackBurstCon(playerController)
			}
		};
		cancleAblestate_ConditionsDict = new Dictionary<PlayerData.CharState, StateCondition>
		{
			{
				PlayerData.CharState.Jump,
				new JumpCon(playerController)
			},
			{
				PlayerData.CharState.AirJump,
				new AirJumpCon(playerController)
			},
			{
				PlayerData.CharState.Dash,
				new DashCon(playerController)
			},
			{
				PlayerData.CharState.UseSkill,
				new UseSKill(playerController)
			},
			{
				PlayerData.CharState.UpperAttack,
				new UpperAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirAttack1,
				new AirAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirPlunge,
				new PlungeAttackCon(playerController)
			},
			{
				PlayerData.CharState.InstantDrop,
				new InstantDropCon(playerController)
			},
			{
				PlayerData.CharState.DashAttack,
				new DashAttackCon(playerController)
			},
			{
				PlayerData.CharState.DownAttack,
				new DownAttackCon(playerController)
			}
		};
		otherInput_ConditionsDict = new Dictionary<PlayerData.CharState, StateCondition>
		{
			{
				PlayerData.CharState.BasicAttack2,
				new ChainAttackCon(playerController)
			},
			{
				PlayerData.CharState.BasicAttack3,
				new ChainAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirAttack1,
				new ChainAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirAttack2,
				new ChainAttackCon(playerController)
			},
			{
				PlayerData.CharState.AirPlunge,
				new AirAttackCon(playerController)
			}
		};
	}

	public void AvailableStateSetUp(List<PlayerData.CharState> canTransitionTo)
	{
		availableState = new List<PlayerData.CharState>();
		availableState = canTransitionTo;
	}

	public void UpdateCondition()
	{
		if (availableState != null)
		{
			StateCheck(otherInput_ConditionsDict);
			if (playerController.canCancle)
			{
				StateCheck(cancleAblestate_ConditionsDict);
			}
			if (playerController.canChangeState)
			{
				StateCheck(stateConditionsDict);
			}
			debugString = ((currentState != null) ? currentState.GetType().Name : "No State");
			if (currentState != null)
			{
				ExecuteAction();
			}
		}
	}

	private void StateCheck(Dictionary<PlayerData.CharState, StateCondition> conditionDict)
	{
		foreach (PlayerData.CharState item in availableState)
		{
			if (conditionDict.ContainsKey(item) && conditionDict[item].TransitionCondition())
			{
				OnConditionFullfil(conditionDict[item], item);
			}
		}
	}

	private void OnConditionFullfil(StateCondition state, PlayerData.CharState stateID)
	{
		currentStateID = stateID;
		if (CheckPriotity(state))
		{
			currentState = state;
		}
	}

	private bool CheckPriotity(StateCondition newState)
	{
		if (currentState == null)
		{
			return true;
		}
		return newState.priority >= currentState.priority;
	}

	private void ExecuteAction()
	{
		currentState.Execute();
		currentState = null;
		currentStateID = PlayerData.CharState.Idle;
	}
}
