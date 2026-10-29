using System;
using System.Collections.Generic;
using UnityEngine;

public enum CameraAnimationTask { Zoom, Move, Hold, Return, [InspectorName("Zoom + Move")] ZoomAndMove }
[Flags] public enum CameraAnimationChannels { Zoom = 1, Position = 2, Both = 3 }
public enum CameraAnimationPositionSpace
{
    OffsetFromStart, WorldPoint, ScreenOffsetFromStart,
    [InspectorName("Player + Offset (Live)")] Player
}
public enum CameraAnimationEasing { Linear, SmoothStep, EaseIn, EaseOut }

[Serializable]
public sealed class CameraAnimationSegment
{
    public string label = "Camera Segment";
    [Min(0)] public int startFrame;
    [Min(0)] public int endFrame;
    public CameraAnimationTask task;
    public CameraAnimationChannels channels = CameraAnimationChannels.Both;
    public CameraAnimationEasing easing = CameraAnimationEasing.SmoothStep;
    [Min(0.01f)] public float zoomMultiplier = 1f;
    public CameraAnimationPositionSpace positionSpace;
    public Vector2 position;
    public bool facingRelativeX;

    public CameraAnimationChannels AffectedChannels => task == CameraAnimationTask.ZoomAndMove ? CameraAnimationChannels.Both
        : task == CameraAnimationTask.Zoom ? CameraAnimationChannels.Zoom : task == CameraAnimationTask.Move ? CameraAnimationChannels.Position : channels;

    public CameraAnimationSegment Copy() => (CameraAnimationSegment)MemberwiseClone();

    public float Progress(float frame)
    {
        float t = endFrame == startFrame ? 1f : Mathf.Clamp01((frame - startFrame) / (endFrame - startFrame));
        switch (easing)
        {
            case CameraAnimationEasing.SmoothStep: return Mathf.SmoothStep(0f, 1f, t);
            case CameraAnimationEasing.EaseIn: return t * t;
            case CameraAnimationEasing.EaseOut: return 1f - (1f - t) * (1f - t);
            default: return t;
        }
    }
}

[Serializable]
public sealed class CameraAnimationFrameData
{
    [Tooltip("Off: camera playback ignores nonzero slow motion after this event fires. Pause and full hitstop still stop it.")]
    public bool followSlowMotion = true;
    public List<CameraAnimationSegment> segments = new List<CameraAnimationSegment>();

    public int DurationFrames
    {
        get
        {
            int last = 0;
            if (segments != null)
                foreach (var segment in segments)
                    if (segment != null) last = Mathf.Max(last, segment.endFrame);
            return last + 1;
        }
    }

    public void Rescale(float ratio)
    {
        if (segments == null) return;
        foreach (var segment in segments)
        {
            if (segment == null) continue;
            segment.startFrame = Mathf.Max(0, Mathf.RoundToInt(segment.startFrame * ratio));
            segment.endFrame = Mathf.Max(segment.startFrame, Mathf.RoundToInt(segment.endFrame * ratio));
        }
    }

    public string GetValidationError()
    {
        if (segments == null || segments.Count == 0) return "Add a camera segment.";
        for (int i = 0; i < segments.Count; i++)
        {
            var a = segments[i];
            if (a == null) return $"Segment {i + 1} is missing.";
            if (a.startFrame < 0 || a.endFrame < a.startFrame) return $"Segment {i + 1}: end frame must be at or after start frame.";
            if (!Enum.IsDefined(typeof(CameraAnimationTask), a.task)
                || !Enum.IsDefined(typeof(CameraAnimationEasing), a.easing)
                || !Enum.IsDefined(typeof(CameraAnimationPositionSpace), a.positionSpace)
                || a.AffectedChannels == 0 || (a.AffectedChannels & ~CameraAnimationChannels.Both) != 0)
                return $"Segment {i + 1}: invalid task, channels, space or easing.";
            if (!IsFinite(a.zoomMultiplier) || a.zoomMultiplier <= 0f || !IsFinite(a.position.x) || !IsFinite(a.position.y))
                return $"Segment {i + 1}: zoom must be positive and all targets must be finite.";
            for (int j = 0; j < i; j++)
            {
                var b = segments[j];
                if ((a.AffectedChannels & b.AffectedChannels) == 0) continue;
                bool overlap = a.startFrame < b.endFrame && b.startFrame < a.endFrame;
                bool duplicateCut = a.startFrame == a.endFrame && b.startFrame == b.endFrame && a.startFrame == b.startFrame;
                if (overlap || duplicateCut) return $"Segments {j + 1} and {i + 1} overlap on the same camera channel.";
            }
        }
        return null;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public readonly struct CameraAnimationPose
{
    public readonly Vector2 Position;
    public readonly float Size;
    public CameraAnimationPose(Vector2 position, float size) { Position = position; Size = size; }
}

/// <summary>Per-run snapshot; sampling is independent of playback order and never changes authored data.</summary>
public sealed class CameraAnimationSampler
{
    private readonly List<CameraAnimationSegment> _segments;
    private readonly CameraAnimationPose _baseline;
    private readonly float _aspect;
    private readonly int _facing;
    private readonly List<int> _positionBoundaryFrames = new List<int>();
    public IReadOnlyList<int> PositionBoundaryFrames => _positionBoundaryFrames;
    public int DurationFrames { get; }
    public bool FollowSlowMotion { get; }

    public CameraAnimationSampler(CameraAnimationFrameData data, CameraAnimationPose baseline, float aspect, int facing = 1)
    {
        string error = data?.GetValidationError() ?? (data == null ? "Camera sequence is missing." : null);
        if (error != null) throw new ArgumentException(error, nameof(data));
        _baseline = baseline;
        _aspect = aspect;
        _facing = facing >= 0 ? 1 : -1;
        DurationFrames = data.DurationFrames;
        FollowSlowMotion = data.followSlowMotion;
        _segments = new List<CameraAnimationSegment>(data.segments.Count);
        foreach (var segment in data.segments) _segments.Add(segment.Copy());
        // Cuts at a shared boundary apply before a transition starting at that boundary.
        _segments.Sort((a, b) => a.startFrame != b.startFrame
            ? a.startFrame.CompareTo(b.startFrame) : a.endFrame.CompareTo(b.endFrame));
        foreach (var segment in _segments)
            if ((segment.AffectedChannels & CameraAnimationChannels.Position) != 0
                && (_positionBoundaryFrames.Count == 0 || _positionBoundaryFrames[_positionBoundaryFrames.Count - 1] != segment.startFrame))
                _positionBoundaryFrames.Add(segment.startFrame);
    }

    public CameraAnimationPose Sample(float frame, CameraAnimationPose normal)
        => Sample(frame, normal, _baseline.Position);

    public CameraAnimationPose Sample(float frame, CameraAnimationPose normal, Vector2 playerPosition,
        Func<int, Vector2> playerAtBoundary = null)
    {
        float zoomConstant = 0f, zoomNormalWeight = 1f;
        Vector2 positionConstant = Vector2.zero;
        float positionNormalWeight = 1f;
        float positionPlayerWeight = 0f;
        foreach (var segment in _segments)
        {
            if (frame < segment.startFrame) break;
            float t = segment.Progress(frame);
            var channels = segment.AffectedChannels;
            if ((channels & CameraAnimationChannels.Zoom) != 0)
            {
                if (segment.task == CameraAnimationTask.Hold)
                {
                    zoomConstant += _baseline.Size * zoomNormalWeight;
                    zoomNormalWeight = 0f;
                }
                else if (segment.task == CameraAnimationTask.Return)
                {
                    zoomConstant = Mathf.Lerp(zoomConstant, 0f, t);
                    zoomNormalWeight = Mathf.Lerp(zoomNormalWeight, 1f, t);
                }
                else
                {
                    zoomConstant = Mathf.Lerp(zoomConstant, _baseline.Size * segment.zoomMultiplier, t);
                    zoomNormalWeight *= 1f - t;
                }
            }
            if ((channels & CameraAnimationChannels.Position) != 0)
            {
                // Freeze a completed tracking segment's origin at the next position command's start.
                // Keeping this input explicit makes out-of-order sampling deterministic for a given target history.
                if (positionPlayerWeight != 0f)
                {
                    Vector2 captured = playerAtBoundary != null ? playerAtBoundary(segment.startFrame) : playerPosition;
                    positionConstant += captured * positionPlayerWeight;
                    positionPlayerWeight = 0f;
                }
                if (segment.task == CameraAnimationTask.Hold)
                {
                    positionConstant += _baseline.Position * positionNormalWeight;
                    positionNormalWeight = 0f;
                }
                else if (segment.task == CameraAnimationTask.Return)
                {
                    positionConstant = Vector2.Lerp(positionConstant, Vector2.zero, t);
                    positionNormalWeight = Mathf.Lerp(positionNormalWeight, 1f, t);
                }
                else
                {
                    Vector2 target = segment.position;
                    if (segment.facingRelativeX) target.x *= _facing;
                    if (segment.positionSpace == CameraAnimationPositionSpace.ScreenOffsetFromStart)
                        target = Vector2.Scale(target, new Vector2(_baseline.Size * _aspect * 2f, _baseline.Size * 2f));
                    bool tracksPlayer = segment.positionSpace == CameraAnimationPositionSpace.Player;
                    if (!tracksPlayer && segment.positionSpace != CameraAnimationPositionSpace.WorldPoint) target += _baseline.Position;
                    positionConstant = Vector2.Lerp(positionConstant, target, t);
                    positionNormalWeight *= 1f - t;
                    positionPlayerWeight = tracksPlayer ? t : 0f;
                }
            }
        }
        return new CameraAnimationPose(positionConstant + normal.Position * positionNormalWeight + playerPosition * positionPlayerWeight,
            zoomConstant + normal.Size * zoomNormalWeight);
    }
}
