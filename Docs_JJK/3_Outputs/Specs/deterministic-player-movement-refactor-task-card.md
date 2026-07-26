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

Make player movement read like a fighting-game controller: horizontal input changes movement immediately, jump and airborne attack motion follow explicit state rules, landing happens only on real contact, and repeated attack input while falling can never start a ground attack before landing.

## Source

- Origin: Manual prompt
- Requested by: User
- Reference feel: *BlazBlue Entropy Effect*

## GDD Reference

- `@tag:combat` - Every action has a Ground, Air, or Both cast context; a ground-cast Both action stays ground-authoritative during authored lift.

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
- Airborne combat gravity and landing interruption
- Movement regression tests

### Files To Inspect First

- `Assets/Project/Scripts/Combat/PlayerMovement.cs`
- `Assets/Project/Scripts/Character/MovementProfile.cs`
- `Assets/Project/Scripts/Player/GroundStateProvider.cs`
- `Assets/Project/Scripts/Player/PlayerStateMachine.cs`
- `Assets/Project/Scripts/Player/States/CombatPlayerState.cs`
- `Assets/Project/Scripts/Player/States/PlayerFallingState.cs`

### Out of Scope

- New movement abilities, attacks, animations, VFX, or enemy behavior
- Re-authoring attack frame data or dash frame data
- Changing combo routes beyond correcting ground/air context
- Tuning final game feel without a human playtest

## Acceptance Criteria

The corrective feel implementation is complete. Unchecked gameplay criteria intentionally remain open until the keyboard/controller human E2E pass.

- [x] Ground and air horizontal movement set a target speed directly; no acceleration, deceleration, vertical-speed attenuation, or gravity-scaled drift remains.
- [x] Jump launch sets a deterministic vertical speed rather than applying an impulse.
- [x] One `PlayerJumpState` owns startup, launch, rise, apex, descent, and internal double-jump restart; legacy JumpStartup/JumpRising classes are removed.
- [x] Jump startup remains grounded until the exact launch frame changes the centralized environment context to airborne.
- [x] Double jump clears accumulated vertical/fall momentum before applying the second deterministic launch.
- [x] The player motor owns its vertical acceleration instead of changing `Rigidbody2D.gravityScale` by movement or attack phase.
- [x] Rising velocity decelerates explicitly; descent accelerates toward one terminal speed using a jump-arc or neutral-fall session.
- [x] Airborne attack suspension follows one state rule with no per-phase gravity policy parameters.
- [x] A falling player remains airborne until a real valid ground contact occurs.
- [ ] Spamming attack while falling never resolves a ground attack; it resolves an available air action or remains buffered/rejected.
- [x] Landing an air action interrupts the air action before any grounded action can start.
- [ ] One-way platform and edge landings resolve from collision contact, zero downward speed, and enter Landing exactly once.
- [ ] Valid one-way contact snaps the authored `GroundC` level to the platform top without embedding the body.
- [x] Ground-started Both actions preserve their authored Ground action context during vertical motion.
- [x] Air-started Both actions and standalone actions preserve/enforce their airborne cast context.
- [x] Falling accepts airborne normal, skill, and ultimate resolution through the centralized environment context.
- [x] Ground and air variants of the same non-zero-cooldown Skill slot share cooldown availability; landing cannot bypass an air-skill cooldown by resolving the ground variant.
- [x] A ground action that leaves an edge keeps executing while its action conditions immediately observe Airborne context.
- [x] Unused movement fields and methods are removed after project-wide reference checks.
- [x] Existing character movement profiles migrate without missing references.
- [x] Human E2E plan covers the state/action-selection rules.
- [ ] Works on keyboard and controller input.
- [ ] Does not break dash, double jump, drop-through, action MotionData, or character stat speed overrides.
- [ ] Dash cannot self-chain from spam and observes its complete post-exit cooldown.
- [x] Dash Startup/Active exclusively own authored burst distance; Ground Dash Recovery restores normal walking and facing immediately, while Air Dash Recovery remains locomotion-locked.
- [x] Player physics accepts only scalar horizontal input. W/S remain available only to explicit directional action conditions and Down+Jump drop-through.
- [x] Air dash animation remains authoritative for the complete `PlayerDashState`; Ground Dash Recovery may visibly return to locomotion with restored control.
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
