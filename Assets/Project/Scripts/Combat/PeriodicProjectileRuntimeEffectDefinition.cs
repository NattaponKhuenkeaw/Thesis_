using System.Collections.Generic;
using Project.Combat;
using UnityEngine;

[CreateAssetMenu(fileName = "RuntimeEffect_PeriodicProjectile", menuName = "Game/Runtime Effects/Periodic Projectile")]
public sealed class PeriodicProjectileRuntimeEffectDefinition : RuntimeEffectDefinition
{
    private sealed class FieldState
    {
        internal Vector2 origin;
        internal Vector2 direction;
        internal PlayerCombat combat;
        internal int clearVersion;
        internal int targetIndex;
        internal readonly List<EnemyController> targets = new();
    }

    public override bool RequiresFormulaTicking => true;

    public override void OnApply(RuntimeEffectContext context)
    {
        var parameters = Parameters(context.instance);
        var player = context.targetPlayer;
        if (parameters == null || player == null || ProjectileVFXManager.Instance == null) return;
        var state = new FieldState
        {
            origin = ProjectileSpatialUtility.ResolveReferencePosition(player.transform, ProjectileReferencePointMode.GroundProjection),
            direction = player.transform.localScale.x < 0f ? Vector2.left : Vector2.right,
            combat = player.Combat,
            clearVersion = ProjectileVFXManager.Instance.ClearVersion
        };
        context.instance.formulaState = state;
        context.instance.nextTickAt = Mathf.Max(.05f, parameters.tickInterval);
    }

    public override void OnReapplied(RuntimeEffectContext context)
    {
        OnExpired(context);
        OnApply(context);
    }

    public override bool IsFormulaUpdateDue(RuntimeEffectInstance instance) => true;

    public override void OnUpdate(RuntimeEffectContext context)
    {
        if (context.instance.formulaState is not FieldState state) return;
        var pool = ProjectileVFXManager.Instance;
        if (pool == null || pool.ClearVersion != state.clearVersion ||
            (context.runInventory != null && !context.runInventory.IsRunCharacterAlive(context.sourceCharacter)))
        {
            context.instance.forceExpired = true;
            return;
        }
        var parameters = Parameters(context.instance);
        if (parameters?.projectile == null) return;
        float until = context.instance.UsesTimer
            ? Mathf.Min(context.instance.elapsedSeconds, context.instance.appliedDurationSeconds)
            : context.instance.elapsedSeconds;
        while (context.instance.nextTickAt <= until)
        {
            context.instance.nextTickAt += Mathf.Max(.05f, parameters.tickInterval);
            Vector2 origin = ResolveStrikeOrigin(parameters, state.origin);
            RoomEnemyRegistry.FillTargetable(state.targets);
            EnemyController target = SelectTarget(state.targets, origin, parameters.roundRobinTargets, ref state.targetIndex);
            if (target != null) Spawn(context, parameters, state, origin, target.transform);
        }
    }

    internal static Vector2 ResolveStrikeOrigin(RuntimeEffectProjectileParameters parameters, Vector2 castOrigin)
    {
        var player = PlayerCharacter.Instance;
        return parameters.useActivePlayerOrigin && player != null ? (Vector2)player.transform.position : castOrigin;
    }

    internal static EnemyController SelectTarget(List<EnemyController> targets, Vector2 origin, bool roundRobin, ref int targetIndex)
    {
        if (targets.Count == 0) return null;
        if (roundRobin)
        {
            targets.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            EnemyController target = targets[targetIndex % targets.Count];
            targetIndex = (targetIndex + 1) % targets.Count;
            return target;
        }
        EnemyController nearest = null;
        float distance = float.PositiveInfinity;
        foreach (var enemy in targets)
        {
            float candidate = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (candidate < distance) { distance = candidate; nearest = enemy; }
        }
        return nearest;
    }

    public override void OnExpired(RuntimeEffectContext context)
    {
        context.instance.formulaState = null;
    }

    private static RuntimeEffectProjectileParameters Parameters(RuntimeEffectInstance instance)
    {
        var parameters = RuntimeEffectRuntimeUtility.GetParameters(instance);
        return parameters?.overrideProjectile == true ? parameters.projectile : null;
    }

    private static ProjectileRuntime Spawn(RuntimeEffectContext context, RuntimeEffectProjectileParameters parameters,
        FieldState state, Vector2 origin, Transform target)
    {
        var data = parameters.projectile;
        var stats = RuntimeEffectRuntimeUtility.ResolveCurrentFormulaSourceStats(new RuntimeEffectDamageContext
        { sourceCharacter = context.sourceCharacter, sourceStats = context.sourceStats });
        return ProjectileManager.Instance?.SpawnProjectile(new ProjectileSpawnContext
        {
            projectileType = data.projectileType, projectileData = data,
            origin = origin, direction = state.direction, targetTransform = target,
            sourceCharacter = context.sourceCharacter, sourceCombat = state.combat,
            attackerStats = stats, sourceAttack = stats?.attack ?? 0f,
            sourceActionId = parameters.actionId, sourceActionName = parameters.actionName,
            category = parameters.category, remainingHits = data.maxHits, sizeMultiplier = 1f,
            isSkillOrUltimate = parameters.category == AttackCategory.Skill || parameters.category == AttackCategory.Ultimate
        });
    }
}
