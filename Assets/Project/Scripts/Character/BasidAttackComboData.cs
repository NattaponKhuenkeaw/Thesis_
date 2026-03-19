using UnityEngine;

[System.Serializable]
public class BasicAttackComboData
{
    // =========================================================
    // Combo Settings
    // =========================================================
    [Header("Combo Settings")]
    public float comboResetDelay = 0.8f;

    // =========================================================
    // Attack Steps
    // =========================================================
    [Header("Attack Steps")]
    public AttackStepData[] steps;

    // =========================================================
    // Public Methods
    // =========================================================
    public AttackStepData GetStep(int comboStep)
    {
        int index = comboStep - 1;
        if (steps == null || index < 0 || index >= steps.Length)
            return null;

        return steps[index];
    }

    public int GetMaxCombo()
    {
        return steps != null ? steps.Length : 0;
    }
}