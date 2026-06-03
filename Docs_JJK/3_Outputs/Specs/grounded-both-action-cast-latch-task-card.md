---
slug: "grounded-both-action-cast-latch"
status: approved
source: manual
gdd_tags:
  - "combat-scope"
  - "prototype-truth"
owner: "implementation-agent"
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Task Card: Latch Cast Context for Grounded Both Actions

## Player-Facing Goal

Ground-cast actions authored with `CombatActionContext.Both` should keep grounded cancel routing even if their `MotionData` lifts the collider off the floor. Air-cast versions of the same action should stay airborne.

## Source

- Origin: Manual prompt
- Requested by: User

## GDD Reference

- `@tag:combat-scope` - Air attack systems and extended airborne combo rules are not part of the active design.
- `@tag:prototype-truth` - Complex air routing should be replaced with FSM ground <-> air overriding and a simple gravity policy.

## Type

- [ ] Feature
- [x] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Player FSM attack/skill cancel routing
- Combat ground authority guardrail
- Ground-vs-air action selection

### Files To Inspect First

- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Combat/PlayerCombat.cs`
- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Character/Hero/test_02_rugal/ActionResolverTable_Char_2_Rugal.asset`

### Out of Scope

- Reauthoring Rugal animation or motion curves.
- Adding new combo routes or new airborne-only skills.
- Reworking the whole combat FSM.

## Acceptance Criteria

- [ ] A ground-cast `Both` action stays grounded for action selection and cancel routing while its state-authorized guardrail is active.
- [ ] An air-cast `Both` action still routes as airborne.
- [ ] Interrupting or canceling a non-promoting ground-cast action returns to grounded state logic.
- [ ] Works on primary platform input model.
- [ ] Does not break existing: ground/air action resolution.
- [ ] Follows `RULES_AND_POLICY.md`

## Human Checkpoints

- [x] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- `Docs_JJK/3_Outputs/DevLog/2026-06-03-state-authorized-grounding.md`
