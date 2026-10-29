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
        [Tooltip("Fraction of screen width, relative to facing. Room boundaries limit movement.")]
        [Range(-1f, 1f)] public float horizontalViewportOffset;
    }

    public List<Key> keys = new List<Key>();
    public ScreenImpulseCameraSettings zoomControl;
    [Min(0)] public int holdFrames;
    [Min(0)] public int returnFrames;

    public ScreenImpulseCameraSettings CreateSingleZoomSettings()
    {
        if (zoomControl != null) return zoomControl;
        Key peak = null;
        foreach (Key key in keys ?? new List<Key>())
            if (key != null && (peak == null || key.sizeMultiplier < peak.sizeMultiplier)) peak = key;
        return new ScreenImpulseCameraSettings
        {
            enabled = true,
            zoomMultiplier = peak != null ? peak.sizeMultiplier : 1f,
            horizontalViewportOffset = peak != null ? peak.horizontalViewportOffset : 0f,
            transitionInFrames = peak != null ? Mathf.Max(0, peak.frame) : 0,
            holdFrames = Mathf.Max(0, holdFrames),
            returnFrames = Mathf.Max(0, returnFrames)
        };
    }

    public int DurationFrames
    {
        get
        {
            if (zoomControl != null) return zoomControl.DurationFrames;
            int last = 0;
            for (int i = 0; keys != null && i < keys.Count; i++)
                if (keys[i] != null) last = Mathf.Max(last, keys[i].frame);
            return last + Mathf.Max(0, holdFrames) + Mathf.Max(0, returnFrames) + 1;
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
        if (before == null)
        {
            sizeMultiplier = 1f;
            viewportOffset = 0f;
            return;
        }
        after ??= before;
        float t = before.frame == after.frame ? 0f : Mathf.InverseLerp(before.frame, after.frame, frame);
        t = Mathf.SmoothStep(0f, 1f, t);
        sizeMultiplier = Mathf.Clamp(Mathf.Lerp(before.sizeMultiplier, after.sizeMultiplier, t), 0.8f, 1f);
        viewportOffset = Mathf.Clamp(Mathf.Lerp(before.horizontalViewportOffset, after.horizontalViewportOffset, t), -1f, 1f);
        float holdEnd = before.frame + Mathf.Max(0, holdFrames);
        if (after == before && frame > holdEnd)
        {
            float weight = returnFrames > 0
                ? 1f - Mathf.SmoothStep(0f, 1f, (frame - holdEnd) / returnFrames) : 0f;
            sizeMultiplier = Mathf.Lerp(1f, sizeMultiplier, weight);
            viewportOffset *= weight;
        }
    }

    public void Rescale(float ratio)
    {
        zoomControl?.Rescale(ratio);
        holdFrames = Mathf.Max(0, Mathf.RoundToInt(holdFrames * ratio));
        returnFrames = Mathf.Max(0, Mathf.RoundToInt(returnFrames * ratio));
        for (int i = 0; keys != null && i < keys.Count; i++)
            if (keys[i] != null) keys[i].frame = Mathf.Max(0, Mathf.RoundToInt(keys[i].frame * ratio));
    }
}
