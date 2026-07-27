# ADR-003: Deterministic State-Owned Player Movement

- **Status:** Accepted; amended by user-approved action input/output contract
- **Date:** 2026-07-24; amended 2026-07-27
- **Decision Owners:** User-approved design/architecture checkpoint
- **Related Task:** `deterministic-player-movement-refactor`

## Context

Player motion is currently distributed across `PlayerMovement`, `GroundStateProvider`, `CombatPlayerState`, `PlayerCombat`, `PlayerStateMachine`, Rigidbody gravity, and a large `MovementProfile`. Ground/air action selection can use proximity rather than contact, while airborne attacks modify gravity and drift through a multi-parameter phase policy.

This makes a player's position and action context difficult to infer from the active state. It also permits a falling player near the floor to resolve a ground attack before landing.

The GDD requires Ground, Air, and Both actions to route predictably. The 2026-07-27 amendment separates the context captured at action entry from the state deliberately produced at completion/cancel, and makes the authored Cancel frame—not the first Recovery frame—the Ground Dash control boundary.

## Decision

Adopt these ownership boundaries:

1. `PlayerMovement` owns physical contact, direct horizontal velocity, deterministic jump launch, state-selected falling acceleration, terminal falling speed, and authored movement application.
2. The player FSM owns one centralized environment context (`Grounded` or `Airborne`). Outside combat, physics may change it at support loss, jump launch, and landing. During a combat action, the resolver captures the input context once and ordinary physical contact cannot rewrite it.
3. One `PlayerJumpState` owns startup, launch, rise, apex, descent, and double-jump restart. Startup remains Grounded; assigning launch velocity is the exact Grounded-to-Airborne boundary. Double jump clears current vertical momentum before restarting the same session.
4. `PlayerFallingState` owns edge/drop descent and post-air-action descent. It exposes the complete airborne action set, including Skill and Ultimate conditions.
5. Every active combat action uses one motion rule: suspend base vertical locomotion for Startup, Active, Recovery, and Cancel; allow only explicitly gated horizontal input and authored MotionData. Normal gravity resumes after action authority ends.
6. Landing requires valid contact. A one-way landing is accepted only while crossing its top surface downward and aligns the authored ground-check level before publishing physical support. Contact during combat may satisfy an explicit `Just Landed` condition, but cannot interrupt the action, reset its combo, play landing feedback over its animation, or change its captured context.
7. `MovementProfile` contains only per-character locomotion outcomes, using frame-based timing where the combat/FSM already operates at a 60 Hz logic basis.
8. Drop-through is a Falling transition, not a jump: ignore only the supporting platform, begin a neutral fall at zero vertical speed, and restore collision after the full player body clears it.
9. Jump launch latches a `JumpArc` fall session; edge loss/drop-through latch a `Neutral` fall session. Air actions may suspend motion but do not rewrite the session that resumes.
10. Dash owns a front-loaded authored burst during Active and a continuous authored brake during Recovery. Ground walking/facing and locomotion animation return together on the authored Cancel frame; Recovery itself remains locked. Air Dash Recovery resumes vertical gravity while retaining horizontal dash authority. Cooldown still begins when Dash exits.
11. `PlayerMovement` accepts a scalar horizontal input only. Vertical input remains outside physics and may be consumed only by explicit directional action conditions or Down+Jump platform drop-through.
12. Animation follows gameplay ownership. Dash remains animation-authoritative through locked Recovery, then Ground Dash Cancel permits the visible return to locomotion together with restored control. Each combat step resolves and restarts one explicit Animator state; optional logic-frame synchronization may target only that state and only during a looping Active phase.
13. Every `CombatAction` declares an Input Context (`Ground`, `Air`, or `Both`) and Output Context (`MatchInput`, `Ground`, or `Air`). `MatchInput` preserves the captured entry context. Natural completion or any accepted cancel commits output before routing the next action, locomotion state, or character switch.
14. Ground output snaps to valid ground and clears landing lock. Air output first resolves Air; if already physically supported, reconciliation waits until no frame-data action owns control, then replays one `Landing -> Idle/Move` transition.

## Alternatives Considered

### Keep the existing phase gravity policy and tune it

Rejected because tuning does not remove the coupled ownership or the near-ground action-selection bug. It retains many parameters that can produce materially different trajectories from the same action state.

### Use only Rigidbody forces and collision callbacks

Rejected because jump and attack outcomes would continue to depend on implicit Rigidbody settings and force accumulation rather than explicit motor rules.

### Reclassify combat context continuously from physical contact

Rejected because authored lift/support changes would mutate combo routing mid-action, and landing could steal an active action's animation or cooldown. Physical contact remains available only to explicit environmental trigger conditions.

### Keep Ground Dash control handoff on Recovery

Rejected by the game-feel checkpoint because it made the authored Cancel window meaningless and produced an early, linear locomotion tail that read as slow and unresponsive.

### Replace the Rigidbody with a fully custom kinematic collision solver

Rejected as over-engineering for the current single-player 2D scope. Unity Physics2D remains responsible for collision resolution; the player motor owns intended velocity and state transitions.

## Consequences

### Positive

- Ground/air action selection becomes inspectable and testable.
- Jump and horizontal movement become repeatable across characters and input paths.
- The profile loses unused and interacting parameters.
- All combat action motion has one authority rule instead of competing phase gravity.
- Input and output contexts are explicit in the action editor.
- Ground snaps and Air-on-ground landings use one output reconciliation path across completion, cancel, and team switch.
- Dash response/control/animation boundaries are authored and inspectable.
- The reported falling ground-attack bug is removed at its source.

### Negative

- Existing combat actions and dash may feel different and require a human tuning pass.
- Profile assets and the Player prefab need serialized-data migration.
- Ground contact logic and combat context must change together to avoid regressions.

## Migration

1. Remove predictive near-ground grounding.
2. Move Ground/Air/Both action authority into a captured input context.
3. Add MatchInput/Ground/Air output context and commit it on completion/cancel.
4. Replace acceleration and impulse rules with direct motor output.
5. Remove airborne gravity bridge APIs and obsolete profile fields.
6. Move Dash control handoff to Cancel and apply the starting burst/brake curve.
7. Migrate movement profile assets while preserving current maximum speeds and frame timings as starting values.
8. Compile both assemblies and use the human E2E plan; do not add or run script tests for this task.

## Validation

- Falling attack spam cannot enter a ground action before contact.
- Ground/Both authored lift keeps its captured input context.
- Physical landing cannot interrupt/reset an active action; explicit `Just Landed` triggers remain observable.
- Ground output snaps without a landing lock before the next route.
- Air output on physical ground resolves Air, then Landing, then Idle/Move.
- Attack, Skill, Ultimate, Dash, Jump, and character-switch cancels resolve against the outgoing action's authored output.
- Ground Dash remains locked through Recovery and returns movement plus locomotion animation on Cancel.
- One-way platform center, edge, pass-through, and drop-through scenarios pass.
- Repeated identical input traces produce matching state transitions and trajectories within normal Physics2D contact tolerance.
