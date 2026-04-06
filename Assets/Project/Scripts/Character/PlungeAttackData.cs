using UnityEngine;

[System.Serializable]
public class PlungeAttackData
{
    [Header("Settings")]
    public bool isEnabled = true;
    public float downwardSpeed = 18f;
    public string fallAnimationStateName = "Plunge_Fall";
    public string impactAnimationStateName = "Attack_1";

    [Header("Impact Step")]
    public AttackStepData impactStep = new AttackStepData
    {
        stepName = "Plunge Impact"
    };
}