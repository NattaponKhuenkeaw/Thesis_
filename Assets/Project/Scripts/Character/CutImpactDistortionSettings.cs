using System;
using UnityEngine;

/// <summary>Optional refraction along the rendered cut. Pixel strengths use a reference screen height.</summary>
[Serializable]
public sealed class CutImpactDistortionSettings
{
    public bool enabled;
    [Min(0)] public int startFrame;
    [Min(1)] public int durationFrames = 18;
    [Tooltip("Distortion band around the visible cut, as a fraction of viewport height.")]
    [Min(0.01f)] public float waveWidth = 0.08f;
    [Range(0f, 64f)] public float refractionPixels = 6f;
    [Range(0f, 8f)] public float chromaticPixels;
    [Range(0f, 4f)] public float blurPixels = 1f;
    [Min(1)] public int referenceHeight = 270;

    public void Rescale(float ratio)
    {
        startFrame = Mathf.Max(0, Mathf.RoundToInt(startFrame * ratio));
        durationFrames = Mathf.Max(1, Mathf.RoundToInt(durationFrames * ratio));
    }
}
