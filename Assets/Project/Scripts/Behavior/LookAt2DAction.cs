using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Look At 2D",
        description: "Rotates the Transform on the Z-axis to look at the Target in 2D.",
        story: "[Transform] looks at [Target] (2D)",
        category: "Action/Transform",
        id: "c82fb6e492b14e39883f3d9a7f3e1a2c")]
    internal partial class LookAt2DAction : Action
    {
        [SerializeReference] public BlackboardVariable<Transform> Transform;
        [SerializeReference] public BlackboardVariable<Transform> Target;
        
        [Tooltip("True: the node process the LookAt every update with status Running." +
            "\nFalse: the node process the LookAt only once.")]
        [SerializeReference] public BlackboardVariable<bool> Continuous = new BlackboardVariable<bool>(false);
        
        [Tooltip("Offset added to the calculated angle (in degrees). Use 90 if your sprite faces 'Up' by default.")]
        [SerializeReference] public BlackboardVariable<float> RotationOffset = new BlackboardVariable<float>(0f);

        protected override Status OnStart()
        {
            if (Transform.Value == null || Target.Value == null)
            {
                LogFailure($"Missing Transform or Target.");
                return Status.Failure;
            }

            ProcessLookAt();
            return Continuous.Value ? Status.Running : Status.Success;
        }

        protected override Status OnUpdate()
        {
            if (Continuous.Value)
            {
                ProcessLookAt();
                return Status.Running;
            }
            return Status.Success;
        }

        void ProcessLookAt()
        {
            if (Transform.Value == null || Target.Value == null) return;

            Vector3 direction = Target.Value.position - Transform.Value.position;
            
            // Calculate angle in degrees using Atan2 (returns -180 to 180)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // Apply rotation around the Z-axis
            Transform.Value.rotation = Quaternion.Euler(0, 0, angle + RotationOffset.Value);
        }
    }
}
