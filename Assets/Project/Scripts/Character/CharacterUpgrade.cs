using UnityEngine;

[System.Serializable]
public class CharacterUpgrade
{
    [Header("Level Info")]
    [Tooltip("LEGACY: Individual runtime level is retained for serialized migration. PlayerRunInventory.GlobalLevel is authoritative.")]
    public int level = 1;
    public int maxLevel = 30;
    [Tooltip("LEGACY: Run Global Level does not use EXP.")]
    public int currentExp = 0;

    [Header("Level Stat Growth")]
    public float attackPerLevel = 0f;
    public float defensePerLevel = 0f;
    public float maxHpPerLevel = 0f;

    // --- Constants ---
    private const float EXP_BASE = 100f;
    private const float EXP_EXPONENT = 1.5f;

    public int CurrentLevel => Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
    public int MaxLevel => Mathf.Max(1, maxLevel);
    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    public string LevelLabel => $"Lv.{CurrentLevel}/{MaxLevel}";

    public CharacterUpgrade Clone()
    {
        return new CharacterUpgrade
        {
            level = level,
            maxLevel = maxLevel,
            currentExp = currentExp,
            attackPerLevel = attackPerLevel,
            defensePerLevel = defensePerLevel,
            maxHpPerLevel = maxHpPerLevel
        };
    }

    /// <summary>EXP needed to advance from given level to level+1.</summary>
    public static int GetRequiredEXP(int forLevel)
    {
        return Mathf.RoundToInt(EXP_BASE * Mathf.Pow(forLevel, EXP_EXPONENT));
    }

    /// <summary>EXP needed to advance from current level.</summary>
    public int ExpToNextLevel => IsMaxLevel ? 0 : GetRequiredEXP(CurrentLevel);

    /// <summary>
    /// Simulates adding EXP without mutating state.
    /// Returns (resultLevel, resultCurrentExp).
    /// </summary>
    public (int resultLevel, int resultExp) SimulateAddExp(int expToAdd)
    {
        int simLevel = CurrentLevel;
        int simExp = currentExp + expToAdd;

        while (simLevel < MaxLevel)
        {
            int required = GetRequiredEXP(simLevel);
            if (simExp < required) break;
            simExp -= required;
            simLevel++;
        }

        if (simLevel >= MaxLevel)
            simExp = 0; // cap

        return (simLevel, simExp);
    }

    /// <summary>
    /// Commits EXP gain — mutates level and currentExp.
    /// Returns number of levels gained.
    /// </summary>
    public int AddExp(int expToAdd)
    {
        if (IsMaxLevel) return 0;
        int startLevel = CurrentLevel;
        var (newLevel, newExp) = SimulateAddExp(expToAdd);
        level = newLevel;
        currentExp = newExp;
        return newLevel - startLevel;
    }

    /// <summary>Calculate stats at a specific level (for preview).</summary>
    public CharacterStats CalculateStatsAtLevel(CharacterStats baseStats, int atLevel)
    {
        CharacterStats result = baseStats != null ? baseStats.Clone() : new CharacterStats();
        int growthLevels = Mathf.Clamp(atLevel, 1, MaxLevel) - 1;
        result.attack += attackPerLevel * growthLevels;
        result.defense += defensePerLevel * growthLevels;
        result.maxHP += maxHpPerLevel * growthLevels;
        return result;
    }

    public CharacterStats ApplyLevelGrowth(CharacterStats baseStats)
    {
        return CalculateStatsAtLevel(baseStats, CurrentLevel);
    }
}
