using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Flip to Face Target",
        description: "Rotates the Transform on Y (0 / 180) to face the Target horizontally.",
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
            if (Transform.Value == null || Target.Value == null)
                return;

            float directionX = Target.Value.position.x - Transform.Value.position.x;
            if (!FacingRightByDefault.Value)
                directionX = -directionX;

            EnemyFacingUtil.FaceHorizontal(Transform.Value, directionX);
        }
    }
}
