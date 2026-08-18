using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Artifact Passive Set", menuName = "Game/Artifacts/Passive Set")]
public sealed class ArtifactPassiveSetDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable content identity. Do not change after release.")]
    public string stableId;

    [Header("Rules")]
    public List<ArtifactPassiveRuleDefinition> rules = new List<ArtifactPassiveRuleDefinition>();
}

