using UnityEngine;

/// <summary>
/// Lifecycle phases that wrap the enemy's runtime presence. This is intentionally separate from
/// <see cref="State"/>, which remains the Behavior Graph's decision/state label.
/// </summary>
public enum EnemyLifecycleStateId
{
    Spawn,
    Active,
    Die,
    Despawn
}

/// <summary>
/// Small lifecycle state object used by EnemyController. It owns presence transitions only;
/// Behavior Graph remains responsible for enemy decisions while the lifecycle is Active.
/// </summary>
public abstract class EnemyLifecycleState
{
    protected EnemyController Owner { get; }

    public abstract EnemyLifecycleStateId Id { get; }

    protected EnemyLifecycleState(EnemyController owner)
    {
        Owner = owner;
    }

    public virtual void Enter() { }

    public virtual void Tick(float deltaTime) { }

    public virtual void Exit() { }
}

public sealed class EnemySpawnState : EnemyLifecycleState
{
    private int enteredFrame;
    private float elapsed;
    public override EnemyLifecycleStateId Id => EnemyLifecycleStateId.Spawn;

    public EnemySpawnState(EnemyController owner)
        : base(owner)
    {
    }

    public override void Enter()
    {
        enteredFrame = Time.frameCount;
        elapsed = 0f;
        Owner.OnSpawnLifecycleEntered();
    }

    public override void Tick(float deltaTime)
    {
        elapsed += Mathf.Max(0f, deltaTime);
        if (Time.frameCount > enteredFrame && Owner.TickSpawnPresentation(elapsed))
            Owner.TransitionToActiveLifecycle();
    }

    public override void Exit() => Owner.OnSpawnLifecycleExited();
}

public sealed class EnemyActiveState : EnemyLifecycleState
{
    public override EnemyLifecycleStateId Id => EnemyLifecycleStateId.Active;

    public EnemyActiveState(EnemyController owner)
        : base(owner)
    {
    }

    public override void Enter()
    {
        Owner.OnActiveLifecycleEntered();
    }
}

public sealed class EnemyDieState : EnemyLifecycleState
{
    private float elapsed;

    public override EnemyLifecycleStateId Id => EnemyLifecycleStateId.Die;

    public EnemyDieState(EnemyController owner)
        : base(owner)
    {
    }

    public override void Enter()
    {
        elapsed = 0f;
        Owner.OnDieLifecycleEntered();
    }

    public override void Tick(float deltaTime)
    {
        elapsed += Mathf.Max(0f, deltaTime);
        if (Owner.IsDeathPresentationComplete(elapsed))
        {
            Owner.TransitionToDespawnLifecycle();
        }
    }
}

public sealed class EnemyDespawnState : EnemyLifecycleState
{
    public override EnemyLifecycleStateId Id => EnemyLifecycleStateId.Despawn;

    public EnemyDespawnState(EnemyController owner)
        : base(owner)
    {
    }

    public override void Enter()
    {
        Owner.OnDespawnLifecycleEntered();
    }
}
