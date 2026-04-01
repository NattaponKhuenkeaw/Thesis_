using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Flip to Face Target",
        description: "Flips the Transform's LocalScale.x to face the Target horizontally.",
        story: "[Transform] flips to face [Target]",
        category: "Action/Transform",
        id: "a1b2c3d4e5f67890abcdef1234567890")]
    internal partial class FlipToFaceTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<Transform> Transform;
        [SerializeReference] public BlackboardVariable<Transform> Target;

        [Tooltip("True: the node process the flip every update with status Running.\nFalse: the node process the flip only once.")]
        [SerializeReference] public BlackboardVariable<bool> Continuous = new BlackboardVariable<bool>(true);

        [Tooltip("Check this if your sprite faces 'Right' by default.")]
        [SerializeReference] public BlackboardVariable<bool> FacingRightByDefault = new BlackboardVariable<bool>(true);

        protected override Status OnStart()
        {
            if (Transform.Value == null || Target.Value == null)
            {
                LogFailure("Missing Transform or Target.");
                return Status.Failure;
            }

            ProcessFlip();
            return Continuous.Value ? Status.Running : Status.Success;
        }

        protected override Status OnUpdate()
        {
            if (Continuous.Value)
            {
                ProcessFlip();
                return Status.Running;
            }
            return Status.Success;
        }

        void ProcessFlip()
        {
            if (Transform.Value == null || Target.Value == null) return;

            Vector3 scale = Transform.Value.localScale;
            float directionX = Target.Value.position.x - Transform.Value.position.x;

            // Threshold to avoid flickering when overlapping
            if (Mathf.Abs(directionX) < 0.05f) return;

            // If target is to the right (directionX > 0)
            // If FacingRightByDefault is true, right=0, left=180.
            // If FacingRightByDefault is false, right=180, left=0.
            float targetRotY = (directionX > 0) ? (FacingRightByDefault.Value ? 0f : 180f) : (FacingRightByDefault.Value ? 180f : 0f);

            if (!Mathf.Approximately(Transform.Value.eulerAngles.y, targetRotY))
            {
                Transform.Value.eulerAngles = new Vector3(Transform.Value.eulerAngles.x, targetRotY, Transform.Value.eulerAngles.z);
            }
        }
    }
}
