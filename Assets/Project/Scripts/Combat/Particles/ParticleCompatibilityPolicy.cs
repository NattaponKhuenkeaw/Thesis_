using System;

[Serializable]
public sealed class ParticleCompatibilityPolicy
{
    public bool affectEnemies = true;
    public bool affectWearer;
    public bool affectAllies;
    public bool applyOnEnter;
    public bool applyOnPulse = true;
    public bool removeEffectsOnExit = true;
    public bool restrictToAnchorEnemy;
    [UnityEngine.Min(0)] public int targetCap;
}

