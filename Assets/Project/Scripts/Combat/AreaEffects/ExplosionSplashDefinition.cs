using UnityEngine;

[CreateAssetMenu(fileName = "Explosion Splash", menuName = "Game/Combat/Explosion Splash")]
public sealed class ExplosionSplashDefinition : ScriptableObject
{
    [Header("Identity")]
    public string stableId;

    [Header("Area")]
    [Min(0f)] public float radius = 3f;
    public LayerMask targetLayers;
    [Min(0)] public int targetCap;
    public bool excludeOriginalTarget;
}

