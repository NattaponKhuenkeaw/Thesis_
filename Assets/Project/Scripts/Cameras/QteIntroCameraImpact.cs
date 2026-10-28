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

    public bool IsHorizontalCenterActive => _horizontalTarget != null
        && (Time.frameCount == _horizontalStartFrame
            || Time.unscaledTime < _horizontalHoldUntil + _horizontalReleaseSeconds);
    public float HorizontalTargetX => _horizontalTarget != null ? _horizontalTarget.position.x : 0f;
    public float LastHorizontalCorrection { get; private set; }

    public void CenterHorizontal(Transform target, float holdSeconds, float releaseSeconds)
    {
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
        _durationFrames = 0;
        _horizontalTarget = null;
        LastHorizontalCorrection = 0f;
    }
}
