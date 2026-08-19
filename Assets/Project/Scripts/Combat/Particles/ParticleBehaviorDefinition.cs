using System;
using System.Collections.Generic;
using UnityEngine;

public enum ParticleAnchorPolicy
{
    WorldPosition = 0,
    FollowWearer = 1,
    FollowTarget = 2
}

[Serializable]
public sealed class ParticleVolumeDefinition
{
    [Min(0f)] public float radius = 3f;
    public LayerMask targetLayers;
}

[CreateAssetMenu(fileName = "Particle Behavior", menuName = "Game/Combat/Particle Behavior")]
public sealed class ParticleBehaviorDefinition : ScriptableObject
{
    [Header("Identity")]
    public string stableId;

    [Header("Presentation")]
    public GameObject runtimePrefab;

    [Header("Gameplay")]
    public ParticleAnchorPolicy anchorPolicy;
    public ParticleVolumeDefinition volume = new ParticleVolumeDefinition();
    public ParticleCompatibilityPolicy compatibility = new ParticleCompatibilityPolicy();
    [Min(0f)] public float lifetimeSeconds = 1f;
    [Min(0.05f)] public float pulseIntervalSeconds = 1f;
    [SerializeReference] public List<ParticleEffectDefinition> effects = new List<ParticleEffectDefinition>();
}

