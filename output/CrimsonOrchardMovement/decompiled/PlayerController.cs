using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PlayerStateCondition;
using UnityEngine;

[RequireComponent(typeof(PlayerData))]
public class PlayerController : MonoBehaviour
{
	[SerializeField]
	private MovementInputReaderSo movementInputReader;

	public PlayerData playerData;

	public float currentSkillCharge;

	private Collider2D currentGroundCollider;

	[HideInInspector]
	public int dashCounter = 1;

	[HideInInspector]
	public int downAttackCounter;

	[HideInInspector]
	public int maxAirDownAttackCounter;

	[SerializeField]
	private Animator animator;

	[HideInInspector]
	public bool canFlip;

	public Coroutine cancelTimerCo;

	public Coroutine iFrametimerCo;

	public Coroutine dashCoolDownCo;

	public Coroutine animationCouran;

	public Coroutine animationWaitCo;

	public Coroutine disChangeStateCo;

	private bool canChangeAnim;

	[field: SerializeField]
	public PlayerState currentState { get; private set; }

	public PlayerStateConditionManager playerStateConditionManager { get; private set; }

	public Dictionary<PlayerData.CharState, PlayerState> stateDict { get; private set; }

	[field: SerializeField]
	public bool canChangeState { get; private set; }

	public float playerMoveInput { get; private set; }

	public float currentAtkCharge { get; private set; }

	public int upperAttackCounter { get; private set; }

	public bool isCoyoteTime { get; private set; }

	public bool isGround { get; private set; }

	public float LastOnGroundTime { get; private set; }

	public SurfaceType CurrentGroundSurface { get; private set; } = SurfaceType.Leaf;

	public int airJumpCounter { get; private set; }

	public int currentAirAtkPattern { get; private set; }

	public bool canDash { get; private set; }

	public Animator Animator
	{
		get
		{
			return animator;
		}
		set
		{
			animator = value;
		}
	}

	public GameObject benathPlatform { get; private set; }

	public float currentSlowfallPressing { get; private set; }

	public float minSlowfallPressing { get; private set; } = 0.25f;

	public bool jumpCutCon { get; private set; }

	public bool wallSlideCon { get; private set; }

	public bool wallJumpCon { get; private set; }

	public Vector2 playerFacing { get; private set; } = new Vector2(1f, 0f);

	public int playerVertiLook { get; private set; }

	public bool canCancle { get; private set; }

	public MovementInputReaderSo GetMovementInputReader()
	{
		return movementInputReader;
	}

	public void Init()
	{
		SetUpVariable();
		GetStateSO();
		StateSetup();
	}

	private void SetUpVariable()
	{
		playerData = GetComponent<PlayerData>();
		playerStateConditionManager = GetComponent<PlayerStateConditionManager>();
		playerStateConditionManager.Init(this);
		canDash = true;
		playerFacing = new Vector2(1f, 0f);
		currentAirAtkPattern = 0;
		canChangeAnim = true;
		canChangeState = true;
		canCancle = false;
		dashCounter = playerData.MaxDashCounter;
	}

	private void GetStateSO()
	{
		stateDict = new Dictionary<PlayerData.CharState, PlayerState>();
		foreach (PlayerState item in Resources.LoadAll<PlayerState>("StateScriptableObj/PlayerState").ToList())
		{
			stateDict.Add(item.stateName, item);
		}
	}

	private void StateSetup()
	{
		OnGroundReset();
		currentState = FindState(PlayerData.CharState.Idle);
		currentState.StateStart(this);
	}

	private void Update()
	{
		GetMovementInputReader().UpdateInput();
		LastOnGroundTime -= Time.deltaTime;
		if (!playerData.isDeath)
		{
			MoveInput();
			GroudCheck();
			PlayerVertiLookCheck();
			SlowFallPressCheck();
			SkillHolding();
			currentState.StateUpdate(this);
			playerStateConditionManager.UpdateCondition();
		}
	}

	private void FixedUpdate()
	{
		currentState.StateFixedUpdate(this);
	}

	public void ChangeState(PlayerData.CharState stateName)
	{
		if (stateName != currentState.stateName || currentState.canRepeat)
		{
			OnStateEndReset();
			currentState = FindState(stateName);
			Singleton<GameManager>.Instance.PlayerEventManager.OnPlayerChangeStateEvent.Invoke(currentState);
			currentState.StateStart(this);
		}
	}

	public void ChangeState(PlayerState state)
	{
		if (state.stateName != currentState.stateName || state.stateName == PlayerData.CharState.UseSkill || currentState.canRepeat)
		{
			OnStateEndReset();
			currentState = state;
			Singleton<GameManager>.Instance.PlayerEventManager.OnPlayerChangeStateEvent.Invoke(state);
			currentState.StateStart(this);
		}
	}

	private void OnStateEndReset()
	{
		canChangeState = false;
		currentState.StateExit(this);
		if (iFrametimerCo != null)
		{
			setIframe(can: false);
			StopCoroutine(iFrametimerCo);
		}
		if (cancelTimerCo != null)
		{
			canCancle = false;
			StopCoroutine(cancelTimerCo);
		}
		if (disChangeStateCo != null)
		{
			StopCoroutine(disChangeStateCo);
		}
		canFlip = true;
		FlipCheck();
		canChangeState = true;
	}

	public PlayerState FindState(PlayerData.CharState stateName)
	{
		if (stateDict.ContainsKey(stateName))
		{
			return stateDict[stateName];
		}
		return null;
	}

	public void MoveInput()
	{
		playerMoveInput = movementInputReader.MoveInput.x;
		FlipCheck();
	}

	private void SlowFallPressCheck()
	{
		if (movementInputReader.JumpRelease || isGround)
		{
			currentSlowfallPressing = 0f;
		}
		if (movementInputReader.JumpHold && !isCoyoteTime)
		{
			currentSlowfallPressing += Time.deltaTime;
		}
	}

	public bool ChargeAttackHolding()
	{
		if (movementInputReader.AttackHold)
		{
			if (!isGround)
			{
				ResetAttackHold();
			}
			currentAtkCharge += Time.deltaTime;
			if (currentAtkCharge >= playerData.minAtkCharge)
			{
				return false;
			}
		}
		if (movementInputReader.AttackRelease && currentAtkCharge <= playerData.minAtkCharge)
		{
			return true;
		}
		if (!movementInputReader.AttackHold && currentAtkCharge > 0f)
		{
			ResetAttackHold();
		}
		return false;
	}

	public bool SkillHolding()
	{
		if (movementInputReader.UseSkillHold)
		{
			currentSkillCharge += Time.deltaTime;
			if (currentSkillCharge >= playerData.minAtkCharge)
			{
				return false;
			}
		}
		if (movementInputReader.UseSkillRelease && currentSkillCharge <= playerData.minAtkCharge)
		{
			return true;
		}
		if (!movementInputReader.UseSkillHold && currentSkillCharge > 0f)
		{
			ResetSkillHold();
		}
		return false;
	}

	private void PlayerVertiLookCheck()
	{
		if (movementInputReader.CrouchHold)
		{
			playerVertiLook = -1;
		}
		else if (movementInputReader.LookUpHold)
		{
			playerVertiLook = 1;
		}
		else
		{
			playerVertiLook = 0;
		}
	}

	public void FlipCheck()
	{
		if (playerMoveInput > 0.1f)
		{
			if (canFlip)
			{
				base.transform.localScale = new Vector3(1f, 1f, 1f);
				playerFacing = new Vector2(1f, 0f);
			}
		}
		else if (playerMoveInput < -0.1f && canFlip)
		{
			base.transform.localScale = new Vector3(-1f, 1f, 1f);
			playerFacing = new Vector2(-1f, 0f);
		}
	}

	public void Move(float multiplier = 1f)
	{
		float num = playerMoveInput * playerData.Speed;
		float num2 = ((!(LastOnGroundTime > 0f)) ? ((Mathf.Abs(num) > 0.01f) ? (playerData.runAccelAmount * playerData.accelInAir) : (playerData.runDeccelAmount * playerData.deccelInAir)) : ((Mathf.Abs(num) > 0.01f) ? playerData.runAccelAmount : playerData.runDeccelAmount));
		if (playerData.doConserveMomentum && Mathf.Abs(playerData.rb.linearVelocity.x) > Mathf.Abs(num) && Mathf.Sign(playerData.rb.linearVelocity.x) == Mathf.Sign(num) && Mathf.Abs(num) > 0.01f && LastOnGroundTime < 0f)
		{
			num2 = 0f;
		}
		float num3 = (num - playerData.rb.linearVelocity.x) * num2;
		playerData.rb.AddForce(num3 * multiplier * Vector2.right, ForceMode2D.Force);
	}

	public void SetAnim(string animName, float exitTime = 0f, bool canCancle = false)
	{
		if (canCancle)
		{
			Animator.Play(animName, 0, 0f);
			if (animationWaitCo != null)
			{
				StopCoroutine(animationWaitCo);
			}
			canChangeAnim = true;
			if (animationCouran != null)
			{
				StopCoroutine(animationCouran);
			}
			animationCouran = StartCoroutine(AnimationTime());
		}
		else if (canChangeAnim)
		{
			if (animationCouran != null)
			{
				StopCoroutine(animationCouran);
			}
			animationCouran = StartCoroutine(AnimationTime());
		}
		else
		{
			if (animationWaitCo != null)
			{
				StopCoroutine(animationWaitCo);
			}
			animationWaitCo = StartCoroutine(WaitForAnimation(animName));
		}
		IEnumerator AnimationTime()
		{
			Animator.Play(animName, 0, 0f);
			canChangeAnim = false;
			yield return new WaitForSeconds(exitTime);
			canChangeAnim = true;
		}
		IEnumerator WaitForAnimation(string animName_)
		{
			yield return new WaitUntil(() => canChangeAnim);
			Animator.Play(animName_, 0, 0f);
		}
	}

	public void AirAttack()
	{
		switch (currentAirAtkPattern)
		{
		case 0:
			ChangeState(PlayerData.CharState.AirAttack1);
			break;
		case 1:
			ChangeState(PlayerData.CharState.AirAttack2);
			break;
		case 2:
			ChangeState(PlayerData.CharState.AirPlunge);
			break;
		default:
			Debug.Log("falling");
			break;
		}
		currentAirAtkPattern++;
	}

	public void AirJumpCounterReduce()
	{
		airJumpCounter--;
	}

	public void UpperAttackCounterReduce()
	{
		upperAttackCounter--;
	}

	public void setIframe(bool can)
	{
		SetSuperArmor(can);
		playerData.playerHealth.canDamaged = !can;
	}

	public void SetSuperArmor(bool can)
	{
		playerData.playerHealth.canKnockback = !can;
	}

	public void ResetAttackHold()
	{
		currentAtkCharge = 0f;
	}

	public void ResetSkillHold()
	{
		currentSkillCharge = 0f;
	}

	public void OnGroundReset()
	{
		ResetAirJump();
		ResetAirAttackCounter();
		ResetSpecialAttackCounter();
	}

	public void ResetAirJump()
	{
		airJumpCounter = playerData.MaxAirjump;
	}

	public void ResetAirAttackCounter()
	{
		currentAirAtkPattern = 0;
	}

	public void ResetSpecialAttackCounter()
	{
		upperAttackCounter = 1;
		downAttackCounter = maxAirDownAttackCounter;
	}

	public void ForceResetVelocity()
	{
		playerData.rb.linearVelocity = Vector2.zero;
		playerData.rb.gravityScale = playerData.baseGravityScale;
	}

	public IEnumerator DecreaseYVelocity(float decreaseRate = 100f)
	{
		while (playerData.rb.linearVelocityY != 0f)
		{
			playerData.rb.linearVelocityY = Mathf.MoveTowards(playerData.rb.linearVelocityY, 0f, decreaseRate * Time.deltaTime);
			yield return null;
		}
	}

	public IEnumerator DecreaseXVelocity(float decreaseRate = 100f)
	{
		while (playerData.rb.linearVelocityX != 0f)
		{
			playerData.rb.linearVelocityX = Mathf.MoveTowards(playerData.rb.linearVelocityX, 0f, decreaseRate * Time.deltaTime);
			yield return null;
		}
	}

	public void ForceFlip()
	{
		Debug.Log("Flip");
		if (playerFacing == new Vector2(-1f, 0f))
		{
			base.transform.localScale = new Vector3(1f, 1f, 1f);
			playerFacing = new Vector2(1f, 0f);
		}
		else
		{
			base.transform.localScale = new Vector3(-1f, 1f, 1f);
			playerFacing = new Vector2(-1f, 0f);
		}
	}

	private void GroudCheck()
	{
		Collider2D collider2D = Physics2D.OverlapBox((Vector2)base.transform.position + new Vector2(0f, playerData.boxOffsetY), playerData.boxSize, 0f, playerData.groundLayer);
		if ((bool)collider2D)
		{
			UpdateGroundSurface(collider2D);
			isCoyoteTime = true;
			if (!isGround)
			{
				isGround = true;
				Singleton<GameManager>.Instance.PlayerEventManager.OnGroundStateChangedEvent.Invoke(isGround);
			}
			playerData.rb.sharedMaterial = playerData.groundMat;
			LastOnGroundTime = 0.1f;
		}
		else if (!collider2D)
		{
			currentGroundCollider = null;
			CurrentGroundSurface = SurfaceType.Leaf;
			if (isGround)
			{
				isGround = false;
				Singleton<GameManager>.Instance.PlayerEventManager.OnGroundStateChangedEvent.Invoke(isGround);
			}
			playerData.rb.sharedMaterial = playerData.airMat;
			if (LastOnGroundTime < 0f - playerData.coyoteTime)
			{
				isCoyoteTime = false;
			}
		}
		if (!(collider2D == null) && collider2D.CompareTag("Platform"))
		{
			benathPlatform = collider2D.gameObject;
		}
	}

	private void UpdateGroundSurface(Collider2D groundCollider)
	{
		if (!(currentGroundCollider == groundCollider))
		{
			currentGroundCollider = groundCollider;
			GroundSurface componentInParent = groundCollider.GetComponentInParent<GroundSurface>();
			CurrentGroundSurface = ((componentInParent != null) ? componentInParent.SurfaceType : SurfaceType.Leaf);
		}
	}

	public void DashCoolDown()
	{
		if (dashCoolDownCo != null)
		{
			StopCoroutine(dashCoolDownCo);
		}
		canDash = false;
		dashCoolDownCo = StartCoroutine(CoolDown());
		IEnumerator CoolDown()
		{
			yield return new WaitUntil(() => isGround);
			yield return new WaitForSeconds(0.5f);
			canDash = true;
			dashCounter = playerData.MaxDashCounter;
		}
	}

	public void DisableChangeState(float stateDuration)
	{
		disChangeStateCo = StartCoroutine(Timer());
		IEnumerator Timer()
		{
			canChangeState = false;
			yield return new WaitForSeconds(stateDuration);
			canChangeState = true;
		}
	}

	public void CancelTimer(float startFrame, float duration)
	{
		cancelTimerCo = StartCoroutine(Timer());
		IEnumerator Timer()
		{
			yield return new WaitForSeconds(startFrame);
			canCancle = true;
			yield return new WaitForSeconds(duration);
			canCancle = false;
		}
	}

	public void IframeTimer(float startFrame, float duration, bool isIframe)
	{
		iFrametimerCo = StartCoroutine(Timer());
		IEnumerator Timer()
		{
			yield return new WaitForSeconds(startFrame);
			if (isIframe)
			{
				setIframe(can: true);
			}
			else
			{
				SetSuperArmor(can: true);
			}
			yield return new WaitForSeconds(duration);
			setIframe(can: false);
		}
	}
}
