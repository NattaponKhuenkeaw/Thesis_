using System;
using UnityEngine;

[Serializable]
public class CombatMeterDefinition
{
    public string counterId;
    public string cachedCounterId;
    public string displayName;
    [Min(1)] public int pointsPerBox = 20;
    [Min(1)] public int baseBoxes = 2;
    [Min(1)] public int maximumBoxes = 5;
    public Color fillColor = new Color(0.25f, 0.9f, 1f);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(counterId);
}

[Serializable]
public class PassiveMeterCapacityModifier
{
    public string counterId;
    public int additionalBoxes;
}
