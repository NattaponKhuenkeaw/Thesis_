using UnityEngine;

[CreateAssetMenu(fileName = "Explosion Splash", menuName = "Game/Combat/Explosion Splash")]
public sealed class ExplosionSplashDefinition : ScriptableObject
{
    [Tooltip("Optional presentation prefab; its animation receives the same center and radius as the hit query.")]
    public GameObject visualPrefab;
    [Header("Identity")]
    public string stableId;

    [Header("Area")]
    [Min(0f)] public float radius = 3f;
    [Range(1f, 5f), Tooltip("Vertical hit radius relative to horizontal radius. One preserves circular legacy areas.")]
    public float verticalRadiusMultiplier = 1f;
    public Vector2 centerOffset;
    public LayerMask targetLayers;
    [Min(0)] public int targetCap;
    public bool excludeOriginalTarget;
}

