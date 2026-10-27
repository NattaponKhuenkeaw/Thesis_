using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ActionCameraMotionSettings
{
    [Serializable]
    public sealed class Key
    {
        [Min(0)] public int frame;
        [Range(0.8f, 1f)] public float sizeMultiplier = 1f;
        [Range(-0.03f, 0.03f)] public float horizontalViewportOffset;
    }

    public List<Key> keys = new List<Key>();

    public int DurationFrames
    {
        get
        {
            int last = 0;
            for (int i = 0; keys != null && i < keys.Count; i++)
                if (keys[i] != null) last = Mathf.Max(last, keys[i].frame);
            return last + 1;
        }
    }

    public void Sample(float frame, out float sizeMultiplier, out float viewportOffset)
    {
        Key before = null;
        Key after = null;
        for (int i = 0; keys != null && i < keys.Count; i++)
        {
            Key key = keys[i];
            if (key == null) continue;
            if (key.frame <= frame && (before == null || key.frame >= before.frame)) before = key;
            if (key.frame >= frame && (after == null || key.frame < after.frame)) after = key;
        }
        before ??= after;
        after ??= before;
        if (before == null)
        {
            sizeMultiplier = 1f;
            viewportOffset = 0f;
            return;
        }
        float t = before.frame == after.frame ? 0f : Mathf.InverseLerp(before.frame, after.frame, frame);
        t = Mathf.SmoothStep(0f, 1f, t);
        sizeMultiplier = Mathf.Clamp(Mathf.Lerp(before.sizeMultiplier, after.sizeMultiplier, t), 0.8f, 1f);
        viewportOffset = Mathf.Clamp(Mathf.Lerp(before.horizontalViewportOffset, after.horizontalViewportOffset, t), -0.03f, 0.03f);
    }

    public void Rescale(float ratio)
    {
        for (int i = 0; keys != null && i < keys.Count; i++)
            if (keys[i] != null) keys[i].frame = Mathf.Max(0, Mathf.RoundToInt(keys[i].frame * ratio));
    }
}
