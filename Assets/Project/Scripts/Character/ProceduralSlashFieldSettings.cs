using System;
using UnityEngine;

[Serializable]
public sealed class ProceduralSlashFieldSettings
{
    [Header("Render")]
    public Material material;
    [Min(0.1f)] public float overallScale = 1f;
    public bool screenMaskMode;
    public bool fitViewport = true;
    [Range(0.5f, 2f)] public float viewportCoverage = 1.15f;
    [Min(0)] public int screenSpanningCount;
    [Header("Depth Layers")]
    [Range(0f, 1f)] public float foregroundFraction = 0.2f;
    public string normalSortingLayer = "WorldUI";
    public int normalSortingOrder = 999;
    public string foregroundSortingLayer = "WorldUI";
    public int foregroundSortingOrder = 12000;
    public Vector2 center = new Vector2(4f, 1.6f);
    public Vector2 spread = new Vector2(7f, 4f);
    [Min(1)] public int slashCount = 19;
    [Min(0)] public int seamCount = 12;
    [Min(0)] public int firstFrame = 14;
    [Min(1)] public int intervalFrames = 3;
    [Min(1)] public int endFrame = 90;
    public Vector2 lengthRange = new Vector2(7f, 15f);
    public Vector2 widthRange = new Vector2(0.08f, 0.48f);
    [Tooltip("Random ribbon rotation in degrees before mirroring for player facing. Equal values give a fixed angle.")]
    public Vector2 angleRange = new Vector2(0f, 180f);
    [Tooltip("Fullscreen transparency-cut rotation in degrees before mirroring for player facing.")]
    public float screenMaskAngle = 104f;
    public Vector2 revealFrames = new Vector2(4f, 9f);
    public Vector2 decayFrames = new Vector2(16f, 27f);
    [Min(1f)] public float pixelsPerUnit = 26.25f;
    [Tooltip("Zero chooses a new private seed per execution; nonzero supports repeatable preview.")]
    public int seed;
    [Header("Finishing Impact")]
    public CutImpactDistortionSettings impactDistortion = new CutImpactDistortionSettings();
    [Header("Actor")]
    public bool hideActor = true;
    [Min(0)] public int hideFromFrame = 18;
    [Min(0)] public int showAtFrame = 90;

    public void Rescale(float ratio)
    {
        firstFrame = Mathf.Max(0, Mathf.RoundToInt(firstFrame * ratio));
        intervalFrames = Mathf.Max(1, Mathf.RoundToInt(intervalFrames * ratio));
        endFrame = Mathf.Max(1, Mathf.RoundToInt(endFrame * ratio));
        hideFromFrame = Mathf.Max(0, Mathf.RoundToInt(hideFromFrame * ratio));
        showAtFrame = Mathf.Max(hideFromFrame, Mathf.RoundToInt(showAtFrame * ratio));
        revealFrames *= ratio; decayFrames *= ratio;
        impactDistortion?.Rescale(ratio);
    }
}
