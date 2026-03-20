using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Navigate To Target 2D",
        description: "Navigates an Agent towards a Target GameObject in 2D (XY) space using its transform.",
        story: "[Agent] navigates to [Target] (2D)",
        category: "Action/Navigation",
        id: "7d8e9f0a1b2c3d4e5f6g7h8i9j0k1l2m")]
    internal partial class NavigateToTarget2DAction : Action
    {
        public enum TargetPositionMode
        {
            ClosestPointOnAnyCollider,      // Use the closest point on any 2D collider, including child objects
            ClosestPointOnTargetCollider,   // Use the closest point on the target's own 2D collider only
            ExactTargetPosition             // Use the exact position of the target, ignoring colliders
        }

        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<float> Speed = new BlackboardVariable<float>(1.0f);
        [SerializeReference] public BlackboardVariable<float> DistanceThreshold = new BlackboardVariable<float>(0.2f);
        [SerializeReference] public BlackboardVariable<string> AnimatorSpeedParam = new BlackboardVariable<string>("SpeedMagnitude");
        
        [Tooltip("Defines how the target position is determined for navigation in 2D.")]
        [SerializeReference] public BlackboardVariable<TargetPositionMode> m_TargetPositionMode = new(TargetPositionMode.ClosestPointOnAnyCollider);

        private Animator m_Animator;
        private Vector2 m_LastTargetPosition;
        private Vector2 m_ColliderAdjustedTargetPosition;
        private float m_ColliderOffset;

        protected override Status OnStart()
        {
            if (Agent.Value == null || Target.Value == null)
            {
                return Status.Failure;
            }

            m_Animator = Agent.Value.GetComponentInChildren<Animator>();
            Initialize();
            
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null || Target.Value == null)
            {
                return Status.Failure;
            }

            Vector2 currentTargetPos = Target.Value.transform.position;
            
            // Check if the target position has moved significantly
            if (Vector2.Distance(m_LastTargetPosition, currentTargetPos) > 0.01f)
            {
                m_LastTargetPosition = currentTargetPos;
                m_ColliderAdjustedTargetPosition = GetPositionColliderAdjusted();
            }

            float distance = Vector2.Distance(Agent.Value.transform.position, m_ColliderAdjustedTargetPosition);
            bool destinationReached = distance <= (DistanceThreshold.Value + m_ColliderOffset);

            if (destinationReached)
            {
                UpdateAnimatorSpeed(0f);
                return Status.Success;
            }

            // Simple movement logic for 2D (Transform-based)
            float step = Speed.Value * Time.deltaTime;
            Agent.Value.transform.position = Vector2.MoveTowards(Agent.Value.transform.position, m_ColliderAdjustedTargetPosition, step);

            UpdateAnimatorSpeed(Speed.Value);

            return Status.Running;
        }

        protected override void OnEnd()
        {
            UpdateAnimatorSpeed(0f);
            m_Animator = null;
        }

        private void Initialize()
        {
            m_LastTargetPosition = Target.Value.transform.position;
            m_ColliderAdjustedTargetPosition = GetPositionColliderAdjusted();

            // Add the extents of the 2D colliders to the stopping distance.
            m_ColliderOffset = 0.0f;
            Collider2D agentCollider = Agent.Value.GetComponentInChildren<Collider2D>();
            if (agentCollider != null)
            {
                Vector2 colliderExtents = agentCollider.bounds.extents;
                m_ColliderOffset += Mathf.Max(colliderExtents.x, colliderExtents.y);
            }
        }

        private Vector2 GetPositionColliderAdjusted()
        {
            switch (m_TargetPositionMode.Value)
            {
                case TargetPositionMode.ClosestPointOnAnyCollider:
                    Collider2D anyCollider = Target.Value.GetComponentInChildren<Collider2D>(false);
                    if (anyCollider == null || anyCollider.enabled == false)
                        break;
                    return anyCollider.ClosestPoint(Agent.Value.transform.position);
                    
                case TargetPositionMode.ClosestPointOnTargetCollider:
                    Collider2D targetCollider = Target.Value.GetComponent<Collider2D>();
                    if (targetCollider == null || targetCollider.enabled == false)
                        break;
                    return targetCollider.ClosestPoint(Agent.Value.transform.position);
            }

            // Default to exact target position.
            return Target.Value.transform.position;
        }

        private void UpdateAnimatorSpeed(float speed)
        {
            if (m_Animator != null && !string.IsNullOrEmpty(AnimatorSpeedParam.Value))
            {
                m_Animator.SetFloat(AnimatorSpeedParam.Value, speed);
            }
        }
    }
}
