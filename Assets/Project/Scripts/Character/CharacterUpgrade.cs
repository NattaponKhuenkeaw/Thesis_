using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class CharacterUpgrade
{
    // =========================================================
    // Upgrade Info
    // =========================================================
    [Header("Upgrade Info")]
    public int level = 1;
    public int maxLevel = 30;
    public int currentExp = 0;
    public int expToNextLevel = 100;
    public int constellationRank = 0;

    [FormerlySerializedAs("skillTreePoints")]
    public int progressionPoints = 3;

    [Header("Level Stat Growth")]
    public float attackPerLevel = 0f;
    public float defensePerLevel = 0f;
    public float maxHpPerLevel = 0f;

    public int CurrentLevel => Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    public string LevelLabel => $"Lv.{CurrentLevel}/{MaxLevel}";

    public CharacterStats ApplyLevelGrowth(CharacterStats baseStats)
    {
        CharacterStats leveledStats = baseStats != null ? baseStats.Clone() : new CharacterStats();
        int levelsGained = Mathf.Max(0, CurrentLevel - 1);

        leveledStats.attack += attackPerLevel * levelsGained;
        leveledStats.defense += defensePerLevel * levelsGained;
        leveledStats.maxHP += maxHpPerLevel * levelsGained;

        return leveledStats;
    }
}
