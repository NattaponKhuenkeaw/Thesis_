using System;
using System.Collections.Generic;
using UnityEngine;

public static class CameraAnimationConversion
{
    public static CameraAnimationFrameData Preview(ActionEventEntry source)
    {
        if (source?.eventType == ActionEventType.ScreenImpulse) return FromEnvelope(source.screenImpulseSettings?.cameraControl);
        if (source?.eventType == ActionEventType.ActionCameraMotion) return FromLegacyMotion(source.cameraMotionSettings);
        throw new ArgumentException("Select a legacy camera event to convert.");
    }

    // The editor records Undo before applying this explicit, in-memory conversion.
    public static ActionEventEntry Apply(ActionEventFrameData owner, ActionEventEntry source, CameraAnimationFrameData sequence)
    {
        int index = owner?.entries?.IndexOf(source) ?? -1;
        if (source == null || index < 0) throw new ArgumentException("Source camera event is not in this action.");
        string error = sequence?.GetValidationError();
        if (sequence == null || error != null) throw new ArgumentException(error ?? "Sequence is missing.");
        if (source.eventType == ActionEventType.ScreenImpulse && source.screenImpulseSettings?.cameraControl?.enabled == true)
        {
            var result = new ActionEventEntry { eventType = ActionEventType.CameraAnimationControl, label = source.label + " Camera",
                triggerFrame = source.triggerFrame, trackColor = source.trackColor, cameraAnimation = sequence };
            owner.entries.Insert(index + 1, result);
            source.screenImpulseSettings.cameraControl.enabled = false;
            return result;
        }
        if (source.eventType != ActionEventType.ActionCameraMotion) throw new ArgumentException("Source has no legacy camera control to convert.");
        source.cameraAnimation = sequence;
        source.eventType = ActionEventType.CameraAnimationControl;
        return source;
    }

    public static CameraAnimationFrameData FromEnvelope(ScreenImpulseCameraSettings settings)
    {
        if (settings == null || !settings.enabled) throw new ArgumentException("Camera control is disabled.");
        var data = new CameraAnimationFrameData();
        int transition = Mathf.Max(0, settings.transitionInFrames);
        int holdEnd = transition + Mathf.Max(0, settings.holdFrames);
        int restore = Mathf.Max(0, settings.returnFrames);
        data.segments.Add(new CameraAnimationSegment { label = "Zoom", task = CameraAnimationTask.Zoom,
            endFrame = transition, zoomMultiplier = settings.zoomMultiplier });
        if (holdEnd > transition) data.segments.Add(new CameraAnimationSegment { label = "Peak Zoom Hold",
            task = CameraAnimationTask.Hold, channels = CameraAnimationChannels.Zoom, startFrame = transition, endFrame = holdEnd });
        bool position = settings.jumpCutLock || settings.horizontalViewportOffset != 0f;
        if (position)
        {
            if (settings.jumpCutLock && transition > 0)
                data.segments.Add(new CameraAnimationSegment { label = "Jump Cut Lock", task = CameraAnimationTask.Move });
            data.segments.Add(new CameraAnimationSegment { label = "Camera Offset", task = CameraAnimationTask.Move,
                endFrame = transition, positionSpace = CameraAnimationPositionSpace.ScreenOffsetFromStart,
                position = new Vector2(settings.horizontalViewportOffset * settings.zoomMultiplier, 0f), facingRelativeX = true });
            if (holdEnd > transition) data.segments.Add(new CameraAnimationSegment { label = "Position Hold",
                task = CameraAnimationTask.Hold, channels = CameraAnimationChannels.Position,
                startFrame = transition, endFrame = holdEnd });
        }
        data.segments.Add(new CameraAnimationSegment { label = "Return To Normal", task = CameraAnimationTask.Return,
            channels = position ? CameraAnimationChannels.Both : CameraAnimationChannels.Zoom,
            startFrame = restore == 0 ? holdEnd + 1 : holdEnd, endFrame = restore == 0 ? holdEnd + 1 : holdEnd + restore });
        return data;
    }

    public static CameraAnimationFrameData FromLegacyMotion(ActionCameraMotionSettings settings)
    {
        if (settings?.zoomControl != null) return FromEnvelope(settings.zoomControl);
        var data = new CameraAnimationFrameData();
        var keys = new List<ActionCameraMotionSettings.Key>();
        if (settings?.keys != null)
            foreach (var key in settings.keys) if (key != null) keys.Add(key);
        keys.Sort((a, b) => a.frame.CompareTo(b.frame));
        if (keys.Count == 0)
        {
            data.segments.Add(new CameraAnimationSegment { label = "Normal Zoom", task = CameraAnimationTask.Zoom });
            return data;
        }
        for (int i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            int start = i == 0 ? key.frame : keys[i - 1].frame;
            if (i > 0 && key.frame == start) throw new ArgumentException("Legacy keys share a frame. Resolve duplicate keys before conversion.");
            float size = Mathf.Clamp(key.sizeMultiplier, 0.8f, 1f);
            data.segments.Add(new CameraAnimationSegment { label = $"Zoom Key {i + 1}", task = CameraAnimationTask.Zoom,
                startFrame = start, endFrame = key.frame, zoomMultiplier = size });
            data.segments.Add(new CameraAnimationSegment { label = $"Position Key {i + 1}", task = CameraAnimationTask.Move,
                startFrame = start, endFrame = key.frame, positionSpace = CameraAnimationPositionSpace.ScreenOffsetFromStart,
                position = new Vector2(Mathf.Clamp(key.horizontalViewportOffset, -1f, 1f) * size, 0f), facingRelativeX = true });
        }
        int last = keys[keys.Count - 1].frame;
        int holdEnd = last + Mathf.Max(0, settings.holdFrames);
        if (holdEnd > last) data.segments.Add(new CameraAnimationSegment { label = "Hold Final Pose", task = CameraAnimationTask.Hold,
            channels = CameraAnimationChannels.Both, startFrame = last, endFrame = holdEnd });
        int restore = Mathf.Max(0, settings.returnFrames);
        data.segments.Add(new CameraAnimationSegment { label = "Return To Normal", task = CameraAnimationTask.Return,
            startFrame = restore == 0 ? holdEnd + 1 : holdEnd, endFrame = restore == 0 ? holdEnd + 1 : holdEnd + restore });
        return data;
    }
}
