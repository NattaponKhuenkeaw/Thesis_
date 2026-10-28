using System;
using UnityEngine;

public enum QteEntryMode { Grounded, LightningPlunge }

[Serializable]
public sealed class QtePlungeSettings
{
    [Tooltip("Offset from the captured enemy. X follows the outgoing approach direction.")]
    public Vector2 targetStartOffset = new Vector2(1f, 5f);
    public LightningPlungeVFX prefab;
    public LightningPlungeVfxSettings visuals = new LightningPlungeVfxSettings();
}
