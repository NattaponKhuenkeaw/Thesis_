using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Standard wrapper around Unity's <see cref="BehaviorGraphAgent"/>.
/// Does not subclass the Agent — it binds the shared blackboard contract and locomotion
/// so ground, fly, and warp enemies can share one graph style.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(BehaviorGraphAgent))]
[RequireComponent(typeof(EnemyController))]
[RequireComponent(typeof(EnemyLocomotion))]
public class EnemyBehaviorBrain : MonoBehaviour
{
    public const string AgentKey = "Agent";
    public const string TargetKey = "Target";
    public const string CanAttackKey = "CanAttack";
    public const string IsTakingDamageKey = "IsTakingDamage";
    public const string DetectRadiusKey = "DetectRadius";

    [SerializeField] private BehaviorGraphAgent agent;
    [SerializeField] private EnemyController enemy;
    [SerializeField] private EnemyLocomotion locomotion;

    [Tooltip("Keep the Agent enabled unless a dedicated boss driver is present.")]
    [SerializeField] private bool enableAgentWhenNoBossDriver = true;

    public BehaviorGraphAgent Agent => agent;
    public EnemyLocomotion Locomotion => locomotion;
    public EnemyMoveMode MoveMode => locomotion != null ? locomotion.MoveMode : EnemyMoveMode.Ground;

    private void Reset()
    {
        Cache();
    }

    private void Awake()
    {
        Cache();
        BindBlackboardSelf();
        if (enableAgentWhenNoBossDriver && GetComponent<Project.AIBehaviour.Boss.BossPatternController>() == null)
        {
            SetAgentActive(true);
        }
    }

    public void SetAgentActive(bool active)
    {
        Cache();
        if (agent != null)
        {
            agent.enabled = active;
        }
    }

    public bool TrySetBlackboard<T>(string key, T value)
    {
        Cache();
        return agent != null && agent.SetVariableValue(key, value);
    }

    private void BindBlackboardSelf()
    {
        if (agent == null)
        {
            return;
        }

        agent.SetVariableValue(AgentKey, gameObject);
        if (agent.GetVariable(CanAttackKey, out BlackboardVariable<bool> canAttack) && canAttack != null)
        {
            canAttack.Value = true;
        }

        if (agent.GetVariable(IsTakingDamageKey, out BlackboardVariable<bool> takingDamage) && takingDamage != null)
        {
            takingDamage.Value = false;
        }
    }

    private void Cache()
    {
        if (agent == null)
        {
            agent = GetComponent<BehaviorGraphAgent>();
        }

        if (enemy == null)
        {
            enemy = GetComponent<EnemyController>();
        }

        if (locomotion == null)
        {
            locomotion = GetComponent<EnemyLocomotion>();
        }
    }
}
