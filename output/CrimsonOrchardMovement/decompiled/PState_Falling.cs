using UnityEngine;

[CreateAssetMenu(fileName = "P_State_New", menuName = "State/PlayerState/Falling")]
public class PState_Falling : PlayerState
{
	[SerializeField]
	protected float baseGravityScale = 1f;

	[SerializeField]
	protected float maxGravityScale = 5f;

	[SerializeField]
	protected AnimationCurve gravityCurve;

	[SerializeField]
	protected float gravityDuration = 1f;

	protected float gravityTimer;

	protected Rigidbody2D rb;

	public override void StateStart(PlayerController manager)
	{
		base.StateStart(manager);
		manager.canFlip = true;
		manager.FlipCheck();
		gravityTimer = 0f;
		rb = manager.playerData.rb;
		baseGravityScale = manager.playerData.baseGravityScale;
	}

	public override void StateUpdate(PlayerController manager)
	{
		if (manager.isGround)
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
		gravityTimer += Time.fixedDeltaTime;
		float time = gravityTimer / gravityDuration;
		float value = gravityCurve.Evaluate(time) * maxGravityScale;
		rb.gravityScale = Mathf.Clamp(value, baseGravityScale, maxGravityScale);
	}

	public override void StateExit(PlayerController manager)
	{
		rb.gravityScale = baseGravityScale;
	}

	public override void OnEnable()
	{
		base.stateName = PlayerData.CharState.Falling;
	}
}
