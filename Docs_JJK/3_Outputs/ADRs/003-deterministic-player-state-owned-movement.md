# ADR-003: Deterministic State-Owned Player Movement

- **Status:** Accepted; amended by user-approved action input/output contract
- **Date:** 2026-07-24; amended 2026-07-27
- **Decision Owners:** User-approved design/architecture checkpoint
- **Related Task:** `deterministic-player-movement-refactor`

## Context

Player motion is currently distributed across `PlayerMovement`, `GroundStateProvider`, `CombatPlayerState`, `PlayerCombat`, `PlayerStateMachine`, Rigidbody gravity, and a large `MovementProfile`. Ground/air action selection can use proximity rather than contact, while airborne attacks modify gravity and drift through a multi-parameter phase policy.

This makes a player's position and action context difficult to infer from the active state. It also permits a falling player near the floor to resolve a ground attack before landing.

The GDD requires Ground, Air, and Both actions to route predictably. The 2026-07-27 action amendment separates the context captured by each executing step from the state deliberately produced at completion/cancel. A later user feel correction restores the first Recovery frame as the Ground Dash control boundary and makes Air Dash Recovery gravity depend on live horizontal input.

## Decision

Adopt these ownership boundaries:

1. `PlayerMovement` owns physical contact, direct horizontal velocity, deterministic jump launch, state-selected falling acceleration, terminal falling speed, and authored movement application.
2. The player FSM owns one centralized environment context (`Grounded` or `Airborne`). Outside combat, physics may change it at support loss, jump launch, and landing. During combat, each executing step captures its input context and ordinary physical contact cannot interrupt or rewrite that running step.
3. One `PlayerJumpState` owns startup, launch, rise, apex, descent, and double-jump restart. Startup remains Grounded; assigning launch velocity is the exact Grounded-to-Airborne boundary. Ground and double-jump startup buffer directional input but lock horizontal motor output until launch. Double jump clears current vertical momentum before restarting the same session.
4. `PlayerFallingState` owns edge/drop descent and post-air-action descent. It exposes the complete airborne action set, including Skill and Ultimate conditions.
5. Every active combat action uses one motion rule: suspend base vertical locomotion for Startup, Active, Recovery, and Cancel; allow only explicitly gated horizontal input and authored MotionData. Normal gravity resumes after action authority ends.
6. Landing requires valid contact. A one-way landing is accepted only while crossing its top surface downward and aligns the authored ground-check level before publishing physical support. Contact during combat may satisfy an explicit `Just Landed` condition, but cannot interrupt the executing step, reset its combo, play landing feedback over its animation, or change that step's captured context.
7. `MovementProfile` contains only per-character locomotion outcomes, using frame-based timing where the combat/FSM already operates at a 60 Hz logic basis.
8. Drop-through is a Falling transition, not a jump: ignore only the supporting platform, begin a neutral fall at zero vertical speed, and restore collision after the full player body clears it.
9. Jump launch latches a `JumpArc` fall session; edge loss/drop-through latch a `Neutral` fall session. Air actions may suspend motion but do not rewrite the session that resumes.
10. Dash restores its prior Active curve: full authored speed through the first 55% of Active, then quadratic deceleration to 15% at the final Active frame. Recovery does not add a second dash brake. Ground walking/facing and locomotion animation return together on the first Recovery frame and remain open through late Cancel. Air Dash Recovery keeps horizontal locomotion locked; neutral A/D holds vertical gravity at zero, while any A/D input releases normal falling without adding walk speed. Cooldown still begins when Dash exits.
11. `PlayerMovement` accepts a scalar horizontal input only. Vertical input remains outside physics and may be consumed only by explicit directional action conditions or Down+Jump platform drop-through.
12. Animation follows gameplay ownership. Dash remains animation-authoritative through Startup/Active and Air Dash Recovery; Ground Dash Recovery permits the visible return to locomotion together with restored control. Each combat step resolves and restarts one explicit Animator state; optional logic-frame synchronization may target only that state and only during a looping Active phase.
13. Every `CombatAction` declares an Input Context (`Ground`, `Air`, or `Both`) and Output Context (`MatchInput`, `Ground`, or `Air`). At each legal next-step boundary, cause-aware Ground/Air is sampled. Only genuine lost support changes a Ground action to Air; authored vertical MotionData remains Ground-owned. Unchanged context continues the current tree; changed context re-resolves the same category and starts the selected action's exact root step. No match ends combat into physical locomotion.
14. `MatchInput` preserves the context captured when the executing step began for both accepted cancels and natural completion. Genuine edge-loss re-routing is resolved at legal next-step boundaries and does not rewrite the completed step's output. Explicit `Ground` and `Air` outputs remain authoritative.
15. Ground output stops outgoing MotionData, resolves a fresh physical floor point instead of trusting the pre-motion cached ground sample, snaps to valid ground, and clears landing lock without publishing a synthetic `JustLanded` transition or landing animation. The authored Ground correction owns the remainder of that fixed step so contacts generated before the snap cannot republish Air; ordinary contact evaluation resumes on the next fixed step. Locomotion entry forces the Grounded Animator state immediately rather than allowing a queued Landed/Fall transition to finish. When a physically airborne Ground/`MatchInput` action accepts a direct Jump cancel, the snap refreshes ground resources and Jump launches in the same state transition; ordinary jumps retain authored startup. Air output first resolves Air; if already physically supported, reconciliation waits until no frame-data action owns control, then replays one `Landing -> Idle/Move` transition.
16. Team Switch is a Ground-context-only transition. Eligibility follows the action-owned context rather than a temporary Rigidbody pose caused by authored MotionData. Every accepted switch silently resolves the shared body to the floor and gives the incoming character direct Idle/Move authority without Fall, Landing, or landing recovery. When the correction occurs from `Update` between physics simulations, it remains authoritative through the upcoming fixed-step contact sample so stale pre-snap contacts cannot create a real landing session.
16. `autoChainToNext` is a structural transition and is evaluated before player input in both Recovery and Cancel. An authored Cancel window that starts on the first Recovery frame cannot mask the auto-chain.

## Alternatives Considered

### Keep the existing phase gravity policy and tune it

Rejected because tuning does not remove the coupled ownership or the near-ground action-selection bug. It retains many parameters that can produce materially different trajectories from the same action state.

### Use only Rigidbody forces and collision callbacks

Rejected because jump and attack outcomes would continue to depend on implicit Rigidbody settings and force accumulation rather than explicit motor rules.

### Reclassify combat context continuously from physical contact

Rejected because authored lift/support changes would mutate combo routing inside a running step, and landing could steal its animation or cooldown. Physical contact is sampled for routing only at legal completed-step boundaries and remains available to explicit environmental trigger conditions during execution.

### Delay Ground Dash control handoff until Cancel

Rejected by the latest game-feel checkpoint because the additional locked tail made the dash feel slow and unresponsive after its visible motion/animation had effectively finished. Dash is a movement action and explicitly opens grounded locomotion in Recovery.

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
2. Move Ground/Air/Both action authority into a context captured per executing step.
3. Re-sample cause-aware context at legal next-step boundaries and re-route only genuine support-loss changes to the matching action root.
4. Add MatchInput/Ground/Air output context and commit it on completion/cancel.
5. Replace acceleration and impulse rules with direct motor output.
6. Remove airborne gravity bridge APIs and obsolete profile fields.
7. Restore the earlier Dash Active curve and first-Recovery grounded handoff; add neutral-input gravity lock with A/D release during Air Dash Recovery.
8. Migrate movement profile assets while preserving current maximum speeds and frame timings as starting values.
9. Compile both assemblies and use the human E2E plan; do not add or run script tests for this task.

## Validation

- Falling attack spam cannot enter a ground action before contact.
- Ground/Both authored lift keeps its captured context for the running step.
- Edge loss or landing during a step cannot interrupt it; the next legal step samples physical context and routes from the matching action root.
- Positive-Y MotionData does not count as edge loss and cannot reclassify a Ground action.
- Same-context chains continue their current node rather than restarting the root; no matching changed-context action ends into physical locomotion.
- Auto-chain reaches its authored next node even when Cancel begins on the first Recovery frame.
- Physical landing cannot interrupt/reset an active action; explicit `Just Landed` triggers remain observable.
- Ground output snaps without a landing lock or synthetic `JustLanded` transition before the next route.
- A direct Jump cancel from a physically airborne Ground/`MatchInput` action launches in the same transition after the silent resource refresh; it cannot idle through another startup hold.
- Air output on physical ground resolves Air, then Landing, then Idle/Move.
- Attack, Skill, Ultimate, Dash, Jump, and character-switch cancels resolve against the outgoing action's authored output.
- Ground Dash returns movement plus locomotion animation on its first Recovery frame and does not relock at late Cancel.
- Air Dash Recovery holds gravity with neutral A/D and releases normal falling immediately on A/D input without injecting horizontal walk speed.
- One-way platform center, edge, pass-through, and drop-through scenarios pass.
- Repeated identical input traces produce matching state transitions and trajectories within normal Physics2D contact tolerance.
