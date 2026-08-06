# Audit: Trash Enemy Combat Feel

Date: 2026-08-05

## Problems found

1. **Facing mismatch** — `EnemyAttackAction` flipped `localScale.x` while Chase/Patrol used `eulerAngles.y`. Hitbox sat on the wrong side during swings.
2. **Telegraph soft-cancel** — any player hit during warning aborted the attack and reopened `CanAttack` immediately → warning spam, almost never connects.
3. **No swing commitment** — trash had zero armor during the actual attack frames.
4. **2D distance checks** — attack range used full Vector2 distance (Y included), so standing slightly above/below blocked attacks oddly.
5. **Chase moved full XY via transform** — ignored Rigidbody2D and vertical side-scroller constraints.
6. **Static swing** — no lunge, easy to step back out of a tiny 0.2s hit window.

## Fixes applied

| File | Change |
|------|--------|
| `EnemyFacingUtil.cs` | Shared face + horizontal distance |
| `EnemyAttackAction.cs` | Commit armor, lunge, interrupt CD, recovery split, Y-facing |
| `ChaseAction.cs` | X-only chase via Rigidbody2D, pause in hitstun |
| `PatrolAction.cs` | Use shared facing |
| `CanAttackCondition.cs` / `IsPlayerInAttackRangeCondition.cs` | Horizontal range |
| `EnemyCore.prefab` | Hitbox forward (+X) |

## Still optional later

- Tune blackboard Warning/Hit/Cooldown per prefab in Behavior Graph
- Author Breaks Armor on player heavy hits for Enemy1
- Add a second undodgeable pattern for Enemy2 only on some swings (not every hit)
