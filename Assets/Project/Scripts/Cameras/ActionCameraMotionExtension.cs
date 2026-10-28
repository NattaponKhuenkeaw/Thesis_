using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActionCameraMotionExtension : CinemachineExtension
{
    private CameraManager _owner;
    private CinemachineConfiner2D _confiner;

    public void Configure(CameraManager owner, CinemachineConfiner2D confiner, bool staticCamera)
    {
        _owner = owner;
        _confiner = confiner;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || _owner == null
            || !_owner.TrySampleActionCamera(out float size, out float offset)) return;
        var bounds = _confiner != null && _confiner.enabled ? _confiner.BoundingShape2D as BoxCollider2D : null;
        if (bounds == null || !bounds.enabled || Mathf.Abs(Mathf.DeltaAngle(bounds.transform.eulerAngles.z, 0f)) > 0.01f)
            return;
        if (_owner.TrySampleScreenImpulseCamera(out float zoom, out Vector3 lockedPosition, out float lockWeight))
            ApplyBounded(ref state, bounds.bounds, zoom, offset, true, lockedPosition, lockWeight);
        else
            ApplyBounded(ref state, bounds.bounds, size, offset);
    }

    public static bool ApplyBounded(ref CameraState state, Bounds bounds, float sizeMultiplier, float viewportOffset,
        bool allowZoomOut = false, Vector3 lockedPosition = default, float lockWeight = 0f)
    {
        if (!state.Lens.Orthographic || Quaternion.Angle(state.GetCorrectedOrientation(), Quaternion.identity) > 0.1f
            || Mathf.Abs(state.Lens.Dutch) > 0.01f) return false;

        float size = state.Lens.OrthographicSize * (allowZoomOut
            ? Mathf.Max(0.01f, sizeMultiplier) : Mathf.Clamp(sizeMultiplier, 0.8f, 1f));
        if (allowZoomOut && state.Lens.Aspect > 0f)
            size = Mathf.Min(size, Mathf.Min(bounds.extents.y, bounds.extents.x / state.Lens.Aspect));
        float halfWidth = size * state.Lens.Aspect;
        if (size <= 0f || halfWidth <= 0f || bounds.size.x < halfWidth * 2f || bounds.size.y < size * 2f)
            return false;

        Vector3 position = state.GetCorrectedPosition();
        if (allowZoomOut)
        {
            position.x = Mathf.Lerp(position.x, lockedPosition.x, Mathf.Clamp01(lockWeight));
            position.y = Mathf.Lerp(position.y, lockedPosition.y, Mathf.Clamp01(lockWeight));
        }
        position.x += Mathf.Clamp(viewportOffset, -1f, 1f) * halfWidth * 2f;
        position.x = Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = Mathf.Clamp(position.y, bounds.min.y + size, bounds.max.y - size);
        state.PositionCorrection += position - state.GetCorrectedPosition();
        state.Lens.OrthographicSize = size;
        return true;
    }
}
