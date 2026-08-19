using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

namespace Unity.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Detect Target",
        description: "Always knows the player location. Assigns the tagged target without a search radius.",
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
            if (Agent.Value == null)
                return Status.Failure;

            if (IsValidTarget(Target?.Value))
                return Status.Success;

            GameObject found = FindPlayer();
            if (found == null)
                return Status.Failure;

            if (Target != null)
                Target.Value = found;
            return Status.Success;
        }

        private GameObject FindPlayer()
        {
            string tag = TargetTag != null && !string.IsNullOrEmpty(TargetTag.Value)
                ? TargetTag.Value
                : "Player";

            PlayerCharacter playerCharacter = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            if (playerCharacter != null)
                return playerCharacter.gameObject;

            try
            {
                return GameObject.FindGameObjectWithTag(tag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private static bool IsValidTarget(GameObject target)
        {
            return target != null && target.activeInHierarchy;
        }
    }
}
