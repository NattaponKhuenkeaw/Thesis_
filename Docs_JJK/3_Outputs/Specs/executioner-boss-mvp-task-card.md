---
slug: executioner-boss-mvp
status: implemented-pending-playtest
source: manual
gdd_tags:
  - bosses
  - enemies
owner: human+agent
human_checkpoint: playtest-feel
next_agent: human
blocked_by: []
---

# Task Card: Executioner Boss MVP

## Player-Facing Goal

Fight one readable heavy swordsman boss that teaches dash i-frames, wind-up reads, and recovery punish windows.

## Source

- Origin: manual prompt (Executioner / Heavy Swordsman MVP brief)
- Requested by: project owner

## GDD Reference

- `@tag:bosses` — telegraph → dodge → recovery damage window → phase density increase
- Sprint 9 intent: pattern-based boss, stagger resistance during attacks

## Type

- [x] Feature

## Scope

### Systems Affected

- Enemy AI / Boss patterns
- EnemyController (armored attack windows)
- Room spawn path via RoomWaveManager + RoomKind.Boss

### Files To Inspect First

- `Assets/Project/Scripts/AIBehaviour/EnemyController.cs`
- `Assets/Project/Scripts/Combat/EnemyAttackHitbox.cs`
- `Assets/Project/Scripts/Behavior/Actions/EnemyAttackAction.cs`
- `Assets/Project/Scripts/Core/ZoneState/RoomWaveManager.cs`

### Out of Scope

- Full boss intro cinematic / phase music
- Player projectile reuse for boss attacks
- Multiple unique boss characters
- Replacing Behavior Graph for trash enemies

## Acceptance Criteria

- [ ] Boss uses EnemyController with HP 800 / ATK 150 / DEF 500 / `isEliteOrBoss`
- [ ] Phase 1 uses Cleave + Jump Smash only
- [ ] Phase 2 (<50% HP) unlocks Bull Charge and may chain Jump Smash → Cleave
- [ ] Each pattern follows Telegraph → Active → Recovery
- [ ] Spawns through RoomWaveManager as a single-wave boss encounter
- [ ] Does not break existing trash enemy Behavior Graph flow

## Human Checkpoints

- [x] Design/game-feel approval (provided in author brief)
- [ ] Architecture approval (pattern controller over Behavior Graph for boss only)
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature`
