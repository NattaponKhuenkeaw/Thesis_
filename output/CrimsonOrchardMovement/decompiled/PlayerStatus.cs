using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class PlayerStatus
{
	private float maxHealthUpgradePercent;

	private float currentHealthPercent = 100f;

	private float atkPercent = 100f;

	private float speedPercent = 100f;

	private int potionAmount = 6;

	private int money;

	private List<string> unlockedSkillIdsPercent = new List<string>();

	public float MaxHealthUpgradePercent => maxHealthUpgradePercent;

	public float CurrentHealthPercent => currentHealthPercent;

	public float ATKPercent => atkPercent;

	public float SpeedPercent => speedPercent;

	public int PotionAmount => potionAmount;

	public int Money => money;

	public List<string> UnlockSkillIdsPercent => unlockedSkillIdsPercent;

	public void CaptureFrom(PlayerData player)
	{
		maxHealthUpgradePercent = player.maxHealthUpgradePercentage;
		currentHealthPercent = 100f * (player.playerHealth.health / player.currentMaxHealth);
		atkPercent = 100f * (player.Atk / player.CurCharacter.ATK);
		speedPercent = 100f * (player.Speed / player.CurCharacter.Speed);
		potionAmount = player.playerInventory.PotionAmout;
		money = player.playerInventory.Money;
		unlockedSkillIdsPercent = (from s in player.playerPassiveSkillData.skillList
			where s != null
			select s.skillName).ToList();
	}
}
