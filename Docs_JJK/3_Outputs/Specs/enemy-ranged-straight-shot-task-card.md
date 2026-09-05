---
slug: enemy-ranged-straight-shot
status: implemented-pending-playtest
source: manual
gdd_tags:
  - enemies
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Enemy Ranged Straight Shot

## Player-Facing Goal

Enemy3 fires a horizontal shot from an authored muzzle. Telegraph is a red laser from that point, not a bang icon. If the laser cannot hit the player, the enemy patrols like melee trash.

## Type

- [x] Feature

## Acceptance Criteria

- [x] Shot travels in the facing line, not homing at the player
- [x] Muzzle Transform on the prefab sets spawn origin
- [x] Telegraph is a red laser from the muzzle
- [x] No shot unless the straight line can hit; otherwise walk/patrol
