using Unity.Cinemachine;
using UnityEngine;

/// <summary>Preserves vertical follow when a rectangular room is narrower than the camera view.</summary>
[AddComponentMenu("Cinemachine/Procedural/Extensions/Room Camera Confiner 2D")]
public sealed class RoomCameraConfiner2D : CinemachineConfiner2D
{
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body || !state.Lens.Orthographic
            || !(BoundingShape2D is BoxCollider2D box) || !box.isActiveAndEnabled
            || Quaternion.Angle(box.transform.rotation, Quaternion.identity) > .01f
            || Quaternion.Angle(state.GetCorrectedOrientation(), Quaternion.identity) > .01f)
        {
            base.PostPipelineStageCallback(vcam, stage, ref state, deltaTime);
            return;
        }

        Bounds bounds = box.bounds;
        float halfHeight = state.Lens.OrthographicSize;
        float halfWidth = halfHeight * state.Lens.Aspect;
        if (bounds.size.x > 2f * halfWidth && bounds.size.y > 2f * halfHeight)
        {
            base.PostPipelineStageCallback(vcam, stage, ref state, deltaTime);
            return;
        }

        // The native undersized polygon can collapse both axes to its midpoint.
        // Confine each axis independently without changing the authored lens.
        Vector3 position = state.GetCorrectedPosition();
        Vector3 confined = position;
        confined.x = bounds.size.x <= 2f * halfWidth ? bounds.center.x
            : Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        confined.y = bounds.size.y <= 2f * halfHeight ? bounds.center.y
            : Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        state.PositionCorrection += confined - position;
    }
}
