using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QteIntroCameraImpact : CinemachineExtension
{
    private float _sizeReduction;
    private int _startFrame;
    private int _durationFrames;
    private Transform _horizontalTarget;
    private BoxCollider2D _horizontalBounds;
    private float _horizontalHoldUntil;
    private float _horizontalReleaseSeconds;
    private int _horizontalStartFrame;
    private CameraManager _plungeOwner;
    private int _plungeGeneration, _plungeImpactFrame;
    private float _plungeFrame;
    private Vector3 _plungeContact;
    private CinemachineConfiner2D _plungeConfiner;
    private bool _plungeReturning, _plungeHasSample, _plungeReturnStarted;
    private float _plungeReturnSeconds, _plungeReturnStartTime, _plungeLastSize;
    private Vector3 _plungeLastPosition;

    public void BeginPlunge(CameraManager owner, int generation, Vector3 start, Vector3 contact, int impactFrame, float returnSeconds)
    {
        _plungeOwner = owner; _plungeGeneration = generation;
        _plungeContact = contact; _plungeImpactFrame = impactFrame;
        _plungeFrame = 0f; _durationFrames = 0;
        _plungeReturning = false; _plungeHasSample = false; _plungeReturnStarted = false;
        _plungeReturnSeconds = Mathf.Max(0.05f, returnSeconds);
        _horizontalTarget = null;
        _plungeConfiner = GetComponent<CinemachineConfiner2D>();
    }

    public void SamplePlunge(int generation, float frame)
    { if (generation == _plungeGeneration) _plungeFrame = frame; }

    public void ReleasePlunge(int generation)
    {
        if (generation != _plungeGeneration) return;
        BeginPlungeReturn();
        _plungeOwner = null;
    }

    private void BeginPlungeReturn()
    {
        if (_plungeReturning || !_plungeHasSample) return;
        _plungeReturning = true;
        _plungeReturnStarted = false;
    }

    private bool ApplyPlungeReturn(ref CameraState state)
    {
        if (!_plungeReturning) return false;
        if (!_plungeReturnStarted)
        {
            _plungeReturnStartTime = Time.time;
            _plungeReturnStarted = true;
        }
        float t = Mathf.Clamp01((Time.time - _plungeReturnStartTime) / _plungeReturnSeconds);
        float blend = Mathf.SmoothStep(0f, 1f, t);
        // Destination is evaluated afresh: combat, exploration or the next action may own it.
        Vector3 destination = state.GetCorrectedPosition();
        Vector3 position = Vector3.Lerp(_plungeLastPosition, destination, blend);
        state.PositionCorrection += position - destination;
        state.Lens.OrthographicSize = Mathf.Lerp(_plungeLastSize, state.Lens.OrthographicSize, blend);
        if (t >= 1f) { _plungeReturning = false; _plungeHasSample = false; _plungeOwner = null; }
        return true;
    }

    private bool ApplyPlunge(ref CameraState state)
    {
        if (_plungeReturning) return ApplyPlungeReturn(ref state);
        if (_plungeOwner == null || !_plungeOwner.TryGetQtePlungeCameraFocus(_plungeGeneration,
            out Vector3 focus, out float size))
        {
            BeginPlungeReturn();
            return ApplyPlungeReturn(ref state);
        }
        if (_plungeFrame >= _plungeImpactFrame)
        {
            BeginPlungeReturn();
            if (_plungeReturning) return ApplyPlungeReturn(ref state);
        }
        // Preserve the handoff lens: group framing and the old impact pulse cannot pump the zoom.
        state.Lens.OrthographicSize = size;
        var box = _plungeConfiner != null && _plungeConfiner.enabled
            ? _plungeConfiner.BoundingShape2D as BoxCollider2D : null;
        Vector3 desired = state.GetCorrectedPosition();
        desired.x = focus.x;
        desired.y = focus.y;
        // The user requested an above-room midair lock. Keep horizontal room limits,
        // temporarily lift the vertical ceiling, and use an actor-relative floor limit.
        if (box != null && box.enabled && Mathf.Abs(Mathf.DeltaAngle(box.transform.eulerAngles.z, 0f)) < 0.01f)
        {
            Bounds bounds = box.bounds;
            float halfWidth = size * state.Lens.Aspect;
            desired.x = bounds.size.x >= halfWidth * 2f
                ? Mathf.Clamp(desired.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth) : bounds.center.x;
        }
        desired.y = Mathf.Max(desired.y, _plungeContact.y + 0.5f);
        state.PositionCorrection += desired - state.GetCorrectedPosition();
        _plungeLastPosition = state.GetCorrectedPosition();
        _plungeLastSize = state.Lens.OrthographicSize;
        _plungeHasSample = true;
        return true;
    }

    public bool IsHorizontalCenterActive => _horizontalTarget != null
        && (Time.frameCount == _horizontalStartFrame
            || Time.unscaledTime < _horizontalHoldUntil + _horizontalReleaseSeconds);
    public float HorizontalTargetX => _horizontalTarget != null ? _horizontalTarget.position.x : 0f;
    public float LastHorizontalCorrection { get; private set; }

    public void CenterHorizontal(Transform target, float holdSeconds, float releaseSeconds)
    {
        // A newer handoff owns the camera; an old release cannot keep steering it.
        _plungeOwner = null; _plungeReturning = false; _plungeHasSample = false;
        _horizontalTarget = target;
        _horizontalStartFrame = Time.frameCount;
        _horizontalHoldUntil = Time.unscaledTime + Mathf.Max(0f, holdSeconds);
        _horizontalReleaseSeconds = Mathf.Max(0.01f, releaseSeconds);
        var confiner = GetComponent<CinemachineConfiner2D>();
        _horizontalBounds = confiner != null && confiner.enabled
            ? confiner.BoundingShape2D as BoxCollider2D : null;
    }

    public void Play(float sizeReduction, int frames)
    {
        _sizeReduction = Mathf.Max(0f, sizeReduction);
        _startFrame = Time.frameCount;
        _durationFrames = Mathf.Max(1, frames);
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !Application.isPlaying
            || !state.Lens.Orthographic) return;
        if (ApplyPlunge(ref state)) return;

        // Start at full impact, then ease back to the authored/group-framed size over rendered frames.
        int elapsedFrames = Time.frameCount - _startFrame;
        if (_durationFrames > 0 && _sizeReduction > 0f && elapsedFrames >= 0 && elapsedFrames < _durationFrames)
        {
            float progress = Mathf.Clamp01((float)elapsedFrames / _durationFrames);
            float recovery = Mathf.SmoothStep(0f, 1f, progress);
            float remainingReduction = _sizeReduction * (1f - recovery);
            state.Lens.OrthographicSize = Mathf.Max(0.01f, state.Lens.OrthographicSize - remainingReduction);
        }

        LastHorizontalCorrection = 0f;
        if (!IsHorizontalCenterActive) return;

        // Reset the rendered camera, not just its proxy: group framing, composer damping
        // and camera blends can otherwise retain the outgoing character's horizontal framing.
        float targetX = HorizontalTargetX;
        if (_horizontalBounds != null && _horizontalBounds.enabled)
        {
            Bounds bounds = _horizontalBounds.bounds;
            float halfWidth = state.Lens.OrthographicSize * state.Lens.Aspect;
            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            targetX = minX <= maxX ? Mathf.Clamp(targetX, minX, maxX) : bounds.center.x;
        }
        float release = Time.frameCount == _horizontalStartFrame ? 0f
            : Mathf.Clamp01((Time.unscaledTime - _horizontalHoldUntil) / _horizontalReleaseSeconds);
        float weight = 1f - Mathf.SmoothStep(0f, 1f, release);
        LastHorizontalCorrection = (targetX - state.GetCorrectedPosition().x) * weight;
        state.PositionCorrection += Vector3.right * LastHorizontalCorrection;
    }

    private void OnDisable()
    {
        _plungeOwner = null;
        _plungeReturning = false; _plungeHasSample = false;
        _durationFrames = 0;
        _horizontalTarget = null;
        LastHorizontalCorrection = 0f;
    }
}
