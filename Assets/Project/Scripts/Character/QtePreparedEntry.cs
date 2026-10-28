using UnityEngine;

public readonly struct QteEntryPlacement
{
    public readonly Vector2 startBodyPosition, landingBodyPosition, surfacePoint, surfaceNormal;
    public readonly Vector2 capturedEnemyPosition, supportLocalPoint;
    public readonly Collider2D support;
    public readonly int approachFacing, landingFacing;

    public QteEntryPlacement(Vector2 start, Vector2 landing, Vector2 surface, Vector2 normal,
        Vector2 enemy, Collider2D floor, int approach, int facing)
    {
        startBodyPosition = start; landingBodyPosition = landing; surfacePoint = surface;
        surfaceNormal = normal; capturedEnemyPosition = enemy; support = floor;
        supportLocalPoint = floor != null ? (Vector2)floor.transform.InverseTransformPoint(surface) : surface;
        approachFacing = approach; landingFacing = facing;
    }
}

public readonly struct QtePreparedEntry
{
    public readonly CombatAction action;
    public readonly AttackStepData step;
    public readonly EnemyController target;
    public readonly QteEntryMode mode;
    public readonly QteEntryPlacement placement;

    public QtePreparedEntry(CombatAction action, AttackStepData step, EnemyController target,
        QteEntryMode mode, QteEntryPlacement placement)
    { this.action = action; this.step = step; this.target = target; this.mode = mode; this.placement = placement; }
}
