using UnityEngine;

[CreateAssetMenu(fileName = "P_Passive_New", menuName = "AbilitiesUpgrade/Jump/TrippleJump")]
public class Jump_Upgrade_TrippleJump : Skill_Data
{
	public override void OnSubscribe(PlayerData playerData)
	{
		playerData.MaxAirjump++;
	}

	public override void OnUnSubscribe(PlayerData playerData)
	{
		playerData.MaxAirjump--;
	}
}
