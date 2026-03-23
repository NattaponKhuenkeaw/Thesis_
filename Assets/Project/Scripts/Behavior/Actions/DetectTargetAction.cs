using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Detect Target",
        description: "Scans for a target with a specific tag using an overlap circle and assigns it to the Target property.",
        story: "[Agent] detects target in [Radius] with [TargetTag] tag and stores in [Target]",
        category: "Action/Detection",
        id: "d1e2t3e4c5t6t7a8r9g0e1t2a3c4t5i6")]
    public partial class DetectTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<float> Radius = new BlackboardVariable<float>(8f);
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        
        [Header("Detection Settings")]
        [SerializeReference] public BlackboardVariable<string> TargetTag = new BlackboardVariable<string>("Player");

        protected override Status OnUpdate()
        {
            if (Agent.Value == null) return Status.Failure;
            if (Target != null && Target.Value != null) return Status.Failure;

            Collider2D[] results = Physics2D.OverlapCircleAll(Agent.Value.transform.position, Radius.Value);
            foreach (var col in results)
            {
                if (col.CompareTag(TargetTag.Value))
                {
                    if (Target != null) Target.Value = col.gameObject;
                    Debug.Log($"[DetectTarget] Found {col.gameObject.name} with tag {TargetTag.Value}");
                    return Status.Success;
                }
            }

            return Status.Failure;
        }
    }
}
