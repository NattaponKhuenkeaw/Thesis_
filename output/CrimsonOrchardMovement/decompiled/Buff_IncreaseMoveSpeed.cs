using UnityEngine;

[CreateAssetMenu(fileName = "Buff_Effect_IncreaseMoveSpeed", menuName = "EntityStatus/Effect/IncreasMoveSpeed")]
public class Buff_IncreaseMoveSpeed : Modifier_Effect
{
	[SerializeField]
	private float increaseAmout = 10f;

	private float increasedSpeed;

	public override void Execute(Entity entity)
	{
		increasedSpeed = entity.Speed * increaseAmout / 100f;
		entity.Speed += increasedSpeed;
	}

	public override void RunOut(Entity entity)
	{
		entity.Speed -= increasedSpeed;
	}
}
