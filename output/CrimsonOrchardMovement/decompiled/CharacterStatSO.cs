using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStatSO", menuName = "CharacterData/CharacterStatSO")]
public class CharacterStatSO : ScriptableObject
{
	public float MaxHealthUpgrade;

	public float ATK;

	public float Speed;

	public int PotionAmount;
}
