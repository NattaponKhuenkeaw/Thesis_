using System;
using UnityEngine;

[Serializable]
public sealed class LightningPlungeVfxSettings
{
    [Header("Shape")]
    [Min(0.1f)] public float scale = 1f;
    [Tooltip("Ground burst, cracks, wind and particles only; keeps the falling bolt narrow.")]
    [Min(0.1f)] public float impactScale = 2.5f;
    [Min(0.01f)] public float coreWidth = 0.08f;
    [Min(0.01f)] public float envelopeWidth = 0.3f;
    [Min(0.1f)] public float groundSpread = 3.3f;
    [Min(0.1f)] public float impactHeight = 2.5f;
    [Range(4, 24)] public int branches = 11;
    [Header("Thunderstrike")]
    [Tooltip("World-space overscan above the visible camera top.")]
    [Min(0f)] public float aboveScreenPadding = 2f;
    [Min(1f)] public float minimumBoltLength = 14f;
    [Tooltip("Absolute side deflection, independent of bolt length.")]
    [Min(0f)] public float boltJitter = 0.12f;
    [Min(1f)] public float glowWidth = 3.8f;
    [Header("Color")]
    [ColorUsage(true, true)] public Color coreColor = new Color(1.7f, 1.25f, 2.4f, 1f);
    public Color violetColor = new Color(0.32f, 0.018f, 0.7f, 0.88f);
    public Color blueColor = new Color(0.26f, 0.06f, 0.65f, 0.6f);
    public Color shadowColor = new Color(0.035f, 0.008f, 0.085f, 0.95f);
    public Color auraColor = new Color(0.11f, 0.006f, 0.25f, 0.42f);
    [ColorUsage(true, true)] public Color glowColor = new Color(0.45f, 0.045f, 1.4f, 0.22f);
    [Header("Local URP Light (starting values)")]
    [Min(0f)] public float impactLightIntensity = 0.8f;
    [Min(0.1f)] public float impactLightRadius = 7f;
    [Header("Tail (action logic ticks, starting values)")]
    [Tooltip("Hold the impact silhouette before its fade begins; does not extend damage or change recovery.")]
    [Min(0)] public int impactHoldFrames = 18;
    [Min(1)] public int columnFadeFrames = 54;
    [Min(1)] public int lightFadeFrames = 36;
    [Min(1)] public int flashFrames = 10;
    [Min(1)] public int burstFrames = 90;
    [Min(1)] public int windFrames = 108;
    [Min(1)] public int residualFrames = 118;
    [Header("Exit motion (tunable starting values)")]
    [Tooltip("Additional readable hold before the storm flows away from the contact point.")]
    [Min(0)] public int exitDelayFrames = 12;
    [Min(1)] public int exitFrames = 78;
    [Tooltip("Additional outward travel of the storm front relative to its impact radius.")]
    [Min(0f)] public float exitScatter = 0.65f;
    [Tooltip("Upward drift of the clearing explosion lobes, before impact scale.")]
    [Min(0f)] public float stormLift = 0.6f;

    [Header("Ground shockwave")]
    [Tooltip("Optional expanding ground rings and wide ground-glow pool. Independent of the lightning blast and electrical accents.")]
    public bool groundShockwaveEnabled = true;

    [Header("Purple electrical accents (starting values)")]
    [Range(0, 12)] public int fallingArcCount = 6;
    [Range(0, 16)] public int impactArcCount = 9;
    [Min(0.1f)] public float electricalSpread = 1f;
    [Min(0.1f)] public float electricalArcLength = 0.8f;
    [Min(0.005f)] public float electricalWidth = 0.028f;
    [Min(0f)] public float electricalGlow = 1.2f;
    [ColorUsage(true, true)] public Color electricalColor = new Color(0.95f, 0.28f, 2.4f, 1f);
    [Range(0, 24)] public int fallingSparkCount = 8;

    [Header("Particles")]
    [Range(0, 128)] public int sparkCount = 36;
    [Range(0, 64)] public int debrisCount = 18;
    [Range(0, 64)] public int residualCount = 15;
    [Header("Sorting")]
    public string sortingLayer = "WorldUI";
    public int sortingOrder = 995;
    [Tooltip("Zero selects a private seed at activation. A nonzero value is repeatable.")]
    public int seed;
    [Header("Refraction")]
    public CutImpactDistortionSettings impactDistortion = new CutImpactDistortionSettings
    {
        enabled = false, durationFrames = 12, waveWidth = 0.04f,
        refractionPixels = 4f, chromaticPixels = 0f, blurPixels = 0f
    };
    [Tooltip("Suppress the camera Volume stack's chromatic aberration while this bolt is visible, without editing its profile.")]
    public bool suppressChromaticAberration = true;
}
