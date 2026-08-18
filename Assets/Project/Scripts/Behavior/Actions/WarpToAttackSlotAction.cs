using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Snap the agent into its authored attack slot (BlazBlue-style). Uses EnemyLocomotion.
/// </summary>
[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Warp To Attack Slot",
    description: "Warps the Agent to its attack slot beside the Target. Ground keeps Y; Fly uses slot height.",
    story: "[Agent] warps to attack slot of [Target]",
    category: "Action/Enemy",
    id: "f1a2b3c4d5e60007aabbccddeeff0031")]
public partial class WarpToAttackSlotAction : Unity.Behavior.Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
    [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();

    protected override Status OnStart()
    {
        if (Agent == null || Target == null || Agent.Value == null || Target.Value == null)
        {
            return Status.Failure;
        }

        EnemyController controller = Agent.Value.GetComponent<EnemyController>();
        if (controller != null && controller.CurrentReactionState != EnemyReactionState.None)
        {
            return Status.Failure;
        }

        EnemyLocomotion locomotion = Agent.Value.GetComponent<EnemyLocomotion>();
        if (locomotion == null)
        {
            return Status.Failure;
        }

        locomotion.WarpToAttackSlot(Target.Value.transform);
        return Status.Success;
    }
}
