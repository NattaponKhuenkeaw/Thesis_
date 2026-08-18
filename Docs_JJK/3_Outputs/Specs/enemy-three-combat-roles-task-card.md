---
slug: enemy-three-combat-roles
status: implementing
source: manual
gdd_tags:
  - enemies
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Enemy 1 / 2 / 3 Combat Roles

## Player-Facing Goal

Three trash kits share one Behavior Graph. Prefab `Combat Role` changes the feel:

1. **Enemy1** — melee, hits cancel windup and swing.
2. **Enemy2** — melee. Once the telegraph starts, the swing cannot be staggered. Idle / walk still take hitstun.
3. **Enemy3** — ranged shot. Does not back off when the player walks in.

## Source

- Origin: playtest polish (ตัว2 ง้างแล้วขัดไม่ได้ / ตัว3 ยิงไกลไม่ถอย)
- Requested by: project owner

## Type

- [x] Feature

## Scope

### Systems Affected

- `EnemyController` combat role
- Shared Behavior Graph actions/conditions
- `Enemy1` / `Enemy2` / new `Enemy3` prefabs
- Combat encounter mix

### Out of Scope

- New Behavior Graph assets
- Boss / Executioner Agent migration
- Map prefab resize

## Acceptance Criteria

- [x] Enemy2 commits `ignoreHitReactions` at telegraph start; idle is still hittable
- [x] Enemy1 has no standing Super Armor and no swing armor
- [x] Enemy3 shoots a projectile, uses ~7 attack range, and skips evasion
- [ ] Playtest the three kits in a combat room
