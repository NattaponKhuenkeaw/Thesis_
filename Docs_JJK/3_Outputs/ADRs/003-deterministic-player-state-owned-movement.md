# ADR-003: Deterministic State-Owned Player Movement

- **Status:** Accepted
- **Date:** 2026-07-24
- **Decision Owners:** User-approved design/architecture checkpoint
- **Related Task:** `deterministic-player-movement-refactor`

## Context

Player motion is currently distributed across `PlayerMovement`, `GroundStateProvider`, `CombatPlayerState`, `PlayerCombat`, `PlayerStateMachine`, Rigidbody gravity, and a large `MovementProfile`. Ground/air action selection can use proximity rather than contact, while airborne attacks modify gravity and drift through a multi-parameter phase policy.

This makes a player's position and action context difficult to infer from the active state. It also permits a falling player near the floor to resolve a ground attack before landing.

The GDD requires Ground, Air, and Both actions to route predictably, including preserving Ground authority for a ground-cast Both action during authored lift.

## Decision

Adopt these ownership boundaries:

1. `PlayerMovement` owns physical contact, direct horizontal velocity, deterministic jump launch, state-selected falling acceleration, terminal falling speed, and authored movement application.
2. The player FSM owns one centralized environment context (`Grounded` or `Airborne`). Physics may change it only at support loss, jump launch, and landing; action resolvers consume it and never query contact independently.
3. One `PlayerJumpState` owns startup, launch, rise, apex, descent, and double-jump restart. Startup remains Grounded; assigning launch velocity is the exact Grounded-to-Airborne boundary. Double jump clears current vertical momentum before restarting the same session.
4. `PlayerFallingState` owns edge/drop descent and post-air-action descent. It exposes the complete airborne action set, including Skill and Ultimate conditions.
5. Airborne combat uses one state rule: suspend base vertical motion while the air action is active, allow only explicitly gated horizontal input and authored MotionData, then resume standard falling on exit.
6. Landing requires valid contact. A one-way landing is accepted only while crossing its top surface downward, aligns the authored ground-check level to that surface before Ground is published, clears downward velocity, and interrupts an active Air action before Ground action input may resolve.
7. `MovementProfile` contains only per-character locomotion outcomes, using frame-based timing where the combat/FSM already operates at a 60 Hz logic basis.
8. Drop-through is a Falling transition, not a jump: ignore only the supporting platform, begin a neutral fall at zero vertical speed, and restore collision after the full player body clears it.
9. Jump launch latches a `JumpArc` fall session; edge loss/drop-through latch a `Neutral` fall session. Air actions may suspend motion but do not rewrite the session that resumes.
10. Dash owns authored burst velocity only during Startup and Active. A dash that started grounded returns normal walking and facing control on its first Recovery frame; an air dash remains locomotion-locked through Recovery. Cooldown still begins when Dash exits.
11. `PlayerMovement` accepts a scalar horizontal input only. Vertical input remains outside physics and may be consumed only by explicit directional action conditions or Down+Jump platform drop-through.
12. Animation follows gameplay ownership. Dash remains animation-authoritative through Startup and Active, while Ground Dash Recovery permits the visible return to locomotion together with restored control. Each combat step resolves and restarts one explicit Animator state; optional logic-frame synchronization may target only that state and only during a looping Active phase.

## Alternatives Considered

### Keep the existing phase gravity policy and tune it

Rejected because tuning does not remove the coupled ownership or the near-ground action-selection bug. It retains many parameters that can produce materially different trajectories from the same action state.

### Use only Rigidbody forces and collision callbacks

Rejected because jump and attack outcomes would continue to depend on implicit Rigidbody settings and force accumulation rather than explicit motor rules.

### Replace the Rigidbody with a fully custom kinematic collision solver

Rejected as over-engineering for the current single-player 2D scope. Unity Physics2D remains responsible for collision resolution; the player motor owns intended velocity and state transitions.

## Consequences

### Positive

- Ground/air action selection becomes inspectable and testable.
- Jump and horizontal movement become repeatable across characters and input paths.
- The profile loses unused and interacting parameters.
- Air attack behavior has one rule instead of a phase curve.
- The reported falling ground-attack bug is removed at its source.

### Negative

- Existing air attacks may feel different and require a human tuning pass.
- Profile assets and the Player prefab need serialized-data migration.
- Ground contact logic and combat context must change together to avoid regressions.

## Migration

1. Add tests around current context invariants.
2. Remove predictive near-ground grounding.
3. Move Ground/Both action authority into captured combat context.
4. Replace acceleration and impulse rules with direct motor output.
5. Remove airborne gravity bridge APIs and obsolete profile fields.
6. Migrate the three movement profile assets while preserving current maximum speeds and frame timings as starting values.

## Validation

- Falling attack spam cannot enter a ground action before contact.
- Ground/Both authored lift remains a ground combo context.
- Air attack landing exits the air action exactly once.
- One-way platform center, edge, pass-through, and drop-through scenarios pass.
- Repeated identical input traces produce matching state transitions and trajectories within normal Physics2D contact tolerance.
