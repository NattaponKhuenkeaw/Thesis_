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
            // If FacingRightByDefault is true, multiplier is 1. If target is right, scale.x should be positive.
            // If FacingRightByDefault is false, multiplier is -1. If target is right, scale.x should be negative.
            float multiplier = FacingRightByDefault.Value ? 1f : -1f;
            float newScaleX = Mathf.Abs(scale.x) * Mathf.Sign(directionX) * multiplier;

            if (!Mathf.Approximately(scale.x, newScaleX))
            {
                scale.x = newScaleX;
                Transform.Value.localScale = scale;
            }
        }
    }
}
