---
slug: "deterministic-player-movement-refactor"
status: ready-for-human-e2e
source: manual
gdd_tags:
  - "combat"
owner: "human"
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Deterministic Player Movement Refactor

## Player-Facing Goal

Make player movement read like a fighting-game controller: authored action motion has deterministic authority, combat actions keep a captured Ground/Air input context until completion or cancel, their authored output context decides the next state, and Dash feels front-loaded while returning control on its Cancel frame.

## Source

- Origin: Manual prompt
- Requested by: User
- Reference feel: *BlazBlue Entropy Effect*

## GDD Reference

- `@tag:combat` - Every action has a captured Ground/Air/Both input context and a MatchInput/Ground/Air output context committed only at completion or accepted cancel.

## Type

- [ ] Feature
- [x] Bug fix
- [x] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- Player movement motor and physical ground contact
- Movement profile data
- Player FSM ground/air action selection
- Combat-action input/output context authoring and routing
- Combat landing feedback/reconciliation
- Dash motion curve, phase authority, and animation handoff

### Files To Inspect First

- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Character/MovementProfile.cs`
- `Assets/Project/Scripts/Player/GroundStateProvider.cs`
- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Scripts/Player/States/PlayerFallingState.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`

### Out of Scope

- New movement abilities, attacks, enemy behavior, or animation assets
- Choosing special Ground-to-Air output values for unnamed character actions without designer direction
- Changing combo routes beyond correcting their state contract
- Tuning final game feel without a human playtest
- Adding or running automated/script tests, per the user's explicit instruction

## Acceptance Criteria

The corrective feel implementation is complete. Unchecked gameplay criteria intentionally remain open until the keyboard/controller human E2E pass.

- [x] Ground and air horizontal movement set a target speed directly; no acceleration, deceleration, vertical-speed attenuation, or gravity-scaled drift remains.
- [x] Jump launch sets a deterministic vertical speed rather than applying an impulse.
- [x] One `PlayerJumpState` owns startup, launch, rise, apex, descent, and internal double-jump restart; legacy JumpStartup/JumpRising classes are removed.
- [x] Jump startup remains grounded until the exact launch frame changes the centralized environment context to airborne.
- [x] Double jump clears accumulated vertical/fall momentum before applying the second deterministic launch.
- [x] The player motor owns its vertical acceleration instead of changing `Rigidbody2D.gravityScale` by movement or attack phase.
- [x] Rising velocity decelerates explicitly; descent accelerates toward one terminal speed using a jump-arc or neutral-fall session.
- [x] Every active combat action suspends base vertical locomotion for its complete authored duration; MotionData remains the only action-motion authority.
- [x] A falling player remains airborne until a real valid ground contact occurs.
- [ ] Spamming attack while falling never resolves a ground attack; it resolves an available air action or remains buffered/rejected.
- [x] Physical landing cannot interrupt an active combat action, reset its combo, or steal its action animation.
- [ ] One-way platform and edge landings resolve from collision contact, zero downward speed, and enter Landing exactly once.
- [ ] Valid one-way contact snaps the authored `GroundC` level to the platform top without embedding the body.
- [x] Every combat action captures its Ground/Air input context once and does not reclassify it from physical contact or support loss mid-action.
- [x] Every combat action exposes an authored output context (`MatchInput`, `Ground`, or `Air`) in both serialized data and the action editor.
- [x] Ground-started Both actions preserve their authored Ground input context during vertical motion; Air-started Both and standalone actions preserve/enforce Air input context.
- [x] Natural completion and accepted cancels commit output context before locomotion, a different action, or character-switch cleanup resolves.
- [x] Ground output snaps to valid ground, clears landing lock, and routes directly to grounded action/locomotion authority.
- [x] Air output resolves Air first; if physically supported, it then replays Landing and reaches Idle/Move only after the action has ended.
- [x] Explicit `Require Just Landed` trigger conditions can observe physical contact without changing the locked action context.
- [x] Falling accepts airborne normal, skill, and ultimate resolution through the centralized environment context.
- [x] Ground and air variants of the same non-zero-cooldown Skill slot share cooldown availability; landing cannot bypass an air-skill cooldown by resolving the ground variant.
- [x] A ground-input action that leaves an edge keeps executing and its ordinary action conditions continue to observe the captured Ground context.
- [x] Unused movement fields and methods are removed after project-wide reference checks.
- [x] Existing character movement profiles migrate without missing references.
- [x] Human E2E plan covers the state/action-selection rules.
- [ ] Works on keyboard and controller input.
- [ ] Does not break dash, double jump, drop-through, action MotionData, or character stat speed overrides.
- [ ] Dash cannot self-chain from spam and observes its complete post-exit cooldown.
- [x] Dash Startup/Active begin with a front-loaded burst and Dash Recovery owns a continuous brake; normal ground locomotion/facing returns exactly on the authored Cancel frame.
- [x] Player physics accepts only scalar horizontal input. W/S remain available only to explicit directional action conditions and Down+Jump drop-through.
- [x] Dash animation remains authoritative through locked Recovery; Ground Dash Cancel returns locomotion animation on the same frame that control returns.
- [x] Every executed combat step restarts its resolved animation exactly once, including click, auto-chain, conditional, and same-state transitions.
- [x] Logic-frame animation synchronization targets only the resolved action state and runs only for looping Active phases.
- [x] Follows `RULES_AND_POLICY.md`.

## Human Checkpoints

- [x] Design/game-feel direction supplied by user
- [x] Architecture approval
- [ ] Playtest feel approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/fix-bug`

Next artifact:

- `Docs_JJK/3_Outputs/Specs/deterministic-player-movement-refactor-impact-analysis.md`
