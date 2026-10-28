using System;
using UnityEngine;

public enum QteEntryMode { Grounded, LightningPlunge }

[Serializable]
public sealed class QtePlungeSettings
{
    [Tooltip("Offset from the captured enemy. X follows the outgoing approach direction.")]
    public Vector2 targetStartOffset = new Vector2(1f, 5f);
    [Header("Camera (tunable starting values)")]
    [Tooltip("World-space height above Main's body root. Independent of camera zoom.")]
    public float cameraActorOffsetY = 1.5f;
    [Tooltip("Scaled seconds to blend from the last QTE view to the current gameplay camera after contact or early exit.")]
    [Min(0.05f)] public float cameraReturnSeconds = 0.45f;
    public LightningPlungeVFX prefab;
    public LightningPlungeVfxSettings visuals = new LightningPlungeVfxSettings();
}
