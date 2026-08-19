using System;
using Unity.Properties;
using UnityEngine;

using Unity.Behavior;
    [Serializable, GeneratePropertyBag]
    [Condition(
        name: "Is Target In Radius",
        description: "Returns Success if the Target is within DetectRadius of the Agent. Optionally performs a line-of-sight raycast.",
        story: "[Agent] detects [Target] within [DetectRadius]",
        category: "Conditions/Detection",
        id: "a1b2c3d4e5f60001aabbccddeeff0001")]
    public partial class IsPlayerInRadiusCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();
        [SerializeReference] public BlackboardVariable<float> DetectRadius = new BlackboardVariable<float>(8f);
        [SerializeReference] public BlackboardVariable<bool> RequireLineOfSight = new BlackboardVariable<bool>(false);
        public LayerMask ObstacleLayer;

        public override bool IsTrue()
        {
            if (Agent == null || Target == null)
                return false;
            if (Agent.Value == null || Target.Value == null)
                return false;

            // Trash enemies always know the player. Radius is no longer a search gate.
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
