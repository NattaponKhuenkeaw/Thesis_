---
slug: "polish-jump-combat-refactor"
status: draft
source: manual
gdd_tags:
  - "player-movement"
  - "combat-fsm"
owner: "implementation-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Polish Jump and Combat Refactor

## Player-Facing Goal

Smooth, responsive, and deterministic jump and combat transitions. Ground attacks shouldn't float or get confused with air attacks due to motion deltas, jumps should rise purely vertically without accidental steering, and hardcoded plunge mechanics are removed in favor of clean design-driven state branching.

## Source

- Origin: Manual request
- Requested by: User

## GDD Reference

- `@tag:combat-movement` - Ground/air state transitions, deterministic jumps, plunge attacks.

## Type

- [ ] Feature
- [ ] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Player FSM (Jump, Airborne, Attack states)
- Physics / Ground State Provider
- Plunge Attack legacy systems

### Files To Inspect First

- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Scripts/Player/States/PlayerAttackState.cs`
- `Assets/Project/Scripts/Combat/PlayerCombat.cs`
- `Assets/Project/Scripts/Combat/PlayerMovement.cs`

### Out of Scope

- Adding new combat actions or animations.
- Refactoring enemy AI.

## Acceptance Criteria

- [ ] MotionData vertical offsets do not trigger airborne state transitions or confuse FSM combo routing.
- [ ] Jump starting phase is purely vertical (only upward) with buffered inputs executed only upon reaching the airborne state.
- [ ] Hardcoded plunge mechanics and states are removed from code.
- [ ] Follows `RULES_AND_POLICY.md`

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval
