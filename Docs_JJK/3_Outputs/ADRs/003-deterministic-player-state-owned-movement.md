# ADR-003: Deterministic State-Owned Player Movement

## Approved responsive movement tuning — 2026-11-03

The user approved `../Specs/responsive-movement-flow-design-spec.md` with "lgtm". The accepted motor/FSM/contact/MotionData ownership remains unchanged. Responsive profiles use zero ordinary/double-jump startup and landing recovery; an explicitly authored nonzero jump startup remains supported. Character DashData uses zero startup, preserving each Active curve, burst speed/duration, Recovery duration, and cooldown duration. Absolute Cancel indices shift by the removed Startup so their Recovery-relative offsets stay intact.

Dash Recovery now returns ground/air steering, facing, normal fall-session gravity, and locomotion animation together. It no longer freezes vertical velocity based on neutral A/D. Dash remains unavailable through its existing state timeline, and `FinishDash` starts the existing cooldown only at actual exit. Locomotion facing has no extra turnaround timer. Landing publishes contact/resources and feedback before accepting a queued movement action. No tap/hold jump-height, glide, or SlowFalling mechanics are introduced.

The user's subsequent 2026-11-03 bug-fix request corrects the unconditional animation handoff: Recovery returns control/gravity, but a neutral unfinished Dash clip may continue cosmetically beyond gameplay Dash completion. Actual movement, a new action, or clip completion returns the appropriate presentation. This adds no gameplay state or delay to resources/cooldown.

This approved tuning supersedes earlier startup-position holds and Dash Recovery input/hover restrictions below; it does not supersede authored combat-motion/context/cancel authority. References below to Recovery returning locomotion animation are conditional on new movement/action or clip completion, rather than an unconditional clip replacement.

## Approved motion-clock option — 2026-10-29

User approved `../Specs/animation-slow-motion-options-task-card.md` with “lgtm”. MotionFrameData adds Follow Slow Motion, default on. Turning it off changes only an already-triggered motion's progression; action phases, key trigger frames, animation/hit timing, input windows, cancellation and resource authority remain with the existing state/resolver.

Normal motion uses unscaled fixed-step elapsed time and converts each requested displacement into velocity over the actual scaled physics step. Existing collision casts and motor application remain authoritative; only the opt-out path adjusts its minimum integration step for low time scales. Captured-world motion advances through the fixed motor with a per-motion counter and a fixed-time guard against duplicate progression; the action's samples must not rewind it. Full pause/hitstop still gate the generic opt-out. Existing explicit cinematic unscaled behavior remains on its established path. No transform movement from a new render Update loop or global physics setting change is introduced.

- **Status:** Accepted; amended by user-approved action input/output contract
- **Date:** 2026-07-24; amended 2026-07-27 and 2026-08-10
- **Decision Owners:** User-approved design/architecture checkpoint
- **Related Task:** `deterministic-player-movement-refactor`

## Context

Player motion is currently distributed across `PlayerMovement`, `GroundStateProvider`, `CombatPlayerState`, `PlayerCombat`, `PlayerStateMachine`, Rigidbody gravity, and a large `MovementProfile`. Ground/air action selection can use proximity rather than contact, while airborne attacks modify gravity and drift through a multi-parameter phase policy.

This makes a player's position and action context difficult to infer from the active state. It also permits a falling player near the floor to resolve a ground attack before landing.

The GDD requires Ground, Air, and Both actions to route predictably. The 2026-07-27 action amendment separates the context captured by each executing step from the state deliberately produced at completion/cancel. A later user feel correction made Air Dash Recovery gravity depend on live horizontal input. The 2026-08-10 checkpoint keeps Ground Dash locomotion at the authored Cancel phase, permits Landing to transition directly into Dash, lets Recovery accept action interrupts without walking, and makes genuine edge/drop departure override a ground-started `MatchInput` output. A short `GroundC.x - 1` support ray prevents a lingering edge contact from masking departure.

## Decision

Adopt these ownership boundaries:

1. `PlayerMovement` owns physical contact, direct horizontal velocity, deterministic jump launch, state-selected falling acceleration, terminal falling speed, and authored movement application.
2. The player FSM owns one centralized environment context (`Grounded` or `Airborne`). Outside combat, physics may change it at support loss, jump launch, and landing. During combat, each executing step captures its input context and ordinary physical contact cannot interrupt or rewrite that running step.
3. One `PlayerJumpState` owns startup, launch, rise, apex, descent, and double-jump restart. Assigning launch velocity is the exact Grounded-to-Airborne boundary. Responsive profiles use zero startup and launch ground/double jumps on entry while retaining horizontal control. Explicitly authored nonzero startup can still hold the motor until launch. Double jump clears current vertical momentum before restarting the same session; an immediate action-output override cannot commit a zero-startup jump twice.
4. `PlayerFallingState` owns edge/drop descent and post-air-action descent. It exposes the complete airborne action set, including Skill and Ultimate conditions.
5. Every active combat action uses one motion rule: suspend base vertical locomotion for Startup, Active, Recovery, and Cancel; allow only explicitly gated horizontal input and authored MotionData. Normal gravity resumes after action authority ends.
6. Landing requires valid contact. A one-way landing is accepted only while crossing its top surface downward and aligns the authored ground-check level before publishing physical support. Contact during combat may satisfy an explicit `Just Landed` condition, but cannot interrupt the executing step, reset its combo, play landing feedback over its animation, or change that step's captured context.
7. `MovementProfile` contains only per-character locomotion outcomes, using frame-based timing where the combat/FSM already operates at a 60 Hz logic basis.
8. Drop-through is a Falling transition, not a jump: ignore only the supporting platform, begin a neutral fall at zero vertical speed, and restore collision after the full player body clears it.
9. Jump launch latches a `JumpArc` fall session; edge loss/drop-through latch a `Neutral` fall session. Air actions may suspend motion but do not rewrite the session that resumes.
10. Dash retains its Active curve: full authored speed through the first 55% of Active, then quadratic deceleration to 15% at the final Active frame. Responsive DashData uses zero startup, publishing Active on entry so the motor cannot observe an initial locked None phase. Recovery accepts Attack, Skill, Ultimate, and Jump interrupts and returns movement/facing/locomotion animation on ground and in air. Normal fall-session gravity resumes independent of A/D. The authored Cancel phase retains its full action window and Recovery-relative timing. Returning steering does not release the dash resource; cooldown still begins on actual Dash exit. Landing may transition directly into an available Dash.
11. `PlayerMovement` accepts a scalar horizontal input only. Vertical input remains outside physics and may be consumed only by explicit directional action conditions or Down+Jump platform drop-through.
12. Animation follows gameplay ownership. Dash remains animation-authoritative through Startup/Active; both ground and air Dash Recovery return visible locomotion together with restored control. Landing feedback publishes before a buffered movement launch so it cannot overwrite the next jump/dash animation. Each combat step resolves and restarts one explicit Animator state; optional logic-frame synchronization may target only that state and only during a looping Active phase.
13. Every `CombatAction` declares an Input Context (`Ground`, `Air`, or `Both`) and Output Context (`MatchInput`, `Ground`, or `Air`). At each legal next-step boundary, cause-aware Ground/Air is sampled. Only genuine lost support changes a Ground action to Air; authored vertical MotionData remains Ground-owned. Unchanged context continues the current tree; changed context re-resolves the same category and starts the selected action's exact root step. No match ends combat into physical locomotion.
14. `MatchInput` preserves the context captured when the executing step began except when a ground-started step genuinely loses support or enters an authored drop-through. Contact must also retain a short support ray at `GroundC.x - 1`; a lingering edge contact without that support counts as genuine loss. At an accepted cancel or natural completion, that cause-aware ground departure resolves Air and cannot invoke the Ground snap. Authored positive-Y MotionData remains Ground-owned. Explicit `Ground` and `Air` outputs remain authoritative.
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

### Historical alternative: return Ground Dash control on the first Recovery frame

The 2026-08-10 checkpoint rejected this timing because walking could visibly override committed Dash recovery. The 2026-11-03 user-approved responsive tuning now returns both ground and air locomotion at Recovery, pairing the animation handoff with body control while retaining the original dash resource/cooldown timeline.

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

## Original Migration (historical; responsive tuning is amended above)

1. Remove predictive near-ground grounding.
2. Move Ground/Air/Both action authority into a context captured per executing step.
3. Re-sample cause-aware context at legal next-step boundaries and re-route only genuine support-loss changes to the matching action root.
4. Add MatchInput/Ground/Air output context and commit it on completion/cancel.
5. Replace acceleration and impulse rules with direct motor output.
6. Remove airborne gravity bridge APIs and obsolete profile fields.
7. Restore the earlier Dash Active curve, keep Recovery committed, hand grounded control back at Cancel, and retain neutral-input gravity lock with A/D release during Air Dash Recovery.
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
- Dash Recovery accepts Attack, Skill, Ultimate, and Jump while returning ground/air movement, facing, normal gravity, and locomotion animation together; Cancel retains the full discrete action set.
- Landing Dash immediately replaces the landing lock/animation when Dash is available.
- Ground-started `MatchInput` actions resolve Air after genuine ledge loss, a failing `GroundC.x - 1` support probe, or drop-through and never snap to distant ground; authored positive-Y motion remains Ground-owned.
- Air Dash Recovery resumes the existing normal fall session for both neutral and held A/D; returning horizontal steering does not reset resources or start cooldown before actual Dash exit.
- One-way platform center, edge, pass-through, and drop-through scenarios pass.
- Repeated identical input traces produce matching state transitions and trajectories within normal Physics2D contact tolerance.
