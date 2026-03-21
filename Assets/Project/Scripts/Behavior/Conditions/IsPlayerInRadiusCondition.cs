using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Is Player In Radius",
        description: "Returns Success if the Target is within DetectRadius of the Agent. Optionally performs a line-of-sight raycast.",
        story: "[Agent] detects [Target] within [DetectRadius]",
        category: "Flow/Condition",
        id: "a1b2c3d4e5f60001aabbccddeeff0001")]
    public partial class IsPlayerInRadiusCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> DetectRadius = new BlackboardVariable<float>(8f);
        [SerializeReference] public BlackboardVariable<bool> RequireLineOfSight = new BlackboardVariable<bool>(false);
        [SerializeReference] public BlackboardVariable<LayerMask> ObstacleLayer;

        public override bool IsTrue()
        {
            if (Agent.Value == null || Target.Value == null) return false;

            float dist = Vector2.Distance(Agent.Value.transform.position, Target.Value.transform.position);
            if (dist > DetectRadius.Value) return false;

            if (RequireLineOfSight.Value)
            {
                Vector2 dir = (Vector2)(Target.Value.transform.position - Agent.Value.transform.position);
                RaycastHit2D hit = Physics2D.Raycast(Agent.Value.transform.position, dir.normalized, dist, ObstacleLayer.Value);
                if (hit.collider != null) return false; // something is blocking
            }

            return true;
        }

#if UNITY_EDITOR
        // Draw gizmo so designer can visualise the radius
        public void OnDrawGizmosSelected(GameObject agent)
        {
            if (agent == null) return;
            UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.25f);
            UnityEditor.Handles.DrawSolidDisc(agent.transform.position, Vector3.forward, DetectRadius.Value);
        }
#endif
    }
