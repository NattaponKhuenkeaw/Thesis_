---
slug: "edge-snap-dash-sequencing"
status: needs-human
source: manual
gdd_tags:
  - "combat-scope"
  - "platform-input"
owner: "implementation-agent"
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Edge Snap and Dash Sequencing

## Player-Facing Goal

Attacks that genuinely leave a ledge or begin immediately after a platform drop must finish in air without snapping to distant ground or losing their animation. Dash must remain committed through Recovery, become interruptible at its authored Cancel phase, and immediately replace Landing when pressed after touchdown.

## Source

- Origin: Manual prompt
- Requested by: User

## GDD Reference

- `@tag:combat-scope` - Action context changes are cause-aware; authored motion must remain distinct from genuine support loss.
- `@tag:platform-input` - Shift is the player dash input and must resolve predictably at legal state boundaries.

## Type

- [ ] Feature
- [x] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Player FSM ground/air action output resolution
- Player ground/drop transition causes
- Dash phase interrupt policy and animation ownership
- Landing input routing

### Files To Inspect First

- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Scripts/Player/States/PlayerDashState.cs`
- `Assets/Project/Scripts/Player/States/PlayerLandingState.cs`
- `Assets/Project/Scripts/Player/InterruptPolicy.cs`
- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Animation/PlayerAnimator.cs`
- `Docs_JJK/3_Outputs/ADRs/003-deterministic-player-state-owned-movement.md`

### Out of Scope

- Reauthoring DashData frame counts, speeds, or animation clips.
- Changing explicit `Ground` action outputs.
- Reworking unrelated combat priorities or combo trees.

## Acceptance Criteria

- [ ] A ground-started `MatchInput` action that genuinely walks off a ledge exits to air without a downward ground snap.
- [ ] Dropping through a one-way platform and immediately attacking stays airborne and preserves the action animation.
- [ ] Authored positive-Y action motion does not become an edge-loss transition.
- [ ] Dash Recovery accepts Attack, Skill, Ultimate, and Jump but keeps walking/facing locked; Cancel retains its full action window.
- [ ] A short x = -1 support ray rejects a lingering edge-contact before a ground-started `MatchInput` output can snap.
- [ ] Dash pressed during Landing immediately enters Dash and clears the remaining landing lock/animation.
- [ ] Works on KB+M and controller input routing.
- [ ] Does not break explicit Ground output snapping or Air Dash recovery gravity behavior.
- [ ] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Design/game-feel direction supplied by the user in the manual request.
- [x] FSM/cancel-boundary direction supplied by the user in the manual request.
- [ ] In-Editor game-feel verification.
- [ ] PR review before merge.
- [ ] Team status publish approval.

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/edge-snap-dash-sequencing-design-spec.md`
