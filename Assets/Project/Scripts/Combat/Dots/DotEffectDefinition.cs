using UnityEngine;

public enum DotStackingPolicy
{
    StrongestRefresh = 0,
    Refresh = 1,
    IndependentBySource = 2,
    CappedStacks = 3
}

[CreateAssetMenu(fileName = "DOT Effect", menuName = "Game/Combat/DOT Effect")]
public sealed class DotEffectDefinition : ScriptableObject
{
    [Header("Identity")]
    public string stableId;
    public string displayName;

    [Header("Damage")]
    public ElementType elementType = ElementType.None;
    public DamageCriticalPolicy criticalPolicy = DamageCriticalPolicy.Prevent;
    [Min(0f)] public float durationSeconds = 3f;
    [Min(0.05f)] public float tickIntervalSeconds = 1f;

    [Header("Stacking")]
    public DotStackingPolicy stackingPolicy = DotStackingPolicy.IndependentBySource;
    [Min(1)] public int maximumStacks = 1;
}

