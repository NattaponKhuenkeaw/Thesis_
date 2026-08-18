using System;
using Unity.Properties;
using UnityEngine;
using Unity.Behavior;

[Serializable, GeneratePropertyBag]
[Condition(
    name: "Can Attack",
    description: "Returns Success when the CanAttack blackboard variable is true, the target is within AttackRange, the enemy is not in hit reaction, and the enemy is grounded.",
    story: "[Agent] [CanAttack] to [Target] within [AttackRange]",
    category: "Conditions/Combat",
    id: "a1b2c3d4e5f60005aabbccddeeff0005")]
public partial class CanAttackCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent = new BlackboardVariable<GameObject>();
    [SerializeReference] public BlackboardVariable<GameObject> Target = new BlackboardVariable<GameObject>();
    [SerializeReference] public BlackboardVariable<bool> CanAttack = new BlackboardVariable<bool>();
    [SerializeReference] public BlackboardVariable<float> AttackRange = new BlackboardVariable<float>(1.5f);

    public override bool IsTrue()
    {
        if (Agent == null || Target == null || CanAttack == null) return false;
        if (Agent.Value == null || Target.Value == null) return false;
        if (!CanAttack.Value) return false;

        EnemyController controller = Agent.Value.GetComponent<EnemyController>();
        if (controller != null)
        {
            if (controller.CurrentReactionState != EnemyReactionState.None)
                return false;

            EnemyHitReactionHandler hitReactionHandler = controller.hitReactionHandler != null
                ? controller.hitReactionHandler
                : Agent.Value.GetComponent<EnemyHitReactionHandler>();

            if (hitReactionHandler != null && !hitReactionHandler.IsGroundedForCombat)
                return false;
        }

        return EnemyFacingUtil.HorizontalDistance(
            Agent.Value.transform.position,
            Target.Value.transform.position) <= AttackRange.Value;
    }
}
