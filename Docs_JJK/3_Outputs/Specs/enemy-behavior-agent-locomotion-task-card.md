---
slug: enemy-behavior-agent-locomotion
status: implementing
source: manual
gdd_tags:
  - enemies
  - bosses
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: Enemy Behavior Agent Locomotion

## Player-Facing Goal

Every enemy (ground, future flyers, later bosses) uses the same Behavior Graph Agent brain. Movement is slot-based (walk / fly / warp) so attacks meet the player instead of drifting on X-only chase.

## Source

- Origin: manual prompt (polish / enemy AI)
- Requested by: team

## GDD Reference

- `@tag:enemies` — Unity Behavior Graph for enemy AI
- BlazBlue Entropy Effect: warp to attack position, not physics chase

## Type

- [x] Feature
- [x] Refactor

## Scope

### Systems Affected

- Behavior Graph actions/conditions
- Enemy locomotion next to `BehaviorGraphAgent`

### Out of Scope

- Migrating Executioner off `BossPatternController` (still disables Agent)
- Authoring new Behavior Graph assets in the editor
- Map prefab resize

## Acceptance Criteria

- [x] Shared `EnemyMoveMode` Ground / Fly / Warp
- [x] `EnemyLocomotion` + `EnemyBehaviorBrain` wrap the Agent
- [x] Existing Chase / attack-range nodes use locomotion when present
- [x] Fly mode does not require grounded-for-combat to attack
- [x] New graph node: Warp To Attack Slot
- [x] Trash prefabs inherit `EnemyLocomotion` + `EnemyBehaviorBrain` from `EnemyCore`
- [ ] Playtest: Warp snap-then-swing still optional; 3 combat roles are the current playtest
