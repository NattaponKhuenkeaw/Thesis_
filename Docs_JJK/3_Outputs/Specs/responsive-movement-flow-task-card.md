---
slug: responsive-movement-flow
status: needs-human
source: manual
gdd_tags:
  - platform-input
  - combat-scope
owner: Codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Responsive Move, Jump, and Dash Flow

## Player-Facing Goal

Fast, responsive move <-> jump <-> dash transitions, using Hollow Knight as the user's qualitative feel reference. Jump remains a single-press, fixed-arc action. No tap/hold jump-height mechanic, glide, or SlowFalling system.

## Source

- Origin: User request in this chat, 2026-11-03.
- Requested by: User.
- Reference analysis: `output/CrimsonOrchardMovement/movement-analysis.md`.
- The reference informs feel; its force-based controller is not approved as a replacement for this project's deterministic motor.

## GDD Reference

- `@tag:platform-input`: A/D locomotion, Space jump/double jump, Shift dash, action buffering, Down+Jump platform drop-through.
- `@tag:combat-scope`: state-owned action motion, authored cancel windows, dash movement exception, and grounded/airborne output ownership.
- Current GDD says jump startup pauses horizontal movement and Dash Recovery locks locomotion. The new request proposes revising those feel commitments; the GDD has not yet been changed.
- Existing governing architecture: accepted ADR-003, `003-deterministic-player-state-owned-movement.md`.

## Type

- [x] Feature / feel tuning
- [ ] Bug fix
- [ ] Structural refactor
- [ ] Code review
- [ ] Report
- [ ] Tooling / CI

## Scope

### Systems Affected

- Locomotion timing and facing responsiveness.
- Jump launch/startup and landing recovery.
- Dash entry and return of ground/air steering, normal gravity, and facing.
- Existing movement profiles and character DashData where approved.

### Files Inspected

- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Player/States/PlayerJumpState.cs`
- `Assets/Project/Scripts/Player/States/PlayerDashState.cs`
- `Assets/Project/Scripts/Player/States/PlayerLandingState.cs`
- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/ActionInputBuffer.cs`
- `Assets/Project/Scripts/Character/MovementProfile.cs`
- `Assets/Project/Scripts/Character/DashData.cs`
- `Assets/Project/Resources/MovementSO/MovementProfile_{Sample,Fast,Slow}.asset`
- CharacterObject assets referencing those profiles.

### Out of Scope

- New tap/hold, jump cutting, glide, or SlowFalling mechanics.
- Replacing the direct-velocity motor with AddForce, Rigidbody gravity, or a new collision solver.
- Combat MotionData ownership, attack/skill/ultimate timing, hitstun immunity, or unrestricted attack cancels.
- New camera, VFX, animation, or audio assets.
- CI/build/dependency configuration, commits, publishing, or merge.

## Acceptance Criteria

- [x] Native checks: immediate direction/release response and facing reversals.
- [x] Native checks: ground/double jumps launch without a startup hold, with one resource/event commit.
- [x] Native checks/code review: zero landing movement lock and buffered Jump/Dash after resource/feedback ordering.
- [x] Native checks: dash publishes Active on entry and retains its authored burst.
- [x] Native checks: ground/air Recovery opens steering/facing and normal gravity, independent of A/D.
- [x] Native checks: coyote jump, jump budget, and dash/cooldown authority.
- [ ] Human E2E: platform drop-through, collision, combat-context transitions, and rendered feedback remain correct.
- [x] No hold/release-driven jump-height, glide, or SlowFalling system added.
- [x] All 12 tuning assets passed scoped-field verification; Goenitz's concurrent dash-speed edit to 35 was preserved.
- [x] Unrelated working-tree edits preserved.

## Human Checkpoints

- [x] User approved the concrete design with "lgtm" on 2026-11-03.
- [x] Continued use of the accepted ADR-003 ownership validated; approved changes affect timing/gates, not architectural authority.
- [ ] Human E2E feel review after implementation.
- [ ] Review before merge or publishing.

## Router Decision

- Workflow: `/implement-feature`.
- Current artifact: `responsive-movement-flow-design-spec.md`.
- Next: human E2E feel review using `../TestPlans/responsive-movement-flow-test-plan.md`.
- Current state: implementation complete; runtime/editor compilation and nine native checks passed. Subjective traversal/collision/presentation review remains pending.
