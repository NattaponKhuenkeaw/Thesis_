using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActionCameraMotionExtension : CinemachineExtension
{
    private CameraManager _owner;
    private CinemachineConfiner2D _confiner;
    private bool _staticCamera;

    public void Configure(CameraManager owner, CinemachineConfiner2D confiner, bool staticCamera)
    {
        _owner = owner;
        _confiner = confiner;
        _staticCamera = staticCamera;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || _owner == null
            || !_owner.TrySampleActionCamera(out float size, out float offset)) return;
        var bounds = _confiner != null && _confiner.enabled ? _confiner.BoundingShape2D as BoxCollider2D : null;
        if (bounds == null || !bounds.enabled || Mathf.Abs(Mathf.DeltaAngle(bounds.transform.eulerAngles.z, 0f)) > 0.01f)
            return;
        ApplyBounded(ref state, bounds.bounds, size, _staticCamera ? 0f : offset);
    }

    public static bool ApplyBounded(ref CameraState state, Bounds bounds, float sizeMultiplier, float viewportOffset)
    {
        if (!state.Lens.Orthographic || Quaternion.Angle(state.GetCorrectedOrientation(), Quaternion.identity) > 0.1f
            || Mathf.Abs(state.Lens.Dutch) > 0.01f) return false;

        float size = state.Lens.OrthographicSize * Mathf.Clamp(sizeMultiplier, 0.8f, 1f);
        float halfWidth = size * state.Lens.Aspect;
        if (size <= 0f || halfWidth <= 0f || bounds.size.x < halfWidth * 2f || bounds.size.y < size * 2f)
            return false;

        Vector3 position = state.GetCorrectedPosition();
        position.x += Mathf.Clamp(viewportOffset, -0.03f, 0.03f) * halfWidth * 2f;
        position.x = Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = Mathf.Clamp(position.y, bounds.min.y + size, bounds.max.y - size);
        state.PositionCorrection += position - state.GetCorrectedPosition();
        state.Lens.OrthographicSize = size;
        return true;
    }
}
